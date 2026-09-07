# API GeoMap

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
