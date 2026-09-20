namespace BirdyCreditStatus.Core;

/// <summary>Codex-OAuth-Login aus einer Auth-Datei (D004): Access- plus Refresh-Token,
/// Account-ID für den ChatGPT-Account-Header (optional).</summary>
public sealed record CodexOAuthCredentials(string AccessToken, string RefreshToken, string? AccountId);
