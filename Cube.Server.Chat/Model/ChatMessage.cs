using Cube.Packet;

namespace Cube.Server.Chat.Model;

interface IModel<T>
{
    static abstract T? Read(ReadOnlyMemory<byte> payload);
}

public class ChatMessage : IModel<ChatMessage>
{
    public string Sender { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public static ChatMessage? Read(ReadOnlyMemory<byte> payload)
    {
        try
        {
            var reader = new PacketReader(payload);
            if (reader.RemainingLength < 2)
            {
                return null;
            }

            var message = reader.ReadString();
            return new ChatMessage { Message = message };
        }
        catch (Exception)
        {
            return null;
        }
    }
}

public static class ChatMessageExtensions
{
    public static ChatMessage? WithSender(this ChatMessage? message, string sender)
    {
        if (message != null)
            message.Sender = sender;
        return message;
    }
}