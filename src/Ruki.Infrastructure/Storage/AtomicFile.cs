namespace Ruki.Infrastructure.Storage;

/// <summary>
/// Scrittura "atomica" su file: si scrive prima un file temporaneo nella stessa
/// cartella e poi lo si rinomina sul file di destinazione. Così un crash a metà
/// scrittura non lascia mai il file finale troncato o corrotto — o c'è la versione
/// vecchia, o c'è quella nuova completa.
/// </summary>
internal static class AtomicFile
{
    public static void WriteAllBytes(string path, byte[] bytes)
    {
        // Il temp sta nella stessa cartella del file finale, così il rename resta
        // sullo stesso volume (operazione veloce e atomica su Windows/NTFS).
        var tempPath = path + ".tmp";
        File.WriteAllBytes(tempPath, bytes);
        File.Move(tempPath, path, overwrite: true);
    }

    public static void WriteAllText(string path, string content)
        => WriteAllBytes(path, System.Text.Encoding.UTF8.GetBytes(content));
}
