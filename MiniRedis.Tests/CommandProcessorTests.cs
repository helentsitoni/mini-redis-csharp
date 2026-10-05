using MiniRedis;

namespace MiniRedis.Tests;

public class CommandProcessorTests
{
    [Fact]
    public void Ping_ReturnsPong()
    {
        var p = new CommandProcessor();
        Assert.Equal("PONG", p.Execute("PING"));
    }

    [Fact]
    public void Set_ThenGet_ReturnsValue()
    {
        var p = new CommandProcessor();
        Assert.Equal("OK", p.Execute("SET name Eleni"));
        Assert.Equal("Eleni", p.Execute("GET name"));
    }

    [Fact]
    public void Get_MissingKey_ReturnsNil()
    {
        var p = new CommandProcessor();
        Assert.Equal("(nil)", p.Execute("GET nothing"));
    }

    [Fact]
    public void Del_RemovesKey_OnlyOnce()
    {
        var p = new CommandProcessor();
        p.Execute("SET name Eleni");
        Assert.Equal("1", p.Execute("DEL name"));
        Assert.Equal("(nil)", p.Execute("GET name"));
        Assert.Equal("0", p.Execute("DEL name"));
    }

    [Fact]
    public void Key_ExpiresAfterTtl()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var p = new CommandProcessor(() => now);

        p.Execute("SET token abc EX 10");

        now = now.AddSeconds(9);
        Assert.Equal("abc", p.Execute("GET token"));

        now = now.AddSeconds(2);
        Assert.Equal("(nil)", p.Execute("GET token"));
    }

    [Fact]
    public void Ttl_ReturnsRemainingSeconds()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var p = new CommandProcessor(() => now);

        p.Execute("SET token abc EX 10");
        p.Execute("SET name Eleni");

        now = now.AddSeconds(3);
        Assert.Equal("7", p.Execute("TTL token"));
        Assert.Equal("-1", p.Execute("TTL name"));
        Assert.Equal("-2", p.Execute("TTL nothing"));
    }

    [Fact]
    public void RemoveExpired_CleansOnlyExpiredKeys()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var p = new CommandProcessor(() => now);

        p.Execute("SET temp 1 EX 5");
        p.Execute("SET keep 2");

        now = now.AddSeconds(6);
        Assert.Equal(1, p.RemoveExpired());
        Assert.Equal("2", p.Execute("GET keep"));
    }

    [Fact]
    public void UnknownCommand_ReturnsError()
    {
        var p = new CommandProcessor();
        Assert.StartsWith("ERR", p.Execute("HELLO"));
    }

    [Fact]
    public async Task ConcurrentSets_AllKeysAreStored()
    {
        var p = new CommandProcessor();

        var tasks = Enumerable.Range(0, 1000)
            .Select(i => Task.Run(() => p.Execute($"SET key{i} value{i}")));
        await Task.WhenAll(tasks);

        for (int i = 0; i < 1000; i++)
            Assert.Equal($"value{i}", p.Execute($"GET key{i}"));
    }
}