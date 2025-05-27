using System.Buffers;

namespace Cube.Core.Pool;

public class BufferArrayPool
{
    public static bool TryRent(Span<byte> source, int size, out ReadOnlyMemory<byte> data, out byte[]? rentedBuffer)
    {
        data = ReadOnlyMemory<byte>.Empty;
        rentedBuffer = null;

        if (source.Length < size)
        {
            return false;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(size);
        if (buffer == null)
        {
            return false;
        }

        source.CopyTo(buffer);
        data = new ReadOnlyMemory<byte>(buffer, 0, size);
        rentedBuffer = buffer;

        return true;
    }

    public static bool TryRent(int size, out ReadOnlyMemory<byte> data, out byte[]? rentedBuffer)
    {
        data = ReadOnlyMemory<byte>.Empty;
        rentedBuffer = null;
        if (size <= 0)
        {
            return false;
        }
        var buffer = ArrayPool<byte>.Shared.Rent(size);
        if (buffer == null)
        {
            return false;
        }
        data = new ReadOnlyMemory<byte>(buffer, 0, size);
        rentedBuffer = buffer;
        return true;
    }

    public static void Return(byte[]? rentedBuffer)
    {
        if (rentedBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }
}
