namespace Cube.Client;

internal class Program
{
    private static bool _isRunning = true;

    static async Task Main(string[] args)
    {
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            _isRunning = false;
        };

        try
        {
            await RunClientAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"오류 발생: {ex.Message}");
        }
    }

    private static async Task RunClientAsync()
    {
        using var client = new ChatClient("127.0.0.1", 7777);

        client.OnError += (error) => Console.WriteLine($"오류: {error}");
        client.OnStatusChanged += (status) => Console.WriteLine(status);
        client.OnChatMessageReceived += (sender, message) =>
            Console.WriteLine($"> {sender}: {message}");

        try
        {
            await client.StartAsync();
            Console.Write("사용자 이름을 입력하세요: ");
            var username = Console.ReadLine() ?? "Anonymous";
            await client.LoginAsync(username);

            while (_isRunning)
            {
                var message = Console.ReadLine();
                if (string.IsNullOrEmpty(message)) continue;

                if (message.Equals("/quit", StringComparison.OrdinalIgnoreCase))
                {
                    _isRunning = false;
                    break;
                }

                await client.SendChatMessageAsync(message);
            }
        }
        finally
        {
            client.Stop();
        }
    }
}
