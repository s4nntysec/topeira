// topeira simulator (C#): gera tráfego de exfiltração simulado.
// Envia somente dados fictícios marcados como LAB-SYNTHETIC, codificados em hex,
// para o listener do laboratório. Não lê arquivos nem coleta nada do host.
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

const string Marker = "LAB-SYNTHETIC";

string url = "";
int records = 5;
bool insecure = false;
bool dryRun = false;
double timeout = 5.0;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--url": url = args[++i]; break;
        case "--records": records = int.Parse(args[++i]); break;
        case "--timeout": timeout = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--insecure": insecure = true; break;
        case "--dry-run": dryRun = true; break;
        default:
            Console.Error.WriteLine($"argumento desconhecido: {args[i]}");
            return 2;
    }
}

if (string.IsNullOrEmpty(url))
{
    Console.Error.WriteLine("uso: topeira-simulator --url <https://ip:porta/api/v1/sync> [--records N] [--insecure] [--timeout S] [--dry-run]");
    return 2;
}

var handler = new HttpClientHandler();
if (insecure)
{
    // Somente para certificado autoassinado do laboratório.
    handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
}
using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(timeout) };
client.DefaultRequestHeaders.UserAgent.ParseAdd("topeira-sim/0.1");

var sessionId = Guid.NewGuid().ToString();
for (int i = 1; i <= records; i++)
{
    var rec = new Dictionary<string, object>
    {
        ["marker"] = Marker,
        ["record_id"] = $"SYN-{i:D4}",
        ["session"] = sessionId,
        ["generated_at"] = DateTimeOffset.UtcNow.ToString("o"),
        ["fake_payload"] = $"dado-ficticio-{i}",
    };

    var json = JsonSerializer.Serialize(rec);
    var hex = Convert.ToHexString(Encoding.UTF8.GetBytes(json)).ToLowerInvariant();

    if (dryRun)
    {
        Console.WriteLine(hex);
        continue;
    }

    using var content = new StringContent(hex, Encoding.ASCII, "text/plain");
    using var resp = await client.PostAsync(url, content);
    Console.WriteLine($"{rec["record_id"]} enviado status={(int)resp.StatusCode}");
}

return 0;
