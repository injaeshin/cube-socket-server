using System;
using System.Collections.Generic;
using System.Text;
using Common.Network;
using Common.Network.Packet;

namespace PacketDataGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("패킷 데이터 생성 도구");
            Console.WriteLine("1. 테스트 패킷 생성");
            Console.WriteLine("2. 임의 크기 패킷 생성");
            Console.WriteLine("3. 버퍼 경계 테스트 패킷 생성");
            Console.WriteLine("4. 패킷 분할 테스트");
            Console.Write("선택: ");

            var input = Console.ReadLine();
            switch (input)
            {
                case "1":
                    GenerateTestPackets();
                    break;
                case "2":
                    GenerateRandomSizePackets();
                    break;
                case "3":
                    GenerateBufferBoundaryTestPackets();
                    break;
                case "4":
                    GenerateFragmentedPackets();
                    break;
                default:
                    Console.WriteLine("잘못된 선택입니다.");
                    break;
            }
        }

        static void GenerateTestPackets()
        {
            Console.Write("생성할 패킷 수: ");
            if (!int.TryParse(Console.ReadLine(), out int count) || count <= 0)
            {
                Console.WriteLine("유효하지 않은 값입니다.");
                return;
            }

            var packets = new List<byte[]>();
            for (int i = 0; i < count; i++)
            {
                // 패킷 타입 선택
                PacketType packetType = (PacketType)(i % 3 + 1); // Login, LoginSuccess, ChatMessage

                // 페이로드 생성
                byte[] payload;
                switch (packetType)
                {
                    case PacketType.Login:
                        payload = Encoding.UTF8.GetBytes($"user_{i}");
                        break;
                    case PacketType.LoginSuccess:
                        payload = Encoding.UTF8.GetBytes($"Welcome user_{i}!");
                        break;
                    case PacketType.ChatMessage:
                        payload = Encoding.UTF8.GetBytes($"Message from user_{i}: Hello world!");
                        break;
                    default:
                        payload = Array.Empty<byte>();
                        break;
                }

                // 패킷 생성
                byte[] packet = BuildPacket(packetType, payload);
                packets.Add(packet);

                // 패킷 정보 출력
                Console.WriteLine($"패킷 {i+1}: 타입={packetType}, 크기={packet.Length}, 페이로드={Encoding.UTF8.GetString(payload)}");
            }

            // 전체 패킷을 하나의 스트림으로 연결
            int totalSize = 0;
            foreach (var packet in packets)
            {
                totalSize += packet.Length;
            }

            byte[] combinedData = new byte[totalSize];
            int offset = 0;
            foreach (var packet in packets)
            {
                Buffer.BlockCopy(packet, 0, combinedData, offset, packet.Length);
                offset += packet.Length;
            }

            // HEX 형식으로 출력
            Console.WriteLine("\n========== 결합된 패킷 데이터 (HEX) ==========");
            PrintHex(combinedData);
        }

        static void GenerateRandomSizePackets()
        {
            Console.Write("최소 크기(바이트): ");
            if (!int.TryParse(Console.ReadLine(), out int minSize) || minSize < 4)
            {
                Console.WriteLine("최소 크기는 4바이트 이상이어야 합니다.");
                return;
            }

            Console.Write("최대 크기(바이트): ");
            if (!int.TryParse(Console.ReadLine(), out int maxSize) || maxSize < minSize)
            {
                Console.WriteLine("최대 크기는 최소 크기보다 크거나 같아야 합니다.");
                return;
            }

            Random random = new Random();
            int size = random.Next(minSize, maxSize + 1);

            // 패킷 생성
            byte[] payload = new byte[size - 4]; // 헤더(2바이트) + 타입(2바이트)를 제외한 크기
            random.NextBytes(payload);

            byte[] packet = BuildPacket(PacketType.ChatMessage, payload);

            Console.WriteLine($"패킷 생성 완료: 크기={packet.Length} 바이트");
            Console.WriteLine("\n========== 패킷 데이터 (HEX) ==========");
            PrintHex(packet);
        }

        static void GenerateBufferBoundaryTestPackets()
        {
            Console.Write("버퍼 크기: ");
            if (!int.TryParse(Console.ReadLine(), out int bufferSize) || bufferSize <= 10)
            {
                bufferSize = Constant.PACKET_BUFFER_SIZE;
                Console.WriteLine($"기본 버퍼 크기 사용: {bufferSize}");
            }

            // 경계를 걸치도록 두 개의 패킷 생성
            int firstPacketSize = bufferSize - 5; // 버퍼 끝에 가깝게
            byte[] firstPacketPayload = new byte[firstPacketSize - 4];
            for (int i = 0; i < firstPacketPayload.Length; i++)
            {
                firstPacketPayload[i] = (byte)(i % 256);
            }

            byte[] firstPacket = BuildPacket(PacketType.ChatMessage, firstPacketPayload);

            // 두 번째 패킷은 작게 생성
            byte[] secondPacket = BuildPacket(PacketType.Login, Encoding.UTF8.GetBytes("testuser"));

            // 결합
            byte[] combinedData = new byte[firstPacket.Length + secondPacket.Length];
            Buffer.BlockCopy(firstPacket, 0, combinedData, 0, firstPacket.Length);
            Buffer.BlockCopy(secondPacket, 0, combinedData, firstPacket.Length, secondPacket.Length);

            Console.WriteLine($"첫 번째 패킷 크기: {firstPacket.Length} 바이트");
            Console.WriteLine($"두 번째 패킷 크기: {secondPacket.Length} 바이트");
            Console.WriteLine($"결합된 데이터 크기: {combinedData.Length} 바이트");
            Console.WriteLine($"버퍼 크기: {bufferSize} 바이트");

            Console.WriteLine("\n========== 결합된 패킷 데이터 (HEX) ==========");
            PrintHex(combinedData);
        }

        static void GenerateFragmentedPackets()
        {
            Console.Write("패킷 조각 수: ");
            if (!int.TryParse(Console.ReadLine(), out int fragmentCount) || fragmentCount <= 1)
            {
                fragmentCount = 3;
                Console.WriteLine($"기본 조각 수 사용: {fragmentCount}");
            }

            // 테스트용 로그인 패킷 생성
            byte[] payload = Encoding.UTF8.GetBytes("fragmenteduser");
            byte[] packet = BuildPacket(PacketType.Login, payload);

            // 조각으로 분할
            int fragmentSize = packet.Length / fragmentCount;
            List<byte[]> fragments = new List<byte[]>();

            for (int i = 0; i < fragmentCount; i++)
            {
                int start = i * fragmentSize;
                int size = (i == fragmentCount - 1) ? packet.Length - start : fragmentSize;

                byte[] fragment = new byte[size];
                Buffer.BlockCopy(packet, start, fragment, 0, size);
                fragments.Add(fragment);

                Console.WriteLine($"조각 {i+1}: 오프셋={start}, 크기={size}");
            }

            // 각 조각 출력
            for (int i = 0; i < fragments.Count; i++)
            {
                Console.WriteLine($"\n========== 조각 {i+1} 데이터 (HEX) ==========");
                PrintHex(fragments[i]);
            }
        }

        static byte[] BuildPacket(PacketType type, byte[] payload)
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

        static void PrintHex(byte[] data)
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
} 