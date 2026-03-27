using Bot.Handlers;
using Bot.Utils;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Message = WTelegram.Types.Message;
using Update = WTelegram.Types.Update;

namespace Bot;

public class BotDispatcher
{
    private readonly CallbackQueryHandler _callbackQueryHandler;
    private readonly CommandHandler _commandHandler;

    private readonly MessageHandler _messageHandler;
    
    public ChatId AllowedUser { get; }
    
    public NewFileHandler NewFileHandler { get; }

    public BotDispatcher(WTelegram.Bot bot, TaskQueue queue)
    {
        Bot = bot;
        Queue = queue;

        AllowedUser = Convert.ToInt64(Environment.GetEnvironmentVariable("TELEGRAM_AUTH_USER_ID"));

        DirectoryHandler = new DirectoryHandler();
        MnamerHandler = new MnamerHandler(DirectoryHandler);
        NewFileHandler = new NewFileHandler(MnamerHandler, bot, PendingFilesHandler, AllowedUser);
        
        _commandHandler = new CommandHandler(this);
        _messageHandler = new MessageHandler(this);
        _callbackQueryHandler = new CallbackQueryHandler(this);
        PendingActionHandler = new PendingActionHandler(Bot);
    }

    public WTelegram.Bot Bot { get; }

    public TaskQueue Queue { get; }

    public PendingActionHandler PendingActionHandler { get; }

    public PendingFilesHandler PendingFilesHandler { get; } = new();
    public DirectoryHandler DirectoryHandler { get; }
    public MnamerHandler MnamerHandler { get; }

    public async Task InitBot()
    {
        var me = await Bot.GetMe();
        Log.Info($"Bot connected as @{me.Username}");

        await Bot.DropPendingUpdates();

        await Bot.SendMessage(AllowedUser, "Bot started");

        Bot.OnMessage += HandleMessage;
        Bot.OnUpdate += HandleUpdate;
        Bot.OnError += HandleError;

        Log.Info("Bot initialized. Waiting for updates...");
    }

    public async Task HandleMessage(Message msg, UpdateType type)
    {
        if (msg.From == null || msg.From.Id != AllowedUser)
        {
            Log.Info($"User {msg.From?.Username} with ID({msg.From?.Id}) is not allowed.");
            return;
        }

        if (!string.IsNullOrEmpty(msg.Text))
        {
            if (msg.Text.StartsWith('/'))
                await _commandHandler.Handle(msg, type);
            else if (PendingActionHandler.HasPendingAction())
                await PendingActionHandler.Handle(msg, type);
            else
                await _messageHandler.Handle(msg, type);
        }
    }

    private async Task HandleUpdate(Update update)
    {
        switch (update.Type)
        {
            case UpdateType.CallbackQuery:
                if (update.CallbackQuery == null)
                {
                    Log.Error("Update type is CallbackQuery but no callback query was provided.");
                    return;
                }

                var callback = update.CallbackQuery;

                if (callback.From == null || callback.From.Id != AllowedUser)
                {
                    Log.Info($"User {callback.From?.Username} with ID({callback.From?.Id}) is not allowed.");
                    return;
                }

                await _callbackQueryHandler.HandleCallbackQueryAsync(callback);
                break;
            case UpdateType.Unknown:
                Console.WriteLine("Unknown update type: {0}", update.TLUpdate?.GetType().Name);
                break;
            default:
                Console.WriteLine($"No case to {update.Type}. {update.TLUpdate?.GetType().Name}");
                break;
        }
    }

    private Task HandleError(Exception e, HandleErrorSource src)
    {
        Log.Error($"Error ({src}) at {e.Source}", e);
        return Task.CompletedTask;
    }
}
