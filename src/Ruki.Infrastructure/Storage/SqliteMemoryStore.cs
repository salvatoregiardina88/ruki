using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Ruki.Core.Memory;

namespace Ruki.Infrastructure.Storage;

/// <summary>
/// Implementazione di <see cref="IMemoryStore"/> su SQLite (file singolo).
/// <para>
/// L'albero è una tabella auto-referenziante: ogni nodo punta al padre tramite
/// <c>parent_id</c>, con eliminazione a cascata (la cancellazione di una categoria rimuove
/// tutto il suo sottoalbero). Le chiamate sono serializzate con un lock: per un'app desktop
/// monoutente è semplice e più che sufficiente.
/// </para>
/// </summary>
public sealed class SqliteMemoryStore : IMemoryStore
{
    private readonly object _gate = new();
    private readonly string _connectionString;
    private readonly ILogger<SqliteMemoryStore> _logger;

    /// <param name="databasePath">
    /// Percorso del file .db. In produzione si lascia <c>null</c> (percorso predefinito);
    /// nei test si passa un file temporaneo.
    /// </param>
    public SqliteMemoryStore(ILogger<SqliteMemoryStore> logger, string? databasePath = null)
    {
        _logger = logger;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath ?? RukiPaths.DatabaseFile,
        }.ToString();

