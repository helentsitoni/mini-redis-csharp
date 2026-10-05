using System.Net;
using System.Net.Sockets;
using System.Text;
using MiniRedis;

var processor = new CommandProcessor();
var listener = new TcpListener(IPAddress.Loopback, 6380);
listener.Start();
Console.WriteLine("Mini Redis listening on port 6380...");

_ = Task.Run(async () =>
{
    while (true)
    {
        await Task.Delay(1000);
        var removed = processor.RemoveExpired();
        if (removed > 0) Console.WriteLine($"Cleaned up {removed} expired key(s)");
    }
});

while (true)
{
    var client = await listener.AcceptTcpClientAsync();
    _ = HandleClientAsync(client, processor);
}

static async Task HandleClientAsync(TcpClient client, CommandProcessor processor)
{
    Console.WriteLine("Client connected");
    using (client)
    {
        var stream = client.GetStream();
        var reader = new StreamReader(stream, Encoding.UTF8);
        var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            var response = processor.Execute(line);
            await writer.WriteLineAsync(response);
        }
    }
    Console.WriteLine("Client disconnected");
}