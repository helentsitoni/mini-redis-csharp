using System.Collections.Concurrent;

namespace MiniRedis;

public class CommandProcessor
{
    private record Entry(string Value, DateTime? ExpiresAt);

    private readonly ConcurrentDictionary<string, Entry> _store = new();
    private readonly Func<DateTime> _now;

    public CommandProcessor(Func<DateTime>? now = null)
    {
        _now = now ?? (() => DateTime.UtcNow);
    }

    public string Execute(string line)
    {
        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "ERR empty command";

        var command = parts[0].ToUpperInvariant();

        switch (command)
        {
            case "PING":
                return "PONG";

            case "SET":
                if (parts.Length == 3)
                {
                    _store[parts[1]] = new Entry(parts[2], null);
                    return "OK";
                }
                if (parts.Length == 5 && parts[3].ToUpperInvariant() == "EX")
                {
                    if (!int.TryParse(parts[4], out var seconds) || seconds <= 0)
                        return "ERR invalid expire time";
                    _store[parts[1]] = new Entry(parts[2], _now().AddSeconds(seconds));
                    return "OK";
                }
                return "ERR usage: SET key value [EX seconds]";

            case "GET":
                if (parts.Length != 2) return "ERR usage: GET key";
                return GetLive(parts[1])?.Value ?? "(nil)";

            case "DEL":
                if (parts.Length != 2) return "ERR usage: DEL key";
                var existing = GetLive(parts[1]);
                if (existing is null) return "0";
                return _store.TryRemove(new KeyValuePair<string, Entry>(parts[1], existing)) ? "1" : "0";

            case "TTL":
                if (parts.Length != 2) return "ERR usage: TTL key";
                var entry = GetLive(parts[1]);
                if (entry is null) return "-2";
                if (entry.ExpiresAt is null) return "-1";
                var remaining = (entry.ExpiresAt.Value - _now()).TotalSeconds;
                return ((int)Math.Ceiling(remaining)).ToString();

            default:
                return $"ERR unknown command '{parts[0]}'";
        }
    }

    // Lazy expiration: όταν κάποιος ζητήσει ένα ληγμένο κλειδί, το σβήνουμε εκείνη τη στιγμή
    private Entry? GetLive(string key)
    {
        if (!_store.TryGetValue(key, out var entry)) return null;
        if (entry.ExpiresAt is null || entry.ExpiresAt > _now()) return entry;

        _store.TryRemove(new KeyValuePair<string, Entry>(key, entry));
        return null;
    }

    // Active expiration: καλείται κάθε 1 δευτερόλεπτο από background task
    public int RemoveExpired()
    {
        var now = _now();
        var removed = 0;
        foreach (var (key, entry) in _store)
        {
            if (entry.ExpiresAt is not null && entry.ExpiresAt <= now)
            {
                if (_store.TryRemove(new KeyValuePair<string, Entry>(key, entry)))
                    removed++;
            }
        }
        return removed;
    }
}