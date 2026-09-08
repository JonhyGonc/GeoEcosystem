# Registro de decisões

As decisões aprovadas são vinculantes até serem substituídas por nova decisão documentada.

## DEC-001 — PostgreSQL + PostGIS

Status: aprovada.

PostgreSQL com PostGIS será o armazenamento espacial. Motivo: consultas geográficas, índices espaciais, transformações de CRS e geração eficiente de dados para mapa.

## DEC-002 — MapLibre GL JS

Status: aprovada.

MapLibre será o visualizador web. Evita dependência obrigatória de um provedor comercial e suporta tiles vetoriais.

## DEC-003 — ASP.NET Core / .NET 8

Status: aprovada.

O backend inicial será ASP.NET Core em C# e .NET 8, mantendo alinhamento com a experiência e os projetos existentes da equipe.

## DEC-004 — React + TypeScript

Status: aprovada.

O frontend inicial usará React e TypeScript.

## DEC-005 — Geometria criada no Civil 3D

Status: aprovada.

O GeoMap web é visualizador e não editor CAD. Desenho, movimento e edição de vértices permanecem no Civil 3D.

## DEC-006 — Entrega espacialmente segmentada

Status: aprovada.

Redes grandes serão entregues por região e zoom, preferencialmente em MVT. Não haverá carga integral por padrão.

## DEC-007 — Mapa base desacoplado

Status: aprovada.

A fonte de mapa base será configurável. Google Maps, MapTiler pago ou qualquer fornecedor específico não será requisito arquitetural.

## DEC-008 — Modelo GeoMap normalizado

Status: aprovada.

Banco, backend e frontend não dependem de tipos internos do Civil 3D. Um exportador converte diferentes objetos para o contrato GeoMap.

## DEC-009 — Monólito modular para o MVP

Status: aprovada.

O MVP usará frontend, backend, banco e ferramentas claramente separados, sem microserviços ou infraestrutura distribuída.

## DEC-010 — Provar o fluxo web antes do exportador

Status: aprovada.

Primeiro será validado PostGIS → API → MVT → MapLibre com dados artificiais grandes. O exportador Civil 3D será implementado depois.

## DEC-011 — EPSG:4326 como CRS canônico de armazenamento

Status: aprovada.

As geometrias normalizadas serão armazenadas em EPSG:4326. Toda importação deverá declarar o CRS de origem e transformá-lo para o CRS canônico. Para tiles web, o envelope será transformado de EPSG:3857 para EPSG:4326 antes do filtro espacial, permitindo que o índice GiST da coluna armazenada seja utilizado; a geometria resultante poderá então ser transformada para EPSG:3857.

Cálculos de distância ou comprimento não devem tratar graus como metros. Quando necessários, deverão usar CRS projetado adequado ou estratégia geodésica documentada.

## DEC-012 — Migrations SQL versionadas no protótipo

Status: aprovada.

O modelo físico será evoluído por scripts SQL up/down numerados em database/migrations e executados com psql usando ON_ERROR_STOP. Esta escolha mantém o protótipo simples, torna PostGIS e índices explícitos e evita acoplar a evolução inicial do banco a um ORM. Uma ferramenta de migration poderá substituir esse processo futuramente mediante decisão registrada.

## DEC-013 — Consultas espaciais obrigatoriamente regionais e limitadas

Status: aprovada.

A consulta de feições exige projeto, rede e bounding box em EPSG:4326. O envelope deve possuir no máximo 1 grau de longitude por 1 grau de latitude. A resposta retorna 500 feições por padrão e no máximo 2.000, usando `hasMore` para indicar que a região deve ser reduzida. O comando no banco possui timeout de 5 segundos.

O endpoint retorna apenas identificadores e geometria necessários para validar a consulta regional. Não fornece paginação para percorrer uma rede inteira nem inclui o JSONB detalhado. MVT e consulta de atributos permanecem nas Issues #7 e #13.

## DEC-014 — Tiles vetoriais MVT recortados e com conteúdo mínimo

Status: aprovada.

As redes serão entregues por `GET /api/projects/{projectId}/networks/{networkId}/tiles/{z}/{x}/{y}.mvt`, em uma camada `network_features`. O PostGIS converte o envelope XYZ para Web Mercator, filtra candidatos no CRS canônico EPSG:4326 com o índice GiST e produz o tile com `ST_AsMVTGeom` e `ST_AsMVT`.

