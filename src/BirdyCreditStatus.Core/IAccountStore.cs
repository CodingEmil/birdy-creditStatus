namespace BirdyCreditStatus.Core;

/// <summary>Konten-Naht A (F006-T1, Tripel seit F007-T1): Liste laden/speichern plus
/// Anlegen/Entfernen/Umbenennen. Namen sind global über alle Anbieter eindeutig.
/// Laden wirft nie (fehlend/korrupt = leer bzw. Defaults-Migration).</summary>
public interface IAccountStore
{
    IReadOnlyList<Account> Load();

    void Save(IReadOnlyList<Account> accounts);

    bool Add(Account account);

    bool Remove(string name);

    bool Rename(string oldName, string newName);
}
