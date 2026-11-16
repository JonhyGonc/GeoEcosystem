# M3 — Contrato GADX estável

- **Estado:** M3.1 em revisão (PR do branch `contrato/011-gadx-v1-1`). Acompanhamento: Issue [#11](https://github.com/JonhyGonc/GeoEcosystem/issues/11). Mudança de contrato: Issue [#12](https://github.com/JonhyGonc/GeoEcosystem/issues/12).
- **Decisão:** [DEC-ECO-0002](../../decisoes/DEC-ECO-0002-versionamento-gadx.md)
- **Especificação:** [`contratos/gadx-inspecao/SPEC.md`](../../contratos/gadx-inspecao/SPEC.md) (GADX 1.1)
- **Dono:** agente único (Emenda 2); merge, instalação e testes no Civil 3D: usuário

## Passos
| Passo | Entrega | Verificação | Estado |
|---|---|---|---|
| M3.1 | Especificação normativa GADX 1.1 + DEC-ECO-0002 | PR mesclado pelo usuário | 🔄 |
| M3.2 | Fixtures em `contratos/gadx-inspecao/fixtures/`: frames binários e JSON de cada mensagem; `Hello`/`HelloAck` com e sem `capabilities`; `CorrectionPush` com `Point`, `Range` e `MultiPoint`; `CorrectionAck` `Partial` | Fixtures lidas pelo código atual dos dois lados (testes nas duas CIs) | ⏳ |
| M3.3 | `libs/GeoEcosystem.Contracts.Inspection` a partir das duas cópias; GAE e GAD passam a referenciá-la; servidor aplica S1–S3 (§7 da especificação); alvo de compilação decidido aqui | CI das duas aplicações verde com as fixtures | ⏳ |
| M3.4 | GAD envia `capabilities: ["corrections"]` e `applicationVersion` do produto; teste de que `locationMode` sai com o nome canônico | CI verde; roteiro manual no Civil 3D 2026 | ⏳ |
| M3.5 | Instalação: GAE primeiro, depois GAD (sem urgência entre os dois, pela regra S2) | Usuário confirma correções funcionando com GAD 1.1.0 e com o GAD novo | ⏳ |

## Decisões tomadas neste marco
| Data | Decisão | Quem |
|---|---|---|
| 16/11/2026 | Cliente sem `capabilities` (GAD 1.1.0) continua recebendo correções (regra S2) | Usuário |
| 16/11/2026 | Versão no fio continua `1`; a especificação vira a revisão 1.1 | Agente (DEC-ECO-0002) |

## Pontos em aberto
- Alvo de compilação da biblioteca (`netstandard2.0` × `net8.0`): decidir no M3.3, considerando que só o Civil 3D 2026 é homologado e que os hosts 2024/2025 continuam no código.
