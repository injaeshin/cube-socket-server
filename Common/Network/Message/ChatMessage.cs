using Common.Network.Packet;
using System;
using System.Buffers;
using System.Text;

namespace Common.Network.Message
{
    /// <summary>
    /// 채팅 메시지 데이터 모델
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// 메시지 발신자
        /// </summary>
        public string Sender { get; set; }
        
        /// <summary>
        /// 메시지 내용
        /// </summary>
        public string Message { get; set; }

        public ChatMessage(string sender, string message)
        {
            Sender = sender;
            Message = message;
        }

        public static ChatMessage Create(ref PacketReader reader)
        {
            string sender = reader.ReadString();
            string message = reader.ReadString();
            return new ChatMessage(sender, message);
        }

        /// <summary>
        /// 메시지를 패킷으로 변환 (안전한 버전 - 내부적으로 메모리 복사)
        /// </summary>
        public static ReadOnlyMemory<byte> ToPacket(ChatMessage message)
        {
            using var writer = new PacketWriter();
            writer.Write(message.Sender);
            writer.Write(message.Message);
            
            // 복사본 생성 (메모리 관리 필요 없음)
            var data = writer.ToMemory();
            byte[] copy = new byte[data.Length];
            data.CopyTo(copy);
            return copy;
        }

        /// <summary>
        /// 메시지를 패킷으로 변환 (PacketWriter 직접 반환 - 수신자가 관리 책임)
        /// 반드시 using 문으로 사용해야 함.
        /// example:
        /// using var packet = ChatMessage.CreatePacket(message);
        /// await user.Session.SendAsync(PacketType.ChatMessage, packet.ToMemory());
        /// </summary>
        public static PacketWriter CreatePacket(ChatMessage message)
        {
            var writer = new PacketWriter();
            writer.Write(message.Sender);
            writer.Write(message.Message);
            return writer;
        }

        // 디버깅을 위한 오버라이드
        public override string ToString()
        {
            return $"[{Sender}] {Message}";
        }
    }
} 