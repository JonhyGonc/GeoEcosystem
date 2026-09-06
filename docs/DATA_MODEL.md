# Modelo de dados inicial

Este modelo é conceitual. A definição física, tipos exatos e SRID serão fechados na Issue #3.

## Relações

~~~mermaid
erDiagram
    PROJECT ||--o{ NETWORK : possui
    NETWORK ||--o{ NETWORK_FEATURE : agrupa
    PROJECT {
        uuid id
        string name
        timestamp created_at
    }
    NETWORK {
        uuid id
        uuid project_id
        string name
        string type
        timestamp created_at
    }
    NETWORK_FEATURE {
        uuid id
        uuid network_id
        geometry geometry
        string source_id
        string source_layer
        string source_type
        jsonb properties
        timestamp created_at
    }
~~~

## Project

Representa uma área ou trabalho que agrupa redes.

Campos mínimos propostos:

| Campo | Finalidade |
|---|---|
| id | Identificador estável |
| name | Nome de apresentação |
| created_at | Auditoria mínima |

## Network

Agrupa feições de mesma finalidade ou origem dentro de um projeto.

| Campo | Finalidade |
|---|---|
| id | Identificador estável |
| project_id | Projeto proprietário |
| name | Nome de apresentação |
| type | Categoria normalizada inicial |
| created_at | Auditoria mínima |

## NetworkFeature

Representa uma feição espacial normalizada.

| Campo | Finalidade |
|---|---|
| id | Identificador GeoMap |
| network_id | Rede proprietária |
| geometry | Geometria PostGIS |
| source_id | Identificador na origem, quando disponível |
| source_layer | Layer CAD de origem |
| source_type | Tipo de origem, como Polyline |
| properties | Atributos variáveis em JSONB |
| created_at | Auditoria mínima |

## Regras iniciais

- O MVP prioriza LineString e MultiLineString 2D.
- Coordenadas Z podem ser recebidas futuramente, mas a política de preservação ainda precisa ser decidida.
- Toda geometria importada deve declarar o CRS de origem.
- O banco não armazena objetos Autodesk serializados.
- properties contém apenas valores de negócio permitidos; não é depósito de arquivos binários.
- source_id não substitui o id interno.
- exclusões em cascata, unicidade de source_id e estratégia de reimportação serão decididas na Issue #3 ou #15.
- índices mínimos esperados: GiST na geometria e índices nas chaves estrangeiras.
- índices em JSONB só devem ser criados para consultas reais.

## Sistemas de referência

Duas informações são obrigatórias no desenho da solução:

1. CRS de origem declarado pela importação;
2. CRS canônico de armazenamento e transformação para Web Mercator.

A escolha entre armazenamento canônico em EPSG:4326, CRS projetado por projeto ou outra estratégia permanece pendente até um teste com dados brasileiros reais. A decisão deve equilibrar precisão, simplicidade de tiles, índices e múltiplas cidades.

## Evolução controlada

Novos campos devem surgir quando atributos forem comprovadamente padronizados e consultados. Até lá, propriedades variáveis permanecem em JSONB para evitar dezenas de colunas especulativas.
