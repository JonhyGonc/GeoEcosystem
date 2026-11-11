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

### PERF-010 — Guarda inicial de regressão MVT

No cenário reproduzível LARGE em zoom 15, o tile deve permanecer abaixo de 512 KiB e o p95 de 30 chamadas HTTP aquecidas deve permanecer abaixo de 100 ms em runner equivalente ao baseline. Esses valores são guardas de regressão do protótipo, não SLA de produção.

### PERF-011 — Limites do contrato inicial

Um documento `.geomap.json` aceita no máximo 50 MiB e 10.000 feições, nas versões 0.1, 0.2 e 0.3. Cada feição aceita até 100.000 vértices e 64 KiB de `properties`. Esses limites protegem o importador inicial; MEDIUM e LARGE usam `.geomap.ndjson` sequencial.

### PERF-012 — Importação inicial limitada e transacional

O importador de JSON carrega somente um documento já limitado por PERF-011. A persistência usa `COPY` binário para uma tabela temporária e uma transformação/inserção SQL em conjunto, sem comando individual por feição. O teste de integração mede a importação SMALL de 1.000 feições e confirma a reimportação atômica.

No baseline da CI da Issue #15, um documento de 265.218 bytes com 1.000 `LineString` em EPSG:31982 foi validado, transformado para EPSG:4326 e persistido em 3.616 ms medidos pelo importador. O processo iniciado a frio por `dotnet run` levou 9,79 s e atingiu 127.556 KiB de memória residente máxima no runner Ubuntu hospedado. Esses valores registram a primeira referência reproduzível do protótipo e não constituem SLA.

### PERF-013 — NDJSON com memória independente do total de feições

O transporte `.geomap.ndjson` lê blocos de 64 KiB, limita cada registro a 16 MiB e mantém em memória somente o registro atual e o lote configurado. A tabela temporária recebe os lotes por `COPY`; o tamanho total do arquivo não é materializado no processo. A CI gera, importa e mede MEDIUM com 100.000 feições e LARGE com 1.000.000, verificando contagem, geometria, propriedades, reimportação e falha após um lote já confirmado no staging.

No baseline verde da CI da Issue #16:

| Perfil | Feições | Arquivo | Lote | Tempo total | Memória residente máxima |
|---|---:|---:|---:|---:|---:|
| MEDIUM | 100.000 | 27.766.036 bytes | 10.000 | 7,35 s | 123.608 KiB |
| LARGE | 1.000.000 | 275.666.232 bytes | 25.000 | 50,26 s | 123.432 KiB |

O crescimento de dez vezes na quantidade e no arquivo não aumentou o pico observado do processo, que permaneceu em aproximadamente 121 MiB. Isso confirma o objetivo de memória independente do total de feições para esse cenário. Os tempos incluem inicialização de `dotnet run --no-build`, validação, `COPY`, transformação e commit final no runner Ubuntu hospedado; são baseline reproduzível do protótipo, não SLA.

Os comandos que contam, removem e inserem o snapshot completo da rede usam timeout explícito de 600 s (`PostgresGeoMapImportStore.SnapshotCommandTimeoutSeconds`), aplicado tanto ao JSON quanto ao NDJSON. Antes disso eles herdavam o padrão de 30 s do Npgsql, e o `INSERT … SELECT` final do LARGE, com transformação de CRS e manutenção do índice GiST, ficava perto desse limite. Em 10 de setembro de 2026 a CI da `main` falhou com código 3 nessa etapa, embora o mesmo commit tivesse passado no Pull Request. O valor continua finito para que um bloqueio não prenda a importação indefinidamente. Se o importador falhar, a CI imprime o resumo JSON, o progresso e o log do banco.

## Extensão e foco do projeto (Issue #47)

`GET /api/projects/{id}/extent` foi medido na Backend CI com 30 amostras por dataset, com os três datasets carregados na mesma base:

| Dataset | Mediana | p95 | Máximo |
|---|---:|---:|---:|
| SMALL (1.000 feições) | 127,7 ms | 131,7 ms | 166,6 ms |
| MEDIUM (100.000) | 139,1 ms | 144,3 ms | 180,4 ms |
| LARGE (1.000.000) | 253,2 ms | 284,1 ms | 296,8 ms |

O cálculo sob demanda atende ao uso, que é um clique ou uma troca de projeto, e dispensa guardar a extensão na importação (DEC-027). O SMALL não é proporcionalmente mais rápido porque os três datasets ocupam a mesma área: a busca do foco por vizinho mais próximo percorre feições de outros projetos antes de encontrar as do projeto pedido. A CI falha se o p95 de qualquer dataset passar de 2 s.

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

