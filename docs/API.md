# API GeoMap

## Catálogo de redes do projeto

~~~http
GET /api/projects/{projectId}/networks
~~~

Lista as camadas disponíveis no projeto atual sem consultar nem contar `network_features`. A resposta contém somente `id`, `name` e `type`, ordenados por nome e identificador. O frontend usa cada identificador para montar a fonte MVT regional correspondente.

Um projeto existente sem redes e um UUID de projeto sem correspondência retornam HTTP 200 com `networks: []`. O identificador na rota deve ser um UUID válido. A consulta possui timeout de 5 segundos e usa o índice relacional de `networks.project_id`.

Exemplo:

~~~json
{
  "projectId": "60000000-0000-4000-8000-000000000001",
  "networks": [
    {
      "id": "61000000-0000-4000-8000-000000000001",
      "name": "Drenagem principal",
      "type": "stormwater"
    }
  ]
}
~~~

## Tiles vetoriais da rede

~~~http
GET /api/projects/{projectId}/networks/{networkId}/tiles/{z}/{x}/{y}.mvt
~~~

O endpoint entrega uma camada MVT chamada `network_features`, pronta para ser configurada como fonte vetorial do MapLibre. Cada feição contém somente a geometria recortada ao tile e a propriedade textual `id`; atributos detalhados permanecem fora do tile.

O frontend usa `id` como identificador estável da seleção e como futuro token para consultar uma única feição. O clique e o destaque não criam chamada adicional a este endpoint; a consulta detalhada permanece reservada à Issue #13.

### Regras

| Entrada ou comportamento | Regra |
|---|---|
| `projectId` e `networkId` | UUIDs obrigatórios; a rede precisa pertencer ao projeto |
| `z` | inteiro entre 14 e 22 |
| `x` e `y` | inteiros dentro da grade `0` a `2^z - 1` |
| projeção do tile | Web Mercator, EPSG:3857 |
| extensão MVT | 4.096 unidades |
| buffer | 64 unidades, com recorte da geometria |
| conteúdo | propriedade `id` e geometria linear |
| timeout do banco | 5 segundos |
| cache | `Cache-Control: public, max-age=60` |

Um tile válido com feições retorna HTTP 200 e `Content-Type: application/vnd.mapbox-vector-tile`. Um tile válido sem feições, inclusive quando projeto e rede não correspondem, retorna HTTP 204. Coordenadas inválidas retornam HTTP 400.

O zoom mínimo 14 impede pedidos continentais que agregariam uma rede extensa em um único tile. A futura fonte MapLibre deve declarar o mesmo `minzoom`. Não existe fallback para carregar a rede inteira.

Exemplo para a região do dataset sintético:

~~~http
GET /api/projects/{projectId}/networks/{networkId}/tiles/15/11938/18866.mvt
~~~

## Consulta espacial por região

~~~http
GET /api/projects/{projectId}/networks/{networkId}/features
    ?minLongitude={valor}
    &minLatitude={valor}
    &maxLongitude={valor}
    &maxLatitude={valor}
    [&limit={valor}]
~~~

A consulta retorna somente feições da rede informada, pertencente ao projeto informado, que intersectam a bounding box obrigatória.

### Regras de entrada

| Entrada | Regra |
|---|---|
| `projectId` | UUID obrigatório na rota |
| `networkId` | UUID obrigatório na rota |
| `minLongitude`, `maxLongitude` | números finitos entre -180 e 180 |
| `minLatitude`, `maxLatitude` | números finitos entre -90 e 90 |
| ordem | mínimos estritamente menores que máximos |
| extensão | no máximo 1 grau de longitude por 1 grau de latitude |
| `limit` | opcional; padrão 500; mínimo 1; máximo 2.000 |

As coordenadas da API usam o CRS canônico **EPSG:4326**. O limite de um grau é uma proteção de escopo da consulta, não uma medida métrica: graus não devem ser interpretados como metros.

A consulta ao banco possui timeout de 5 segundos. Entradas inválidas retornam HTTP 400 com os campos problemáticos. UUIDs válidos sem correspondência entre projeto e rede retornam uma lista vazia, sem executar uma consulta de carga integral alternativa.

### Exemplo

~~~http
GET /api/projects/60000000-0000-4000-8000-000000000001/networks/61000000-0000-4000-8000-000000000001/features?minLongitude=-48.90&minLatitude=-26.35&maxLongitude=-48.85&maxLatitude=-26.30&limit=500
~~~

Resposta resumida:

~~~json
{
  "projectId": "60000000-0000-4000-8000-000000000001",
  "networkId": "61000000-0000-4000-8000-000000000001",
  "boundingBox": {
    "minLongitude": -48.90,
    "minLatitude": -26.35,
    "maxLongitude": -48.85,
    "maxLatitude": -26.30
  },
  "limit": 500,
  "returned": 2,
  "hasMore": false,
  "features": [
    {
      "id": "62000000-0000-4000-8000-000000000001",
      "networkId": "61000000-0000-4000-8000-000000000001",
      "geometry": {
        "type": "LineString",
        "coordinates": [[-48.85, -26.30], [-48.84, -26.29]]
      }
    }
  ]
}
~~~

`hasMore: true` indica que ao menos uma feição adicional intersecta a região. O consumidor deve reduzir a bounding box ou aguardar o fluxo MVT da Issue #7; não existe paginação que permita baixar silenciosamente uma rede inteira.

As propriedades JSONB detalhadas não são repetidas nesta resposta. A consulta de atributos pertence à Issue #13.
