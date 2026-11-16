# Registro de contratos entre projetos

Todo formato, protocolo ou estrutura de dados que atravessa a fronteira entre dois projetos tem uma linha aqui. Uma mudança de contrato segue o template [`mudanca-de-contrato`](../.github/ISSUE_TEMPLATE/mudanca-de-contrato.md), com versão e compatibilidade com o que já existe (regra 8), e só entra com o merge do usuário (Emenda 2 do protocolo).

| ID | Contrato | Dono | Produtor(es) | Consumidor(es) | Versão(ões) | Local canônico | Testes cruzados | Estado |
|---|---|---|---|---|---|---|---|---|
| CT-01 | GeoMap Import Contract (`.geomap.json` / `.geomap.ndjson`) | GeoMap | Ponte DXF (GM, 0.1–0.3); GAE `GEOAUTO3GEOMAPEXPORT` (só 0.1) | Importador GeoMap | 0.1, 0.2, 0.3 | [`apps/geomap/docs/IMPORT_CONTRACT.md`](../apps/geomap/docs/IMPORT_CONTRACT.md) + `apps/geomap/contracts/geomap/` | ❌ Fixture copiada à mão (GM-10, GAE-08) | Ativo; GAE nunca usado com dados reais |
| CT-02 | GADX (Named Pipe `GeoAutoEngine.Inspection`) | GeoEcosystem (`contratos/` + `libs/`, [DEC-ECO-0002](../decisoes/DEC-ECO-0002-versionamento-gadx.md)) | GAE (servidor) / GAD (cliente) | ambos | no fio `1`; especificação **1.1** (`capabilities`) | [`gadx-inspecao/SPEC.md`](gadx-inspecao/SPEC.md) (normativa) | ✅ [fixtures](gadx-inspecao/fixtures/README.md) lidas pelas duas CIs (M3.2) | Especificado; `capabilities` ainda não implementado (M3.3) |
| CT-03 | `GeoAutoDocs.Inspection` (payload do pipe e arquivo `.geoadocs.json`) | GeoEcosystem (DEC-ECO-0002) | GAD | GAE (`InspectionPush`, `GEOAUTO3VIIMPORT`) | 1 | [`SPEC.md` §8](gadx-inspecao/SPEC.md#8-documento-geoautodocsinspection-v1-ct-03) | ✅ fixture `inspection-push` (M3.2) | Ativo; código duplicado até o M3.3 |
| CT-04 | Correções de inspeção (mensagens 6/7) | GeoEcosystem (DEC-ECO-0002) | GAE | GAD 1.1.0+ | capacidade `corrections` do GADX 1.1 | [`SPEC.md` §9](gadx-inspecao/SPEC.md#9-correções-ct-04) | ✅ fixtures `correction-push` / `correction-ack-partial` (M3.2) | Semântica definida: estado completo |
| CT-05 | Dados do GAE no DWG (XData/NOD) | GAE | GAE | GAE; indiretamente o GeoMap (handle = `sourceId`) | `STORAGE_VERSION`, sufixos `_V2`/`_V3` | [`dwg-xdata/README.md`](dwg-xdata/README.md) | — | Contrato implícito |

Fora do registro, porque não atravessam fronteira de projeto: importação Multi-CSV do GAE (`apps/geoautoengine/docs/IMPORT_BATCH_CONTRACT.md`) e projeto compartilhado do GAD (`project.geoadocs.json`).
