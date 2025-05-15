using System.Threading.Tasks;
using Common.Network.Packet;
using Common.Network;
using Microsoft.Extensions.Logging;

namespace DummyClient;

public class MessageHandler
{
    private readonly ILogger _logger;
    public MessageHandler(ILogger<MessageHandler> logger)
    {
        _logger = logger;
    }

    // 수신 패킷 처리용 메서드
    public async Task HandlePacket(ClientSession session, MessageType type, ReadOnlyMemory<byte> payload)
    {
        switch (type)
        {
            case MessageType.Login:
                await HandleLogin(session, payload);
                break;
            case MessageType.ChatMessage:
                await HandleChatMessage(session, payload);
                break;
            case MessageType.Logout:
                await HandleLogout(session, payload);
                break;
            default:
                // 필요시 로깅
                break;
        }
    }

    public async Task HandleLogin(ClientSession session, ReadOnlyMemory<byte> payload)
    {
        // 로그인 응답 처리 로직 구현
        await Task.CompletedTask;
    }

    public async Task HandleChatMessage(ClientSession session, ReadOnlyMemory<byte> payload)
    {
        // 채팅 메시지 수신 처리 로직 구현
        await Task.CompletedTask;
    }

    public async Task HandleLogout(ClientSession session, ReadOnlyMemory<byte> payload)
    {
        // 로그아웃 응답 처리 로직 구현
        await Task.CompletedTask;
    }

    // 필요시 패킷 타입별 추가 핸들러 구현
}