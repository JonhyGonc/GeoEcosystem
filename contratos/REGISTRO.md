# Registro de contratos entre projetos

Todo formato, protocolo ou estrutura de dados que atravessa a fronteira entre dois projetos tem uma linha aqui. Uma mudança de contrato segue o template [`mudanca-de-contrato`](../.github/ISSUE_TEMPLATE/mudanca-de-contrato.md) e precisa de aprovação do Chat 00 (regras 5, 7 e 8).

| ID | Contrato | Dono | Produtor(es) | Consumidor(es) | Versão(ões) | Local canônico | Testes cruzados | Estado |
|---|---|---|---|---|---|---|---|---|
| CT-01 | GeoMap Import Contract (`.geomap.json` / `.geomap.ndjson`) | GeoMap | Ponte DXF (GM, 0.1–0.3); GAE `GEOAUTO3GEOMAPEXPORT` (só 0.1) | Importador GeoMap | 0.1, 0.2, 0.3 | [`Geomap/docs/IMPORT_CONTRACT.md`](https://github.com/JonhyGonc/Geomap/blob/main/docs/IMPORT_CONTRACT.md) + `contracts/geomap/` | ❌ Fixture copiada à mão (GM-10, GAE-08) | Ativo; GAE nunca usado com dados reais |
| CT-02 | GADX (Named Pipe `GeoAutoEngine.Inspection`) | **Indefinido (D-02)** | GAE (servidor) / GAD (cliente) | ambos | "1", em duas variantes | [`gadx-inspecao/SPEC.md`](gadx-inspecao/SPEC.md) (descritiva) | ❌ | ⚠️ Ruptura silenciosa |
| CT-03 | `GeoAutoDocs.Inspection` (payload do pipe e arquivo `.geoadocs.json`) | **Indefinido (D-02)** | GAD | GAE (`InspectionPush`, `GEOAUTO3VIIMPORT`) | 1 | [`SPEC.md` §4](gadx-inspecao/SPEC.md#4-documento-geoautodocsinspection-v1) | ❌ | Ativo; código duplicado |
| CT-04 | Correções de inspeção (mensagens 6/7) | **Indefinido (D-02)** | GAE | GAD (só a fix) | sob GADX "1" | [`SPEC.md` §5](gadx-inspecao/SPEC.md#5-correções-tipos-67) | ❌ | ⚠️ Semântica divergente (GAD-14) |
| CT-05 | Dados do GAE no DWG (XData/NOD) | GAE | GAE | GAE; indiretamente o GeoMap (handle = `sourceId`) | `STORAGE_VERSION`, sufixos `_V2`/`_V3` | [`dwg-xdata/README.md`](dwg-xdata/README.md) | — | Contrato implícito |

Fora do registro, porque não atravessam fronteira de projeto: importação Multi-CSV do GAE (`GeoAutoEngine/docs/IMPORT_BATCH_CONTRACT.md`) e projeto compartilhado do GAD (`project.geoadocs.json`).
