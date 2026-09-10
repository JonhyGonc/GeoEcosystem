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

## DEC-018 — Catálogo por projeto e visibilidade local das camadas

Status: aprovada.

As redes disponíveis serão obtidas por `GET /api/projects/{projectId}/networks`. O endpoint retorna somente `id`, `name` e `type`, filtra no banco pelo projeto e não conta nem lê `network_features`. Um projeto sem redes retorna uma lista vazia.

Cada rede recebe fonte e camada MapLibre próprias, com IDs derivados de seu UUID e cor simples determinística. Ligar ou desligar uma rede altera apenas a propriedade `visibility`; fontes não são recriadas durante zoom, pan ou alternância normal. O estado permanece apenas na sessão atual e não constitui preferência persistida.

`VITE_GEOMAP_PROJECT_ID` define o projeto atual. `VITE_GEOMAP_NETWORK_ID` passa a ser opcional e, quando pertence ao catálogo, indica a única camada inicialmente visível; sem ela, todas começam visíveis. A troca futura do projeto reutiliza o mesmo fluxo, removendo as camadas anteriores antes de instalar as novas.

## DEC-019 — Seleção local pela ordem visual do MapLibre

Status: aprovada.

O clique consulta somente as camadas de rede visíveis no ponto indicado e aceita apenas feições cujo `id` MVT seja um UUID válido. Quando houver sobreposição, a primeira feição retornada por `queryRenderedFeatures` é escolhida, acompanhando a ordem visual do MapLibre. Um clique sem feição válida limpa a seleção.

O destaque usa uma camada de linha adicional por rede sobre a mesma fonte MVT e filtra pelo `id` selecionado. A seleção fica no estado local da sessão, sobrevive a zoom e pan e é limpa quando sua rede é ocultada, o projeto muda ou o estilo é reinstalado. Nenhuma geometria é editada e nenhum atributo detalhado é carregado nesta etapa.

## DEC-020 — Atributos sob demanda e isolados pelo contexto completo

Status: aprovada.

Os atributos básicos de uma feição serão obtidos por `GET /api/projects/{projectId}/networks/{networkId}/features/{featureId}`. A consulta exige correspondência simultânea entre projeto, rede e feição; ausência ou combinação cruzada retorna HTTP 404 para não revelar a existência de dados fora do contexto solicitado.

A resposta inclui identificadores, nome e tipo da rede, metadados de origem, data de registro e o objeto JSONB `properties`. A geometria não é repetida e os tiles continuam contendo apenas `id`. O frontend consulta somente após a seleção, cancela requisições obsoletas e oferece visualização sem edição.

## DEC-021 — Contrato v0.1 em JSON estrito, 2D e limitado

Status: aprovada.

A importação inicial usa um documento JSON UTF-8 com extensão `.geomap.json`, media type `application/vnd.geomap.v0.1+json` e `contractVersion` exata `0.1`. O contrato aceita somente `LineString` e `MultiLineString`, exige CRS no formato EPSG e unidade declarada, e usa tipos neutros sem classes Autodesk.

Cada posição contém exatamente `[x, y]`. Coordenadas Z são rejeitadas, sem descarte implícito. O produtor só pode projetar para 2D antes da exportação quando essa perda for uma decisão explícita fora do contrato.

A versão inicial limita o documento a 50 MiB e 10.000 feições, além de limites por geometria e `properties`. Campos estruturais desconhecidos são rejeitados; apenas o conteúdo de `properties` é extensível. Versões futuras exigem validador próprio e não sofrem downgrade silencioso. A Issue #16 poderá adicionar transporte por streaming preservando o modelo lógico.

## DEC-022 — Reimportação como substituição atômica do snapshot da rede

Status: aprovada.

O arquivo `.geomap.json` v0.1 representa o estado completo de uma rede. O importador valida o documento integralmente antes de abrir a persistência e, em uma única transação, cria ou atualiza o projeto e a rede, substitui as feições anteriores e insere o novo snapshot. Um advisory lock transacional derivado do ID da rede impede duas substituições concorrentes da mesma rede.