        EnsureSchema();
    }

    public IReadOnlyList<MemoryNodeInfo> GetChildren(string? parentId)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();

            // has_children evita una query per ogni figlio quando l'albero viene esplorato.
            command.CommandText =
                """
                SELECT n.id, n.parent_id, n.type, n.title, n.summary,
                       EXISTS(SELECT 1 FROM memory_node c WHERE c.parent_id = n.id) AS has_children,
                       n.is_obsolete
                FROM memory_node n
                WHERE
                """
                + (parentId is null ? " n.parent_id IS NULL" : " n.parent_id = @parent")
                // Categorie prima delle memorie, poi ordine alfabetico (case-insensitive).
                + " ORDER BY CASE n.type WHEN 'Category' THEN 0 ELSE 1 END, n.title COLLATE NOCASE";

            if (parentId is not null)
                command.Parameters.AddWithValue("@parent", parentId);

            using var reader = command.ExecuteReader();
            var result = new List<MemoryNodeInfo>();
            while (reader.Read())
            {
                result.Add(new MemoryNodeInfo(
                    Id: reader.GetString(0),
                    ParentId: reader.IsDBNull(1) ? null : reader.GetString(1),
                    Type: Enum.Parse<MemoryNodeType>(reader.GetString(2)),
                    Title: reader.GetString(3),
                    Summary: reader.IsDBNull(4) ? null : reader.GetString(4),
                    HasChildren: reader.GetInt64(5) != 0,
                    IsObsolete: reader.GetInt64(6) != 0));
            }

            return result;
        }
    }

    public MemoryNode? GetNode(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT id, parent_id, type, title, summary, content, metadata,
                       created_at, updated_at, use_count, last_used_at, is_obsolete
                FROM memory_node WHERE id = @id
                """;
            command.Parameters.AddWithValue("@id", id);

            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadNode(reader) : null;
        }
    }

    public MemoryNode Add(MemoryNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(node.Title);

        if (string.IsNullOrEmpty(node.Id))
            node.Id = Guid.NewGuid().ToString("N");

        var now = DateTimeOffset.UtcNow;
        node.CreatedAt = now;
        node.UpdatedAt = now;

        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO memory_node
                    (id, parent_id, type, title, summary, content, metadata,
                     created_at, updated_at, use_count, last_used_at, is_obsolete)
                VALUES
                    (@id, @parent, @type, @title, @summary, @content, @metadata,
                     @created, @updated, @useCount, @lastUsed, @isObsolete)
                """;
            BindNode(command, node);
            command.ExecuteNonQuery();
        }

        _logger.LogInformation("Memoria aggiunta: '{Title}' ({Type}, id {Id}).", node.Title, node.Type, node.Id);
        return node;
    }

    public void Update(MemoryNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(node.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(node.Title);

        node.UpdatedAt = DateTimeOffset.UtcNow;

        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE memory_node
                SET parent_id = @parent, type = @type, title = @title, summary = @summary,
                    content = @content, metadata = @metadata, updated_at = @updated, is_obsolete = @isObsolete
                WHERE id = @id
                """;
            command.Parameters.AddWithValue("@id", node.Id);
            command.Parameters.AddWithValue("@parent", (object?)node.ParentId ?? DBNull.Value);
            command.Parameters.AddWithValue("@type", node.Type.ToString());
            command.Parameters.AddWithValue("@title", node.Title);
            command.Parameters.AddWithValue("@summary", (object?)node.Summary ?? DBNull.Value);
            command.Parameters.AddWithValue("@content", (object?)node.Content ?? DBNull.Value);
            command.Parameters.AddWithValue("@metadata", (object?)node.Metadata ?? DBNull.Value);
            command.Parameters.AddWithValue("@updated", node.UpdatedAt.ToUnixTimeMilliseconds());
            command.Parameters.AddWithValue("@isObsolete", node.IsObsolete ? 1 : 0);
            command.ExecuteNonQuery();
        }
    }

    public void Move(string id, string? newParentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE memory_node SET parent_id = @parent, updated_at = @updated WHERE id = @id";
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@parent", (object?)newParentId ?? DBNull.Value);
            command.Parameters.AddWithValue("@updated", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            command.ExecuteNonQuery();
        }
    }

    public void Delete(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            // I figli vengono rimossi automaticamente (ON DELETE CASCADE + foreign_keys=ON).
            command.CommandText = "DELETE FROM memory_node WHERE id = @id";
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();
        }

        _logger.LogInformation("Memoria eliminata (id {Id}) con il suo sottoalbero.", id);
    }

    public void TouchUsage(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "UPDATE memory_node SET use_count = use_count + 1, last_used_at = @now WHERE id = @id";
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            command.ExecuteNonQuery();
        }
    }

    public void SetObsolete(string id, bool obsolete)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE memory_node SET is_obsolete = @value, updated_at = @now WHERE id = @id";
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@value", obsolete ? 1 : 0);
            command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            command.ExecuteNonQuery();
        }
    }

    // -----------------------------------------------------------------------------------------

    /// <summary>Apre una connessione con le foreign key attive (necessarie per la cascata).</summary>
    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private void EnsureSchema()
    {
        lock (_gate)
        {
            using var connection = Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    PRAGMA journal_mode = WAL;

                    CREATE TABLE IF NOT EXISTS memory_node (
                        id           TEXT PRIMARY KEY,
                        parent_id    TEXT REFERENCES memory_node(id) ON DELETE CASCADE,
                        type         TEXT NOT NULL,
                        title        TEXT NOT NULL,
                        summary      TEXT,
                        content      TEXT,
                        metadata     TEXT,
                        created_at   INTEGER NOT NULL,
                        updated_at   INTEGER NOT NULL,
                        use_count    INTEGER NOT NULL DEFAULT 0,
                        last_used_at INTEGER,
                        is_obsolete  INTEGER NOT NULL DEFAULT 0
                    );

                    CREATE INDEX IF NOT EXISTS idx_memory_parent ON memory_node(parent_id);
                    """;
                command.ExecuteNonQuery();
            }

            // Migrazione per i database creati prima dell'aggiunta di is_obsolete.
            EnsureColumn(connection, "is_obsolete", "INTEGER NOT NULL DEFAULT 0");
        }
    }

    private static void EnsureColumn(SqliteConnection connection, string column, string definition)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('memory_node') WHERE name = @name";
        check.Parameters.AddWithValue("@name", column);
        if (Convert.ToInt64(check.ExecuteScalar()) > 0)
            return;

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE memory_node ADD COLUMN {column} {definition}";
        alter.ExecuteNonQuery();
    }

    /// <summary>Imposta i parametri comuni a partire da un nodo (usato in INSERT).</summary>
    private static void BindNode(SqliteCommand command, MemoryNode node)
    {
        command.Parameters.AddWithValue("@id", node.Id);
        command.Parameters.AddWithValue("@parent", (object?)node.ParentId ?? DBNull.Value);
        command.Parameters.AddWithValue("@type", node.Type.ToString());
        command.Parameters.AddWithValue("@title", node.Title);
        command.Parameters.AddWithValue("@summary", (object?)node.Summary ?? DBNull.Value);
        command.Parameters.AddWithValue("@content", (object?)node.Content ?? DBNull.Value);
        command.Parameters.AddWithValue("@metadata", (object?)node.Metadata ?? DBNull.Value);
        command.Parameters.AddWithValue("@created", node.CreatedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("@updated", node.UpdatedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("@useCount", node.UseCount);
        command.Parameters.AddWithValue("@lastUsed",
            node.LastUsedAt is { } last ? last.ToUnixTimeMilliseconds() : DBNull.Value);
        command.Parameters.AddWithValue("@isObsolete", node.IsObsolete ? 1 : 0);
    }

    /// <summary>Costruisce un <see cref="MemoryNode"/> dalla riga corrente del reader.</summary>
    private static MemoryNode ReadNode(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        ParentId = reader.IsDBNull(1) ? null : reader.GetString(1),
        Type = Enum.Parse<MemoryNodeType>(reader.GetString(2)),
        Title = reader.GetString(3),
        Summary = reader.IsDBNull(4) ? null : reader.GetString(4),
        Content = reader.IsDBNull(5) ? null : reader.GetString(5),
        Metadata = reader.IsDBNull(6) ? null : reader.GetString(6),
        CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(7)),
        UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(8)),
        UseCount = reader.GetInt32(9),
        LastUsedAt = reader.IsDBNull(10) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(10)),
        IsObsolete = reader.GetInt64(11) != 0,
    };
}
