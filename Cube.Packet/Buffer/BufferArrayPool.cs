using System.Buffers;
using System.Diagnostics;

namespace Cube.Packet;

public static class BufferArrayPool
{
    private static readonly HashSet<byte[]> _activeBuffers = new();
    private static int _inUse = 0;

    public static byte[] Rent(int size)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be greater than zero.");
        }

        var buffer = ArrayPool<byte>.Shared.Rent(size) ?? throw new InvalidOperationException("Failed to rent buffer from ArrayPool.");

#if DEBUG
        lock (_activeBuffers)
        {
            _activeBuffers.Add(buffer);
        }
#endif
        Interlocked.Increment(ref _inUse);

        return buffer;
    }

    public static bool TryRent(ReadOnlySpan<byte> source, int size, out ReadOnlyMemory<byte> data, out byte[]? rentedBuffer)
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

#if DEBUG
        lock (_activeBuffers)
        {
            _activeBuffers.Add(buffer);
        }
#endif
        Interlocked.Increment(ref _inUse);

        return true;
    }

    public static bool TryRent(int size, out byte[]? rentedBuffer)
    {
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

        rentedBuffer = buffer;

#if DEBUG
        lock (_activeBuffers)
        {
            _activeBuffers.Add(buffer);
        }
#endif
        Interlocked.Increment(ref _inUse);

        return true;
    }

    public static void Return(byte[]? rentedBuffer)
    {
        if (rentedBuffer != null)
        {
#if DEBUG
            lock (_activeBuffers)
            {
                if (!_activeBuffers.Contains(rentedBuffer))
                {
                    Debug.Fail("Buffer was returned multiple times!");
                }
            }
#endif

            ArrayPool<byte>.Shared.Return(rentedBuffer);
            Interlocked.Decrement(ref _inUse);
            if (_inUse < 0)
            {
                throw new InvalidOperationException("Buffer was returned multiple times!");
            }
        }
    }

    public static int GetInUseCount()
    {
        return _inUse;
    }
}