As medições das Issues #6 e #7 formam o primeiro baseline. A guarda quantitativa inicial de MVT foi definida em PERF-010; ela deve ser revista quando houver frontend e rede CAD real, sempre com nova medição registrada.

## Nota sobre os baselines registrados

Os baselines das Issues #5 a #7 e de PERF-012/PERF-013 foram medidos com .NET 8, antes da migração para o .NET 10 (DEC-003, Issue #38), e não foram medidos de novo depois dela. As guardas automáticas (payload e p95 do MVT, p95 da extensão, contagens do LARGE) continuam rodando a cada execução da Backend CI já em .NET 10; os números das tabelas abaixo são registro histórico.

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

Os valores representam um runner compartilhado e não constituem SLA. A amostra HTTP foi aquecida; o `EXPLAIN` também registra leituras de blocos que variam conforme o estado do cache. Memória por consulta não foi isolada neste baseline, mas o volume de resposta ficou limitado a 500 geometrias. Tamanho e latência de tile foram medidos separadamente na Issue #7.

## Baseline de Vector Tiles — Issue #7

Ambiente: GitHub Actions `ubuntu-24.04`, Backend CI nº 19, PostgreSQL 16 com PostGIS 3.5 em Docker Compose e API .NET 8. Os três datasets estavam carregados simultaneamente. A medição usou o tile XYZ `15/11938/18866`, extent 4.096, buffer 64 e somente a propriedade `id`.

Após uma chamada de aquecimento e validação do MVT, cada perfil recebeu 30 chamadas HTTP sequenciais:

| Dataset | Volume da rede | Feições no tile | Tamanho | Mediana HTTP | p95 HTTP | Máxima HTTP |
|---|---:|---:|---:|---:|---:|---:|
| SMALL | 1.000 | 5 | 324 bytes | 2,742 ms | 5,162 ms | 5,326 ms |
| MEDIUM | 100.000 | 296 | 17.619 bytes | 3,790 ms | 8,796 ms | 9,025 ms |
| LARGE | 1.000.000 | 2.907 | 171.200 bytes | 20,690 ms | 32,423 ms | 33,024 ms |

No LARGE, `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` registrou 15,024 ms de planejamento e 14,024 ms de execução. O plano usou `ix_network_features_geometry`, processou 2.907 feições candidatas e produziu um único agregado MVT. O resumo versionado está em [benchmarks/vector-tile-issue-7.md](benchmarks/vector-tile-issue-7.md); o plano JSON completo permanece no log da CI nº 19.

O payload LARGE observado equivale a aproximadamente 167,2 KiB. A CI aplica uma guarda de regressão de 512 KiB e p95 de 100 ms para esse cenário. Os limites deixam margem para variação de runner e crescimento moderado sem transformar o baseline em SLA.

O leitor de teste confirmou MVT versão 2, camada `network_features`, extent 4.096, geometrias lineares válidas e identificadores UUID. Memória por tile não foi isolada. Redes CAD reais podem possuir geometrias muito mais complexas que as linhas sintéticas de dois vértices e deverão ser medidas na Issue #17.

## Validação MapLibre com LARGE — Issue #10

A integração do frontend preserva as guardas do endpoint MVT: fonte vetorial limitada aos zooms 14–22, template regional z/x/y e detecção baseada somente nas feições renderizadas no viewport. Testes de contrato verificam a URL exata, a source-layer `network_features`, mudanças de estado em pan/zoom, região vazia, erro isolado e a ausência de qualquer rota de carga integral.

A Backend CI continua sendo a validação integrada com volume: carrega simultaneamente SMALL, MEDIUM e LARGE, incluindo 1.000.000 de feições na rede LARGE, chama o endpoint MVT real e verifica formato, plano espacial, payload máximo de 512 KiB e p95 máximo de 100 ms. A Frontend CI compila a configuração com os identificadores determinísticos dessa rede, ligando o contrato MapLibre ao mesmo cenário. Os números observados mais recentes permanecem no baseline da Issue #7; esta Issue não altera consulta, índice, payload ou limites do backend.

## Validação de múltiplas camadas — Issue #11

O catálogo de redes consulta somente a tabela `networks`, filtrada por `project_id` e coberta pelo índice `ix_networks_project_id`; ele não lê geometrias, atributos JSONB ou contagens de feições. Cada camada continua usando o endpoint MVT regional e as mesmas guardas de zoom, payload e latência.

Alternar visibilidade usa a propriedade de layout do MapLibre e não remove nem recria fontes. Assim, pan e zoom preservam a seleção e tiles já obtidos podem permanecer no cache interno. Somente camadas visíveis participam da renderização; projetos com muitas redes oferecem ações combinadas para reduzir o conjunto ativo.

