# Modelo de dados inicial

Status: implementado pela Issue #3.

## Relações

~~~mermaid
erDiagram
    PROJECT ||--o{ NETWORK : possui
    NETWORK ||--o{ NETWORK_FEATURE : agrupa
    PROJECT {
        uuid id
        varchar name
        timestamptz created_at
    }
    NETWORK {
        uuid id
        uuid project_id
        varchar name
        varchar type
        timestamptz created_at
    }
    NETWORK_FEATURE {
        uuid id
        uuid network_id
        geometry geometry
        varchar source_id
        varchar source_layer
        varchar source_type
        jsonb properties
        timestamptz created_at
    }
~~~

## Modelo físico

### projects

| Campo | Tipo | Regra |
|---|---|---|
| id | uuid | chave primária, fornecida pela aplicação/importação |
| name | varchar(200) | obrigatório e não vazio |
| created_at | timestamptz | obrigatório, padrão CURRENT_TIMESTAMP |

### networks

| Campo | Tipo | Regra |
|---|---|---|
| id | uuid | chave primária |
| project_id | uuid | FK obrigatória para projects |
| name | varchar(200) | obrigatório e não vazio |
| type | varchar(100) | categoria normalizada obrigatória |
| color | varchar(7) | opcional; cor da layer no CAD em `#RRGGBB` maiúsculo (migration 002, DEC-028) |
| created_at | timestamptz | obrigatório, padrão CURRENT_TIMESTAMP |

### network_features

| Campo | Tipo | Regra |
|---|---|---|
| id | uuid | chave primária GeoMap |
| network_id | uuid | FK obrigatória para networks |
| geometry | geometry(Geometry, 4326) | somente LineString ou MultiLineString 2D, não vazia |
| source_id | varchar(255) | identificador opcional na origem |
| source_layer | varchar(255) | layer opcional de origem |
| source_type | varchar(100) | tipo de origem obrigatório |
| properties | jsonb | objeto JSON obrigatório, padrão vazio |
| created_at | timestamptz | obrigatório, padrão CURRENT_TIMESTAMP |

## CRS e dimensionalidade

O CRS canônico de armazenamento é **EPSG:4326 (WGS 84), em longitude/latitude**.

- Toda entrada deve declarar seu CRS de origem.
- A transformação para EPSG:4326 acontece antes ou durante a importação.
- A coluna e o contrato v0.1 são 2D. Posições com Z são rejeitadas; nenhum componente pode descartar Z silenciosamente.
- Para MVT, o envelope do tile em EPSG:3857 deverá ser transformado para EPSG:4326 antes do filtro espacial, preservando o uso do índice GiST. A geometria selecionada poderá então ser transformada para EPSG:3857.
- Cálculos métricos futuros devem transformar a geometria para um CRS projetado adequado ou usar outra estratégia explicitamente documentada.

EPSG:4326 foi escolhido como formato canônico único para simplificar múltiplas cidades e fontes. O CRS original continua obrigatório no contrato de importação para garantir posicionamento e rastreabilidade.

## Integridade e índices

- Exclusão de Project remove suas Networks e NetworkFeatures por cascade.
- Exclusão de Network remove suas NetworkFeatures por cascade.
- Não há endpoint de exclusão nesta etapa.
- source_id, quando informado, é único dentro da combinação network_id + source_type.
- O importador deriva IDs determinísticos do sistema de origem e dos identificadores externos. Dentro de um documento v0.1, `sourceType` + `sourceId` não pode se repetir.
- Cada documento é o snapshot completo da rede identificada. A reimportação atualiza os nomes e o tipo e substitui todas as feições da rede na mesma transação.
- Feições com `sourceId` mantêm ID estável pela combinação rede + `sourceType` + `sourceId`. Sem `sourceId`, o ID é estável somente enquanto a posição da feição no documento não mudar.
- Índices B-tree existem nas FKs project_id e network_id.
- ix_network_features_geometry usa GiST.
- Não há índice JSONB sem uma consulta real que o justifique.
- properties deve ser um objeto JSON; arrays e valores escalares são rejeitados.

## Migrations

A fonte de verdade física está em database/migrations.

- 001_initial_spatial_model.up.sql cria a extensão necessária, tabelas, restrições e índices.
- 001_initial_spatial_model.down.sql reverte as tabelas em ordem segura.
- O rollback não remove a extensão PostGIS.
- Novas alterações devem criar nova migration, sem editar uma migration já aplicada em ambiente compartilhado.
