using System.Text.RegularExpressions;
using Bot.CallbackQueries.Callbacks;
using Bot.Handlers;
using Telegram.Bot.Extensions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.Utils;

public enum MediaType
{
    Movie,
    Episode
}

public class NewFileHandler
{
    public static readonly string[] VideoExtensions =
        { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm" };

    private readonly WTelegram.Bot _bot;
    private readonly int _chatId;
    private readonly MnamerHandler _mnamer;
    private readonly PendingFilesHandler _pendingFilesHandler;

    // Store message ID -> (FilePath, DetectType)
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, (string FilePath, MediaType Type)> _awaitingReply = new();

    public NewFileHandler(MnamerHandler mnamer, WTelegram.Bot bot, PendingFilesHandler pendingFilesHandler, int chatId)
    {
        _mnamer = mnamer;
        _bot = bot;
        _pendingFilesHandler = pendingFilesHandler;
        _chatId = chatId;
    }

    public async Task<bool> HandleFile(string file)
    {
        return await HandleFile(file, null, null);
    }

    public async Task<bool> HandleFile(string file, string? forcedId, MediaType? forcedType)
    {
        const string movieFormat = "MOVIE__SEP__{id_tmdb}__SEP__{name}__SEP__{year}";
        const string showFormat =
            "SHOW__SEP__{id_tvdb}__SEP__{series}__SEP__{season}__SEP__{episode}__SEP__{title}__SEP__{date}";

        var arguments =
            $"--test --batch --no-style --language {_mnamer.Language} --movie-format \"{movieFormat}\" --episode-format \"{showFormat}\" --movie-api tmdb --episode-api tvdb \"{file}\"";

        if (forcedId != null && forcedType != null)
        {
            if (forcedType == MediaType.Movie)
                arguments += $" --id-tmdb {forcedId}";
            else
                arguments += $" --id-tvdb {forcedId}";
        }

        var output = await _mnamer.ExecuteMnamer(arguments);

        var match = Regex.Match(output, "moving to .+/(.+)$", RegexOptions.Multiline);
        if (!match.Success)
        {
            Log.Error($"\"moving to\" was not found. File: {Markdown.Escape(file)}. Output: {output}");
            await _bot.SendMessage(_chatId, $"Couldn't find movie with path {Markdown.Escape(file)}.");
            return true;
        }

        var lastPart = match.Groups[1].Value; // MOVIE__SEP__558__SEP__Spider-Man 2__SEP__2004
        var parts = lastPart.Split("__SEP__");

        var message = "Could not detect if it is a Movie or a Show";

        var fileName = Path.GetFileName(file);

        if (lastPart.StartsWith("MOVIE"))
            message = GetMovieMessage(parts, fileName);
        else if (lastPart.StartsWith("SHOW"))
            message = GetEpisodeMessage(parts, fileName);

        if (string.IsNullOrEmpty(message))
        {
            message = lastPart.StartsWith("MOVIE")
                ? $"Movie not found for file `{Markdown.Escape(file)}`."
                : $"Episode not found for file `{Markdown.Escape(file)}`.";
            var sent = await _bot.SendMessage(_chatId, message, ParseMode.MarkdownV2);
            _awaitingReply.TryAdd(sent.Id, (file, lastPart.StartsWith("MOVIE") ? MediaType.Movie : MediaType.Episode));
        }
        else
        {
            var fileGuid = _pendingFilesHandler.RegisterFile(file, forcedId, forcedType);

            var sent = await _bot.SendMessage(_chatId, message, ParseMode.MarkdownV2,
                linkPreviewOptions: new LinkPreviewOptions { ShowAboveText = true },
                replyMarkup: new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Move",
                            MoveFileCallback.Pack(fileGuid))
                    }
                });

            _awaitingReply.TryAdd(sent.Id, (file, lastPart.StartsWith("MOVIE") ? MediaType.Movie : MediaType.Episode));
        }

        return false;
    }

    private string? GetMovieMessage(string[] parts, string file)
    {
        // MOVIE__SEP__{id_tmdb}__SEP__{name}__SEP__{year}
        var tmdbId = parts.Length > 1 ? parts[1] : "";
        var name = parts.Length > 2 ? parts[2] : "";
        var year = parts.Length > 3 ? parts[3] : "";

        Log.Info($"Movie: {name} | {year} | {tmdbId}");

        return string.IsNullOrEmpty(tmdbId)
            ? null
            : $"""
               New {Icons.MovieIcon}Movie found `{Markdown.Escape(file)}`

               Name: {name}
               Year: {year}
               TMDB: [{tmdbId}](https://www.themoviedb.org/movie/{tmdbId})

               Do you want to move to the movies folder?
               """;
    }

    private string? GetEpisodeMessage(string[] parts, string file)
    {
        // SHOW__SEP__{id_tvdb}__SEP__{series}__SEP__{season}__SEP__{episode}__SEP__{title}__SEP__{date}

        var tvdbId = parts.Length > 1 ? parts[1] : "";
        var series = parts.Length > 2 ? parts[2] : "";
        var season = parts.Length > 3 ? parts[3] : "";
        var episode = parts.Length > 4 ? parts[4] : "";
        var title = parts.Length > 5 ? parts[5] : "";
        var date = parts.Length > 6 ? parts[6] : "";

        Log.Info(
            $"Episode: tvdb({tvdbId}) | series({series}) | season({season}) | episode({episode}) | title({title}) | date({date})");

        // Why tvdb, why you cannot link directly with the id?
        // TODO: Include TVDB library to get link to thetvdb directly
        return string.IsNullOrEmpty(tvdbId)
            ? null
            : $"""
               New {Icons.TvIcon}Episode found `{Markdown.Escape(file)}`

               Series: {Markdown.Escape(series)}
               Season: {season}
               Episode: {episode}
               Title: {Markdown.Escape(title)}
               Release Date: {date}
               TVDB: [{tvdbId}](https://www.thetvdb.com/search?query={tvdbId})

               Do you want to move it to the shows folder?
               """;
    }

    public void Clear()
    {
        _pendingFilesHandler.Clear();
        _awaitingReply.Clear();
    }

    public async Task HandleReply(int replyToMsgId, string text)
    {
        if (!_awaitingReply.TryGetValue(replyToMsgId, out var data))
            return;

        var (filePath, mediaType) = data;
        string? forcedId = null;
        MediaType? forcedType = null;

        // Check for prefixes
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1)
        {
            var prefix = parts[0].ToLowerInvariant();
            var id = parts[1];

            if (prefix == "tmdb" || prefix == "movie")
            {
                forcedType = MediaType.Movie;
                forcedId = id;
            }
            else if (prefix == "tvdb" || prefix == "show" || prefix == "series")
            {
                forcedType = MediaType.Episode;
                forcedId = id;
            }
        }

        if (forcedId == null)
        {
            // Assume it's just the ID, use stored type
            forcedId = text.Trim();
            forcedType = mediaType;
        }

        Log.Info($"Reprying file {filePath} with ID {forcedId} and Type {forcedType}");
        await _bot.SendMessage(_chatId, $"Retrying with ID {forcedId}...", replyParameters: new ReplyParameters { MessageId = replyToMsgId });

        _awaitingReply.TryRemove(replyToMsgId, out _);

        await HandleFile(filePath, forcedId, forcedType);
    }
}