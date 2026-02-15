using System.Text.RegularExpressions;
using Bot.Handlers;
using Telegram.Bot.Types;

using Telegram.Bot.Types;
using Bot.Utils;
namespace Bot.CallbackQueries.Callbacks;

[Callback(Id)]
public class AcceptMoveFileCallback : ICallbackQuery
{
    public const string Id = "accept_move";
    private readonly WTelegram.Bot _bot;
    private readonly DirectoryHandler _directoryHandler;
    private readonly MnamerHandler _mnamer;
    private readonly PendingFilesHandler _pendingFilesHandler;

    private readonly string _guid;


    private AcceptMoveFileCallback(string guid, PendingFilesHandler pendingFilesHandler, MnamerHandler mnamer,
        WTelegram.Bot bot, DirectoryHandler dispatcherDirectoryHandler)
    {
        _guid = guid;
        _pendingFilesHandler = pendingFilesHandler;
        _mnamer = mnamer;
        _bot = bot;
        _directoryHandler = dispatcherDirectoryHandler;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var fileData = _pendingFilesHandler.GetFile(_guid);

        if (fileData == null)
        {
            Log.Error($"File with guid {_guid} not found.");
            //TODO: if not found, try to get the file from inside the message text
            return;
        }

        var (file, forcedId, forcedType) = fileData.Value;

        var arguments =
            $"--batch --no-style --language {_mnamer.Language} --movie-directory \"{_mnamer.MovieDirectoryFormat}\" --movie-format \"{_mnamer.MovieFormat}\" --episode-directory \"{_mnamer.EpisodeDirectoryFormat}\" --episode-format \"{_mnamer.EpisodeFormat}\" --movie-api tmdb --episode-api tvdb \"{file}\"";

        if (forcedId != null && forcedType != null)
        {
            if (forcedType == MediaType.Movie)
                arguments += $" --id-tmdb {forcedId}";
            else
                arguments += $" --id-tvdb {forcedId}";
        }

        var result = await _mnamer.ExecuteMnamer(arguments);

        var match = Regex.Match(result, "moving to (/.+)", RegexOptions.Multiline);
        if (!match.Success)
        {
            Log.Error($"\"moving to\" was not found. File: {file}. Output: {result}");
            await _bot.SendMessage(message.Chat.Id, $"Couldn't find movie with path {file}.");
            return;
        }

        if (result.Contains("OK!"))
        {
            await _bot.EditMessageText(message.Chat.Id, message.Id, $"File {file} moved to `{match.Groups[1].Value}`");
            _pendingFilesHandler.UnregisterFile(_guid);
        }

    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        var guid = fields[0];
        return new AcceptMoveFileCallback(
            guid,
            dispatcher.PendingFilesHandler,
            dispatcher.MnamerHandler,
            dispatcher.Bot,
            dispatcher.DirectoryHandler);
    }

    public static string Pack(string guid)
    {
        return CallbackDataPacker.Pack(Id, [guid]);
    }
}