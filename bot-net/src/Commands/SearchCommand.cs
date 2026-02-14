using Bot.Handlers;
using Bot.Utils;
using Message = WTelegram.Types.Message;

namespace Bot.Commands;

public class SearchCommand : ICommand
{
    private readonly NewFileHandler _newFileHandler;
    private readonly DirectoryHandler _directoryHandler;

    public SearchCommand(NewFileHandler newFileHandler, DirectoryHandler directoryHandler)
    {
        _newFileHandler = newFileHandler;
        _directoryHandler = directoryHandler;
    }

    public async Task Execute(string[] args, Message msg)
    {
        var validExtensions = new[] { ".mkv", ".mp4", ".avi" };

        var files = Directory.EnumerateFiles(_directoryHandler.WatchDirectory, "*", SearchOption.AllDirectories)
            .Where(f => validExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToList();


        _newFileHandler.Clear();

        foreach (var file in files)
            if (await _newFileHandler.HandleFile(file))
                return;
    }

    public string Key => "/search";
    public string Description => "Searches for all the media in the watch folder.";
    public string Usage => "/search";
}