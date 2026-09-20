namespace BirdyCreditStatus.Core;

/// <summary>Naht für den OpenCode-Go-Key (F003, D005): ausschließlich lesend aus der
/// Pi-Harness-Datei, kein Save-Pfad (API-Key, keine Rotation). Tests stubben per Interface.</summary>
public interface IOpenCodeGoCredentialStore
{
    /// <summary>Liefert den API-Key oder null (Setup-Signal), nie Throw.</summary>
    Task<string?> LoadAsync(CancellationToken cancellationToken = default);
}
