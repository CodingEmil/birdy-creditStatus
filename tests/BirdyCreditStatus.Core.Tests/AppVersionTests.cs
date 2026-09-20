using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

/// <summary>Laufzeit-Versionsanzeige im Popup-Footer (F006-Nachlese):
/// „vX.Y.Z\" aus der Assembly-Version, Fallback ohne Absturz.</summary>
public sealed class AppVersionTests
{
    [Fact]
    public void Formats_assembly_version_with_v_prefix()
    {
        Assert.Equal("v1.1.0", AppVersion.Format(new Version(1, 1, 0)));
    }

    [Fact]
    public void Omits_build_and_revision_parts()
    {
        Assert.Equal("v1.1.0", AppVersion.Format(new Version(1, 1, 0, 0)));
    }

    [Fact]
    public void Null_version_yields_fallback_text()
    {
        Assert.Equal("Version unbekannt", AppVersion.Format(null));
    }
}
