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

## Decisões pendentes

| ID provisório | Decisão | Momento esperado |
|---|---|---|
| PEND-002 | Política de importação para coordenada Z: rejeitar ou descartar antes da normalização 2D | Issue #14 |
| PEND-003 | Estratégia de reimportação e idempotência | Issues #14 e #15 |
| PEND-004 | Provedor gratuito inicial do mapa base e seus termos | Issue #9 |
| PEND-005 | Metas quantitativas de latência e tamanho de tile | Issues #6 e #7 |
| PEND-006 | Formato físico inicial do contrato e pacote | Issue #14 |
| PEND-007 | Estratégia de hospedagem do protótipo | após fluxo local |
| PEND-008 | Primeiro dataset real e autorização de uso | Issue #17 |

Uma decisão pendente não deve ser resolvida silenciosamente. A Issue indicada deve registrar a conclusão neste documento.
