using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace DeckSoundboard.D6Plugin;

internal static class Program
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };

    static async Task<int> Main(string[] args)
    {
        var parsed = ParseArgs(args);
        if (!parsed.TryGetValue("-port", out var port) || !parsed.TryGetValue("-pluginUUID", out var pluginUuid) || !parsed.TryGetValue("-registerEvent", out var registerEvent)) return 2;

        using var ws = new ClientWebSocket();
        try { await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}"), CancellationToken.None); }
        catch { return 3; }

        await SendAsync(ws, new { @event = registerEvent, uuid = pluginUuid });
        var buffer = new byte[64 * 1024];
        while (ws.State == WebSocketState.Open)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) return 0;
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType != WebSocketMessageType.Text) continue;
            using var doc = JsonDocument.Parse(ms.ToArray());
            var root = doc.RootElement;
            var evt = root.TryGetProperty("event", out var ev) ? ev.GetString() : null;
            if (evt == "keyDown")
            {
                var context = root.TryGetProperty("context", out var c) ? c.GetString() : null;
                if (string.IsNullOrWhiteSpace(context)) continue;
                try
                {
                    var response = await Http.PostAsJsonAsync("http://127.0.0.1:42761/d6/keydown", new { context });
                    if (response.IsSuccessStatusCode) await SendAsync(ws, new { @event = "showOk", context });
                    else await SendAsync(ws, new { @event = "showAlert", context });
                }
                catch { await SendAsync(ws, new { @event = "showAlert", context }); }
            }
            else if (evt == "willAppear")
            {
                var context = root.TryGetProperty("context", out var c) ? c.GetString() : null;
                if (!string.IsNullOrWhiteSpace(context)) await SendAsync(ws, new { @event = "setTitle", context, payload = new { title = "SOUND", target = 0, state = 0 } });
            }
        }
        return 0;
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length - 1; i++) if (args[i].StartsWith('-')) d[args[i]] = args[++i];
        return d;
    }

    private static async Task SendAsync(ClientWebSocket ws, object message)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }
}
