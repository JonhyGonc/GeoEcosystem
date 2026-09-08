# Arquitetura

## Visão geral

~~~mermaid
flowchart TD
    CAD["Civil 3D"] --> EXP["Exportador futuro"]
    EXP --> IMP["Importador GeoMap"]
    IMP --> DB["PostgreSQL + PostGIS"]
    DB --> API["ASP.NET Core API"]
    API --> MVT["Vector Tiles (MVT)"]
    MVT --> WEB["React + MapLibre"]
~~~

A primeira prova técnica cobre PostGIS → API → MVT → MapLibre. O exportador do Civil 3D entra somente depois que esse fluxo funcionar com grande volume.

## Estilo arquitetural

Monólito modular simples:

- um frontend web;
- um backend;
- um banco PostgreSQL/PostGIS;
- uma ferramenta de importação;
- um exportador Civil 3D futuro.

Não há justificativa no MVP para serviços distribuídos, mensageria ou infraestrutura complexa.

## Componentes

### Frontend

Responsável por:

- mapa e navegação;
- fonte de mapa base configurável;
- carregamento de MVT por região e zoom;
- controle de camadas;
- seleção de feições;
- apresentação de atributos.

Não edita geometrias.

### Backend

Responsável por:

- API de projetos e redes;
- consultas espaciais;
- geração de MVT;
- consulta de atributos;
- limites, validação e respostas de erro;
- composição simples dos módulos do monólito.

Não expõe endpoint que retorne uma rede grande completa por padrão.

### Database

Responsável por:

- persistência de Project, Network e NetworkFeature;
- geometria PostGIS;
- JSONB para atributos ainda não padronizados;
- integridade referencial;
- índices espaciais e relacionais;
- suporte a consultas por envelope do tile.

### Importer

Responsável por:

- ler o contrato GeoMap;
- validar versão, metadados, CRS e geometria;
- normalizar dados;
- gravar em lotes;
- registrar erros e resumo da importação;
- limitar uso de memória.

O contrato não depende dos tipos internos do Civil 3D.

### Civil 3D Exporter

Componente futuro, carregado no Civil 3D, responsável apenas por extrair e converter objetos CAD para o contrato GeoMap. O primeiro tipo planejado é Polyline.

## Fluxo de visualização

1. O MapLibre solicita um tile z/x/y.
2. A API converte o tile em envelope espacial.
3. O PostGIS filtra somente feições que intersectam o envelope, usando índice espacial.
4. A consulta recorta e simplifica quando apropriado.
5. O PostGIS ou a API produz MVT.
6. O navegador renderiza o tile e descarta tiles fora da área necessária.

## Fluxo de atributos

A seleção no mapa usa um identificador mínimo presente no tile. A consulta detalhada busca uma única feição ou um conjunto pequeno, sem embutir propriedades pesadas em todos os tiles.

## Mapa base

O frontend converte uma configuração validada em uma fonte raster XYZ e nas camadas de estilo do mapa base. URL, nome, tamanho de tile, limites de zoom e atribuição são definidos em um único módulo e podem ser substituídos por variáveis de ambiente.

A fonte e as camadas do fundo usam identificadores reservados com prefixo `basemap-`. As camadas das redes devem usar o prefixo `geomap-`. A troca do mapa base remove e recria somente os elementos `basemap-`, inserindo-os antes da primeira camada GeoMap; assim, a visualização das redes permanece independente do provedor.

O padrão inicial é o raster XYZ OpenStreetMap Standard, restrito ao protótipo interno de baixo tráfego e às regras públicas de uso e atribuição do serviço. A arquitetura admite adaptadores posteriores para outro XYZ, WMS, WMTS, ortofoto própria, COG ou provedor comercial sem alterar o modelo das redes. Somente raster XYZ está implementado nesta etapa.

## Limites entre componentes

- Autodesk existe apenas no exportador.
- O importador recebe GeoMap Contract.
- O banco armazena o modelo GeoMap.
- A API fornece recursos web e MVT.
- O frontend desconhece objetos Autodesk.
- O mapa base não interfere no armazenamento das redes.

## Implantação inicial

O desenvolvimento local usará Docker Compose. A estratégia de hospedagem de produção permanece pendente e não bloqueia o Protótipo 0.1.

## Estrutura prevista do repositório

Os diretórios serão criados somente quando a Issue correspondente lhes der uso:

~~~text
apps/
  api/                 backend ASP.NET Core
  web/                 frontend React + TypeScript
tools/
  importer/            geração e importação de dados
database/
  migrations/          evolução do esquema PostGIS
tests/                 testes que não pertençam aos projetos
test-data/
  small/
  medium/
  large/
~~~

Não criar diretórios vazios apenas para reproduzir esta árvore.
