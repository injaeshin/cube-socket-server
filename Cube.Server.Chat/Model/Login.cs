
using Cube.Packet;

namespace Cube.Server.Chat.Model;

public class Login : IModel<Login>
{
    public string Username { get; set; } = string.Empty;

    public static Login? Read(ReadOnlyMemory<byte> payload)
    {
        try
        {
            var reader = new PacketReader(payload);
            if (reader.RemainingLength < 2)
            {
                return null;
            }

            var username = reader.ReadString();
            return new Login { Username = username };
        }
        catch (Exception)
        {
            return null;
        }
    }
}