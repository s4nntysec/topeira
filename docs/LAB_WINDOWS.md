# topeira no lab Windows

Stack: C# (.NET 8). Os binários são executáveis únicos, self-contained (não exigem .NET instalado no lab).
Todos os dados são fictícios e marcados com `LAB-SYNTHETIC`.

## Binários
Gerados com `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`:

| Arquivo | Função |
|---|---|
| `topeira-listener.exe` | listener HTTP/HTTPS que registra eventos |
| `topeira-simulator.exe` | cliente que envia tráfego simulado |
| `topeira-persist.exe` | persistência simulada (tarefa agendada) |

Os arquivos ficam em `dist/` no ambiente de build. Copie cada `.exe` para a VM do lab (ex.: `C:\topeira\`).

## 1. Preparação
1. Confirme que o snapshot da VM existe e que a rede do lab está isolada da produção e da internet.
2. Copie os três `.exe` para `C:\topeira\` no lab. Não é necessário instalar o .NET.
3. Confira os hashes SHA-256 contra os valores registrados na issue, antes de executar:
   ```powershell
   Get-FileHash C:\topeira\*.exe -Algorithm SHA256
   ```

## 2. Listener (no lab)
```powershell
cd C:\topeira
.\topeira-listener.exe --bind <IP_DO_LAB> --port 8443 --log C:\topeira\logs\listener_events.jsonl
```
Para HTTPS, gere um certificado PFX autoassinado do laboratório e passe `--pfx` e `--pfx-pass`:
```powershell
.\topeira-listener.exe --bind <IP_DO_LAB> --port 8443 --pfx C:\topeira\lab.pfx --pfx-pass <SENHA> --log C:\topeira\logs\listener_events.jsonl
```

## 3. Simulação de comunicação (cliente)
```powershell
.\topeira-simulator.exe --url http://<IP_DO_LAB>:8443/api/v1/sync --records 5
```
Para HTTPS com certificado autoassinado, adicione `--insecure` (somente laboratório).
Para ver a mensagem codificada sem enviar, use `--dry-run`.

## 4. Persistência simulada
Requer `TOPEIRA_LAB=1`. A tarefa se chama `TopeiraLabSim`, roda no logon com privilégio limitado e se remove sozinha após o prazo (padrão: 30 minutos).
```powershell
$env:TOPEIRA_LAB = "1"
.\topeira-persist.exe install --ttl 30   # cria a tarefa
.\topeira-persist.exe status              # consulta
.\topeira-persist.exe remove              # limpeza manual
```
Para ver os comandos sem executar, use `--dry-run`.

A tarefa aponta para o caminho do `topeira-persist.exe` que executou `install`. Não mova o executável depois de instalar.

## 5. Limpeza ao final
```powershell
$env:TOPEIRA_LAB = "1"
.\topeira-persist.exe remove
```
Depois, reverta o snapshot da VM.

## Eventos esperados (para validação de detecção)

| Ação | Evento / fonte | Indicador |
|---|---|---|
| Criação da tarefa | Security 4698 | Nome `TopeiraLabSim`, ação com `topeira-persist.exe tick` |
| Execução de `schtasks` | Sysmon 1 (ProcessCreate) | `schtasks.exe /Create /TN TopeiraLabSim` |
| Disparo no logon | Security 4688 / Sysmon 1 | `topeira-persist.exe tick` |
| Escrita de marcador | Sysmon 11 (FileCreate) | `C:\topeira_lab\topeira_marker.txt` |
| Remoção da tarefa | Security 4699 | Nome `TopeiraLabSim` |
| Comunicação HTTP(S) | Sysmon 3 (NetworkConnect), proxy/firewall | `POST /api/v1/sync`, `User-Agent: topeira-sim/0.1` |
| Conteúdo | Listener (`listener_events.jsonl`) | `decoded` contém `LAB-SYNTHETIC` |

Auditoria de criação de tarefas (4698) precisa estar habilitada na política do host.
