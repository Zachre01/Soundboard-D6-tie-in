using System.Net;
using System.Text;
using System.Text.Json;

namespace DeckSoundboard;

public sealed class D6Server : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly AppConfig _config;
    private readonly ConfigStore _store;
    private readonly SoundboardController _controller;
    private CancellationTokenSource? _cts;
    public event Action<string>? ContextSeen;

    public D6Server(AppConfig config, ConfigStore store, SoundboardController controller)
    {
        _config = config; _store = store; _controller = controller;
        _listener.Prefixes.Add("http://127.0.0.1:42761/");
    }

    public void Start()
    {
        _listener.Start(); _cts = new CancellationTokenSource(); _ = LoopAsync(_cts.Token);
    }

    private async Task LoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch when (token.IsCancellationRequested) { break; }
            catch { continue; }
            _ = Task.Run(() => HandleAsync(ctx), token);
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            if (ctx.Request.HttpMethod == "POST" && ctx.Request.Url?.AbsolutePath == "/d6/keydown")
            {
                using var sr = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                var json = await sr.ReadToEndAsync();
                var req = JsonSerializer.Deserialize<KeyRequest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (req is null || string.IsNullOrWhiteSpace(req.Context)) { await Reply(ctx, 400, "bad request"); return; }

                var binding = _config.Bindings.FirstOrDefault(b => b.Context == req.Context);
                if (binding is null)
                {
                    binding = new D6Binding { Context = req.Context, FriendlyName = "D6 Key " + (_config.Bindings.Count + 1), LastSeenUtc = DateTime.UtcNow };
                    _config.Bindings.Add(binding); _store.Save(_config);
                }
                else binding.LastSeenUtc = DateTime.UtcNow;
                ContextSeen?.Invoke(req.Context);

                if (!string.IsNullOrWhiteSpace(binding.SoundId)) await _controller.ToggleAsync(binding.SoundId);
                await Reply(ctx, 200, string.IsNullOrWhiteSpace(binding.SoundId) ? "unassigned" : "ok");
                return;
            }
            if (ctx.Request.HttpMethod == "POST" && ctx.Request.Url?.AbsolutePath == "/stop")
            {
                await _controller.EmergencyStopAsync(); await Reply(ctx, 200, "stopped"); return;
            }
            if (ctx.Request.HttpMethod == "GET" && ctx.Request.Url?.AbsolutePath == "/health") { await Reply(ctx, 200, "ok"); return; }
            await Reply(ctx, 404, "not found");
        }
        catch (Exception ex) { try { await Reply(ctx, 500, ex.Message); } catch { } }
    }

    private static async Task Reply(HttpListenerContext ctx, int status, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text); ctx.Response.StatusCode = status; ctx.Response.ContentType = "text/plain"; ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes); ctx.Response.Close();
    }

    public void Dispose() { _cts?.Cancel(); try { _listener.Stop(); } catch { } _listener.Close(); }
    private sealed class KeyRequest { public string Context { get; set; } = ""; }
}
