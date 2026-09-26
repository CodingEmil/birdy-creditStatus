using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class AccountMenuTargetTests
{
    [Theory]
    [InlineData(AccountProviders.Codex)]
    [InlineData(AccountProviders.Claude)]
    [InlineData(AccountProviders.OpenCodeGo)]
    public void Concrete_account_named_like_provider_targets_itself(string provider)
    {
        var first = new Account(provider, "Arbeit", "first.json");
        var second = new Account(provider, provider, "second.json");
        Account[] accounts = [first, second];

        Assert.Equal(second.Name, AccountMenuTarget.Resolve(second, accounts));
        Assert.Equal(first.Name, AccountMenuTarget.Resolve(provider, accounts));
    }

    [Fact]
    public void Removed_or_replaced_account_context_never_falls_back_to_another_account()
    {
        var old = new Account(AccountProviders.Codex, "codex", "old.json");
        var replacement = old with { AuthFilePath = "different.json" };

        Assert.Null(AccountMenuTarget.Resolve(old, []));
        Assert.Null(AccountMenuTarget.Resolve(old, [replacement]));
        Assert.Null(AccountMenuTarget.Resolve(null, [replacement]));
        Assert.Null(AccountMenuTarget.Resolve("unknown", [replacement]));
        Assert.Null(AccountMenuTarget.Resolve(AccountProviders.Claude, [replacement]));
    }
}
