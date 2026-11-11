# Dados do GeoAutoEngine gravados no DWG (CT-05)

Contrato **implícito**: outras versões do GAE leem o que versões anteriores gravaram, e o GeoMap usa o handle da entidade como `sourceId`. Fonte: auditoria GAE (Q3/Q4).

## Aplicações registradas (XData)
| RegApp | Entidades | Campos principais |
|---|---|---|
| `GEOAUTOENGINE` | geradas do fluxo CSV | `CODIGO`, `ORIGEM`, `REGISTRO`, `TIPO`, `SOURCE_FILE`, `SOURCE_LINE` |
| `GEOAUTOENGINE_VI` | anotações de vídeo-inspeção | `SCHEMA_VERSION`, `REPORT_ID`, `SECTION_ID`, `OCCURRENCE_ID`, `STABLE_KEY` |
| `GEOAUTOENGINE_VI_COVERAGE_V3` | coberturas (lê também a V2) | — |

## Dicionários (NOD)
| Dicionário | Chave | Conteúdo |
|---|---|---|
| `GEOAUTOENGINE_VIDEO_INSPECTION_ASSOCIATIONS` | `R_{reportId}_S_{sectionId}` | `CadSectionAssociation`: handle da Polyline (texto), direção, comprimento conhecido, `STORAGE_VERSION` |
| `GEOAUTOENGINE_VIDEO_INSPECTION_COVERAGE_V3` | — | coberturas |

## Pontos de atenção entre projetos
- O handle é gravado como **texto**: não é traduzido em WBLOCK, INSERT nem cópia entre DWGs.
- O BREAK de cobertura divide a Polyline da rede: a parte restante ganha handle novo, o que muda o `sourceId` no GeoMap (hipótese H-04).
- `REGISTRO` depende do caminho absoluto + SHA-256 do CSV (H-05).
