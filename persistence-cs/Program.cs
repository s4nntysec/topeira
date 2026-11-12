// topeira persistence simulation (C#, Windows, laboratório).
// Port de persistence/persist_sim.py. Simula persistência por tarefa agendada,
// com eventos observáveis e remoção automática. NÃO é furtiva: o nome da tarefa
// e os arquivos são identificáveis e não há técnica de ocultação.
//
// Comandos: install | tick | remove | status [--ttl MIN] [--dry-run]
// Trava: só executa com TOPEIRA_LAB=1 no ambiente.
using System.Diagnostics;
using System.Globalization;

const string TaskName = "TopeiraLabSim";
const string LabDir = @"C:\topeira_lab";
static string MarkerPath() => Path.Combine(LabDir, "topeira_marker.txt");
static string ExpiryPath() => Path.Combine(LabDir, "topeira_expiry.txt");
const int DefaultTtlMin = 30;

if (args.Length == 0)
{
    Console.Error.WriteLine("uso: topeira-persist <install|tick|remove|status> [--ttl MIN] [--dry-run]");
    return 2;
}

string command = args[0];
int ttl = DefaultTtlMin;
bool dryRun = false;
for (int i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--ttl": ttl = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--dry-run": dryRun = true; break;
        default:
            Console.Error.WriteLine($"argumento desconhecido: {args[i]}");
            return 2;
    }
}

switch (command)
{
    case "install": return Install(ttl, dryRun);
    case "tick": return Tick(dryRun);
    case "remove": return Remove(dryRun);
    case "status": return Status();
    default:
        Console.Error.WriteLine($"comando desconhecido: {command}");
        return 2;
}

static void RequireLab()
{
    if (Environment.GetEnvironmentVariable("TOPEIRA_LAB") != "1")
    {
        Console.Error.WriteLine("recusado: defina TOPEIRA_LAB=1 (somente laboratório isolado)");
        Environment.Exit(3);
    }
}

static int RunSchtasks(string[] schtasksArgs, bool dryRun, bool quiet = false)
{
    var line = "schtasks " + string.Join(' ', schtasksArgs);
    Console.WriteLine("+ " + line);
    if (dryRun) return 0;
    try
    {
        var psi = new ProcessStartInfo("schtasks") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in schtasksArgs) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0 && !quiet)
        {
            Console.WriteLine(string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim());
        }
        return p.ExitCode;
    }
    catch (System.ComponentModel.Win32Exception)
    {
        Console.WriteLine("schtasks não encontrado: este programa é para Windows");
        return 127;
    }
}

static int Install(int ttl, bool dryRun)
{
    RequireLab();
    Directory.CreateDirectory(LabDir);
    var expiry = DateTime.Now.AddMinutes(ttl);
    File.WriteAllText(ExpiryPath(), expiry.ToString("o", CultureInfo.InvariantCulture));

    var exe = Environment.ProcessPath ?? "topeira-persist.exe";
    var action = $"\"{exe}\" tick";
    int rc = RunSchtasks(new[] { "/Create", "/TN", TaskName, "/TR", action,
                                 "/SC", "ONLOGON", "/RL", "LIMITED", "/F" }, dryRun);
    Console.WriteLine(rc == 0 ? "tarefa criada" : "falha ao criar tarefa");
    return rc;
}

static int Tick(bool dryRun)
{
    RequireLab();
    Directory.CreateDirectory(LabDir);
    var now = DateTime.Now;
    File.AppendAllText(MarkerPath(),
        $"{now.ToString("o", CultureInfo.InvariantCulture)} LAB-SYNTHETIC persistencia-simulada{Environment.NewLine}");

    DateTime expiry = now; // sem prazo registrado: remove imediatamente
    if (File.Exists(ExpiryPath()))
    {
        expiry = DateTime.Parse(File.ReadAllText(ExpiryPath()).Trim(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
    if (now >= expiry)
    {
        RunSchtasks(new[] { "/Delete", "/TN", TaskName, "/F" }, dryRun);
        Console.WriteLine("prazo expirado: tarefa removida");
    }
    return 0;
}

static int Remove(bool dryRun)
{
    RequireLab();
    RunSchtasks(new[] { "/Delete", "/TN", TaskName, "/F" }, dryRun, quiet: true);
    if (!dryRun && Directory.Exists(LabDir))
    {
        foreach (var f in new[] { MarkerPath(), ExpiryPath() })
        {
            if (File.Exists(f)) File.Delete(f);
        }
        try { Directory.Delete(LabDir); } catch (IOException) { /* diretório não vazio */ }
    }
    Console.WriteLine("limpeza concluída");
    return 0;
}

static int Status()
{
    RunSchtasks(new[] { "/Query", "/TN", TaskName }, false, quiet: true);
    if (File.Exists(MarkerPath())) Console.WriteLine($"marcador: {MarkerPath()}");
    return 0;
}
