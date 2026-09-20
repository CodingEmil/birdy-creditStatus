using System.Text.Json;

namespace BirdyCreditStatus.Core;

/// <summary>Liest ausschließlich das Feld <c>opencode-go.key</c> aus der Pi-Harness-Datei
/// (D005, Default %USERPROFILE%/.pi/agent/auth.json). Fehlende Datei, fehlendes Feld,
/// leeres/korruptes JSON → null (Setup-Signal), nie Throw. Fremde Felder werden ignoriert,
/// nichts wird geloggt oder zurückgeschrieben (read-only).</summary>
public sealed class FileOpenCodeGoCredentialStore(string? pathOverride = null) : IOpenCodeGoCredentialStore
{
    private readonly string _path = pathOverride
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pi", "agent", "auth.json");

    public async Task<string?> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            await using var stream = File.OpenRead(_path);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("opencode-go", out var section)
                || section.ValueKind != JsonValueKind.Object
                || !section.TryGetProperty("key", out var key)
                || key.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var apiKey = key.GetString();
            return string.IsNullOrEmpty(apiKey) ? null : apiKey;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
