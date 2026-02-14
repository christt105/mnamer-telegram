using Bot;
using Bot.Handlers;
using Bot.Utils;
using System.Collections.Concurrent;

public class FileWatcherService
{
    private readonly WTelegram.Bot _bot;
    private readonly DirectoryHandler _directoryHandler;
    private readonly NewFileHandler _newFileHandler;
    private readonly TaskQueue _queue;
    private FileSystemWatcher? _watcher;

    private readonly ConcurrentDictionary<string, Timer> _timers = new();

    public FileWatcherService(BotDispatcher bot)
    {
        _newFileHandler = bot.NewFileHandler;
        _directoryHandler = bot.DirectoryHandler;
        _queue = bot.Queue;
        _bot = bot.Bot;
    }

    public Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var watchDirectory = _directoryHandler.WatchDirectory;
        _watcher = new FileSystemWatcher(watchDirectory, "*.*")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite
        };

        _watcher.Created += OnFileEvent;
        _watcher.Changed += OnFileEvent;
        _watcher.EnableRaisingEvents = true;

        Log.Info($"File watcher started on {watchDirectory}");
        return Task.CompletedTask;
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        var ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
        if (!NewFileHandler.VideoExtensions.Contains(ext))
            return;

        _timers.AddOrUpdate(e.FullPath,
            key => CreateTimer(e.FullPath),
            (key, oldTimer) =>
            {
                oldTimer.Change(TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
                return oldTimer;
            });
    }

    private Timer CreateTimer(string filePath)
    {
        return new Timer(async _ =>
        {
            _timers.TryRemove(filePath, out var timer);

            try
            {
                Log.Info($"File {filePath} appears stable. Checking if ready...");

                await WaitForFileReady(filePath);

                Log.Info($"File {filePath} is fully ready. Processing...");
                await _queue.Enqueue(async () => await _newFileHandler.HandleFile(filePath));
            }
            catch (Exception ex)
            {
                Log.Error($"Error waiting for file {filePath} to be ready: {ex.Message}");
            }

        }, null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
    }

    private async Task WaitForFileReady(string filePath)
    {
        const int maxRetries = 50;
        const int delayMs = 500;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using (FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    if (stream.Length > 0)
                    {
                        return;
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            await Task.Delay(delayMs);
        }

        throw new TimeoutException($"File {filePath} was not ready after {(maxRetries * delayMs) / 1000} seconds.");
    }
}
