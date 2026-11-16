# Decisões entre projetos

Uma decisão por arquivo: `DEC-ECO-NNNN-<tema>.md`, com contexto, alternativas consideradas, decisão, impacto por projeto, data e quem aprovou. Decisões internas de um projeto ficam no repositório dele (por exemplo, `Geomap/docs/DECISIONS.md`).

## Índice
| ID | Tema | Estado | Data |
|---|---|---|---|
| [DEC-ECO-0001](DEC-ECO-0001-monorepo.md) | Repositório único (monorepo) no GeoEcosystem + regra 6 por pasta | Aprovada | 11/11/2026 |
| [DEC-ECO-0002](DEC-ECO-0002-versionamento-gadx.md) | Versionamento (`capabilities`) e semântica de estado completo do GADX | Aprovada (PR #13) | 16/11/2026 |
| [DEC-ECO-0003](DEC-ECO-0003-caminho-dwg-geomap-e-identidade.md) | Exportador do GAE como caminho oficial DWG → GeoMap; código próprio por feição | Aprovada pelo usuário (registro no PR #19) | 16/11/2026 |

## Decisões pendentes (identificadas na Etapa 2)
Detalhes em [`auditorias/2026-11-etapa1/ETAPA_2_CONSOLIDACAO.md`](../auditorias/2026-11-etapa1/ETAPA_2_CONSOLIDACAO.md#6-decisões-técnicas-a-arbitrar-insumos-da-etapa-4-nenhuma-tomada-aqui).

| ID | Tema |
|---|---|
| D-01 | Linha de base de cada projeto (GAD `main` × fix; ordem de merge das branches do GAE) |
| D-02 | → DEC-ECO-0002 |
| D-03 | → DEC-ECO-0003 |
| D-04 | → DEC-ECO-0003 |
| D-05 | Rastreabilidade e histórico mínimos (origem, esquema, versão de build) |
| D-06 | Testes cruzados de contrato |
| D-07 | Escopo da integração GAD ↔ GeoMap — requisito definido em 11/11/2026: anomalias no mapa (ver M5 do roadmap) |
| D-08 | → DEC-ECO-0001 |
