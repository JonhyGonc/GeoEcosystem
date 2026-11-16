# Bibliotecas compartilhadas

Código usado por mais de uma aplicação do monorepo. Cada biblioteca tem a sua CI e só muda por PR com a CI de todas as aplicações consumidoras verde.

| Biblioteca | O que contém | Consumidores | Alvo | Decisão |
|---|---|---|---|---|
| [`GeoEcosystem.Contracts.Inspection`](GeoEcosystem.Contracts.Inspection/) | Protocolo GADX (frame, tipos de mensagem, `Hello`/`HelloAck` com `capabilities`, `InspectionAck`, `Error`), documento `GeoAutoDocs.Inspection` v1 com a validação obrigatória e payloads das correções (`CorrectionPush`/`CorrectionAck`) | `apps/geoautoengine` (`GeoAutoEngine.Application`), `apps/geoautodocs` (`GeoAutoDocs.Core`) | `net8.0` | [DEC-ECO-0002](../decisoes/DEC-ECO-0002-versionamento-gadx.md) |

## `GeoEcosystem.Contracts.Inspection`
- **Especificação:** [`contratos/gadx-inspecao/SPEC.md`](../contratos/gadx-inspecao/SPEC.md). A biblioteca implementa a especificação, e não o contrário. Mudou o contrato → muda a especificação, as fixtures e a biblioteca no mesmo PR.
- **Testes:** `GeoEcosystem.Contracts.Inspection.Tests` lê e produz todas as [fixtures](../contratos/gadx-inspecao/fixtures/README.md). CI: `.github/workflows/libs-contracts-inspection-ci.yml`.
- **Só .NET 8:** decisão do usuário em 16/11/2026 (apenas o Civil 3D 2026 é suportado). Por isso o host 2024 do GeoAutoEngine (`netstandard2.0`/`net48`) **não compila mais** a partir do M3.3. Para gerar o plugin, use `scripts\build-release.ps1 -Versions 2026`.
- **Uso:** os projetos consumidores declaram `<Using Include="GeoEcosystem.Contracts.Inspection" />`, então o código continua usando os mesmos nomes de tipo de antes (`InspectionPipeProtocol`, `InspectionExchangeDocument`…).
- **Correções (M3.3b):** os nomes são os do GAE: `InspectionCorrectionPush`, `InspectionOccurrenceCorrection`, `InspectionCorrectionAcknowledgement`. O GAD trocou `InspectionCorrectionPackage` e `InspectionCorrection` por eles. As propriedades `Has*` dizem se um campo veio no JSON (tolerância da SPEC §9.2). `RejectedCount` é calculado (`requestedCount − appliedCount`), e `AppliedCount` conta também os itens `Unchanged`, como o GAD já fazia.
