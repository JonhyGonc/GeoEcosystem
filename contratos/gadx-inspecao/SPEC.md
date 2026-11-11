# GADX + GeoAutoDocs.Inspection — especificação **descritiva**

> **Estado: descritiva, não normativa.** Este documento descreve o protocolo **como ele está implementado** em 10/11/2026. Ele não define como o protocolo deve ser. Dono, versão normativa e evolução dependem da decisão **D-02** (ver [`decisoes/`](../../decisoes/README.md)). Até lá, mudanças no protocolo continuam exigindo aprovação do Chat 00 (regra 5).

**Fontes** (código duplicado nos dois repositórios):
- GAE: `GeoAutoEngine@3acab63:src/GeoAutoEngine.Application/VideoInspection/InspectionPipeProtocol.cs`, `InspectionExchangeContracts.cs`, `InspectionCorrectionContracts.cs`; servidor em `src/GeoAutoEngine.Plugin/VideoInspection/Transport/NamedPipeInspectionServer.cs`; doc `docs/VIDEO_INSPECTION_CORRECTIONS.md`.
- GAD: `GeoAutoDocs@073191a` e `@5d333d6`: `src/GeoAutoDocs.Infrastructure/InspectionPipeProtocol.cs`, `NamedPipeInspectionTransport.cs`, `src/GeoAutoDocs.Core/InspectionExchange.cs`, `InspectionCorrections.cs` (só na fix).
- Auditorias: GAE-09/10, GAD-12b/13/14 e a verificação do Chat 00 na Etapa 2.

## 1. Transporte
| Item | Valor |
|---|---|
| Canal | Named Pipe local `GeoAutoEngine.Inspection` |
| Servidor | GAE (dentro do AutoCAD/Civil 3D), **1 instância**, modo byte, assíncrono, sem `PipeSecurity` explícita |
| Cliente | GAD |
| Alcance | mesma máquina |

## 2. Frame (cabeçalho de 16 bytes, little-endian)
| Offset | Tipo | Campo |
|---|---|---|
| 0 | 4 bytes ASCII | magic `GADX` |
| 4 | `u16` | versão do protocolo (`1`) |
| 6 | `u16` | tipo da mensagem |
| 8 | `i32` | tamanho do payload em bytes (máx. 16 MiB) |
| 12 | `i32` | `requestId` (as respostas repetem o do pedido; os pedidos do servidor começam em 100001) |

Payload: JSON UTF-8, `JsonSerializerDefaults.Web` (camelCase; nulos serializados). Um tipo de mensagem desconhecido faz o leitor lançar `InvalidDataException` **nos dois lados**.

## 3. Mensagens
| Tipo | Nome | Sentido | Payload |
|---|---|---|---|
| 1 | `Hello` | GAD → GAE | `application`, `applicationVersion`, `protocolVersion`, `inspectionSchemaVersion` |
| 2 | `HelloAck` | GAE → GAD | os mesmos campos + `cadProduct`, `cadVersion` |
| 3 | `InspectionPush` | GAD → GAE | documento `GeoAutoDocs.Inspection` v1 (seção 4) |
| 4 | `InspectionAck` | GAE → GAD | `accepted`, `status`, `message` |
| 5 | `Error` | GAE → GAD | `status`: `IncompatibleVersion` / `CommunicationError` / `InvalidPayload` |
| 6 | `CorrectionPush` | GAE → GAD | `reportId`, `generatedAtUtc`, `corrections[]` (seção 5) |
| 7 | `CorrectionAck` | GAD → GAE | `accepted`, `status`, `message`, contagens, `results[]` |

**Handshake:** o GAE só aceita `protocolVersion = 1` e `inspectionSchemaVersion = 1`. **Não há negociação de capacidade:** o `Hello` não declara quais tipos o cliente entende.

## 4. Documento `GeoAutoDocs.Inspection` v1
Também é usado como arquivo `.geoadocs.json`: sidecar do `.docx` gerado pelo GAD, importado no GAE por `GEOAUTO3VIIMPORT`.

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
Os IDs são GUIDs v4 gerados pelo GAD. O documento **não tem** coordenadas, CRS nem handle CAD.

## 5. Correções (tipos 6/7)
Cada item de `corrections[]`: `sectionId`, `occurrenceId`, `locationMode`, `meterage`, `endMeterage`, `meteragePoints`, `description`.

**Semântica real (verificada na Etapa 2):**
- O GAE envia o **estado completo** de cada ocorrência editada, com todos os campos, inclusive nulos (`VideoInspectionSession.GetCorrections`).
- O GAD (fix) marca `HasX = true` sempre que o setter é chamado. O desserializador chama o setter de toda propriedade presente, inclusive `null`. Portanto, **com o produtor atual, todos os `Has*` são sempre verdadeiros**, e a semântica de "campo ausente = não alterado" nunca é exercitada.
- Efeito ao aplicar (sobrescrever com nulo ou vazio): **hipótese, sem teste cruzado.**

## 6. Variantes em uso (mesmo número de versão)
| Implementação | Tipos suportados | Conexão do cliente |
|---|---|---|
| GAE `main@3acab63` e branches | 1–7 | servidor; espera síncrona de até 5 min pelo `CorrectionAck` na `main` (corrigida só em `sync-section-names`) |
| GAD `main@073191a` | 1–5 | efêmera (conecta, envia, desconecta) |
| GAD `fix/section-edit-save-stability@5d333d6` | 1–7 | persistente; liberada antes de cada push (o servidor tem 1 instância) |

Combinações por máquina: [`status/COMPATIBILIDADE.md`](../../status/COMPATIBILIDADE.md).

## 7. Lacunas conhecidas
- Sem negociação de capacidade nem versão distinta para 6/7 (ruptura silenciosa, regra 8).
- Código do contrato duplicado nos dois repositórios, sem teste cruzado.
- Sem ACL no pipe.
- Correções pendentes só em memória no GAE.
