using System;
using System.Reflection;
using System.Text;
using Common.Network;
using Common.Network.Packet;

namespace PacketBufferDebugger
{
    /// <summary>
    /// PacketBuffer의 내부 상태를 디버그하기 위한 래퍼 클래스
    /// </summary>
    public class DebugPacketBuffer
    {
        private readonly PacketBuffer _buffer;
        private readonly int _bufferSize;
        private int _readPos;
        private int _writePos;
        
        public DebugPacketBuffer()
        {
            _buffer = new PacketBuffer();
            
            // 리플렉션을 통해 private 필드 접근
            var bufferField = typeof(PacketBuffer).GetField("_buffer", BindingFlags.NonPublic | BindingFlags.Instance);
            var bufferSizeField = typeof(PacketBuffer).GetField("_bufferSize", BindingFlags.NonPublic | BindingFlags.Instance);
            var readPosField = typeof(PacketBuffer).GetField("_readPos", BindingFlags.NonPublic | BindingFlags.Instance);
            var writePosField = typeof(PacketBuffer).GetField("_writePos", BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (bufferSizeField != null)
            {
                _bufferSize = (int)bufferSizeField.GetValue(_buffer)!;
            }
            else
            {
                _bufferSize = Constant.PACKET_BUFFER_SIZE;
            }
            
            UpdateInternalState();
        }
        
        /// <summary>
        /// 리플렉션을 통해 내부 상태 업데이트
        /// </summary>
        private void UpdateInternalState()
        {
            var readPosField = typeof(PacketBuffer).GetField("_readPos", BindingFlags.NonPublic | BindingFlags.Instance);
            var writePosField = typeof(PacketBuffer).GetField("_writePos", BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (readPosField != null && writePosField != null)
            {
                _readPos = (int)readPosField.GetValue(_buffer)!;
                _writePos = (int)writePosField.GetValue(_buffer)!;
            }
        }
        
        /// <summary>
        /// 패킷 버퍼에 데이터 추가
        /// </summary>
        public bool Append(ReadOnlyMemory<byte> data)
        {
            Console.WriteLine($"[Append] 추가 전 상태: ReadPos={_readPos}, WritePos={_writePos}, DataSize={GetDataSize()}, FreeSize={GetFreeSize()}");
            Console.WriteLine($"[Append] 추가할 데이터: {data.Length} 바이트");
            
            var result = _buffer.Append(data);
            
            UpdateInternalState();
            Console.WriteLine($"[Append] 추가 결과: {result}, 추가 후 상태: ReadPos={_readPos}, WritePos={_writePos}, DataSize={GetDataSize()}, FreeSize={GetFreeSize()}");
            
            return result;
        }
        
        /// <summary>
        /// 패킷 버퍼에서 패킷 읽기
        /// </summary>
        public bool TryReadPacket(out ReadOnlyMemory<byte> packet, out byte[]? rentedBuffer)
        {
            Console.WriteLine($"[TryReadPacket] 읽기 전 상태: ReadPos={_readPos}, WritePos={_writePos}, DataSize={GetDataSize()}, FreeSize={GetFreeSize()}");
            
            var result = _buffer.TryReadPacket(out packet, out rentedBuffer);
            
            UpdateInternalState();
            Console.WriteLine($"[TryReadPacket] 읽기 결과: {result}, 패킷 크기: {(result ? packet.Length : 0)}, 읽은 후 상태: ReadPos={_readPos}, WritePos={_writePos}, DataSize={GetDataSize()}, FreeSize={GetFreeSize()}");
            
            if (result && packet.Length > 0)
            {
                // 패킷 내용 출력
                Console.WriteLine($"[패킷 내용] 타입: {PacketIO.GetPacketType(packet)}");
                
                // 패킷 내용 HEX 덤프
                Console.WriteLine("패킷 HEX 덤프:");
                PrintHex(packet.ToArray());
            }
            
            return result;
        }
        
        /// <summary>
        /// 패킷 버퍼 초기화
        /// </summary>
        public void Reset()
        {
            Console.WriteLine($"[Reset] 리셋 전 상태: ReadPos={_readPos}, WritePos={_writePos}, DataSize={GetDataSize()}, FreeSize={GetFreeSize()}");
            
            _buffer.Reset();
            
            UpdateInternalState();
            Console.WriteLine($"[Reset] 리셋 후 상태: ReadPos={_readPos}, WritePos={_writePos}, DataSize={GetDataSize()}, FreeSize={GetFreeSize()}");
        }
        
        /// <summary>
        /// 버퍼의 내부 상태 시각화
        /// </summary>
        public void VisualizeBufferState()
        {
            Console.WriteLine("\n===== 버퍼 상태 시각화 =====");
            Console.WriteLine($"버퍼 크기: {_bufferSize} 바이트");
            Console.WriteLine($"읽기 위치(ReadPos): {_readPos}");
            Console.WriteLine($"쓰기 위치(WritePos): {_writePos}");
            Console.WriteLine($"사용 중인 데이터 크기: {GetDataSize()} 바이트");
            Console.WriteLine($"사용 가능한 공간: {GetFreeSize()} 바이트");
            
            Console.WriteLine("\n버퍼 레이아웃:");
            
            // 버퍼 시각화 (간단한 ASCII 아트)
            const int width = 50; // 시각화 너비
            int readPosMarker = (int)((_readPos / (double)_bufferSize) * width);
            int writePosMarker = (int)((_writePos / (double)_bufferSize) * width);
            
            // 버퍼 레이아웃 출력
            Console.Write("[");
            for (int i = 0; i < width; i++)
            {
                if (i == readPosMarker && i == writePosMarker)
                {
                    Console.Write("X"); // 읽기/쓰기 위치가 같은 경우
                }
                else if (i == readPosMarker)
                {
                    Console.Write("R"); // 읽기 위치
                }
                else if (i == writePosMarker)
                {
                    Console.Write("W"); // 쓰기 위치
                }
                else if (_writePos >= _readPos)
                {
                    // 일반적인 경우 (쓰기 >= 읽기)
                    if (i > readPosMarker && i < writePosMarker)
                    {
                        Console.Write("="); // 데이터가 있는 영역
                    }
                    else
                    {
                        Console.Write(" "); // 빈 공간
                    }
                }
                else
                {
                    // 버퍼 경계를 넘어간 경우 (쓰기 < 읽기)
                    if (i < writePosMarker || i > readPosMarker)
                    {
                        Console.Write("="); // 데이터가 있는 영역
                    }
                    else
                    {
                        Console.Write(" "); // 빈 공간
                    }
                }
            }
            Console.WriteLine("]");
            Console.WriteLine($"0{new string(' ', width - 2)}{_bufferSize}");
        }
        
        /// <summary>
        /// 버퍼에 있는 데이터 크기 계산
        /// </summary>
        private int GetDataSize()
        {
            if (_writePos >= _readPos)
                return _writePos - _readPos;
            
            return _bufferSize - _readPos + _writePos;
        }
        
        /// <summary>
        /// 버퍼의 사용 가능한 공간 계산
        /// </summary>
        private int GetFreeSize()
        {
            if (_writePos >= _readPos)
                return _bufferSize - (_writePos - _readPos) - 1;
            
            return _readPos - _writePos - 1;
        }
        
        /// <summary>
        /// 바이트 배열을 16진수로 출력
        /// </summary>
        private static void PrintHex(byte[] data)
        {
            const int bytesPerLine = 16;
            for (int i = 0; i < data.Length; i += bytesPerLine)
            {
                // 주소 출력
                Console.Write($"{i:X4}: ");
                
                // 16진수 부분 출력
                for (int j = 0; j < bytesPerLine; j++)
                {
                    if (i + j < data.Length)
                        Console.Write($"{data[i + j]:X2} ");
                    else
                        Console.Write("   ");
                    
                    if (j == 7) // 중간에 공백 추가
                        Console.Write(" ");
                }
                
                // ASCII 부분 출력
                Console.Write(" | ");
                for (int j = 0; j < bytesPerLine; j++)
                {
                    if (i + j < data.Length)
                    {
                        char c = (char)data[i + j];
                        if (c >= 32 && c <= 126) // 출력 가능한 ASCII 문자
                            Console.Write(c);
                        else
                            Console.Write(".");
                    }
                    else
                    {
                        Console.Write(" ");
                    }
                }
                Console.WriteLine();
            }
        }
    }
    
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("PacketBuffer 디버거");
            Console.WriteLine("1. 기본 패킷 테스트");
            Console.WriteLine("2. 경계 조건 테스트");
            Console.WriteLine("3. 분할 패킷 테스트");
            Console.Write("선택: ");
            
            var input = Console.ReadLine();
            switch (input)
            {
                case "1":
                    BasicPacketTest();
                    break;
                case "2":
                    BoundaryTest();
                    break;
                case "3":
                    FragmentedPacketTest();
                    break;
                default:
                    Console.WriteLine("잘못된 선택입니다.");
                    break;
            }
        }
        
