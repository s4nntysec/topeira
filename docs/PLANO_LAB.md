# Plano de execução no lab (10.250.250.176)

Referência: `docs/LAB_WINDOWS.md` (passos detalhados) e `docs/PROTOCOLO.md` (IOCs de rede).

## Antes de começar
1. Confirmar snapshot da VM e isolamento da rede (sem rota para produção nem internet).
2. Confirmar política de auditoria para tarefas agendadas (eventos 4698 e 4699) e Sysmon ativo.

## Preparação
3. Copiar `topeira-listener.exe`, `topeira-simulator.exe` e `topeira-persist.exe` para `C:\topeira\`.
4. Conferir hashes SHA-256 com a lista registrada na SOCPURPLE-9. Se algum não bater, parar.
5. Liberar a porta 8443 no firewall do lab, somente para a rede do lab.

## Listener e simulação HTTP
6. Subir o listener com `--bind <IP_DO_LAB>` e `--port 8443`.
7. Rodar o simulador com 5 registros.
8. Verificar: respostas `status=200` e 5 linhas no log com `decoded` contendo `LAB-SYNTHETIC`.
9. Testar rota errada e hex inválido, e verificar o registro dos dois erros.

## HTTPS (opcional)
10. Gerar PFX autoassinado do lab e repetir 6 a 8 com `--pfx`. No simulador, usar `--insecure`.

## Persistência simulada
11. Com `TOPEIRA_LAB=1`: `install --ttl 30` e depois `status`.
12. Conferir a tarefa `TopeiraLabSim` e o marcador em `C:\topeira_lab\topeira_marker.txt` (após logon ou `tick` manual).
13. Executar `remove` e confirmar a remoção da tarefa e do diretório.

## Validação de detecção
14. Para cada evento da tabela em `LAB_WINDOWS.md`, verificar no SIEM ou no Event Viewer. Registrar os eventos ausentes como lacunas.

## Fechamento
15. Reverter o snapshot da VM.
16. Enviar as saídas (listener, simulador, `status`) e a lista de eventos encontrados e ausentes para registro na SOCPURPLE-9.
