# M1 — Versão oficial completa

- **Estado:** em execução desde 11/11/2026. **Homologação conjunta aprovada em 12/11/2026** (GAE `d762359` + GAD `64081d5`); falta instalar nos demais desenhistas (M1.4) e arquivar as branches (M1.5).
- **Aprovação:** roadmap aprovado pelo usuário em 11/11/2026 ([ROADMAP](../ROADMAP.md))
- **Objetivo:** juntar todo o trabalho de cada aplicação numa versão oficial (GAE `3.2.0`, GAD `1.1.0`), instalada em todos os desenhistas.
- **Pré-condição para o M2 (monorepo):** M1 concluído, sem branch funcional fora da `main`.

## Pontos de partida (revalidados em 11/11/2026)
| Repositório | `main` | A integrar | PRs existentes (via `refs/pull`) |
|---|---|---|---|
| GeoAutoEngine | `3acab63` | `sync-section-names@2937469` (contém `fix/correction-export-feedback@c2c10e4`), `feature/atualizar-textos-anomalias@1724528` | #4 → `c2c10e4`, #5 → `1724528` (provavelmente abertos) |
| GeoAutoDocs | `073191a` | `fix/section-edit-save-stability@5d333d6` | #1 → `5d333d6` (provavelmente aberto) |

## Passos
| Passo | Dono | Entrega | Verificação | Merge por |
|---|---|---|---|---|
| M1.0 | Chat 03 | CI permanente do GAD (`windows-latest`: restore, build, test em Release), num PR próprio para a `main` | CI verde no próprio PR | Usuário, após parecer do Chat 00 |
| M1.1 | Chat 03 | Fix do GAD na `main` (atualizar o PR #1 ou substituí-lo), resolvendo `MainWindow.xaml.cs` e mantendo a renumeração de `073191a` | CI verde; roteiro de teste manual (até 10 passos) | Usuário, após parecer do Chat 00 |
| M1.2 | Chat 02 | `sync-section-names` na `main`. O PR #4 só cobre `c2c10e4`: substituí-lo por um PR da branch inteira, + `GEOAUTO3VISYNC` no `PackageContents.xml` | CI verde; roteiro manual: revisar correções não trava o Civil 3D; `GEOAUTO3VISYNC` funciona | Usuário, após parecer do Chat 00 |
| M1.3 | Chat 02 | `feature/atualizar-textos-anomalias` sobre a nova `main` (atualizar o PR #5), resolvendo os 5 conflitos | CI verde; 213+ testes; roteiro manual: os dois comandos funcionam juntos | Usuário, após parecer do Chat 00 |
| M1.4 | Chats 02/03 + usuário | Versões `3.2.0` (GAE: `Directory.Build.props`, `AppVersion` do `PackageContents.xml`) e `1.1.0` (GAD: csproj e `.iss`); tags `v3.2.0` e `v1.1.0` no commit de merge; passo a passo de build e instalação para o usuário | Usuário instala na própria máquina e depois nos demais; `status/COMPATIBILIDADE.md` atualizado | Usuário |
| M1.5 | Chats 02/03 | Depois do M1.4 confirmado: tags `arquivo/Testes-ChatGPT`, `arquivo/ChatGPT`, `arquivo/backup-main` nos SHAs atuais e remoção das branches (autorizado pelo usuário em 11/11/2026) | Tags apontam para `61a47b4`, `427ccac`, `427ccac` | — |

## Regras deste marco
- **Escopo fechado:** só integração das branches, CI do GAD, número de versão e arquivamento. Nada de funcionalidade nova, refatoração ou mudança no protocolo GADX (o protocolo é assunto do M3).
- **Parecer do Chat 00:** o especialista envia ao usuário o link do PR e o resumo. O usuário encaminha ao Chat 00, que revisa o diff por git (repositórios públicos) e responde com `APROVADO-CHAT-00` ou `MUDANÇAS-CHAT-00`. O usuário faz o merge.
- **Acesso de escrita (P-1):** cada especialista precisa que o usuário peça explicitamente na sessão dele: "conecte o <repositório> com permissão de escrita para executar o M1".
- **Teste manual:** cada PR que muda comportamento traz um roteiro de até 10 passos para o usuário executar no Civil 3D ou no GAD.

## Melhorias internas autorizadas (em paralelo, fora do M1)
Ver [ROADMAP §6](../ROADMAP.md#6-melhorias-internas-autorizadas-com-o-roadmap-autonomia-regra-5). Cada item vira uma Issue no repositório da aplicação; o especialista pode mesclar depois de CI verde, sem passar pelo Chat 00, desde que não toque contratos.
