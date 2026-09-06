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

## Estado do repositório

Esta branch contém somente a fundação documental e de governança. Nenhuma funcionalidade do GeoMap foi implementada.

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
