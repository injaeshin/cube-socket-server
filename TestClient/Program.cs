using System.Net;
using System.Net.Sockets;
using System.Text;

class Program
{
    private static readonly string WELCOME_MESSAGE = "knock-knock";
    private static readonly string SERVER_IP = "127.0.0.1";
    private static readonly int SERVER_PORT = 7778;

    static async Task Main(string[] args)
    {
        Console.WriteLine("UDP 테스트 클라이언트 시작...");

        using var client = new UdpClient();
        var serverEndPoint = new IPEndPoint(IPAddress.Parse(SERVER_IP), SERVER_PORT);

        try
        {
            // 세션 ID 생성 (4바이트)
            string sessionId = "TEST";
            byte[] sessionIdBytes = Encoding.UTF8.GetBytes(sessionId);

            // WELCOME_MESSAGE 전송
            byte[] welcomeBytes = Encoding.UTF8.GetBytes(WELCOME_MESSAGE);
            byte[] sendData = new byte[sessionIdBytes.Length + welcomeBytes.Length];

            Buffer.BlockCopy(sessionIdBytes, 0, sendData, 0, sessionIdBytes.Length);
            Buffer.BlockCopy(welcomeBytes, 0, sendData, sessionIdBytes.Length, welcomeBytes.Length);

            Console.WriteLine($"서버로 메시지 전송: {sessionId}-{WELCOME_MESSAGE}");
            await client.SendAsync(sendData, sendData.Length, serverEndPoint);

            // 서버로부터 응답 수신
            var result = await client.ReceiveAsync();
            string response = Encoding.UTF8.GetString(result.Buffer);
            Console.WriteLine($"서버로부터 응답 수신: {response}");

            // 일반 메시지 전송 테스트
            string testMessage = "Hello Server!";
            byte[] messageBytes = Encoding.UTF8.GetBytes(testMessage);
            await client.SendAsync(messageBytes, messageBytes.Length, serverEndPoint);
            Console.WriteLine($"일반 메시지 전송: {testMessage}");

            // 응답 수신
            result = await client.ReceiveAsync();
            response = Encoding.UTF8.GetString(result.Buffer);
            Console.WriteLine($"서버로부터 응답 수신: {response}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"에러 발생: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("클라이언트 종료");
        }
    }
}