Os testes do frontend verificam alternância individual e combinada, projeto trocado, rede vazia, falha isolada e ausência de carga integral. A Backend CI mantém a carga LARGE de 1.000.000 de feições e as guardas quantitativas do tile; esta Issue não altera a consulta MVT nem seus limites.

## Validação de seleção — Issue #12

A identificação acontece somente em resposta ao clique, por `queryRenderedFeatures` nas camadas visíveis e sobre os tiles que o MapLibre já possui no viewport. O destaque reutiliza a fonte MVT da rede e muda apenas um filtro por UUID; não há nova requisição, duplicação de fonte, leitura de JSONB ou carga integral.

Os testes do frontend cobrem seleção, troca, limpeza, clique sem feição, sobreposição pela ordem visual, ocultação da rede selecionada e remoção segura das camadas. A Backend CI continua validando o dataset LARGE com 1.000.000 de feições, payload máximo de 512 KiB e p95 máximo de 100 ms. Esta Issue não altera SQL, índice, conteúdo MVT ou limites do backend.

## Validação de atributos sob demanda — Issue #13

O detalhe consulta uma única feição por sua chave primária e confirma a rede e o projeto por junção relacional. A geometria não é lida nem serializada, o JSONB não é repetido nos tiles e a chamada ocorre somente após uma seleção válida. Trocar ou limpar a seleção cancela a requisição anterior no navegador.

A integração verifica resposta existente, propriedades vazias e variáveis, HTTP 404 para ausência e isolamento entre projetos e redes. A Backend CI também consulta uma feição da rede LARGE enquanto 1.000.000 de feições estão carregadas, além de preservar as guardas quantitativas do MVT. Não foi introduzida rota para listar atributos ou carregar a rede completa.

## Validação do contrato — Issue #14

O contrato inicial impede documentos JSON sem limite: 50 MiB, 10.000 feições, 100.000 vértices por feição e limites próprios para `properties`. O transporte NDJSON da Issue #16 preserva as validações por registro e permite LARGE sem carga integral em memória.

O validador percorre o documento uma vez após o parse, não realiza acesso ao banco e devolve códigos e caminhos estáveis. Os testes cobrem exemplos válidos de LineString/MultiLineString, versão e CRS inválidos, Z, geometria não suportada, limite de propriedades, compatibilidade estrita e identidade de origem duplicada. O modo NDJSON valida metadados e uma feição por vez com as mesmas regras.

## Validação CAD real — Issue #17

O desenho autorizado possui 5,2 MiB em DXF ASCII. A camada pluvial selecionada
no Model Space resultou em 15 `LWPOLYLINE`, 73 vértices e aproximadamente
1.143,60 m de comprimento projetado. A ponte descartou 16 cópias de Paper Space
e ignorou 58 textos da mesma camada, sem incluí-los como feições. O contrato
privado gerado possui 12.499 bytes.

Em uma execução local isolada, leitura, validação estrutural do DXF e conversão
consumiram 63,917 ms e o processo Python registrou RSS máximo de 41.888 KiB.
Esses números são apenas referência da rede real pequena, não SLA. A mudança não
altera consulta MVT, payload, índices ou os baselines SMALL/MEDIUM/LARGE. A CI
usa uma fixture DXF sintética para verificar conversão, importação, transformação
`EPSG:31982` → `EPSG:4326` e atributos sem expor o dataset real.

## Validação do exportador Civil 3D — Issue #18

O exportador reutiliza os limites do documento GeoMap v0.1: até 10.000 Polylines por arquivo e 100.000 posições por feição. A leitura do DWG é somente leitura e a saída JSON é validada integralmente antes de persistir. Volumes acima desse limite exigirão um exportador NDJSON futuro; não há divisão silenciosa do snapshot nesta versão.

A fixture de referência do GeoAutoEngine contém uma `Polyline` de dois vértices em `EPSG:31982`. A CI confirma o contrato, a transformação para `EPSG:4326`, os metadados Autodesk neutralizados em JSON, o catálogo da rede, os atributos sob demanda e a geração do MVT no tile que contém a geometria. Esse teste pequeno comprova compatibilidade funcional e não altera os baselines SMALL, MEDIUM e LARGE nem suas guardas de memória, latência ou payload.

## Antipadrões proibidos

- endpoint all-network-features para visualização;
- GeoJSON completo de uma rede grande no carregamento inicial;
- filtro espacial feito apenas em memória na API;
- índice espacial criado sem verificar o plano;
- propriedade pesada repetida em cada tile;
- cache distribuído introduzido antes de medir a necessidade.
