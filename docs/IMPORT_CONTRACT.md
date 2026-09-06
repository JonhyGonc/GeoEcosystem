# Contrato de importação GeoMap

Status: rascunho conceitual v0.1. A especificação executável será fechada nas Issues #14 e #15.

## Objetivo

Separar a origem Autodesk do restante do sistema. Qualquer exportador deve produzir o mesmo contrato lógico, independentemente de a geometria vir de Polyline, Feature Line, Pipe Network ou outra estrutura.

## Estrutura lógica

### Metadados da importação

| Campo | Obrigatório | Descrição |
|---|---:|---|
| contractVersion | sim | Versão do contrato, inicialmente 0.1 |
| sourceSystem | sim | Sistema de origem, por exemplo Civil3D |
| sourceCrs | sim | Identificador inequívoco do CRS |
| project | sim | Referência ou dados mínimos do projeto |
| network | sim | Referência ou dados mínimos da rede |
| exportedAt | sim | Data e hora UTC |
| units | sim | Unidade linear declarada |

### Feição

| Campo | Obrigatório | Descrição |
|---|---:|---|
| sourceId | recomendado | Identificador estável na origem |
| sourceType | sim | Tipo original do objeto |
| sourceLayer | recomendado | Layer original |
| geometry | sim | Geometria em representação interoperável |
| properties | sim | Objeto de atributos variáveis |

Exemplo lógico:

~~~json
{
  "contractVersion": "0.1",
  "sourceSystem": "Civil3D",
  "sourceCrs": "EPSG:31982",
  "units": "metre",
  "project": { "externalId": "project-001", "name": "Projeto exemplo" },
  "network": { "externalId": "network-001", "name": "Drenagem", "type": "drainage" },
  "exportedAt": "2026-09-06T00:00:00Z",
  "features": [
    {
      "sourceId": "cad-handle-or-guid",
      "sourceType": "Polyline",
      "sourceLayer": "REDE-DRENAGEM",
      "geometry": {
        "type": "LineString",
        "coordinates": [[0, 0], [10, 10]]
      },
      "properties": {
        "diameter": 300,
        "material": "PVC"
      }
    }
  ]
}
~~~

Os valores acima são ilustrativos e não representam um dataset válido para posicionamento real.

## Transporte

O modelo lógico não deve obrigar o importador grande a carregar um FeatureCollection inteiro.

Representações planejadas:

- documento JSON simples para testes e datasets pequenos;
- metadados mais NDJSON de feições para streaming;
- pacote versionado contendo metadados e dados, se necessário.

A Issue #14 escolherá a representação inicial sem alterar o significado dos campos.

## Validação mínima

Uma importação deve rejeitar ou isolar:

- versão desconhecida;
- CRS ausente ou inválido;
- coordenadas não finitas;
- geometria vazia ou tipo não suportado;
- feição sem rede;
- propriedades acima dos limites definidos;
- identificadores duplicados conforme a política de reimportação.

O resultado deve informar totais recebidos, importados, rejeitados e erros por categoria.

## Normalização

- sourceType e sourceLayer preservam rastreabilidade.
- geometry é convertida para o modelo espacial GeoMap.
- properties mantém atributos ainda não padronizados.
- nomes de rede e categorias podem ser mapeados por configuração.
- nenhuma classe Autodesk atravessa o limite do exportador.

## Questões pendentes

- CRS canônico de armazenamento;
- preservação ou descarte de Z;
- limite de tamanho por propriedade e feição;
- política de atualização e reimportação;
- atomicidade por arquivo, lote ou rede;
- formato inicial de pacote;
- códigos normalizados de tipos de rede.
