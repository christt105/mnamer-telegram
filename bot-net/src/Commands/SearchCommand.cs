using System.Text.RegularExpressions;
using Bot.CallbackQueries.Callbacks;
using Bot.Handlers;
using Bot.Utils;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Message = WTelegram.Types.Message;

namespace Bot.Commands;

public class SearchCommand : ICommand
{
    private readonly WTelegram.Bot _bot;
    private readonly MnamerHandler _mnamer;
    private readonly PendingFilesHandler _pendingFilesHandler;

    public SearchCommand(WTelegram.Bot bot, PendingFilesHandler pendingFilesHandler, MnamerHandler mnamerHandler)
    {
        _bot = bot;
        _pendingFilesHandler = pendingFilesHandler;
        _mnamer = mnamerHandler;
    }

    public async Task Execute(string[] args, Message msg)
    {
        var validExtensions = new[] { ".mkv", ".mp4", ".avi" };

        var files = Directory.EnumerateFiles("/data/watch", "*", SearchOption.AllDirectories)
            .Where(f => validExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        const string movieFormat = "MOVIE__SEP__{id_tmdb}__SEP__{name}__SEP__{year}";
        const string showFormat =
            "SHOW__SEP__{id_tvdb}__SEP__{series}__SEP__{season}__SEP__{episode}__SEP__{title}__SEP__{date}";

        _pendingFilesHandler.Clear();
        
        foreach (var file in files)
        {
            var arguments =
                $"--test --batch --no-style --language {_mnamer.Language} --movie-format \"{movieFormat}\" --episode-format \"{showFormat}\" --episode-api tvdb \"{file}\"";
            var output = await _mnamer.ExecuteMnamer(arguments);

            var match = Regex.Match(output, "moving to .+/(.+)$", RegexOptions.Multiline);
            if (!match.Success)
            {
                Log.Error($"\"moving to\" was not found. File: {file}. Output: {output}");
                await _bot.SendMessage(msg.Chat.Id, $"Couldn't find movie with path {file}.");
                return;
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
                    ? $"Movie not found for file `{file}`."
                    : $"Episode not found for file `{file}`.";
                await _bot.SendMessage(msg.Chat.Id, message, ParseMode.MarkdownV2);
                continue;
            }


            await _bot.SendMessage(msg.Chat.Id, message, ParseMode.MarkdownV2,
                linkPreviewOptions: new LinkPreviewOptions { ShowAboveText = true },
                replyMarkup: new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Move",
                            MoveFileCallback.Pack(_pendingFilesHandler.RegisterFile(fileName)))
                    }
                });
        }
    }

    public string Key => "/search";
    public string Description => "Searches for all the media in the watch folder.";
    public string Usage => "/search";

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
               New {Icons.MovieIcon}Movie found '{file}'

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
               New {Icons.TvIcon}Episode found '{file}'

               Series: {series}
               Season: {season}
               Episode: {episode}
               Title: {title}
               Release Date: {date}
               TVDB: [{tvdbId}](https://www.thetvdb.com/search?query={tvdbId})

               Do you want to move it to the shows folder?
               """;
    }
}