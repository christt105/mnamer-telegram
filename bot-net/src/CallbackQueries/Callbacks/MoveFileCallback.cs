using System.Text.RegularExpressions;
using Bot.Handlers;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Bot.Utils;
using Telegram.Bot.Extensions;

namespace Bot.CallbackQueries.Callbacks;

[Callback(Id)]
public class MoveFileCallback : ICallbackQuery
{
    public const string Id = "move";
    private readonly WTelegram.Bot _bot;
    private readonly DirectoryHandler _directoryHandler;
    private readonly string _guid;
    private readonly MnamerHandler _mnamer;
    private readonly PendingFilesHandler _pendingFilesHandler;

    private MoveFileCallback(string guid,
        PendingFilesHandler pendingFilesHandler,
        MnamerHandler mnamerHandler,
        WTelegram.Bot bot,
        DirectoryHandler directoryHandler)
    {
        _guid = guid;
        _pendingFilesHandler = pendingFilesHandler;
        _mnamer = mnamerHandler;
        _bot = bot;
        _directoryHandler = directoryHandler;
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
            $"--test --batch --no-style --language {_mnamer.Language} --movie-directory \"{_mnamer.MovieDirectoryFormat}\" --movie-format \"{_mnamer.MovieFormat}\" --episode-directory \"{_mnamer.EpisodeDirectoryFormat}\" --episode-format \"{_mnamer.EpisodeFormat}\" --movie-api tmdb --episode-api tvdb \"{file}\"";

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

        var lastPart = match.Groups[1].Value;

        var fileExists = File.Exists(lastPart);

        var text = @$"The file `{Markdown.Escape(file)}` will be moved to: 

`{Markdown.Escape(lastPart)}`.{(fileExists ? "\n\n⚠️ File already exists! It will be overwritten." : "")}

Do you want to continue?";

        await _bot.EditMessageText(message.Chat.Id, message.Id, text, ParseMode.MarkdownV2,
            linkPreviewOptions: new LinkPreviewOptions { ShowAboveText = true },
            replyMarkup: new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Move", AcceptMoveFileCallback.Pack(_guid))
                }
            }
        );
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        var guid = fields[0];
        return new MoveFileCallback(guid, dispatcher.PendingFilesHandler, dispatcher.MnamerHandler, dispatcher.Bot,
            dispatcher.DirectoryHandler);
    }

    public static string Pack(string guid)
    {
        return CallbackDataPacker.Pack(Id, [guid]);
    }
}