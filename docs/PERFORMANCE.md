# Requisitos de performance

Performance é requisito arquitetural do GeoMap. O sistema deve continuar utilizável com redes extensas e muitos elementos sem enviar o conjunto inteiro ao navegador.

## Requisitos

### PERF-001 — Sem carga integral

Nenhum endpoint de mapa deve carregar toda uma rede grande por padrão.

### PERF-002 — Índices espaciais

Consultas geográficas devem utilizar índices espaciais quando adequado. Consultas críticas devem ser verificadas com EXPLAIN (ANALYZE, BUFFERS) em ambiente de teste.

### PERF-003 — Região necessária

A visualização deve consultar somente a região e o nível de zoom necessários, por bounding box ou envelope de tile.

### PERF-004 — Datasets

O projeto deve possuir datasets reproduzíveis:

| Dataset | Ordem de grandeza | Uso |
|---|---:|---|
| SMALL | cerca de 1.000 feições | desenvolvimento rápido |
| MEDIUM | cerca de 100.000 feições | integração e regressão |
| LARGE | cerca de 1.000.000 de feições | validação arquitetural |

Os números podem ser ajustados com justificativa registrada.

### PERF-005 — Importação controlada

Importações grandes não devem depender desnecessariamente de carregar todos os dados em memória. Usar lotes, streaming e transações com tamanho controlado.

### PERF-006 — Regressão com LARGE

Alterações relacionadas a mapa, consultas, tiles ou importação devem ser testadas também com o dataset LARGE quando aplicável.

### PERF-007 — Conteúdo do tile

Tiles devem carregar somente atributos necessários para renderização, filtragem e identificação. Atributos detalhados podem ser buscados sob demanda.

### PERF-008 — Níveis de detalhe

Simplificação, recorte e visibilidade por zoom podem ser aplicados quando preservarem a leitura correta da rede.

### PERF-009 — Limites explícitos

Endpoints e importadores devem possuir limites documentados de tamanho, timeout e quantidade quando implementados.

## Práticas de consulta

- filtrar por projeto e rede antes ou junto do filtro espacial;
- usar operador compatível com GiST para reduzir candidatos;
- recortar a geometria ao envelope do tile;
- transformar CRS de forma planejada;
- evitar transformação integral da tabela em tempo de consulta quando isso impedir uso de índice;
- evitar incluir JSONB completo em cada tile;
- medir antes de adicionar cache ou infraestrutura.

## Critérios iniciais de medição

A Issue #4 deve definir geração reproduzível e sementes fixas. As Issues #6 e #7 devem registrar, para SMALL, MEDIUM e LARGE:

- ambiente do teste;
- volume e extensão;
- plano de consulta;
- latência mediana e percentil alto;
- tamanho do tile;
- memória observada;
- limitações encontradas.

Metas numéricas serão definidas após o primeiro baseline, para evitar números arbitrários. A ausência de meta inicial não autoriza regressões sem medição.

## Baseline de carga LARGE — Issue #5

Ambiente: GitHub Actions `ubuntu-latest`, Backend CI nº 10, PostgreSQL 16 com PostGIS 3.5 em Docker Compose e gerador .NET 8 executado no host do runner.

| Medida | Resultado observado |
|---|---:|
| LARGE carregado | 1.000.000 feições |
| Tamanho do lote | 10.000 feições |
| Tempo total informado pelo gerador | 35,937 s |
| Pico do working set do processo .NET | 80,9 MiB |
| RSS máximo independente | aproximadamente 120,4 MiB |
| Heap gerenciado ao final | 3,8 MiB |
| Total simultâneo SMALL + MEDIUM + LARGE | 1.101.000 feições |
| Tamanho de `network_features` com índices | aproximadamente 604,4 MiB |

A carga foi gerada sob demanda, enviada por `COPY` binário a uma tabela temporária limitada ao lote e confirmada em uma transação atômica. Após `ANALYZE network_features`, o banco estimou 1.100.990 linhas; estatísticas de `network_id` e `geometry`, o índice GiST espacial e o índice B-tree relacional foram verificados.

Este baseline mede geração e carga artificial local no runner. Não mede consultas, tiles, tráfego de produção nem complexidade de uma rede CAD real. Detalhes, extensão efetiva e comandos estão em [tools/importer/README.md](../tools/importer/README.md).

## Baseline de consulta espacial — Issue #6

Ambiente: GitHub Actions `ubuntu-24.04`, Backend CI nº 14, PostgreSQL 16 com PostGIS 3.5 em Docker Compose e API .NET 8. Os três datasets foram carregados simultaneamente, totalizando 1.101.000 feições dentro da extensão sintética `[-48.95, -26.40, -48.75, -26.20]` em EPSG:4326.

A medição consultou a bounding box `[-48.90, -26.35, -48.85, -26.30]`, com `limit=500`. Após uma chamada de aquecimento, cada perfil recebeu 30 chamadas HTTP sequenciais:

| Dataset | Volume da rede | Retornadas | `hasMore` | Mediana HTTP | p95 HTTP | Máxima HTTP |
|---|---:|---:|---:|---:|---:|---:|
| SMALL | 1.000 | 64 | `false` | 5,555 ms | 5,939 ms | 6,229 ms |
| MEDIUM | 100.000 | 500 | `true` | 9,471 ms | 12,801 ms | 15,175 ms |
| LARGE | 1.000.000 | 500 | `true` | 20,658 ms | 36,550 ms | 37,067 ms |

`EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` confirmou `ix_network_features_geometry` nos três perfis. O resumo versionado dos planos e a consulta medida estão em [benchmarks/spatial-query-issue-6.md](benchmarks/spatial-query-issue-6.md); os planos JSON completos permanecem no log da CI nº 14.

Os valores representam um runner compartilhado e não constituem SLA. A amostra HTTP foi aquecida; o `EXPLAIN` também registra leituras de blocos que variam conforme o estado do cache. Memória por consulta não foi isolada neste baseline, mas o volume de resposta ficou limitado a 500 geometrias. Tamanho e latência de tile serão medidos na Issue #7.

## Antipadrões proibidos

- endpoint all-network-features para visualização;
- GeoJSON completo de uma rede grande no carregamento inicial;
- filtro espacial feito apenas em memória na API;
- índice espacial criado sem verificar o plano;
- propriedade pesada repetida em cada tile;
- cache distribuído introduzido antes de medir a necessidade.
