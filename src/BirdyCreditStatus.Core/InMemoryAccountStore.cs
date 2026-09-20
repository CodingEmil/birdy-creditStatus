namespace BirdyCreditStatus.Core;

/// <summary>In-Memory-Fake der Konten-Naht A für Tests (keine echte Datei).
/// Startet leer (keine Migration), gleiche Eindeutigkeits-/Validierungsregeln
/// wie <see cref="AccountStore"/>: bekannter Provider, Name global eindeutig,
/// Name/Pfad nicht leer.</summary>
public sealed class InMemoryAccountStore : IAccountStore
{
    private readonly List<Account> _accounts = new();

    public IReadOnlyList<Account> Load() => _accounts.ToList();

    public void Save(IReadOnlyList<Account> accounts)
    {
        _accounts.Clear();
        _accounts.AddRange(accounts.Where(IsValid));
    }

    public bool Add(Account account)
    {
        if (!IsValid(account) || _accounts.Any(a => a.Name == account.Name))
        {
            return false;
        }

        _accounts.Add(account);
        return true;
    }

    public bool Remove(string name) => _accounts.RemoveAll(a => a.Name == name) > 0;

    public bool Rename(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return false;
        }

        var index = _accounts.FindIndex(a => a.Name == oldName);
        if (index < 0 || _accounts.Any(a => a.Name == newName))
        {
            return false;
        }

        _accounts[index] = _accounts[index] with { Name = newName };
        return true;
    }

    private static bool IsValid(Account account) =>
        AccountProviders.IsKnown(account.Provider)
        && !string.IsNullOrWhiteSpace(account.Name)
        && !string.IsNullOrWhiteSpace(account.AuthFilePath);
}