        static void BasicPacketTest()
        {
            var buffer = new DebugPacketBuffer();
            buffer.VisualizeBufferState();
            
            // 간단한 패킷 생성
            byte[] packet = CreateTestPacket(PacketType.Login, "testuser");
            
            // 패킷 추가
            buffer.Append(new ReadOnlyMemory<byte>(packet));
            buffer.VisualizeBufferState();
            
            // 패킷 읽기
            bool readResult = buffer.TryReadPacket(out var readPacket, out var rentedBuffer);
            if (readResult)
            {
                Console.WriteLine("패킷 읽기 성공!");
                
                // 임대된 버퍼 반환
                if (rentedBuffer != null)
                    System.Buffers.ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
            else
            {
                Console.WriteLine("패킷 읽기 실패!");
            }
            
            buffer.VisualizeBufferState();
        }
        
        static void BoundaryTest()
        {
            var buffer = new DebugPacketBuffer();
            
            // 버퍼 크기에 가까운 패킷 생성
            int payloadSize = Constant.PACKET_BUFFER_SIZE - 20;
            byte[] payload = new byte[payloadSize];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % 256);
            }
            
            byte[] largePacket = CreateTestPacket(PacketType.ChatMessage, payload);
            Console.WriteLine($"큰 패킷 크기: {largePacket.Length} 바이트");
            