Projeto e rede recebem UUIDs determinísticos derivados de `sourceSystem` e dos respectivos `externalId`. Feições com `sourceId` usam rede + `sourceType` + `sourceId`; feições anônimas usam sua posição no documento. Assim, a reimportação idêntica mantém IDs e quantidade, enquanto qualquer falha provoca rollback e preserva o snapshot anterior. A transformação espacial usa o EPSG declarado e as definições instaladas no PostGIS, armazenando sempre EPSG:4326.

## DEC-023 — NDJSON com staging temporário para importação escalável

Status: aprovada.

Grandes volumes usam o transporte UTF-8 `.geomap.ndjson`, media type `application/vnd.geomap.v0.1+ndjson`. A primeira linha contém os metadados do documento sem `features`; cada linha seguinte contém uma única feição do mesmo modelo lógico v0.1. Isso permite validação e leitura sequenciais sem materializar milhões de objetos no processo.

O tamanho padrão do lote é 10.000 feições, configurável entre 100 e 50.000. Cada lote recebe commit em uma tabela temporária exclusiva da sessão; nenhum desses commits altera a rede publicada. Após validar e carregar todas as linhas, uma transação final atualiza os contêineres e substitui o snapshot. Falha, duplicata ou cancelamento antes do commit final preserva integralmente a rede anterior. Um lock de sessão por rede impede concorrência entre importações durante todos os lotes.

## DEC-024 — Primeiro dataset real privado e ponte DXF descartável

Status: aprovada.

O primeiro dataset real é uma rede pluvial proveniente de um desenho Civil 3D
autorizado pelo fornecedor em 9 de setembro de 2026 para desenvolvimento e
validação privados. A origem declara SIRGAS 2000 / UTM zona 22S
(`EPSG:31982`) e metros. Arquivos DWG/DXF, o contrato gerado e coordenadas
precisas não serão versionados; somente resultados agregados e fixtures
sintéticas podem entrar no repositório.

Até o exportador da Issue #18, uma ponte provisória sem Autodesk converte DXF
ASCII para o contrato GeoMap v0.1. Ela considera somente o Model Space, aceita
`LINE` e `LWPOLYLINE`, preserva handles e elevações como atributos e exige
consentimento explícito para projetar geometria em 2D. Bulges, extrusões e
geometrias lineares não suportadas são rejeitados. A ponte é descartável e não
se torna uma segunda arquitetura de importação: sua saída passa integralmente
pelo validador e importador existentes.

## DEC-025 — Exportador de Polyline hospedado no GeoAutoEngine

Status: aprovada.

O exportador inicial do Civil 3D será um comando do aplicativo existente GeoAutoEngine, em vez de um segundo plugin Autodesk. Essa escolha preserva uma única instalação compatível com Civil 3D 2024, 2025 e 2026 e mantém o código dependente da Autodesk fora do GeoMap.

O comando `GEOAUTO3GEOMAPEXPORT` seleciona apenas `LWPOLYLINE`, lê o desenho sem modificá-lo e produz o contrato GeoMap v0.1. A primeira versão aceita Polylines abertas ou fechadas com segmentos retos; `Polyline2d`, `Polyline3d`, Feature Line, Pipe Network, Pressure Network e bulges permanecem fora do escopo. Tipos não suportados interrompem a exportação com diagnóstico, sem aproximação silenciosa.

Handle, layer, fechamento, quantidade de vértices e elevação são preservados. CRS e unidade são explícitos. Quando existe elevação, a geometria é projetada para X/Y pelo produtor e essa perda fica declarada em `projectedTo2D`, com `cadElevation` preservado em propriedades.

O GeoAutoEngine mantém implementação, build e instruções de instalação. O GeoMap mantém uma fixture sintética produzida pelo mesmo serializador e a executa pelo validador, importador, PostGIS, catálogo, detalhe de atributos e MVT. A integração entre repositórios continua sendo o contrato v0.1, sem referência binária ou classe Autodesk.

## Decisões pendentes

| ID provisório | Decisão | Momento esperado |
|---|---|---|
| PEND-007 | Estratégia de hospedagem do protótipo | após fluxo local |

Uma decisão pendente não deve ser resolvida silenciosamente. A Issue indicada deve registrar a conclusão neste documento.
