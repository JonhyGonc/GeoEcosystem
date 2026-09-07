# Plano e baseline de Vector Tile — Issue #7

Medição observada na Backend CI nº 19, em GitHub Actions `ubuntu-24.04`, com PostgreSQL 16, PostGIS 3.5 e os datasets SMALL, MEDIUM e LARGE carregados simultaneamente.

## Cenário

- tile XYZ: `15/11938/18866`;
- camada: `network_features`;
- extent: 4.096;
- buffer: 64;
- recorte: habilitado;
- propriedade: somente `id`;
- amostra HTTP: 30 chamadas sequenciais após aquecimento.

## Resultados HTTP

| Dataset | Feições | Bytes | Mediana | p95 | Máxima |
|---|---:|---:|---:|---:|---:|
| SMALL | 5 | 324 | 2,742 ms | 5,162 ms | 5,326 ms |
| MEDIUM | 296 | 17.619 | 3,790 ms | 8,796 ms | 9,025 ms |
| LARGE | 2.907 | 171.200 | 20,690 ms | 32,423 ms | 33,024 ms |

## Plano crítico LARGE

O `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` apresentou:

| Medida | Resultado |
|---|---:|
| planejamento | 15,024 ms |
| execução | 14,024 ms |
| feições antes do agregado | 2.907 |
| linhas na raiz | 1 |
| blocos compartilhados hit/read na raiz | 332 / 213 |
| índice espacial | `ix_network_features_geometry` |
| índice de projeto | `ix_networks_project_id` |

Forma principal: `Aggregate → Nested Loop → Index Scan`. O `Index Scan` espacial limita candidatos ao envelope do tile com buffer; a transformação para EPSG:3857, o recorte e a codificação MVT ocorrem apenas sobre as feições da região e da rede solicitadas.

## Guardas

A CI rejeita, nesse cenário LARGE, tile acima de 512 KiB ou p95 acima de 100 ms. São guardas de regressão para hardware equivalente, não SLA de produção.

O plano JSON completo é emitido por `tests/integration/verify-vector-tile-plan.sql`. O payload é validado por `tests/integration/validate-mvt.py`, que lê a estrutura Protobuf sem dependências externas e verifica versão, camada, extent, geometrias LineString e UUIDs.
