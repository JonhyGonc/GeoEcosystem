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

O exemplo `valid-geoautoengine-polyline.geomap.json` é produzido pelo mesmo serializador usado no comando `GEOAUTO3GEOMAPEXPORT` do GeoAutoEngine. A CI o valida e o importa para confirmar a compatibilidade entre os dois repositórios.

## Formatos físicos

O formato inicial é um único documento JSON UTF-8 com extensão `.geomap.json` e media type `application/vnd.geomap.v0.1+json`. O objeto raiz contém exatamente os metadados e o array `features` descritos neste documento. Não é GeoJSON `FeatureCollection` e não contém classes, assemblies ou nomes de namespace da Autodesk.

Para grandes volumes, a mesma versão lógica usa NDJSON UTF-8 com extensão `.geomap.ndjson` e media type `application/vnd.geomap.v0.1+ndjson`:

1. a linha 1 contém todos os campos do objeto raiz, exceto `features`;
2. cada linha seguinte contém exatamente um objeto de feição;
3. linhas vazias, comentários e vírgulas finais são inválidos;
4. LF e CRLF são aceitos; BOM UTF-8 é aceito somente no início do arquivo;
5. a ordem das feições define a identidade das feições que não possuem `sourceId`.

O exemplo `valid-stream.geomap.ndjson` demonstra os dois tipos geométricos. Os dois transportes preservam os mesmos campos, validações, códigos de erro, IDs determinísticos e semântica de snapshot.

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

No transporte NDJSON, os limites adicionais são 64 KiB para a linha de metadados, 16 MiB por linha de feição e 5.000.000 de feições por arquivo. O leitor usa buffer de 64 KiB e nunca carrega o arquivo inteiro.

`project.externalId`, `network.externalId` e `sourceId` possuem até 255 caracteres. Nomes de projeto e rede possuem até 200 caracteres. O tipo da rede e `sourceType` possuem até 100 caracteres.

## Validação

O validador equivalente e sem dependências externas está em `tools/importer/GeoMap.Contract`. Uma importação rejeita versão desconhecida, CRS ausente ou inválido, coordenadas não finitas, Z, geometria vazia ou não suportada, campos estruturais desconhecidos ou duplicados, propriedades acima dos limites e repetição de `sourceType` + `sourceId` no mesmo documento.

Cada erro possui código estável, caminho JSON e mensagem legível. O importador preserva esses dados no resumo JSON e não inicia a persistência quando houver qualquer erro.

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
- o tipo da rede é normalizado para minúsculas após remoção de espaços externos.
- `geometry` é interpretada no EPSG declarado e transformada pelo PostGIS para EPSG:4326.
- `properties` mantém atributos ainda não padronizados.
- nenhuma classe Autodesk atravessa o limite do exportador.

## Persistência e reimportação

- o documento representa o snapshot completo da rede indicada;
- projeto e rede são resolvidos por UUIDs determinísticos derivados de `sourceSystem` e `externalId`;
- uma reimportação atualiza os nomes e o tipo e substitui as feições da rede;
- validação e gravação são atômicas: qualquer erro mantém o snapshot anterior;
- importações concorrentes da mesma rede são serializadas no banco;
- `sourceType` + `sourceId` produz uma identidade estável; feições sem `sourceId` dependem da posição no documento;
- o resumo informa `received`, `imported`, `rejected`, `replaced`, IDs resolvidos, duração e erros.

No NDJSON, lotes temporários recebem commit sem alterar a rede publicada. Somente a transação final substitui o snapshot. O tamanho padrão é 10.000 e pode variar de 100 a 50.000 com `--batch-size`. Cada lote concluído emite um evento JSON `progress` em stderr; o resumo final é emitido em stdout. Cancelamento por `Ctrl+C` retorna `GMI_CANCELLED`, encerra o staging e preserva o snapshot anterior.

Falhas operacionais usam `GMI_FILE_INVALID`, `GMI_CRS_UNAVAILABLE` ou `GMI_PERSISTENCE_FAILED`. Os códigos `GMC_*` permanecem reservados à validação do contrato.

## Exemplos e testes

Há exemplos válidos de `LineString` e `MultiLineString` e exemplos inválidos de versão, CRS, Z e tipo geométrico. Os testes automatizados executam o mesmo validador que será reutilizado pelo importador, verificam os limites de propriedades, campos futuros incompatíveis e identidades de origem duplicadas.

A integração gera e importa contratos SMALL, MEDIUM e LARGE, confirma propriedades JSONB, validade espacial, contagens, progresso, reimportação idempotente e preservação do banco após falha no meio de um lote. Testes unitários cobrem validação por registro, leitura limitada e cancelamento.
