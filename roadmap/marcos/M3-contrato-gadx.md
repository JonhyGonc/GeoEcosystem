# M3 — Contrato GADX estável

- **Estado:** M3.1 ✅ (PR #13), M3.2 ✅ (PR #14), M3.3a ✅ (PR #15) e M3.3b ✅ (PR #16), mesclados em 16/11/2026. M3.3c em revisão. Acompanhamento: Issue [#11](https://github.com/JonhyGonc/GeoEcosystem/issues/11). Mudança de contrato: Issue [#12](https://github.com/JonhyGonc/GeoEcosystem/issues/12).
- **Decisão:** [DEC-ECO-0002](../../decisoes/DEC-ECO-0002-versionamento-gadx.md)
- **Especificação:** [`contratos/gadx-inspecao/SPEC.md`](../../contratos/gadx-inspecao/SPEC.md) (GADX 1.1)
- **Dono:** agente único (Emenda 2); merge, instalação e testes no Civil 3D: usuário

## Passos
| Passo | Entrega | Verificação | Estado |
|---|---|---|---|
| M3.1 | Especificação normativa GADX 1.1 + DEC-ECO-0002 | PR mesclado pelo usuário | ✅ PR #13 |
| M3.2 | Fixtures em `contratos/gadx-inspecao/fixtures/`: frames binários e JSON de cada mensagem; `Hello`/`HelloAck` com e sem `capabilities`; `CorrectionPush` com `Point`, `Range` e `MultiPoint`; `CorrectionAck` `Partial` | Fixtures lidas pelo código atual dos dois lados (testes nas duas CIs) | ✅ PR #14 |
| M3.3a | `libs/GeoEcosystem.Contracts.Inspection` (`net8.0`) com o protocolo e o documento; GAE e GAD apagam as cópias e passam a usá-la; **sem mudança de comportamento** | CI da biblioteca, do GAE e do GAD verde; o usuário compila o plugin 2026 e faz o teste de sempre (inspeção e correção) | ✅ PR #15 |
| M3.3b | Payloads das correções (mensagens 6/7) unificados na biblioteca | CI verde com as fixtures `correction-*` | ✅ PR #16 (roteiro OK em 16/11/2026) |
| M3.3c | Servidor aplica S1–S3 (§7): lê `capabilities` e responde no `HelloAck` | CI verde; roteiro no Civil 3D 2026 com o GAD 1.1.0 | 🔄 |
| M3.4 | GAD envia `capabilities: ["corrections"]` e `applicationVersion` do produto; teste de que `locationMode` sai com o nome canônico | CI verde; roteiro manual no Civil 3D 2026 | ⏳ |
| M3.5 | ~~Instalação na equipe~~ **adiada para o fim do projeto** (decisão do usuário, 16/11/2026). No M3, só homologação na máquina do usuário | Usuário confirma correções funcionando no Civil 3D 2026 dele, com GAD 1.1.0 e com o GAD novo | ⏳ |

## Decisões tomadas neste marco
| Data | Decisão | Quem |
|---|---|---|
| 16/11/2026 | Cliente sem `capabilities` (GAD 1.1.0) continua recebendo correções (regra S2) | Usuário |
| 16/11/2026 | Versão no fio continua `1`; a especificação vira a revisão 1.1 | Agente (DEC-ECO-0002) |
| 16/11/2026 | Biblioteca só para `net8.0` ("somente com a versão de 2026"); o host 2024 do GAE deixa de compilar | Usuário |
| 16/11/2026 | M3.3 dividido em 3 PRs (a: mover sem mudar comportamento; b: correções; c: regras do servidor) | Agente |
| 16/11/2026 | Instalação na equipe só quando o projeto inteiro estiver terminado (a equipe quase não usa o GAD) | Usuário |

## Pontos em aberto
- `scripts/build-release.ps1` ainda tenta os 3 hosts por padrão, e o instalador exige os 3. Enquanto isso não for ajustado, use `-Versions 2026` (bundle sem instalador, opção A de `docs/INSTALACAO_EQUIPE.md`).
