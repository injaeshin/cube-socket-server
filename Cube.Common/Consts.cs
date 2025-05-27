using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cube.Common;

public class Consts
{
    // 네트워크 설정
    public const int LISTEN_BACKLOG = 100;
    public const int MAX_CONNECTIONS = 1000;
    public const int MAX_UDP_CONNECTIONS = 100;

    // 타임아웃 및 간격
    public const int HEARTBEAT_INTERVAL = 15;    // 간격 (초)
    public const int PING_TIMEOUT = 5;           // 타임아웃 (초)

    // 버퍼 크기
    public const int BUFFER_SIZE = 2048;        // 패킷 버퍼 크기
    public const int SEND_CHUNK_SIZE = 2048;    // 전송 청크 크기

    // 패킷 크기
    public const int SESSION_ID_SIZE = 4; // 세션 ID 크기 
    public const int SEQUENCE_SIZE = 2; // 시퀀스 크기
    public const int ACK_SIZE = 2; // ACK 크기
    public const int LENGTH_SIZE = 2; // 패킷 길이 크기 (TCP 헤더에서 사용)
    public const int TYPE_SIZE = 2; // 패킷 타입 크기 (TCP 헤더에서 사용)
    public const int TCP_HEADER_SIZE = LENGTH_SIZE + TYPE_SIZE;
    public const int UDP_HEADER_SIZE = SESSION_ID_SIZE + SEQUENCE_SIZE + ACK_SIZE;
}
