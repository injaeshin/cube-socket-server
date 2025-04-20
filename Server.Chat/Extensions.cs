using System.Net.Sockets;
using System.Text;

namespace Server.Chat;

public static class Extensions
{
    // string -> ReadOnlyMemory<byte>
    public static ReadOnlyMemory<byte> ToReadOnlyMemory(this string str)
    {
        return Encoding.UTF8.GetBytes(str);
    }

    // string -> ReadOnlySpan<byte>
    public static ReadOnlySpan<byte> ToReadOnlySpan(this string str)
    {
        return Encoding.UTF8.GetBytes(str);
    }

    // ReadOnlyMemory<byte> -> string
    public static string ToString(this ReadOnlyMemory<byte> memory)
    {
        return Encoding.UTF8.GetString(memory.Span);
    }

    // ReadOnlyMemory<byte> -> ReadOnlySpan<byte>
    public static ReadOnlySpan<byte> ToReadOnlySpan(this ReadOnlyMemory<byte> memory)
    {
        return memory.Span;
    }


}

public static class SocketAsyncEventArgsExtensions
{
    public static ReadOnlySpan<byte> ReceivedSpan(this SocketAsyncEventArgs e)
    {
        return new ReadOnlySpan<byte>(e.Buffer!, e.Offset, e.BytesTransferred);
    }

    public static ArraySegment<byte> ReceivedArraySegment(this SocketAsyncEventArgs e)
    {
        return new ArraySegment<byte>(e.Buffer!, e.Offset, e.BytesTransferred);
    }
}