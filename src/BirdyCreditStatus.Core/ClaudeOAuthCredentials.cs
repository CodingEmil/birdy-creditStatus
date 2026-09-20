namespace BirdyCreditStatus.Core;

/// <summary>OAuth-Credentials aus dem Claude-Code-Login desselben Users (kein App-eigenes Secret).</summary>
public sealed record ClaudeOAuthCredentials(string AccessToken, string RefreshToken, long ExpiresAtUnixMs);
