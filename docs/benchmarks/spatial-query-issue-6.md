# Planos da consulta espacial — Issue #6

Baseline observado na Backend CI nº 14, em GitHub Actions `ubuntu-24.04`, com PostgreSQL 16, PostGIS 3.5 e os datasets SMALL, MEDIUM e LARGE carregados simultaneamente.

## Consulta crítica

~~~sql
SELECT
    feature.id,
    feature.network_id,
    ST_AsGeoJSON(feature.geometry, 8)
FROM network_features feature
JOIN networks network ON network.id = feature.network_id
WHERE network.project_id = $1
  AND feature.network_id = $2
  AND feature.geometry && ST_MakeEnvelope(-48.90, -26.35, -48.85, -26.30, 4326)
  AND ST_Intersects(
      feature.geometry,
      ST_MakeEnvelope(-48.90, -26.35, -48.85, -26.30, 4326))
LIMIT 501;
~~~

O valor 501 implementa a leitura de `limit + 1`: a API devolve no máximo 500 feições e usa a feição excedente apenas para calcular `hasMore`.

## Resumo dos planos

| Dataset | Planejamento | Execução | Linhas na raiz | Blocos compartilhados hit/read | Forma principal do plano |
|---|---:|---:|---:|---:|---|
| SMALL | 8,485 ms | 8,436 ms | 64 | 8 / 462 | `Limit → Nested Loop → Bitmap Heap Scan → BitmapAnd` |
| MEDIUM | 0,493 ms | 9,582 ms | 501 | 458 / 106 | `Limit → Nested Loop → Bitmap Heap Scan → BitmapAnd` |
| LARGE | 0,403 ms | 32,808 ms | 501 | 496 / 317 | `Limit → Nested Loop → Gather → Bitmap Heap Scan` |

Índices observados:

| Dataset | Projeto | Rede | Geometria GiST |
|---|---|---|---|
| SMALL | `ix_networks_project_id` | `ix_network_features_network_id` | `ix_network_features_geometry` |
| MEDIUM | `ix_networks_project_id` | `ix_network_features_network_id` | `ix_network_features_geometry` |
| LARGE | `ix_networks_project_id` | filtro após candidatos espaciais | `ix_network_features_geometry` |

No LARGE, o bitmap do GiST produziu os candidatos espaciais e o plano aplicou o filtro de rede durante o `Bitmap Heap Scan`; o `LIMIT 501` encerrou a consulta sem materializar todas as interseções. A verificação automatizada falha quando o plano LARGE não contém `ix_network_features_geometry`.

## Reprodução

`tests/integration/verify-spatial-query-plans.sql` executa e imprime os planos completos com `ANALYZE`, `BUFFERS` e formato JSON. `tests/integration/benchmark-spatial-api.sh` aquece a rota e mede 30 chamadas HTTP sequenciais por dataset.

Os tempos são observações de um runner compartilhado, não metas de produção. Leituras de bloco e tempos de planejamento dependem do estado do cache; regressões devem ser comparadas em ambiente equivalente.
