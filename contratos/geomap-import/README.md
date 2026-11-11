# GeoMap Import Contract (CT-01)

**A especificação canônica fica no GeoMap**, e não aqui: o validador e a CI do GeoMap leem os exemplos do próprio repositório (decisão E3-1).

- Especificação: [`Geomap/docs/IMPORT_CONTRACT.md`](https://github.com/JonhyGonc/Geomap/blob/main/docs/IMPORT_CONTRACT.md)
- Exemplos: [`Geomap/contracts/geomap/`](https://github.com/JonhyGonc/Geomap/tree/main/contracts/geomap)
- Validador: `Geomap/tools/importer/GeoMap.Contract/GeoMapContractValidator.cs`
- Exportador GAE: `GeoAutoEngine/src/GeoAutoEngine.Application/GeoMapExport/GeoMapExportSerializer.cs`

## Regras entre projetos (vigentes)
- Mudança de versão ou de regra que afete produtores exige Issue `mudanca-de-contrato` aqui e aprovação do Chat 00.
- O UUID do projeto no GeoMap depende de `sourceSystem`. Hoje a ponte usa `"Civil 3D DXF"` e o GAE `"Civil 3D"`: **mudar esse valor muda os IDs** (GM-05). Não alterar sem decisão (D-03/D-04).

## Estado por versão (10/11/2026)
| Versão | Novidade | Ponte DXF | GAE |
|---|---|---|---|
| 0.1 | Linhas | ✅ | ✅ (só LWPOLYLINE reta) |
| 0.2 | `network.color` | ✅ | ❌ |
| 0.3 | `Point`/`MultiPoint` | ✅ | ❌ |
