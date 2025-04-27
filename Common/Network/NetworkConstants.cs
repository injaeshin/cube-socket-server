namespace Common.Network;

public class NetConsts
{
    // 네트워크 설정
    public const int PORT = 7777;
    public const int LISTEN_BACKLOG = 100;
    public const int MAX_CONNECTION = 1000;

    // 버퍼 크기
    public const int BUFFER_SIZE = 2048;        // BufferManager에서 사용하는 버퍼 크기
    public const int PACKET_BUFFER_SIZE = 8192; // SocketSession에서 사용하는 패킷 버퍼 크기

    // 패킷 헤더/사이즈
    public const int LENGTH_SIZE = 2;
    public const int TYPE_SIZE = 2;
    public const int HEADER_SIZE = LENGTH_SIZE + TYPE_SIZE;
    public const int MAX_PACKET_SIZE = 2048;
}
