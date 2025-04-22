using Common.Network.Packet;

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

        public static ChatMessage FromPacketReader(ref PacketReader reader)
        {
            string sender = reader.ReadString();
            string message = reader.ReadString();
            return new ChatMessage(sender, message);
        }

        // 디버깅을 위한 오버라이드
        public override string ToString()
        {
            return $"[{Sender}] {Message}";
        }
    }
} 