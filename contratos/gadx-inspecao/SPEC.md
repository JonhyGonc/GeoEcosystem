# GADX 1.1 + GeoAutoDocs.Inspection v1 — especificação **normativa**

> **Estado:** normativa, em vigor desde 16/11/2026 (PR #13, [`DEC-ECO-0002`](../../decisoes/DEC-ECO-0002-versionamento-gadx.md)). Até o M3.3, o código **ainda não implementa** o campo `capabilities` (seção 7); todo o resto descreve o que o código já faz em `main@85be834`. Fixtures de referência: [`fixtures/`](fixtures/README.md) (M3.2).
>
> As palavras **DEVE**, **NÃO DEVE** e **PODE** são obrigatórias no sentido usual de especificação. Onde o texto diz "hoje", é uma observação sobre o código, não uma regra.

Contratos cobertos (ver [`REGISTRO.md`](../REGISTRO.md)): **CT-02** (protocolo GADX), **CT-03** (documento `GeoAutoDocs.Inspection`) e **CT-04** (correções, mensagens 6/7).

**Fontes no monorepo** (`main@85be834`):

| Assunto | GeoAutoEngine (`apps/geoautoengine/src/…`) | GeoAutoDocs (`apps/geoautodocs/src/…`) |
|---|---|---|
| Frame e mensagens | `GeoAutoEngine.Application/VideoInspection/InspectionPipeProtocol.cs` | `GeoAutoDocs.Infrastructure/InspectionPipeProtocol.cs` |
| Servidor / cliente | `GeoAutoEngine.Plugin/VideoInspection/Transport/NamedPipeInspectionServer.cs` | `GeoAutoDocs.Infrastructure/NamedPipeInspectionTransport.cs` |
| Documento | `…/VideoInspection/InspectionExchangeContracts.cs`, `InspectionExchangeSerializer.cs` | `GeoAutoDocs.Core/InspectionExchange.cs`, `GeoAutoDocs.Infrastructure/InspectionExchangeSerializer.cs` |
| Correções | `…/VideoInspection/InspectionCorrectionContracts.cs`, `VideoInspectionSession.cs` (`GetCorrections`) | `GeoAutoDocs.Core/InspectionCorrections.cs`, `GeoAutoDocs.App/MainWindow.xaml.cs` (montagem do `CorrectionAck`) |

## 1. Versões
| Nome | Onde aparece | Valor |
|---|---|---|
| Versão no fio do GADX | cabeçalho (offset 4) e campo `protocolVersion` de `Hello`/`HelloAck` | **`1`**, sem mudança |
| Revisão da especificação | só neste documento | **1.1** |
| Versão do documento de inspeção | `schemaVersion` do documento e `inspectionSchemaVersion` do `Hello`/`HelloAck` | **`1`**, sem mudança |

A revisão 1.1 é **aditiva**: só acrescenta campos opcionais. Ela **não** muda o número no fio, porque o GeoAutoEngine 3.2.0 recusa qualquer valor diferente de `1` com `IncompatibleVersion`. O que distingue um lado 1.1 de um lado 1.0 é a presença do campo `capabilities` (seção 7).

Regras de evolução:
1. Um campo novo e opcional num payload JSON **não** muda a versão no fio. Quem lê **DEVE** ignorar campos que não conhece. Hoje os dois lados já fazem isso, porque usam `System.Text.Json` sem `UnmappedMemberHandling.Disallow`.
2. Um **tipo de mensagem novo** só **PODE** ser enviado a quem declarou, em `capabilities`, a capacidade que o introduz. Hoje, um tipo desconhecido faz o leitor lançar `InvalidDataException` e derrubar a conexão, nos dois lados.
3. Uma mudança que remove ou muda o sentido de um campo exige uma versão nova no fio, ou um `schemaVersion` novo no caso do documento, além de uma nova decisão registrada.

## 2. Transporte
| Item | Regra |
|---|---|
| Canal | Named Pipe local `GeoAutoEngine.Inspection` (`InspectionPipeProtocol.DefaultPipeName`) |
| Servidor | GeoAutoEngine, dentro do Civil 3D. **1 instância** (`maxNumberOfServerInstances = 1`), modo byte, assíncrono |
| Cliente | GeoAutoDocs |
| Alcance | mesma máquina; sem ACL explícita (lacuna, seção 12) |
| Conexões | o servidor atende **uma** conexão por vez. O cliente **DEVE** liberar o canal persistente de correções antes de abrir outra conexão (hoje: `StopCurrentCorrectionSessionAsync` antes do `InspectionPush`) |

## 3. Frame
Cabeçalho de 16 bytes, little-endian, seguido do payload.

| Offset | Tipo | Campo | Regra |
|---|---|---|---|
| 0 | 4 bytes ASCII | magic | **DEVE** ser `GADX`; caso contrário, o leitor descarta a conexão |
| 4 | `u16` | versão no fio | `1` |
| 6 | `u16` | tipo da mensagem | seção 5 |
| 8 | `i32` | tamanho do payload | `0` a `16 777 216` (16 MiB); fora disso, o leitor descarta a conexão |
| 12 | `i32` | `requestId` | seção 4 |

**Payload:** JSON UTF-8 sem BOM, com as opções `JsonSerializerDefaults.Web`:
- nomes em camelCase;
- leitura sem diferenciar maiúsculas de minúsculas;
- nulos **escritos** (não omitidos);
- `Guid` como texto `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`;
- números decimais como número JSON;
- `DateTime` em ISO 8601;
- `TimeSpan` no formato `[d.]hh:mm:ss[.fffffff]`.

## 4. `requestId` e pareamento
- Toda resposta **DEVE** repetir o `requestId` do pedido que ela responde.
- O cliente gera os seus `requestId` ao acaso, entre `1` e `2³¹−1` (hoje: `Random.Shared.Next(1, int.MaxValue)`).
- O servidor numera os seus pedidos (`CorrectionPush`) em sequência, a partir de `100001`.
- O cliente **DEVE** tratar como erro de comunicação uma resposta com `requestId` diferente do esperado.
- O servidor **PODE** descartar, apenas registrando em log, um `CorrectionAck` sem pedido pendente.

## 5. Mensagens
| Tipo | Nome | Sentido | Payload | Resposta |
|---|---|---|---|---|
| 1 | `Hello` | GAD → GAE | §5.1 | `HelloAck` ou `Error` |
| 2 | `HelloAck` | GAE → GAD | §5.2 | — |
| 3 | `InspectionPush` | GAD → GAE | documento `GeoAutoDocs.Inspection` v1 (§8) | `InspectionAck` |
| 4 | `InspectionAck` | GAE → GAD | `accepted`, `status`, `message` (§10) | — |
| 5 | `Error` | GAE ⇄ GAD | `status`, `message` (§10) | — |
| 6 | `CorrectionPush` | GAE → GAD | §9 | `CorrectionAck` |
| 7 | `CorrectionAck` | GAD → GAE | §9.4 | — |

A primeira mensagem de toda conexão **DEVE** ser um `Hello`. Qualquer outra recebe `Error` com `CommunicationError`, e o servidor fecha a conexão.

### 5.1 `Hello`
| Campo | Tipo | Obrigatório | Regra |
|---|---|---|---|
| `application` | texto | sim | `"GeoAutoDocs"` |
| `applicationVersion` | texto | sim | versão do cliente. Hoje é a versão do assembly `GeoAutoDocs.Infrastructure` (`1.0.0.0`), não a do produto; o M3.4 deve trocar pela do produto |
| `protocolVersion` | inteiro | sim | `1` |
| `inspectionSchemaVersion` | inteiro | sim | `1` |
| `capabilities` | lista de texto | **não** (novo na 1.1) | capacidades que o cliente aceita (§7). Ausente = cliente 1.0 |

### 5.2 `HelloAck`
| Campo | Tipo | Obrigatório | Regra |
|---|---|---|---|
| `application` | texto | sim | `"GeoAutoEngine"` |
| `applicationVersion` | texto | sim | versão do plugin |
| `protocolVersion` | inteiro | sim | `1` |
| `inspectionSchemaVersion` | inteiro | sim | `1` |
| `cadProduct`, `cadVersion` | texto ou nulo | não | produto e versão do CAD hospedeiro |
| `capabilities` | lista de texto | **não** (novo na 1.1) | capacidades que o servidor oferece **a esta conexão** (§7). Ausente = servidor 1.0 |

O servidor **DEVE** responder `Error` com `IncompatibleVersion` quando `protocolVersion` ≠ 1 (no cabeçalho ou no payload) ou quando `inspectionSchemaVersion` ≠ 1. O cliente **DEVE** recusar um `HelloAck` com esses valores diferentes de 1.

## 6. Sessão
1. O cliente conecta, envia `Hello` e espera `HelloAck` (hoje: 1,5 s para conectar e 5 s para responder).
2. Depois do `HelloAck`, o cliente **PODE** enviar `InspectionPush` e **DEVE** esperar o `InspectionAck` correspondente.
3. O cliente que declarou `corrections` **PODE** manter a conexão aberta como canal persistente de correções. Nesse canal ele só recebe `CorrectionPush` e responde `CorrectionAck`. Se cair, ele **PODE** reconectar com um novo `Hello` (hoje: a cada 2 s).
4. O servidor só envia `CorrectionPush` numa conexão que concluiu o handshake e respeitando a §7.
5. O tempo de espera do `CorrectionAck` é decisão do servidor (hoje: 5 min, porque a resposta depende de revisão humana no GAD). Vencido o prazo, o servidor descarta o pedido e informa o usuário.

## 7. Capacidades (novo na 1.1)
`capabilities` é uma lista de identificadores em texto. Comparação **sem** diferenciar maiúsculas de minúsculas. Valores desconhecidos **DEVEM** ser ignorados. Lista vazia `[]` é diferente de campo ausente: significa "cliente 1.1 que não aceita nenhuma capacidade".

| Capacidade | Quem declara | Significado |
|---|---|---|
| `corrections` | cliente no `Hello` | o cliente entende `CorrectionPush` (6), responde `CorrectionAck` (7) e aplica as correções pela §9 |
| `corrections` | servidor no `HelloAck` | o servidor pode enviar `CorrectionPush` **nesta** conexão |

Regras do servidor 1.1:
- **S1.** Se o `Hello` traz `capabilities` **sem** `corrections`, o servidor **NÃO DEVE** enviar `CorrectionPush` nessa conexão. Ao usuário que pedir o envio de correções, ele **DEVE** mostrar uma mensagem clara (ex.: "o GeoAutoDocs conectado não aceita correções; atualize-o").
- **S2. Transição (decisão do usuário em 16/11/2026):** se o `Hello` **não traz** `capabilities` (cliente 1.0), o servidor **PODE** enviar `CorrectionPush`, como o GeoAutoEngine 3.2.0 já faz. Motivo: o GeoAutoDocs 1.1.0, instalado na equipe, aceita correções mas não sabe declará-las. O GeoAutoDocs 1.0.x, que não as aceita, usa conexão efêmera (conecta, envia, desconecta) e por isso praticamente nunca está conectado quando um envio é pedido. Tornar S2 estrito ("sem campo, sem correções") exige uma nova decisão, depois que todas as máquinas tiverem um GAD que declara capacidades.
- **S3.** O servidor **DEVE** incluir `corrections` no `HelloAck` quando puder enviar correções naquela conexão (S1/S2) e omiti-la caso contrário.

Regras do cliente 1.1:
- **C1.** O cliente que aceita correções **DEVE** enviar `capabilities: ["corrections"]`.
- **C2.** Se o `HelloAck` não traz `capabilities` (servidor 1.0), o cliente **DEVE** se comportar como hoje: manter o canal persistente e aceitar `CorrectionPush`.
- **C3.** Se o `HelloAck` traz `capabilities` sem `corrections`, o cliente **PODE** dispensar o canal persistente.

## 8. Documento `GeoAutoDocs.Inspection` v1 (CT-03)
É o payload do `InspectionPush` e também o arquivo `*.geoadocs.json`, gravado pelo GAD ao lado de cada `.docx` e importado no GAE por `GEOAUTO3VIIMPORT`. Os campos e a ordem deles são **idênticos** nas duas cópias do código (conferido em `main@85be834`).

```
InspectionExchangeDocument
├─ schema = "GeoAutoDocs.Inspection", schemaVersion = 1, generatedAtUtc
├─ project   { projectId: Guid, name }
├─ report    { reportId: Guid, reportCode, name, client, area, contract, reportDate?, totalInvestigatedMeters? }
├─ locations [ { locationId: Guid, type, name, order } ]
└─ sections  [ { sectionId: Guid, locationId, name, description?, diameter?, startPoint?, endPoint?, reference?,
                 investigatedMeters?, order,
                 videoReferences [ { videoReferenceId, displayName, fileName, order, description?, createdAt? } ],
                 occurrences [ { occurrenceId: Guid, order, locationMode, meterage?, endMeterage?, meteragePoints[],
                                 referencePoint?, position?, notes?, generatedDescription,
                                 manualDescriptionOverride?, effectiveDescription,
                                 interruptedRecording, isSevere, videoReferenceId?, videoTimestamp?,
                                 anomalies [ { anomalyTypeId, name, position, orientation, approximateExtension?,
                                               dimension?, condition, infiltrationCondition,
                                               estimatedPercentage?, notes?, customText?, order } ],
                                 evidence  [ { evidenceId, type, captureId?, videoReferenceId?, videoTimestamp?,
                                               caption?, isPrimary, order } ] } ] } ]
```

**Validação obrigatória** (hoje igual nos dois `InspectionExchangeSerializer.Validate`). Quem lê **DEVE** recusar o documento quando:
- `schema` ≠ `"GeoAutoDocs.Inspection"`: `InvalidPayload`;
- `schemaVersion` ≠ 1: `IncompatibleVersion`;
- faltar `project`, `report`, `locations` ou `sections`;
- `projectId` ou `reportId` estiver vazio;
- houver `locationId` vazio ou repetido;
- algum trecho tiver `sectionId` vazio, `locationId` inexistente ou `sectionId` repetido;
- houver `occurrenceId` vazio ou repetido no documento inteiro.

O GAE faz verificações adicionais de integridade de trecho e de ocorrência (`InspectionExchangeSerializer.cs`). Elas também recusam o documento como `InvalidPayload`.

**`locationMode`** é texto e o produtor **DEVE** escrever exatamente um destes seis valores, com estas maiúsculas: `Point`, `Range`, `EntireSection`, `NotApplicable`, `MultiPoint`, `From`. Hoje:
- o GAD grava `OccurrenceLocationMode.ToString()`;
- o GAE compara o texto exato (`VideoInspectionSession.ParseLocationMode`), e qualquer outro valor vira `Unknown`, que não é desenhado;
- na correção, o GAD aceita qualquer nome do seu enum sem diferenciar maiúsculas.

O enum do GAD tem os aliases `SpecificMeterage` (= `Point`) e `Interval` (= `Range`). O M3.3 **DEVE** garantir, por teste, que a serialização produz os nomes canônicos.

Os IDs são GUIDs gerados pelo GAD. O documento **não** tem coordenadas, CRS nem handle CAD.

## 9. Correções (CT-04)

### 9.1 `CorrectionPush`
| Campo | Tipo | Regra |
|---|---|---|
| `reportId` | Guid | relatório a que as correções pertencem |
| `generatedAtUtc` | data e hora | momento em que o GAE montou o pacote |
| `corrections` | lista | de 1 a **1000** itens (limite do GAD; acima disso, o pacote inteiro é recusado como `Failure`) |

Cada item de `corrections[]`:

| Campo | Tipo | Regra |
|---|---|---|
| `sectionId`, `occurrenceId` | Guid | obrigatórios e não vazios. O par **NÃO DEVE** se repetir no mesmo pacote |
| `locationMode` | texto | o mesmo valor recebido no documento (§8); vazio = "não informado" |
| `meterage` | decimal ou nulo | |
| `endMeterage` | decimal ou nulo | |
| `meteragePoints` | lista de decimais | |
| `description` | texto | descrição efetiva da ocorrência, até **4000** caracteres |

### 9.2 Semântica: **estado completo** (decisão R-09 (i))
- **Produtor (GAE):** **DEVE** enviar, para cada ocorrência editada, **todos** os campos acima com o valor atual completo, inclusive nulos e listas vazias. Hoje: `VideoInspectionSession.GetCorrections`.
- **Consumidor (GAD):** **DEVE** tratar cada item como o estado desejado da ocorrência, interpretando os campos conforme o `locationMode` **cadastrado** no GAD:

| `locationMode` | Campos de posição usados | Exigências (senão o item é `Invalid`) |
|---|---|---|
| `Point`, `From` | `meterage` | `meterage` presente, maior ou igual a 0. `endMeterage` nulo e `meteragePoints` vazia |
| `Range` | `meterage`, `endMeterage` | as duas presentes, maiores ou iguais a 0, e `endMeterage` ≥ `meterage`. `meteragePoints` vazia |
| `MultiPoint` | `meteragePoints` | mesma quantidade de pontos do cadastro; nenhum negativo |
| `EntireSection`, `NotApplicable` | nenhum | só a descrição é considerada |

- Se `locationMode` vier preenchido e for diferente do cadastrado no GAD, o item é `Invalid`. Uma correção **não** muda o tipo de localização.
- **Descrição:**
  - se `description`, sem espaços nas pontas, for igual à descrição efetiva atual, o GAD **NÃO DEVE** transformá-la em texto manual. O GAE reenvia a descrição mesmo quando só a metragem mudou, e virar texto manual congelaria a metragem antiga no texto;
  - se for diferente, ela passa a ser o texto manual da ocorrência;
  - se a posição mudou e a ocorrência tem anomalias, o GAD **DEVE** regenerar a descrição automática na nova metragem.
- **Tolerância:** um campo **ausente** do JSON (não nulo, ausente) **DEVE** ser tratado como "não alterar". Esse caso não ocorre com um produtor conforme. A regra existe para que um consumidor não apague dados por causa de um produtor com defeito. Hoje o GAD faz isso pelas propriedades `Has*` de `InspectionCorrection`.
- Um item sem nenhuma mudança efetiva é `Unchanged`, e conta como confirmado.
- A aplicação é atômica por pacote: se o GAD não conseguir salvar o projeto, **DEVE** desfazer todas as correções do pacote e responder `Failure`, com os itens `SaveFailed`.

### 9.3 Revisão humana
O GAD **PODE** pedir a confirmação do usuário antes de aplicar (hoje: janela de revisão). Se o usuário cancelar, a resposta é `Failure` e os itens válidos voltam como `Cancelled`.

### 9.4 `CorrectionAck`
| Campo | Tipo | Regra |
|---|---|---|
| `accepted` | booleano | `true` só quando `status` é `Success` ou `Partial` |
| `status` | texto | `Success`: todos os itens aplicáveis foram aplicados e nenhum é inválido. `Partial`: aplicou alguns, e algum item é `Invalid`/`NotFound`. `Failure`: nada foi aplicado |
| `message` | texto | mensagem para o usuário |
| `requestedCount` | inteiro | quantidade de itens recebidos |
| `appliedCount` | inteiro | itens aplicados |
| `rejectedCount` | inteiro | `requestedCount − appliedCount` (o GAD calcula) |
| `results[]` | lista | um por item: `sectionId`, `occurrenceId`, `applied`, `status`, `message` |

Valores de `results[].status`: `Applied`, `Unchanged`, `NotFound`, `Invalid`, `Cancelled`, `SaveFailed`, `NotApplied`, `Failure`.

O GAE **DEVE** considerar **confirmados** só os itens `Applied` ou `Unchanged` (ou com `applied = true`). As outras edições continuam pendentes no GAE.

## 10. Status de `Error` e `InspectionAck`
| Mensagem | `status` | Quando |
|---|---|---|
| `Error` | `IncompatibleVersion` | versão no fio ≠ 1, ou `inspectionSchemaVersion` ≠ 1 no handshake |
| `Error` | `CommunicationError` | primeira mensagem não é `Hello`, ou mensagem fora do lugar depois do handshake |
| `Error` | `InvalidPayload` | `Hello` com JSON inválido |
| `InspectionAck` | `Success` (`accepted = true`) | documento válido e recebido |
| `InspectionAck` | `IncompatibleVersion` | `schemaVersion` do documento não suportado |
| `InspectionAck` | `InvalidPayload` | documento inválido (§8) |

Quem recebe um `status` desconhecido **DEVE** tratá-lo como falha.

## 11. Compatibilidade
Depois do M3.3 e do M3.4, combinando o GeoAutoEngine (servidor) e o GeoAutoDocs (cliente):

| GAE \ GAD | GAD 1.0.x (tipos 1–5) | GAD 1.1.0 (1–7, sem `capabilities`) | GAD 1.2+ (1.1, `["corrections"]`) |
|---|---|---|---|
| **GAE 3.2.0** (1.0) | inspeção ✅; correção: conexão efêmera, envio raro, falha visível | ✅ completo (situação atual) | ✅ completo (C2: GAD ignora o `HelloAck` sem campo) |
| **GAE 3.3+** (1.1) | igual à célula acima (S2) | ✅ completo (S2) | ✅ completo, negociado (S1/S3) |

Builds do GAE anteriores à 3.2.0, que ainda estão nas máquinas com Civil 3D 2024/2025, se comportam como a linha "GAE 3.2.0", com as ressalvas de [`status/COMPATIBILIDADE.md`](../../status/COMPATIBILIDADE.md) (por exemplo, a espera síncrona de até 5 min).

Nenhuma combinação muda o comportamento atual. Instalação recomendada (M3.5): GAE primeiro, depois GAD, sem urgência entre os dois.

## 12. Lacunas que continuam (fora do M3)
- Pipe sem ACL explícita: qualquer processo do mesmo usuário pode se conectar.
- No GAE, as correções pendentes ficam só em memória.
- `applicationVersion` do GAD não é a versão do produto (corrigir no M3.4, §5.1).

## 13. Conformidade
- **M3.2:** fixtures de referência em [`fixtures/`](fixtures/README.md), geradas por `ferramentas/gerar_fixtures.py`. Frames binários e JSON de cada mensagem, inclusive `Hello` com e sem `capabilities`, `HelloAck` com e sem `capabilities`, `CorrectionPush` com os modos `Point`, `Range` e `MultiPoint`, e um `CorrectionAck` `Partial`. Lidas pelos testes `GadxContractFixtureTests` das duas aplicações.
- **M3.3/M3.4:** a biblioteca `libs/GeoEcosystem.Contracts.Inspection` e os dois lados testados contra as mesmas fixtures na CI.

## 14. Histórico
| Revisão | Data | Mudança |
|---|---|---|
| descritiva | 10/11/2026 | retrato do código (Etapa 2), com duas variantes sob a versão "1" |
| **1.1** | 16/11/2026 | passa a ser normativa; acrescenta `capabilities` no `Hello`/`HelloAck` (opcional); define correções como estado completo; regra de transição S2 |
