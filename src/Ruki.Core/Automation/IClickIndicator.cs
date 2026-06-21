namespace Ruki.Core.Automation;

/// <summary>
/// Indicatore visivo che segue il cursore mentre l'agente controlla il PC: un anello "click-through"
/// (non intercetta i click) che resta visibile per tutta l'esecuzione e si riempie per segnalare un click.
/// </summary>
public interface IClickIndicator
{
    /// <summary>Mostra l'anello e inizia a seguire il cursore.</summary>
    void Start();

    /// <summary>Nasconde l'anello (a fine esecuzione, per qualunque motivo).</summary>
    void Stop();

    /// <summary>Riempie (true) o svuota (false) il cerchio, per segnalare un click in corso.</summary>
    void SetClicking(bool clicking);
}
