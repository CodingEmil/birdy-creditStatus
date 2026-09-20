using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

/// <summary>Naht (F007-T3): Provider-Defaults für den Hinzufügen-Dialog —
/// vorausgefüllter Pfad, Dateidialog-Start und Titel je Anbieter.</summary>
public sealed class AccountProviderPathsTests
{
    [Fact]
    public void Default_auth_paths_point_into_provider_homes()
    {
        Assert.EndsWith(
            Path.Combine(".codex", "auth.json"),
            AccountProviderPaths.DefaultAuthPath(AccountProviders.Codex));
        Assert.EndsWith(
            Path.Combine(".claude", ".credentials.json"),
            AccountProviderPaths.DefaultAuthPath(AccountProviders.Claude));
        Assert.EndsWith(
            Path.Combine(".pi", "agent", "auth.json"),
            AccountProviderPaths.DefaultAuthPath(AccountProviders.OpenCodeGo));
    }

    [Fact]
    public void Unknown_provider_falls_back_to_codex_path()
    {
        Assert.Equal(
            AccountProviderPaths.DefaultAuthPath(AccountProviders.Codex),
            AccountProviderPaths.DefaultAuthPath("fremd"));
    }

    [Fact]
    public void Dialog_titles_name_the_provider()
    {
        Assert.Contains("Claude", AccountProviderPaths.FileDialogTitle(AccountProviders.Claude));
        Assert.Contains("Codex", AccountProviderPaths.FileDialogTitle(AccountProviders.Codex));
        Assert.Contains("OpenCode", AccountProviderPaths.FileDialogTitle(AccountProviders.OpenCodeGo));
    }
}
