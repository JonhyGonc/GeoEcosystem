# GeoMap

Prova de conceito para armazenar e visualizar, em um mapa web, redes de tubulação 2D originadas no AutoCAD Civil 3D.

## Objetivo atual

O Protótipo 0.1 deve demonstrar que uma rede proveniente do CAD pode ser normalizada, armazenada em PostgreSQL/PostGIS e exibida corretamente no navegador sobre um mapa de fundo.

O site é um visualizador geográfico. A criação e a edição das tubulações permanecem no Civil 3D.

## Arquitetura aprovada

- Frontend: React, TypeScript e MapLibre GL JS.
- Backend: ASP.NET Core, .NET 8 e C#.
- Banco: PostgreSQL e PostGIS.
- Desenvolvimento local: Docker Compose.
- Entrega das redes ao mapa: Vector Tiles (MVT).
- Mapa base: fonte configurável e desacoplada de um fornecedor específico.

O primeiro fluxo técnico a ser provado é:

PostGIS → API → MVT → MapLibre

O exportador do Civil 3D será tratado somente depois de o fluxo geográfico web funcionar com volume alto.

## Desenvolvimento local

### Pré-requisitos

- Git;
- Docker Desktop com Docker Compose;
- .NET 8 SDK, apenas para executar build e testes fora do Docker.

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

### Build e testes do backend

~~~bash
dotnet restore GeoMap.sln
dotnet build GeoMap.sln --configuration Release --no-restore
dotnet test GeoMap.sln --configuration Release --no-build
~~~

### Encerrar o ambiente

~~~bash
docker compose down
~~~

Use docker compose down --volumes somente quando quiser apagar intencionalmente os dados locais do banco.

## Estrutura implementada

~~~text
apps/api/GeoMap.Api/        API mínima ASP.NET Core
tests/GeoMap.Api.Tests/     testes de inicialização
.github/workflows/          CI do backend e PostGIS
docker-compose.yml          ambiente local
~~~

O modelo espacial, as migrations, o frontend e os tiles vetoriais pertencem às próximas Issues e ainda não foram implementados.

## Documentação

- [Produto](docs/PRODUCT.md)
- [MVP](docs/MVP.md)
- [Arquitetura](docs/ARCHITECTURE.md)
- [Modelo de dados](docs/DATA_MODEL.md)
- [Contrato de importação](docs/IMPORT_CONTRACT.md)
- [Performance](docs/PERFORMANCE.md)
- [Decisões](docs/DECISIONS.md)
- [Backlog e roadmap](docs/BACKLOG.md)

## Como contribuir

Antes de iniciar uma Issue, leia [AGENTS.md](AGENTS.md) e os documentos indicados nele. Trabalhe em uma única Issue por vez, mantenha a main funcional e só conclua uma tarefa após build, testes e revisão do escopo.
