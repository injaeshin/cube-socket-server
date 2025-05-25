namespace Cube.Network;

public class NetConsts
{
    // 네트워크 설정
    public const int PORT = 7777;
    public const int LISTEN_BACKLOG = 100;
    public const int MAX_CONNECTIONS = 1000;
    public const int MAX_UDP_CONNECTIONS = 100;

    // 버퍼 크기
    public const int BUFFER_SIZE = 32 * 1024;        // BufferManager에서 사용하는 버퍼 크기
    public const int PACKET_BUFFER_SIZE = 2048; // SocketSession에서 사용하는 패킷 버퍼 크기
    public const int SEND_CHUNK_SIZE = 2048;

    // 패킷 헤더/사이즈
    public const int SESSION_ID_SIZE = 4; // 세션 ID 크기 
    public const int SEQUENCE_SIZE = 2; // 시퀀스 크기
    public const int ACK_SIZE = 2; // ACK 크기
    public const int LENGTH_SIZE = 2; // 패킷 길이 크기 (TCP 헤더에서 사용)
    public const int TYPE_SIZE = 2; // 패킷 타입 크기 (TCP 헤더에서 사용)
    public const int TCP_HEADER_SIZE = LENGTH_SIZE + TYPE_SIZE;
    public const int UDP_HEADER_SIZE = SESSION_ID_SIZE + SEQUENCE_SIZE + ACK_SIZE;
    public const int MAX_PACKET_SIZE = 2048;
}
