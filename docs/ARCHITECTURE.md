# Arquitetura

## Visão geral

~~~mermaid
flowchart TD
    CAD["Civil 3D"] --> EXP["GeoAutoEngine Exporter"]
    EXP --> IMP["Importador GeoMap"]
    IMP --> DB["PostgreSQL + PostGIS"]
    DB --> API["ASP.NET Core API"]
    API --> MVT["Vector Tiles (MVT)"]
    MVT --> WEB["React + MapLibre"]
~~~

A prova técnica PostGIS → API → MVT → MapLibre foi concluída antes da integração do exportador do Civil 3D, inclusive com o dataset LARGE.

## Estilo arquitetural

Monólito modular simples:

- um frontend web;
- um backend;
- um banco PostgreSQL/PostGIS;
- uma ferramenta de importação;
- um exportador Civil 3D hospedado no GeoAutoEngine.

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

O contrato não depende dos tipos internos do Civil 3D. A versão 0.1 usa um documento `.geomap.json` UTF-8 estrito para a importação inicial. O módulo `GeoMap.Contract` valida versão, campos, CRS EPSG, unidades, limites, identidade de origem e geometrias `LineString` ou `MultiLineString` antes de qualquer persistência. `GeoMap.Importer` lê o documento validado, resolve IDs determinísticos de projeto, rede e feições e usa PostGIS para transformar o SRID declarado em EPSG:4326.

Cada arquivo representa o snapshot completo de uma rede. Projeto e rede são atualizados, as feições anteriores são substituídas e as novas são inseridas em uma única transação. Um lock por rede serializa importações concorrentes; validação, CRS indisponível ou falha de banco causam rollback integral. Posições são estritamente 2D; Z é rejeitado para impedir perda silenciosa.

Para grandes volumes, o mesmo modelo lógico v0.1 possui transporte `.geomap.ndjson`: a primeira linha contém os metadados e cada linha seguinte contém uma feição. O leitor trabalha com buffer de 64 KiB e materializa somente um registro por vez; lotes configuráveis são enviados por `COPY` para uma tabela temporária. Cada lote de staging recebe commit próprio, mas projeto, rede e snapshot público só mudam na transação final. Erro ou cancelamento fecha a sessão, descarta o staging temporário e preserva a rede anterior. O progresso de cada lote é emitido como JSON em stderr e o resumo final permanece em stdout.

### Civil 3D Exporter

O exportador é hospedado no aplicativo C# GeoAutoEngine, instalado como bundle do Civil 3D 2024, 2025 e 2026. O comando `GEOAUTO3GEOMAPEXPORT` extrai `LWPOLYLINE` abertas ou fechadas, preserva handle e layer e produz o contrato GeoMap v0.1. CRS e unidade são confirmados pelo operador; nenhum tipo Autodesk atravessa o arquivo de integração.

A versão inicial aceita somente segmentos retos. Polylines fechadas repetem a primeira posição no final da `LineString`; bulges e entidades fora do escopo interrompem a exportação com diagnóstico. A projeção de Z para X/Y fica registrada em `properties.projectedTo2D` e a elevação original em `properties.cadElevation`.

O código Autodesk permanece no repositório GeoAutoEngine. Este repositório mantém o contrato neutro, a fixture de referência e o teste ponta a ponta pelo importador, PostGIS e MVT.

Para a validação real anterior ao exportador definitivo, existe uma ponte DXF
ASCII isolada em `tools/dxf-converter`. Ela lê somente `LINE` e `LWPOLYLINE` do
Model Space e produz o mesmo contrato v0.1 consumido pelo importador. O uso
exige camada, CRS, unidade e metadados explícitos. Elevações só podem ser
projetadas em XY com autorização pela opção `--project-to-2d` e permanecem nos
atributos. Curvas, extrusões e tipos lineares ainda não suportados interrompem a
conversão para evitar alteração silenciosa da geometria.

Essa ponte não referencia bibliotecas Autodesk, não substitui o exportador da
Issue #18 e não altera os limites entre importador, banco, API e frontend. DWG,
DXF e saídas reais permanecem fora do controle de versão.

## Fluxo de visualização

1. O MapLibre solicita um tile z/x/y.
2. A API converte o tile em envelope espacial.
3. O PostGIS filtra somente feições que intersectam o envelope, usando índice espacial.
4. A consulta recorta e simplifica quando apropriado.
5. O PostGIS ou a API produz MVT.
6. O navegador renderiza o tile e descarta tiles fora da área necessária.

## Integração MapLibre com MVT

O frontend configura uma fonte vetorial `geomap-network` com o template `/api/projects/{projectId}/networks/{networkId}/tiles/{z}/{x}/{y}.mvt`, a source-layer `network_features` e zoom de 14 a 22. Projeto, rede e base da API são configuração de ambiente validada; uma configuração ausente ou inválida mantém o mapa base funcional e informa o estado ao usuário.

Pan e zoom fazem o MapLibre requisitar apenas os tiles necessários para a região visível. A aplicação não usa o endpoint de feições como alternativa nem carrega a rede completa. Eventos da fonte controlam os estados de carregamento, região vazia, conteúdo visível e falha; erros da rede ficam isolados do mapa base.

## Catálogo e controle de camadas

A API lista `id`, `name` e `type` das redes filtradas por `project_id`, sem acessar geometrias ou contar feições. O frontend cria uma fonte e uma camada MapLibre por rede do catálogo; seus identificadores `geomap-network-source-{networkId}` e `geomap-network-lines-{networkId}` evitam colisões.

A visibilidade é estado local da sessão e muda por `setLayoutProperty`, mantendo as fontes instaladas e permitindo reutilizar o cache de tiles. Zoom e pan não recriam camadas. Ao trocar o projeto configurado, requisições pendentes são canceladas e as camadas anteriores são removidas antes de instalar o novo catálogo. Cores simples e determinísticas diferenciam as redes, sem editor de estilo.

## Seleção de elementos

O clique consulta apenas as camadas de rede visíveis já renderizadas no ponto do mapa. Entre feições sobrepostas, a primeira retornada pelo MapLibre — a visualmente superior — é selecionada quando contém um UUID válido na propriedade MVT `id`. Clicar fora de uma feição válida limpa a seleção; zoom e pan preservam a seleção corrente.

Cada rede possui uma camada de destaque `geomap-network-selection-{networkId}`, ligada à mesma fonte MVT da linha. A seleção altera somente o filtro dessa camada para o identificador escolhido, sem nova consulta à API, duplicação de fonte ou carga integral. Ocultar a rede selecionada, trocar o projeto ou recarregar o estilo limpa a seleção.

## Fluxo de atributos

Após a seleção, o frontend solicita `GET /api/projects/{projectId}/networks/{networkId}/features/{featureId}`. A API exige os três UUIDs no mesmo contexto e consulta uma única linha pela chave da feição, vinculada à rede e ao projeto. Uma combinação inexistente ou cruzada retorna HTTP 404 sem indicar se a feição existe em outro contexto.

A resposta contém os metadados básicos de origem, data de registro e o objeto `properties`; a geometria permanece no MVT e não é repetida. O frontend cancela a requisição anterior ao trocar a seleção e apresenta carregamento, propriedades vazias, ausência e falha. O painel é somente leitura e não introduz edição de geometria ou atributos.

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
