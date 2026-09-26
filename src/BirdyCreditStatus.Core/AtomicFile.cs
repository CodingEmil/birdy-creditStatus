namespace BirdyCreditStatus.Core;

/// <summary>Writes local state beside the destination, then replaces it in one move.
/// Failed writes leave the previous file intact; no shared temporary filename.</summary>
internal static class AtomicFile
{
    public static bool TryWrite(string path, string content)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temporary, content);
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
