namespace Bot.Handlers;

public class PendingFilesHandler
{
    private readonly Dictionary<string, string> _fileToGuid = new();
    private readonly Dictionary<string, string> _guidToFile = new();

    public void Clear()
    {
        _fileToGuid.Clear();
        _guidToFile.Clear();
    }

    public string RegisterFile(string file)
    {
        if (_fileToGuid.TryGetValue(file, out var value))
            return value;

        value = Guid.NewGuid().ToString();
        _fileToGuid[file] = value;
        _guidToFile[value] = file;

        return value;
    }

    public string? GetFile(string guid)
    {
        return _guidToFile.GetValueOrDefault(guid);
    }

    public void UnregisterFile(string guid)
    {
        if (_guidToFile.TryGetValue(guid, out var value)) _fileToGuid.Remove(value);

        _guidToFile.Remove(guid);
    }
}