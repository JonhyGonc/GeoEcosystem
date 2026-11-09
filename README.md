# GeoMap

Prova de conceito para armazenar e visualizar, em um mapa web, redes de tubulação 2D originadas no AutoCAD Civil 3D.

## Objetivo atual

O Protótipo 0.1 deve demonstrar que uma rede proveniente do CAD pode ser normalizada, armazenada em PostgreSQL/PostGIS e exibida corretamente no navegador sobre um mapa de fundo.

O site é um visualizador geográfico. A criação e a edição das tubulações permanecem no Civil 3D.

## Arquitetura aprovada

- Frontend: React, TypeScript e MapLibre GL JS.
- Backend: ASP.NET Core, .NET 10 (LTS) e C#.
- Banco: PostgreSQL e PostGIS.
- Desenvolvimento local: Docker Compose.
- Entrega das redes ao mapa: Vector Tiles (MVT).
- Mapa base: fonte configurável e desacoplada de um fornecedor específico.

O primeiro fluxo técnico a ser provado é:

PostGIS → API → MVT → MapLibre

O exportador inicial de Polyline é fornecido pelo aplicativo GeoAutoEngine depois da comprovação do fluxo geográfico web com volume alto.

## Desenvolvimento local

### Pré-requisitos

- Git;
- Docker Desktop com Docker Compose;
- .NET 10 SDK, apenas para executar build e testes fora do Docker;
- Node.js 24, para executar o frontend fora do Docker.

### Iniciar banco e API

No PowerShell:

~~~powershell
Copy-Item .env.example .env
docker compose up --build
~~~

No Bash:

~~~bash
cp .env.example .env
docker compose up --build
~~~

A API ficará disponível em http://localhost:8080 e o health check em http://localhost:8080/health.

O arquivo .env é ignorado pelo Git. Troque a senha de exemplo antes de usar e nunca versione credenciais reais.

### Verificar o PostGIS

~~~bash
docker compose exec database sh -ec \
  'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT PostGIS_Version();"'
~~~

### Aplicar o modelo espacial

Bash:

~~~bash
docker compose exec -T database sh -ec \
  'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' \
  < database/migrations/001_initial_spatial_model.up.sql
~~~

PowerShell:

~~~powershell
Get-Content -Raw database/migrations/001_initial_spatial_model.up.sql |
  docker compose exec -T database sh -ec 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
~~~

Consulte [database/migrations/README.md](database/migrations/README.md) antes de executar um rollback, pois a reversão remove as tabelas e seus dados.

### Build e testes do backend

~~~bash
dotnet restore GeoMap.sln
dotnet build GeoMap.sln --configuration Release --no-restore
dotnet test GeoMap.sln --configuration Release --no-build
~~~

### Executar e verificar o frontend

~~~bash
cd apps/web
cp .env.example .env
npm ci
npm run dev
~~~

Abra http://localhost:5173 para navegar pelo mapa com zoom, arraste e controles visuais. O padrão usa OpenStreetMap Standard sem chave e mantém sua atribuição visível. Consulte [apps/web/README.md](apps/web/README.md) para trocar a fonte e conhecer os limites de uso.

O projeto é escolhido no seletor do cabeçalho, que lista os projetos importados. `VITE_GEOMAP_PROJECT_ID` é opcional e apenas indica o projeto aberto na inicialização; sem ela, abre o primeiro da lista. `VITE_GEOMAP_NETWORK_ID` também é opcional e escolhe a camada inicialmente visível desse projeto; sem ela, todas as redes começam ligadas. O frontend solicita somente os tiles MVT da região visível. Consulte [apps/web/README.md](apps/web/README.md) para obter os IDs e ajustar a URL da API.

Antes de enviar uma alteração do frontend:

~~~bash
npm run lint
npm run test
npm run build
~~~

### Gerar dados espaciais de teste

Após aplicar a migration, o gerador carrega dados artificiais reproduzíveis diretamente no PostGIS:

~~~bash
export GEOMAP_DB_CONNECTION_STRING='Host=localhost;Port=5432;Database=geomap;Username=geomap;Password=change-me-local-only'

dotnet run --project tools/importer/GeoMap.DatasetGenerator -- \
  --size small \
  --seed 20260906
~~~

Os perfis disponíveis são SMALL, com 1.000 feições, MEDIUM, com 100.000, e LARGE, com 1.000.000. Consulte [tools/importer/README.md](tools/importer/README.md) para uso no PowerShell, tamanho de lote, extensão espacial e medições.

### Importar um contrato GeoMap v0.1

Com o banco iniciado e a migration aplicada, defina a conexão e informe um arquivo validado:

~~~bash
export GEOMAP_DB_CONNECTION_STRING='Host=localhost;Port=5432;Database=geomap;Username=geomap;Password=change-me-local-only'

dotnet run --project tools/importer/GeoMap.Importer -- \
  --file contracts/geomap/0.1/examples/valid-linestring.geomap.json
~~~

O comando valida o documento inteiro antes de gravar, transforma o CRS declarado para EPSG:4326 e emite um resumo JSON. Uma reimportação substitui atomicamente o conteúdo da mesma rede, sem duplicar feições.

Para MEDIUM e LARGE, use o transporte NDJSON e um lote entre 100 e 50.000:

