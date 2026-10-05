using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

const int clients = 100;
const int opsPerClient = 100;
const int port = 6380;

var errors = 0;
var sw = Stopwatch.StartNew();

var tasks = Enumerable.Range(0, clients).Select(async id =>
{
    using var client = new TcpClient();
    await client.ConnectAsync("localhost", port);
    var stream = client.GetStream();
    var reader = new StreamReader(stream, Encoding.UTF8);
    var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

    for (int i = 0; i < opsPerClient; i++)
    {
        var key = $"client{id}:key{i}";
        var value = $"value{id}-{i}";

        await writer.WriteLineAsync($"SET {key} {value}");
        var setReply = await reader.ReadLineAsync();

        await writer.WriteLineAsync($"GET {key}");
        var getReply = await reader.ReadLineAsync();

        if (setReply != "OK" || getReply != value)
            Interlocked.Increment(ref errors);
    }
});

await Task.WhenAll(tasks);
sw.Stop();

var totalOps = clients * opsPerClient * 2;
Console.WriteLine($"Clients:     {clients}");
Console.WriteLine($"Total ops:   {totalOps}");
Console.WriteLine($"Errors:      {errors}");
Console.WriteLine($"Time:        {sw.ElapsedMilliseconds} ms");
Console.WriteLine($"Throughput:  {totalOps / sw.Elapsed.TotalSeconds:F0} ops/sec");