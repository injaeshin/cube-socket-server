using Microsoft.Extensions.Logging;

using Common.Network.Handler;
using Common.Network.Packet;
using Common.Network.Session;
using Server.Chat.Users;

namespace Server.Chat.Handler;

public class LoginHandler(ILogger<LoginHandler> logger, IUserManager userManager) : IPacketHandler
{
    private readonly ILogger _logger = logger;
    private readonly IUserManager _userManager = userManager;

    public PacketType PacketType => PacketType.Login;

    public async Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> packet)
    {
        try
        {
            // 원시 패킷 데이터 로깅
            _logger.LogDebug("Login packet raw data: {HexDump}, Length: {Length}", 
                BitConverter.ToString(packet.ToArray()), packet.Length);
            
            var reader = new PacketReader(packet);
            // 패킷 페이로드 길이 검증
            if (reader.RemainingLength < 2)
            {
                _logger.LogError("Login packet does not contain string length field: {RemainingLength} bytes", reader.RemainingLength);
                return false;
            }

            //// 먼저 문자열 길이를 검사
            //var stringLengthPos = reader.Position;
            //var stringLength = reader.ReadUInt16();
            
            //_logger.LogDebug("Login string length: {StringLength}, remaining bytes: {RemainingBytes}, Position: {Position}", 
            //    stringLength, reader.RemainingLength, reader.Position);
            
            //if (stringLength > 100) // 최대 길이 제한 설정
            //{
            //    _logger.LogError("Login string too long: {StringLength} bytes", stringLength);
            //    return false;
            //}
            
            //if (reader.RemainingLength < stringLength)
            //{
            //    _logger.LogError("Login string length ({StringLength}) exceeds remaining packet size ({RemainingLength})", 
            //        stringLength, reader.RemainingLength);
                
            //    // 디버깅을 위한 패킷 덤프
            //    _logger.LogDebug("Packet dump: {HexDump}", BitConverter.ToString(packet.ToArray()));
            //    return false;
            //}

            //// 남은 모든 바이트 데이터 로깅
            //var remainingBytes = new byte[reader.RemainingLength];
            //packet.Slice(reader.Position).CopyTo(new Memory<byte>(remainingBytes));
            //_logger.LogDebug("Remaining bytes: {HexDump}", BitConverter.ToString(remainingBytes));

            //// 위치 복원하고 문자열 읽기
            //reader.Seek(stringLengthPos);
            var id = reader.ReadString();
            _logger.LogInformation("Login: {Id}, Bytes: [{HexString}]", id, 
                string.Join(" ", System.Text.Encoding.UTF8.GetBytes(id).Select(b => b.ToString("X2"))));

            //사용자 등록
            if (!_userManager.InsertUser(id, session))
            {
                _logger.LogError("Failed to insert user: {SessionId}", session.SessionId);
                return false;
            }

            using var payload = new PacketWriter();
            payload.WriteType(PacketType.LoginSuccess);
            await session.SendAsync(payload.ToPacket());

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing login packet: {ErrorMessage}", ex.Message);
            // 예외 정보와 함께 패킷 덤프
            _logger.LogDebug("Packet dump: {HexDump}", BitConverter.ToString(packet.ToArray()));
            return false;
        }
    }
}