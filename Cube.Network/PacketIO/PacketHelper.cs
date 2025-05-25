using System.Text;

namespace Cube.Network.PacketIO;

public class PacketHelper
{
    public static bool TryGetUdpHeader(ReadOnlyMemory<byte> data, out string sessionId, out ushort sequence, out ushort ack, out ReadOnlyMemory<byte> payload)
    {
        sessionId = string.Empty;
        sequence = 0;
        ack = 0;
        payload = ReadOnlyMemory<byte>.Empty;

        if (data.Length < 4)
        {
            return false;
        }

        try
        {
            var span = data.Span;
            sessionId = Encoding.UTF8.GetString(span[..4]);
            sequence = (ushort)(span[4] << 8 | span[5]);
            ack = (ushort)(span[6] << 8 | span[7]);
            payload = data[8..];

            if (sessionId == string.Empty || sequence != 0 || ack != 0)
            {
                return false;
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool TryGetPacketType(ReadOnlyMemory<byte> data, out ushort packetType)
    {
        packetType = 0;

        if (data.Length < 2)
        {
            return false;
        }

        packetType = (ushort)(data.Span[0] << 8 | data.Span[1]);
        return true;
    }
}