            // 첫 번째 패킷 추가
            buffer.Append(new ReadOnlyMemory<byte>(largePacket));
            buffer.VisualizeBufferState();
            
            // 패킷 읽기
            bool readResult = buffer.TryReadPacket(out var readPacket, out var rentedBuffer);
            if (readResult)
            {
                if (rentedBuffer != null)
                    System.Buffers.ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
            
            // 두 번째 작은 패킷 추가
            byte[] smallPacket = CreateTestPacket(PacketType.Login, "boundaryuser");
            buffer.Append(new ReadOnlyMemory<byte>(smallPacket));
            buffer.VisualizeBufferState();
            
            // 두 번째 패킷 읽기
            bool readResult2 = buffer.TryReadPacket(out var readPacket2, out var rentedBuffer2);
            if (readResult2)
            {
                if (rentedBuffer2 != null)
                    System.Buffers.ArrayPool<byte>.Shared.Return(rentedBuffer2);
            }
            
            buffer.VisualizeBufferState();
        }
        
        static void FragmentedPacketTest()
        {
            var buffer = new DebugPacketBuffer();
            
            // 패킷 생성
            byte[] packet = CreateTestPacket(PacketType.Login, "fragmenteduser");
            
            // 패킷을 3개 조각으로 분할
            int fragment1Size = 2; // 길이 필드
            int fragment2Size = 2; // 타입 필드
            int fragment3Size = packet.Length - fragment1Size - fragment2Size; // 나머지
            
            byte[] fragment1 = new byte[fragment1Size];
            byte[] fragment2 = new byte[fragment2Size];
            byte[] fragment3 = new byte[fragment3Size];
            
            Buffer.BlockCopy(packet, 0, fragment1, 0, fragment1Size);
            Buffer.BlockCopy(packet, fragment1Size, fragment2, 0, fragment2Size);
            Buffer.BlockCopy(packet, fragment1Size + fragment2Size, fragment3, 0, fragment3Size);
            
            // 첫 번째 조각 추가
            Console.WriteLine("\n===== 첫 번째 조각 추가 =====");
            buffer.Append(new ReadOnlyMemory<byte>(fragment1));
            buffer.VisualizeBufferState();
            
            // 첫 번째 조각 후 읽기 시도
            bool readResult1 = buffer.TryReadPacket(out _, out var rentedBuffer1);
            Console.WriteLine($"첫 번째 조각 후 읽기 결과: {readResult1}");
            
            // 두 번째 조각 추가
            Console.WriteLine("\n===== 두 번째 조각 추가 =====");
            buffer.Append(new ReadOnlyMemory<byte>(fragment2));
            buffer.VisualizeBufferState();
            
            // 두 번째 조각 후 읽기 시도
            bool readResult2 = buffer.TryReadPacket(out _, out var rentedBuffer2);
            Console.WriteLine($"두 번째 조각 후 읽기 결과: {readResult2}");
            
            // 세 번째 조각 추가
            Console.WriteLine("\n===== 세 번째 조각 추가 =====");
            buffer.Append(new ReadOnlyMemory<byte>(fragment3));
            buffer.VisualizeBufferState();
            
            // 세 번째 조각 후 읽기 시도
            bool readResult3 = buffer.TryReadPacket(out var readPacket3, out var rentedBuffer3);
            Console.WriteLine($"세 번째 조각 후 읽기 결과: {readResult3}");
            
            if (readResult3 && rentedBuffer3 != null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rentedBuffer3);
            }
            
            buffer.VisualizeBufferState();
        }
        
        static byte[] CreateTestPacket(PacketType type, string payload)
        {
            return CreateTestPacket(type, Encoding.UTF8.GetBytes(payload));
        }
        
        static byte[] CreateTestPacket(PacketType type, byte[] payload)
        {
            // 패킷 구조: 길이(2바이트) + 타입(2바이트) + 페이로드
            ushort bodyLength = (ushort)(2 + payload.Length); // 타입(2) + 페이로드 길이
            byte[] packet = new byte[2 + bodyLength]; // 길이 필드(2) + 바디 길이
            
            // 바디 길이 (빅 엔디안)
            packet[0] = (byte)(bodyLength >> 8);
            packet[1] = (byte)(bodyLength & 0xFF);
            
            // 패킷 타입
            ushort typeValue = (ushort)type;
            packet[2] = (byte)(typeValue >> 8);
            packet[3] = (byte)(typeValue & 0xFF);
            
            // 페이로드 복사
            Buffer.BlockCopy(payload, 0, packet, 4, payload.Length);
            
            return packet;
        }
    }
} 