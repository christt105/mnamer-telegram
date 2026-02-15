using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Message = WTelegram.Types.Message;

namespace Bot.Handlers;

public class MessageHandler
{
    private readonly BotDispatcher _botDispatcher;

    public MessageHandler(BotDispatcher botDispatcher)
    {
        _botDispatcher = botDispatcher;
    }

    public async Task Handle(Message msg, UpdateType type)
    {
        if (msg.ReplyToMessage != null)
        {
            await _botDispatcher.NewFileHandler.HandleReply(msg.ReplyToMessage.Id, msg.Text);
        }
    }
}