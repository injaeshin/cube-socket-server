using Cube.Client;

class Program
{
    static async Task Main(string[] args)
    {
        const string SERVER_IP = "127.0.0.1";
        const int TCP_PORT = 7777;
        const int UDP_PORT = 7778;
        const int RESEND_INTERVAL_MS = 300;

        using var client = new ChatClient(SERVER_IP, TCP_PORT, UDP_PORT, RESEND_INTERVAL_MS);

        // 이벤트 핸들러 등록
        client.OnError += (message) => Console.WriteLine($"에러: {message}");
        client.OnStatusChanged += (message) => Console.WriteLine($"상태: {message}");
        client.OnChatMessageReceived += (sender, message) => Console.WriteLine($"{sender}: {message}");

        try
        {
            // 1. TCP 연결
            Console.WriteLine("TCP 서버에 연결 중...");
            client.Connect();

            // 2. 로그인 및 UDP 연결
            Console.WriteLine("로그인 중...");
            client.Login();

            // 잠시 대기하여 연결이 완료되도록 함
            await Task.Delay(1000);

            // 3. 테스트 메시지 전송
            Console.WriteLine("테스트 메시지 전송...");
            client.SendChatMessage("안녕하세요! 테스트 메시지입니다.");

            // 4. 종료될 때까지 대기
            Console.WriteLine("메시지를 입력하세요. (종료하려면 'exit' 입력)");
            Console.WriteLine("UDP로 전송하려면 '/udp'로 시작하세요.");

            while (true)
            {
                var input = Console.ReadLine();
                if (string.IsNullOrEmpty(input)) continue;

                if (input.ToLower() == "exit")
                    break;

                try
                {
                    if (input.StartsWith("/udp"))
                    {
                        var message = input.Substring(4).Trim();
                        if (!string.IsNullOrEmpty(message))
                        {
                            client.SendUdpMessage(message);
                        }
                    }
                    else
                    {
                        client.SendChatMessage(input);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"메시지 전송 실패: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"오류 발생: {ex.Message}");
        }
        finally
        {
            client.Stop();
        }
    }
}
