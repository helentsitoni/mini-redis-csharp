# Mini Redis (C#/.NET)

A small Redis-like in-memory key-value server written from scratch in C#. Clients connect over TCP and send plain-text commands. I built it to understand how a server handles many concurrent clients, shared state and key expiration.

## Features

- `SET`, `GET`, `DEL`, `TTL`, `PING` commands
- Key expiration with `SET key value EX seconds`
- Lazy and active expiration (the same approach real Redis uses)
- Many concurrent clients using async/await
- Thread-safe storage with `ConcurrentDictionary`
- 9 xUnit tests, including a fake clock for expiration tests
- Load test tool with 100 parallel clients

## Commands

| Command | Example | Reply |
|---|---|---|
| PING | `PING` | `PONG` |
| SET | `SET name Eleni` | `OK` |
| SET with expiry | `SET token abc EX 10` | `OK` |
| GET | `GET name` | `Eleni` or `(nil)` |
| DEL | `DEL name` | `1` if deleted, `0` if not found |
| TTL | `TTL token` | seconds left, `-1` no expiry, `-2` not found |

## Project structure

```
MiniRedis/            TCP server and command processing
MiniRedis.Tests/      xUnit unit tests
MiniRedis.LoadTest/   load test with parallel clients
```

## How to run

Requires the .NET 10 SDK.

```
dotnet run --project MiniRedis -c Release
```

The server listens on port 6380. To connect from PowerShell:

```powershell
$c = New-Object System.Net.Sockets.TcpClient("localhost", 6380); $s = $c.GetStream(); $w = New-Object System.IO.StreamWriter($s); $w.AutoFlush = $true; $r = New-Object System.IO.StreamReader($s)
$w.WriteLine("SET name Eleni"); $r.ReadLine()
$w.WriteLine("GET name"); $r.ReadLine()
```

## Tests

```
dotnet test
```

## Load test

With the server running:

```
dotnet run --project MiniRedis.LoadTest -c Release
```

100 clients run in parallel, each doing 100 SET and 100 GET commands on its own keys and checking that every reply is correct.

| Clients | Total operations | Errors | Throughput |
|---|---|---|---|
| 100 | 20,000 | 0 | ~8,000 ops/sec |

Measured on a laptop with a Release build.

## Design notes

- **Expiration:** keys are removed lazily when someone reads an expired key, and actively by a background task that runs every second, so unused expired keys don't stay in memory.
- **Safe removal:** expired keys are removed with `TryRemove(KeyValuePair)`, which only deletes the key if it still has the old value. If a client sets a new value at the same moment, it is not lost.
- **Testable time:** `CommandProcessor` takes a `Func<DateTime>` clock, so tests can move time forward instead of waiting.

## Limitations and next steps

- Values cannot contain spaces
- No persistence to disk
- Plain-text protocol instead of the real Redis protocol (RESP)
- Next: pipelining and RESP support