~~~bash
dotnet run --project tools/importer/GeoMap.Importer -- \
  --file caminho/rede.geomap.ndjson \
  --batch-size 10000
~~~

O progresso de cada lote aparece como JSON em stderr. O leitor não carrega o arquivo inteiro; falha ou `Ctrl+C` preserva o snapshot anterior da rede.

### Exportar Polylines diretamente do Civil 3D

O GeoAutoEngine adiciona o comando `GEOAUTO3GEOMAPEXPORT` ao Civil 3D 2024, 2025 e 2026. O comando seleciona `LWPOLYLINE`, exige CRS e unidade explícitos e grava um arquivo `*.geomap.json` aceito pelo importador acima.

Consulte [a integração com o Civil 3D](docs/CIVIL3D_EXPORTER.md) para instalação, escopo e fluxo completo. Um arquivo de referência produzido pelo mesmo serializador está em `contracts/geomap/0.1/examples/valid-geoautoengine-polyline.geomap.json` e percorre validador, banco, catálogo, atributos e MVT na CI.

### Converter um DXF para validação real

A ponte provisória usada antes do exportador definitivo continua disponível para
reproduzir a validação por DXF ASCII e converte uma camada linear para o mesmo contrato v0.1:

~~~bash
python3 tools/dxf-converter/geomap_dxf.py --help
~~~

Ela lê apenas o Model Space, exige CRS e unidade explícitos e não descarta Z ou
curvas silenciosamente. Arquivos CAD e saídas reais permanecem fora do Git. Veja
[a ferramenta](tools/dxf-converter/README.md) e o
[registro sanitizado da primeira validação](docs/REAL_DATA_VALIDATION.md).

### Consultar feições por região

A API aceita somente consultas com projeto, rede e bounding box EPSG:4326 explícitos:

~~~text
GET /api/projects/{projectId}/networks/{networkId}/features
~~~

Não existe endpoint para carregar uma rede completa. Consulte [docs/API.md](docs/API.md) para parâmetros, limites e formato da resposta.

### Listar projetos

O seletor do cabeçalho usa o catálogo leve de projetos:

~~~text
GET /api/projects
~~~

A resposta traz apenas identificador e nome de cada projeto.

### Listar redes do projeto

O controle de camadas usa o catálogo leve do projeto atual:

~~~text
GET /api/projects/{projectId}/networks
~~~

A resposta traz apenas identificador, nome e tipo de cada rede; geometrias continuam sendo obtidas exclusivamente pelos tiles regionais.

### Solicitar um tile vetorial

As redes também podem ser obtidas em MVT, já recortadas por zoom e tile:

~~~text
GET /api/projects/{projectId}/networks/{networkId}/tiles/{z}/{x}/{y}.mvt
~~~

O endpoint aceita zoom de 14 a 22, retorna somente o identificador mínimo de cada feição e nunca usa uma carga integral como alternativa. O contrato completo está em [docs/API.md](docs/API.md).

### Encerrar o ambiente

~~~bash
docker compose down
~~~

Use docker compose down --volumes somente quando quiser apagar intencionalmente os dados locais do banco.

## Estrutura implementada

~~~text
apps/api/GeoMap.Api/                API mínima ASP.NET Core
apps/web/                            frontend React, TypeScript e MapLibre
database/migrations/                modelo espacial e rollback versionados
tools/importer/GeoMap.DatasetGenerator/ gerador e carga de datasets de escala
tools/importer/GeoMap.Contract/        validador do contrato de importação v0.1
tools/importer/GeoMap.Importer/        importador transacional do contrato v0.1
tools/dxf-converter/                    ponte DXF provisória da validação real
tests/GeoMap.Api.Tests/             testes de inicialização
tests/GeoMap.DatasetGenerator.Tests/ testes determinísticos do gerador
tests/GeoMap.Importer.Tests/        testes do parser, CLI e identidade
tests/integration/                  testes reais de schema, integridade e datasets
.github/workflows/                  CI do backend, frontend e PostGIS
docker-compose.yml                  ambiente local
~~~

O modelo espacial, os datasets SMALL, MEDIUM e LARGE, a API espacial, os tiles vetoriais, a fundação do frontend, o mapa base configurável, a visualização MVT, o controle de múltiplas camadas, a seleção, os atributos básicos, a importação GeoMap v0.1, a conversão privada da primeira rede CAD real e o exportador inicial de Polyline no GeoAutoEngine estão implementados.

## Documentação

- [Produto](docs/PRODUCT.md)
- [MVP](docs/MVP.md)
- [Arquitetura](docs/ARCHITECTURE.md)
- [Modelo de dados](docs/DATA_MODEL.md)
- [API](docs/API.md)
- [Contrato de importação](docs/IMPORT_CONTRACT.md)
- [Exportador do Civil 3D](docs/CIVIL3D_EXPORTER.md)
- [Validação da rede CAD real](docs/REAL_DATA_VALIDATION.md)
- [Performance](docs/PERFORMANCE.md)
- [Decisões](docs/DECISIONS.md)
- [Backlog e roadmap](docs/BACKLOG.md)

## Como contribuir

Antes de iniciar uma Issue, leia [AGENTS.md](AGENTS.md) e os documentos indicados nele. Trabalhe em uma única Issue por vez, mantenha a main funcional e só conclua uma tarefa após build, testes e revisão do escopo.
