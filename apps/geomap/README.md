# GeoMap

Prova de conceito para armazenar e visualizar, em um mapa web, redes de tubulação 2D originadas no AutoCAD Civil 3D.

> **Monorepo.** O GeoMap vive na pasta `apps/geomap` do repositório [GeoEcosystem](https://github.com/JonhyGonc/GeoEcosystem), junto com o GeoAutoEngine e o GeoAutoDocs. Todos os comandos deste README rodam **a partir de `apps/geomap`**:
>
> ```bash
> git clone https://github.com/JonhyGonc/GeoEcosystem.git
> cd GeoEcosystem/apps/geomap
> ```
>
> A CI fica na raiz do monorepo, em `.github/workflows/geomap-*.yml`. Números como `#14` ou `#52` nesta documentação são Issues e PRs do repositório antigo `JonhyGonc/Geomap` (arquivado, somente leitura); Issues novas vão para o [GeoEcosystem](https://github.com/JonhyGonc/GeoEcosystem/issues).

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

#### PostgreSQL já instalado no Windows

Um PostgreSQL instalado diretamente no Windows também escuta a porta 5432. Nesse caso, os comandos `docker compose exec` funcionam, porque rodam dentro do container, mas o gerador de datasets e o importador conectam pelo `localhost` e chegam ao PostgreSQL do Windows. O sintoma é o erro `28P01`, de falha de autenticação por senha, muitas vezes com a mensagem em português.

Para confirmar no PowerShell:

~~~powershell
Get-NetTCPConnection -LocalPort 5432 -State Listen |
  ForEach-Object { (Get-Process -Id $_.OwningProcess).ProcessName }
~~~

Se aparecerem `postgres` e `com.docker.backend`, publique o banco do Docker em outra porta e use essa mesma porta nas strings de conexão das ferramentas:

~~~powershell
(Get-Content .env) -replace '^POSTGRES_PORT=.*', 'POSTGRES_PORT=5433' | Set-Content .env
docker compose up -d
$env:GEOMAP_DB_CONNECTION_STRING = 'Host=localhost;Port=5433;Database=geomap;Username=geomap;Password=change-me-local-only'
~~~

A API não muda, porque fala com o banco pela rede interna do Docker.

### Verificar o PostGIS

~~~bash
docker compose exec database sh -ec \
  'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT PostGIS_Version();"'
~~~

### Aplicar o modelo espacial

Em um banco novo, aplique todas as migrations em ordem (001 a 005). Bash:

~~~bash
for migration in database/migrations/[0-9][0-9][0-9]_*.up.sql; do
  docker compose exec -T database sh -ec \
    'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < "$migration"
done
~~~

PowerShell:

~~~powershell
Get-ChildItem database/migrations/*.up.sql | Sort-Object Name | ForEach-Object {
  Get-Content -Raw $_.FullName |
    docker compose exec -T database sh -ec 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
}
~~~

Em um banco que já existe, consulte `schema_migrations` e aplique só as que faltam; veja [database/migrations/README.md](database/migrations/README.md).

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

### Ortofoto do projeto

Cada projeto pode ter uma ortofoto, mostrada abaixo das redes, com liga/desliga e opacidade (DEC-035). **O jeito mais simples** é dar dois cliques em `tools\ortofoto\converter-ortofoto.bat` (ou arrastar a ortofoto para cima dele), escolher o arquivo (TIF, ECW ou JP2) e digitar o nome do projeto. O script mostra cada etapa com [OK], [AVISO] ou [ERRO], usa o GDAL do QGIS, aproveita a banda de transparência do arquivo (sem borda branca), para e liga a API sozinho e grava o `.mbtiles` em `C:\GeoMap\ortofotos`. Os comandos abaixo fazem o mesmo à mão.

A ortofoto de drone (ECW ou JP2) é convertida **uma vez** para MBTiles com o GDAL que vem no QGIS (gratuito). Abra o **OSGeo4W Shell**, que vem junto com o QGIS, e rode:

~~~bat
gdal_translate -of MBTILES -co TILE_FORMAT=WEBP -co QUALITY=85 "C:\Ortos\obra.ecw" "C:\GeoMap\ortofotos\Nome do projeto.mbtiles"
gdaladdo -r average "C:\GeoMap\ortofotos\Nome do projeto.mbtiles" 2 4 8 16 32 64 128 256
~~~

- O primeiro comando gera os tiles no zoom máximo e reprojeta para o Web Mercator. O segundo gera os zooms menores, para a ortofoto aparecer de longe.
- O nome do arquivo é o nome do projeto como aparece no site. Se ele não encontrar o arquivo, o site mostra o nome exato esperado.
- `WEBP` mantém a transparência das bordas e exige GDAL 3.8 ou mais novo. Sem ele, use `JPEG` (bordas pretas, arquivo menor) ou `PNG` (arquivo maior).
- Aponte a pasta no `.env` (`GEOMAP_ORTHOPHOTO_DIR=C:/GeoMap/ortofotos`) e rode `docker compose up -d` de novo. A pasta e os arquivos ficam fora do Git.

### Importar pelo site

Com a API e o site rodando, clique em **Importar arquivos .geomap.json** no quadro do GeoMap e escolha um ou mais arquivos, por exemplo o de linhas e o `-blocos` gerados pelo GeoAutoEngine. Cada arquivo mostra o resultado: as feições importadas ou o erro do validador. No fim, o mapa recarrega e abre o projeto importado. As regras são as mesmas do importador do terminal (DEC-033). O botão é para uso local, porque a API não tem login.

### Importar um contrato GeoMap (v0.1, v0.2 ou v0.3)

Com o banco iniciado e a migration aplicada, defina a conexão e informe um arquivo validado. O exemplo abaixo usa a v0.1; as diferenças da v0.2 (cor da layer) e da v0.3 (pontos de blocos) estão no [contrato de importação](docs/IMPORT_CONTRACT.md):

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

O GeoAutoEngine adiciona o comando `GEOAUTO3GEOMAPEXPORT` ao Civil 3D 2026. Ele é o único caminho oficial do DWG para o GeoMap (a ponte DXF provisória foi aposentada, DEC-034): seleciona por layer (`LWPOLYLINE`, `LINE`, `ARC` e blocos), gera uma rede por layer com a cor dela, exige CRS e unidade explícitos e grava arquivos `*.geomap.json` aceitos pelo importador e pelo botão **Importar** do site.

Consulte [a integração com o Civil 3D](docs/CIVIL3D_EXPORTER.md) para instalação, escopo e fluxo completo. Um arquivo de referência produzido pelo mesmo serializador está em `contracts/geomap/0.1/examples/valid-geoautoengine-polyline.geomap.json` e percorre validador, banco, catálogo, atributos e MVT na CI.

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
tools/importer/GeoMap.Contract/        validador do contrato de importação (v0.1 a v0.3)
tools/importer/GeoMap.Importer/        importador transacional do contrato (v0.1 a v0.3)
tests/GeoMap.Api.Tests/             testes de inicialização
tests/GeoMap.DatasetGenerator.Tests/ testes determinísticos do gerador
tests/GeoMap.Importer.Tests/        testes do parser, CLI e identidade
tests/integration/                  testes reais de schema, integridade e datasets
docker-compose.yml                  ambiente local
~~~

A CI do backend, do frontend e do PostGIS fica na raiz do monorepo: `.github/workflows/geomap-backend-ci.yml` e `.github/workflows/geomap-frontend-ci.yml`.

O modelo espacial, os datasets SMALL, MEDIUM e LARGE, a API espacial, os tiles vetoriais, a fundação do frontend, o mapa base configurável, a visualização MVT, o controle de múltiplas camadas, a seleção, os atributos básicos, a importação GeoMap (v0.1 a v0.3), a conversão privada da primeira rede CAD real e o exportador inicial de Polyline no GeoAutoEngine estão implementados.

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
