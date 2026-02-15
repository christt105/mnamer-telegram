using Bot.Utils;
namespace Bot.Handlers;

public class PendingFilesHandler
{
    private readonly Dictionary<string, string> _fileToGuid = new();
    
    // Store guid -> (FilePath, ForcedId, ForcedType)
    private readonly Dictionary<string, (string FilePath, string? ForcedId, MediaType? ForcedType)> _guidToFile = new();

    public void Clear()
    {
        _fileToGuid.Clear();
        _guidToFile.Clear();
    }

    public string RegisterFile(string file, string? forcedId = null, MediaType? forcedType = null)
    {
        if (_fileToGuid.TryGetValue(file, out var value))
        {
            // Update existing entry with new forced ID/Type if provided
            if (forcedId != null || forcedType != null)
            {
                _guidToFile[value] = (file, forcedId, forcedType);
            }
            return value;
        }

        value = Guid.NewGuid().ToString();
        _fileToGuid[file] = value;
        _guidToFile[value] = (file, forcedId, forcedType);

        return value;
    }

    public (string FilePath, string? ForcedId, MediaType? ForcedType)? GetFile(string guid)
    {
        return _guidToFile.GetValueOrDefault(guid);
    }

    public void UnregisterFile(string guid)
    {
        if (_guidToFile.TryGetValue(guid, out var value)) _fileToGuid.Remove(value.FilePath);

        _guidToFile.Remove(guid);
    }
}