O protótipo aceita zoom de 14 a 22, extensão MVT 4.096, buffer 64, recorte habilitado e timeout de banco de 5 segundos. Cada feição carrega apenas `id`; os atributos detalhados permanecem em consulta separada. Tiles vazios retornam HTTP 204 e respostas válidas recebem `Cache-Control: public, max-age=60`. Não haverá Redis ou CDN nesta etapa.

O zoom mínimo evita agregar uma rede extensa em um tile de área excessiva. Esse limite poderá ser revisto quando existirem níveis de detalhe ou camadas generalizadas medidos, sem criar fallback de carga integral.

## DEC-015 — Guardas quantitativas iniciais para MVT

Status: aprovada.

Com base no baseline da Issue #7, o cenário LARGE no tile de referência em zoom 15 deve manter payload de até 512 KiB e p95 HTTP de até 100 ms em 30 chamadas aquecidas, executadas em ambiente equivalente à CI. A verificação automatizada falha ao ultrapassar esses valores.

As guardas detectam regressões grosseiras e não constituem SLA de produção. O baseline observado foi 171.200 bytes e p95 de 32,423 ms, deixando margem para variação de runner e evolução moderada. Metas de experiência do mapa completo serão revistas após o frontend e uma rede CAD real.

## DEC-016 — OpenStreetMap Standard como mapa base inicial

Status: aprovada.

O Protótipo 0.1 usará inicialmente os tiles raster XYZ do OpenStreetMap Standard em `https://tile.openstreetmap.org/{z}/{x}/{y}.png`, sem chave ou assinatura. A atribuição visível `© OpenStreetMap contributors`, vinculada à página de copyright, é obrigatória.

A escolha vale apenas para desenvolvimento e demonstração interna de baixo tráfego. O serviço público é oferecido sem SLA, exige respeito ao cache e não permite download em massa, varredura, prefetch ou uso offline. Uma implantação pública ou de alto tráfego deverá usar provedor compatível ou fonte própria após nova avaliação dos termos.

URL, nome, tamanho de tile, limites de zoom e atribuição ficam centralizados em configuração validada. Fontes personalizadas exigem atribuição explícita. IDs `basemap-*` são reservados ao fundo e `geomap-*` às redes, permitindo trocar apenas a fonte base sem remover camadas GeoMap.

Somente raster XYZ está implementado. WMS, WMTS, ortofoto própria e COG permanecem extensões previstas, condicionadas a necessidade, licença e medições futuras.

Referências: [Tile Usage Policy](https://operations.osmfoundation.org/policies/tiles/), [Copyright e licença do OpenStreetMap](https://www.openstreetmap.org/copyright) e [fonte raster no MapLibre](https://maplibre.org/maplibre-gl-js/docs/examples/add-a-raster-tile-source/).

## DEC-017 — Uma rede MVT configurada como primeira integração MapLibre

Status: aprovada.

A primeira integração completa PostGIS → API → MVT → MapLibre exibirá uma rede identificada por `VITE_GEOMAP_PROJECT_ID` e `VITE_GEOMAP_NETWORK_ID`. A fonte vetorial `geomap-network` usa diretamente o endpoint regional de tiles, a source-layer `network_features` e zoom de 14 a 22; a camada de linhas `geomap-network-lines` referencia essa fonte.

O MapLibre decide os tiles necessários a cada viewport, pan e zoom. Não há carregamento integral, prefetch ou fallback pelo endpoint de feições. Estados de carregamento, região vazia e falha são derivados dos eventos da fonte, e uma falha da rede não remove nem bloqueia o mapa base.

Esta etapa limita deliberadamente a interface a uma rede configurada. Seleção de projetos, múltiplas redes e controles completos de camadas pertencem às Issues posteriores.

## Decisões pendentes

| ID provisório | Decisão | Momento esperado |
|---|---|---|
| PEND-002 | Política de importação para coordenada Z: rejeitar ou descartar antes da normalização 2D | Issue #14 |
| PEND-003 | Estratégia de reimportação e idempotência | Issues #14 e #15 |
| PEND-006 | Formato físico inicial do contrato e pacote | Issue #14 |
| PEND-007 | Estratégia de hospedagem do protótipo | após fluxo local |
| PEND-008 | Primeiro dataset real e autorização de uso | Issue #17 |

Uma decisão pendente não deve ser resolvida silenciosamente. A Issue indicada deve registrar a conclusão neste documento.
