# Contrato de importação GeoMap

Status: contrato GeoMap v0.1 definido e validável pela Issue #14.

## Objetivo

Separar a origem Autodesk do restante do sistema. Qualquer exportador deve produzir o mesmo contrato lógico, independentemente de a geometria vir de Polyline, Feature Line, Pipe Network ou outra estrutura.

## Estrutura lógica

### Metadados da importação

| Campo | Obrigatório | Descrição |
|---|---:|---|
| `contractVersion` | sim | valor exato `0.1` |
| `sourceSystem` | sim | sistema de origem, até 100 caracteres |
| `sourceCrs` | sim | identificador no formato `EPSG:<código>` |
| `project` | sim | objeto com `externalId` e `name` |
| `network` | sim | objeto com `externalId`, `name` e `type` |
| `exportedAt` | sim | data e hora ISO 8601 em UTC, terminada em `Z` |
| `units` | sim | `metre`, `foot`, `us-survey-foot` ou `degree` |

### Feição

| Campo | Obrigatório | Descrição |
|---|---:|---|
| `sourceId` | não | identificador estável na origem, até 255 caracteres |
| `sourceType` | sim | código neutro do tipo original, até 100 caracteres |
| `sourceLayer` | não | layer original, até 255 caracteres |
| `geometry` | sim | `LineString` ou `MultiLineString` 2D |
| `properties` | sim | objeto JSON de atributos variáveis, inclusive vazio |

Exemplo resumido:

~~~json
{
  "contractVersion": "0.1",
  "sourceSystem": "Civil 3D",
  "sourceCrs": "EPSG:31982",
  "units": "metre",
  "project": { "externalId": "project-001", "name": "Projeto exemplo" },
  "network": { "externalId": "network-001", "name": "Drenagem", "type": "stormwater" },
  "exportedAt": "2026-09-08T14:00:00Z",
  "features": [
    {
      "sourceId": "2A7F",
      "sourceType": "Polyline",
      "sourceLayer": "REDE-DRENAGEM",
      "geometry": {
        "type": "LineString",
        "coordinates": [[688100.25, 7086400.75], [688112.50, 7086411.00]]
      },
      "properties": { "diameter": 300, "material": "PVC" }
    }
  ]
}
~~~

Exemplos executáveis ficam em `contracts/geomap/0.1/examples`.

## Formato físico inicial

O formato inicial é um único documento JSON UTF-8 com extensão `.geomap.json` e media type `application/vnd.geomap.v0.1+json`. O objeto raiz contém exatamente os metadados e o array `features` descritos neste documento. Não é GeoJSON `FeatureCollection` e não contém classes, assemblies ou nomes de namespace da Autodesk.

Esse documento atende ao importador inicial da Issue #15 e possui limites deliberados. A Issue #16 poderá acrescentar uma representação por streaming ou pacote segmentado preservando os mesmos objetos lógicos e a versão explícita, sem transformar o documento v0.1 em uma carga integral sem limite.

## CRS, unidades e dimensionalidade

- `sourceCrs` é obrigatório e aceita somente identificadores EPSG inequívocos, como `EPSG:31982` ou `EPSG:4326`.
- As coordenadas permanecem no CRS declarado; o importador as transformará para o CRS canônico EPSG:4326.
- `units` declara a unidade das coordenadas de origem e aceita `metre`, `foot`, `us-survey-foot` ou `degree`.
- Toda posição deve conter exatamente dois números JSON finitos `[x, y]`.
- Coordenadas Z são rejeitadas no contrato v0.1. O exportador deve projetar explicitamente para 2D antes de gerar o arquivo quando essa perda for aceitável.
- `LineString` exige pelo menos duas posições.
- `MultiLineString` exige ao menos uma linha, cada uma com pelo menos duas posições.

## Limites iniciais

| Item | Limite v0.1 |
|---|---:|
| documento | 50 MiB |
| feições por documento | 10.000 |
| vértices por feição | 100.000 |
| propriedades por feição | 128 campos no primeiro nível |
| tamanho serializado de `properties` | 64 KiB |
| profundidade de `properties` | 4 níveis |
| nome de propriedade | 128 caracteres |
| string dentro de `properties` | 4.096 caracteres |

`project.externalId`, `network.externalId` e `sourceId` possuem até 255 caracteres. Nomes de projeto e rede possuem até 200 caracteres. O tipo da rede e `sourceType` possuem até 100 caracteres.

## Validação

O validador equivalente e sem dependências externas está em `tools/importer/GeoMap.Contract`. Uma importação rejeita versão desconhecida, CRS ausente ou inválido, coordenadas não finitas, Z, geometria vazia ou não suportada, campos estruturais desconhecidos ou duplicados, propriedades acima dos limites e repetição de `sourceType` + `sourceId` no mesmo documento.

Cada erro possui código estável, caminho JSON e mensagem legível. O importador da Issue #15 deverá preservar esses dados em seu resultado.

## Códigos de erro

| Código | Significado |
|---|---|
| `GMC_DOCUMENT_INVALID` | arquivo não é um documento JSON UTF-8 válido |
| `GMC_VERSION_UNSUPPORTED` | versão diferente de `0.1` |
| `GMC_REQUIRED_FIELD` | campo obrigatório ausente |
| `GMC_FIELD_INVALID` | tipo, valor ou campo estrutural inválido |
| `GMC_LIMIT_EXCEEDED` | um dos limites do contrato foi excedido |
| `GMC_CRS_INVALID` | CRS ausente ou fora do formato EPSG |
| `GMC_UNITS_UNSUPPORTED` | unidade não suportada |
| `GMC_GEOMETRY_TYPE_UNSUPPORTED` | geometria diferente de LineString/MultiLineString |
| `GMC_GEOMETRY_EMPTY` | linha sem quantidade mínima de posições |
| `GMC_GEOMETRY_COORDINATE_INVALID` | coordenada ausente, não numérica ou não finita |
| `GMC_GEOMETRY_DIMENSION_UNSUPPORTED` | posição não é estritamente 2D |
| `GMC_SOURCE_ID_DUPLICATE` | identidade de origem repetida no documento |
| `GMC_PROPERTIES_INVALID` | `properties` não é objeto ou possui nome inválido |

## Compatibilidade

- O consumidor seleciona o validador pelo valor exato de `contractVersion`.
- A versão `0.1` é estrita: campos estruturais desconhecidos são rejeitados para detectar erros de digitação.
- Somente o conteúdo interno de `properties` é extensível sem nova versão, respeitando os limites.
- Uma versão futura exige especificação e validador próprios; não existe downgrade silencioso.
- Exportadores devem continuar capazes de emitir uma versão explicitamente suportada pelo importador de destino.

## Normalização

- `sourceType` e `sourceLayer` preservam rastreabilidade usando strings neutras.
- `geometry` será convertida para o modelo espacial GeoMap pelo importador.
- `properties` mantém atributos ainda não padronizados.
- nenhuma classe Autodesk atravessa o limite do exportador.

## Exemplos e testes

Há exemplos válidos de `LineString` e `MultiLineString` e exemplos inválidos de versão, CRS, Z e tipo geométrico. Os testes automatizados executam o mesmo validador que será reutilizado pelo importador, verificam os limites de propriedades, campos futuros incompatíveis e identidades de origem duplicadas.

A política de atualização/reimportação, atomicidade operacional e códigos normalizados adicionais de rede permanecem para a Issue #15. A representação escalável por streaming será definida na Issue #16.
