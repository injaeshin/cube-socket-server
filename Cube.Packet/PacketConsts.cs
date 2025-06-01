
namespace Cube.Packet;

public class PacketConsts
{
    // 패킷 크기
    public const int TOKEN_SIZE = 8; // 토큰 크기 
    public const int SEQUENCE_SIZE = 2; // 시퀀스 크기
    public const int ACK_SIZE = 2; // ACK 크기
    public const int LENGTH_SIZE = 2; // 패킷 길이 크기 (TCP 헤더에서 사용)
    public const int TYPE_SIZE = 2; // 패킷 타입 크기 (TCP 헤더에서 사용)
    public const int TCP_HEADER_SIZE = LENGTH_SIZE + TYPE_SIZE;
    public const int UDP_HEADER_SIZE = TOKEN_SIZE + SEQUENCE_SIZE + ACK_SIZE;

    // 버퍼 크기
    public const int BUFFER_SIZE = 2048;        // 패킷 버퍼 크기
    public const int SEND_CHUNK_SIZE = 2048;    // 전송 청크 크기
}
