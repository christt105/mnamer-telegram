using Microsoft.Data.Sqlite;

namespace Bot;

public class Worker : BackgroundService
{
    private readonly TaskQueue _queue = new();
    private FileWatcherService _fileWatcher;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var apiId = int.Parse(Environment.GetEnvironmentVariable("TELEGRAM_API_ID")!);
            var apiHash = Environment.GetEnvironmentVariable("TELEGRAM_API_HASH")!;
            var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")!;

            await using var connection = new SqliteConnection(@"Data Source=/data/bot.sqlite");

            var bot = new WTelegram.Bot(botToken, apiId, apiHash, connection);

            var botDispatcher = new BotDispatcher(bot, _queue);

            await botDispatcher.InitBot();
            
            _fileWatcher = new FileWatcherService(botDispatcher);
            await _fileWatcher.ExecuteAsync(stoppingToken);

            _ = _queue.StartProcessing(stoppingToken);

            while (!stoppingToken.IsCancellationRequested) await Task.Delay(1000, stoppingToken);
        }
        catch (Exception ex)
        {
            Log.Error("Failed to run the bot", ex);
        }
    }
}