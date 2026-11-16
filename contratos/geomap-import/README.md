# GeoMap Import Contract (CT-01)

**A especificação canônica fica no GeoMap**, e não aqui: o validador e a CI do GeoMap leem os exemplos do próprio GeoMap (decisão E3-1).

- Especificação: [`apps/geomap/docs/IMPORT_CONTRACT.md`](../../apps/geomap/docs/IMPORT_CONTRACT.md)
- Exemplos: [`apps/geomap/contracts/geomap/`](../../apps/geomap/contracts/geomap/)
- Validador: `apps/geomap/tools/importer/GeoMap.Contract/GeoMapContractValidator.cs`
- Produtor oficial: `apps/geoautoengine/src/GeoAutoEngine.Application/GeoMapExport/` (comando `GEOAUTO3GEOMAPEXPORT`)

## Regras entre projetos (vigentes)
- Mudança de versão ou de regra que afete produtores exige Issue `mudanca-de-contrato` aqui e merge do usuário (Emenda 2).
- A identidade do projeto no GeoMap depende só de `project.externalId` (DEC-032 do GeoMap, M4.2); o `sourceSystem` do GAE é `"GeoAutoEngine"` e fica só como registro de origem.
- As amostras do GAE (`apps/geoautoengine/samples/geomap/`) são geradas pelo serializador e conferidas byte a byte contra os exemplos `valid-geoautoengine-*` do GeoMap na CI (GM-10).

## Estado por versão (16/11/2026)
| Versão | Novidade | GAE |
|---|---|---|
| 0.1 | Linhas | ✅ LWPOLYLINE, LINE, ARC e arcos (tesselação de 5 mm) |
| 0.2 | `network.color` | ✅ cor da layer |
| 0.3 | `Point`/`MultiPoint` | ✅ blocos, com atributos |

A ponte DXF provisória (Python) foi aposentada em 16/11/2026 (DEC-034 do GeoMap).
