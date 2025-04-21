namespace Common.Network;

public static class Constant
{
    public const int PORT = 7777;
    public const int MAX_CONNECTION = 2;
    public const int BUFFER_SIZE = 2048;        // buffer manager 에서 사용하는 버퍼 크기
    public const int PACKET_BUFFER_SIZE = 2048; // socket session 에서 사용하는 패킷 버퍼 크기

    public const int HEADER_SIZE = 2;
    public const int OPCODE_SIZE = 2;
    public const int MAX_PACKET_SIZE = 2048;
}
