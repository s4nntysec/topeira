// topeira listener (C#): laboratório de Purple Team.
// Recebe POST em HTTP ou HTTPS, decodifica o corpo (hex -> UTF-8) e registra
// eventos em JSON Lines. Não executa nada do que recebe.
using System.Globalization;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

const int MaxBody = 64 * 1024;

var opts = ParseArgs(args);
if (opts is null) return 2;

var logDir = Path.GetDirectoryName(Path.GetFullPath(opts.LogPath))!;
Directory.CreateDirectory(logDir);
var logLock = new object();

var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.WebHost.ConfigureKestrel(k =>
{
    k.Limits.MaxRequestBodySize = MaxBody;
    if (opts.PfxPath is null)
    {
        k.Listen(IPAddress.Parse(opts.Bind), opts.Port);
    }
    else
    {
        var cert = new X509Certificate2(opts.PfxPath, opts.PfxPassword);
        k.Listen(IPAddress.Parse(opts.Bind), opts.Port,
            lo => lo.UseHttps(cert));
    }
});

var app = builder.Build();
var logger = app.Logger;

void WriteEvent(JsonObject ev)
{
    var line = ev.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    lock (logLock)
    {
        File.AppendAllText(opts.LogPath, line + Environment.NewLine, new UTF8Encoding(false));
    }
}

static string? Decode(byte[] raw, out string? error)
{
    error = null;
    try
    {
        var hex = Encoding.ASCII.GetString(raw).Trim();
        var bytes = Convert.FromHexString(hex);
        return new UTF8Encoding(false, true).GetString(bytes);
    }
    catch (Exception ex) when (ex is FormatException or ArgumentException or DecoderFallbackException)
    {
        error = $"decodificação falhou: {ex.Message}";
        return null;
    }
}

app.MapMethods("/{**rest}", new[] { "GET", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" },
    async (HttpContext ctx) => await Record(ctx, 404, Array.Empty<byte>(), null, "método ou caminho inesperado"));

app.MapPost("/{**rest}", async (HttpContext ctx) =>
{
    if (!string.Equals(ctx.Request.Path.Value, opts.Path, StringComparison.Ordinal))
    {
        return await Record(ctx, 404, Array.Empty<byte>(), null, "path inesperado");
    }

    using var ms = new MemoryStream();
    await ctx.Request.Body.CopyToAsync(ms);
    var raw = ms.ToArray();
    if (raw.Length > MaxBody)
    {
        return await Record(ctx, 413, raw, null, "corpo acima do limite");
    }

    var decoded = Decode(raw, out var error);
    return await Record(ctx, 200, raw, decoded, error);
});

async Task<IResult> Record(HttpContext ctx, int status, byte[] raw, string? decoded, string? error)
{
    var ev = new JsonObject
    {
        ["timestamp"] = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
        ["src_ip"] = ctx.Connection.RemoteIpAddress?.ToString(),
        ["src_port"] = ctx.Connection.RemotePort,
        ["method"] = ctx.Request.Method,
        ["path"] = ctx.Request.Path.Value,
        ["user_agent"] = ctx.Request.Headers.UserAgent.ToString(),
        ["content_length"] = raw.Length,
        ["raw_hex"] = Convert.ToHexString(raw).ToLowerInvariant(),
        ["decoded"] = decoded,
        ["decode_error"] = error,
        ["response_status"] = status,
        ["tls"] = ctx.Request.IsHttps,
    };
    WriteEvent(ev);
    logger.LogInformation("evento de {Ip}:{Port} {Method} {Path} status={Status}",
        ev["src_ip"]?.ToString(), ev["src_port"]?.ToString(), ctx.Request.Method, ctx.Request.Path, status);
    return Results.Text(status == 200 ? "ok" : (status == 413 ? "too large" : "not found"),
        "text/plain", Encoding.UTF8, statusCode: status);
}

logger.LogInformation("listener em {Scheme}://{Bind}:{Port}{Path} -> log {Log}",
    opts.PfxPath is null ? "http" : "https", opts.Bind, opts.Port, opts.Path, opts.LogPath);
app.Run();
return 0;

static Options? ParseArgs(string[] a)
{
    var o = new Options();
    for (int i = 0; i < a.Length; i++)
    {
        string Next() => i + 1 < a.Length ? a[++i] : throw new ArgumentException($"falta valor para {a[i - 1]}");
        try
        {
            switch (a[i])
            {
                case "--bind": o.Bind = Next(); break;
                case "--port": o.Port = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--path": o.Path = Next(); break;
                case "--pfx": o.PfxPath = Next(); break;
                case "--pfx-pass": o.PfxPassword = Next(); break;
                case "--log": o.LogPath = Next(); break;
                default:
                    Console.Error.WriteLine($"argumento desconhecido: {a[i]}");
                    return null;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return null;
        }
    }
    return o;
}

sealed class Options
{
    public string Bind { get; set; } = "127.0.0.1"; // no lab, use o IP da rede isolada
    public int Port { get; set; } = 8443;
    public string Path { get; set; } = "/api/v1/sync";
    public string? PfxPath { get; set; }
    public string? PfxPassword { get; set; }
    public string LogPath { get; set; } = "logs/listener_events.jsonl";
}
