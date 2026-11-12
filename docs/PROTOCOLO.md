# topeira: protocolo de comunicação (v0.1)

Implementação em C# (.NET 8). Os componentes ficam em `listener-cs/`, `simulator-cs/` e `persistence-cs/`. Os arquivos Python `listener/` e `simulator/` foram substituídos pela versão C#.

Projeto de laboratório do Purple Team. Todo dado transmitido é fictício e marcado como `LAB-SYNTHETIC`.

## Transporte
- HTTP ou HTTPS (o listener usa TLS quando recebe `--cert` e `--key`).
- Método `POST`, caminho padrão `/api/v1/sync` (configurável com `--path`).
- `User-Agent` do simulador: `topeira-sim/0.1`.

## Formato da mensagem
1. Objeto JSON em UTF-8 com os campos:
   - `marker`: sempre `LAB-SYNTHETIC`
   - `record_id`: sequencial, `SYN-0001`, `SYN-0002`, ...
   - `session`: UUID v4 por execução do simulador
   - `generated_at`: timestamp ISO 8601 em UTC
   - `fake_payload`: texto fictício
2. Codificação: os bytes UTF-8 do JSON são convertidos para hexadecimal minúsculo (`bytes.hex()`), e o corpo HTTP é essa string ASCII.

Exemplo de corpo (trecho):
```
7b226d61726b6572223a20224c41422d53594e544845544943222c...
```

## Decodificação (listener)
1. Lê o corpo até `Content-Length` (limite de 64 KiB).
2. Decodifica ASCII e aplica `bytes.fromhex`.
3. Decodifica o resultado como UTF-8.
4. Em caso de falha, registra `decode_error` e mantém `raw_hex` para análise.

## Registro de eventos
Cada requisição gera uma linha JSON em `logs/listener_events.jsonl` com:
`timestamp`, `src_ip`, `src_port`, `method`, `path`, `user_agent`, `content_length`,
`raw_hex`, `decoded`, `decode_error`, `response_status`, `tls`.

## IOCs de rede esperados
| Indicador | Valor |
|---|---|
| Método e caminho | `POST /api/v1/sync` |
| User-Agent | `topeira-sim/0.1` |
| Corpo | string hexadecimal ASCII, comprimento par, começando com `7b22` (`{"`) |
| Marcador no conteúdo decodificado | `LAB-SYNTHETIC` |
| Volume | um POST por registro, corpo pequeno (< 1 KiB) |

Use `User-Agent` e o marcador para validar regras de detecção. Essas strings são exclusivas deste laboratório.

## Build e testes locais (C#)
```bash
dotnet build listener-cs -c Release
dotnet build simulator-cs -c Release
dotnet run --project listener-cs -c Release -- --bind 127.0.0.1 --port 8443 --log logs/listener_events.jsonl
dotnet run --project simulator-cs -c Release -- --url http://127.0.0.1:8443/api/v1/sync --records 3
```

HTTPS: passe `--pfx <arquivo.pfx> --pfx-pass <senha>` ao listener. No simulador, use `--insecure` apenas com certificado autoassinado do lab.

## Persistência (Windows)
`persistence-cs/` é um port do `persistence/persist_sim.py`. Exige `TOPEIRA_LAB=1`, cria a tarefa `TopeiraLabSim` (logon, privilégio limitado), grava marcadores `LAB-SYNTHETIC` em `C:\topeira_lab` e remove a tarefa e os arquivos ao expirar o prazo (`--ttl`, padrão 30 min). Não é furtiva.
