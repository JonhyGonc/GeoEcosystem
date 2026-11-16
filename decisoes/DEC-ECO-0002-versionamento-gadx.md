# DEC-ECO-0002 — Versionamento e semântica do contrato GADX (D-02, R-09)

- **Estado:** aprovada — merge do PR #13 pelo usuário em 16/11/2026
- **Data:** 16/11/2026
- **Origem:** roadmap aprovado em 11/11/2026, M3 (D-02: opções a + c; R-09: opção i); pergunta ao usuário sobre a transição, respondida em 16/11/2026
- **Contratos:** CT-02, CT-03, CT-04 ([`contratos/REGISTRO.md`](../contratos/REGISTRO.md))
- **Especificação:** [`contratos/gadx-inspecao/SPEC.md`](../contratos/gadx-inspecao/SPEC.md) (GADX 1.1)

## Contexto
- O GADX tinha duas variantes sob a mesma versão "1": GAD com tipos 1–5 e GAD com tipos 1–7. O `Hello` não diz se o cliente entende correções (risco R-03).
- A semântica das correções divergia em teoria: o GAE envia o estado completo, e o GAD foi escrito para "campo ausente = não alterado" (R-09, GAD-14).
- O código do contrato está duplicado em `apps/geoautoengine` e `apps/geoautodocs` (GAD-12b).
- Desde o M1, a equipe usa o **GAD 1.1.0**, que aceita correções mas não as declara. Máquinas com Civil 3D 2024/2025 ainda têm GAE anteriores à 3.2.0.

## Alternativas
**Como versionar (D-02):**

| Opção | Avaliação |
|---|---|
| **(a) `capabilities` opcional no `Hello` (e no `HelloAck`)** | **Escolhida.** Aditiva; nada muda para quem não conhece o campo |
| (b) Protocolo v2 no fio (6/7 só na v2) | O GAE 3.2.0 recusa qualquer versão ≠ 1 → romperia GAD novo + GAE antigo. Descartada |
| **(c) Biblioteca compartilhada em `libs/`** | **Escolhida, junto com (a).** Acaba com a duplicação; executada no M3.3 |

**Semântica das correções (R-09):**

| Opção | Avaliação |
|---|---|
| **(i) GAD aplica como estado completo** | **Escolhida.** Alinha com o produtor real (`GetCorrections` envia tudo) |
| (ii) GAE omite campos não alterados | Mudaria o produtor e criaria a ambiguidade que a decisão quer remover |

**Cliente sem `capabilities` (transição):**

| Opção | Avaliação |
|---|---|
| **Continua recebendo correções** | **Escolhida pelo usuário (16/11/2026).** O GAD 1.1.0 continua funcionando com um GAE novo. O GAD 1.0.x usa conexão efêmera e praticamente nunca é alvo |
| Para de receber | Mais estrito, mas obrigaria a instalar GAE e GAD novos juntos em todas as máquinas |

## Decisão
1. **Versão no fio continua `1`.** A especificação passa a ser a revisão **1.1**, normativa. Campos novos só entram como opcionais, e quem lê ignora o que não conhece.
2. **`capabilities`** (lista de texto, opcional) no `Hello` e no `HelloAck`. Primeira capacidade: `corrections`. Regras S1–S3 e C1–C3 na §7 da especificação.
3. **Transição (S2):** cliente sem `capabilities` é tratado como hoje e pode receber correções. Tornar isso estrito exige nova decisão, quando todas as máquinas tiverem um GAD que declara capacidades.
4. **Correções = estado completo.** O GAE envia todos os campos de cada ocorrência editada. O GAD interpreta os campos pelo `locationMode` cadastrado e trata campo ausente como "não alterar", só como tolerância (§9.2).
5. **Biblioteca compartilhada** `libs/GeoEcosystem.Contracts.Inspection` no M3.3, testada contra as fixtures do M3.2. **Alvo: só `net8.0`** (decisão do usuário em 16/11/2026: "somente com a versão de 2026"). Consequência aceita: o host 2024 do GAE (`netstandard2.0`/`net48`) deixa de compilar. O host 2025 (.NET 8) continua compilável, mas não é homologado.

## Impacto por aplicação
| Aplicação | O que muda | Quando |
|---|---|---|
| GeoAutoEngine | servidor lê `capabilities`, aplica S1–S3 e responde `capabilities` no `HelloAck`; passa a usar a biblioteca | M3.3 |
| GeoAutoDocs | cliente envia `["corrections"]`; `applicationVersion` = versão do produto; passa a usar a biblioteca | M3.3 / M3.4 |
| GeoMap | nada | — |

## Compatibilidade, testes e rollback
- **Compatibilidade:** todas as combinações GAE × GAD continuam funcionando como hoje (matriz na §11 da especificação).
- **Testes:**
  - fixtures do M3.2 com `Hello`/`HelloAck` com e sem `capabilities`;
  - as duas CIs testam contra as mesmas fixtures (M3.3/M3.4);
  - roteiro manual no Civil 3D 2026 com o GAD 1.1.0 e com o GAD novo.
- **Rollback:**
  - sem `capabilities`, tudo volta ao comportamento 1.0. Reinstalar o GAE 3.2.0 ou o GAD 1.1.0 basta;
  - nenhum dado gravado muda de formato: documento `schemaVersion` 1 e `.geoadocs.json` iguais.
