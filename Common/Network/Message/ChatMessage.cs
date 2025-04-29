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
        public string Sender { get; init; }

        /// <summary>
        /// 메시지 내용
        /// </summary>
        public string Message { get; init; }

        public ChatMessage(string sender, string message)
        {
            Sender = sender;
            Message = message;
        }

        // 디버깅을 위한 오버라이드
        public override string ToString()
        {
            return $"[{Sender}] {Message}";
        }
    }
}