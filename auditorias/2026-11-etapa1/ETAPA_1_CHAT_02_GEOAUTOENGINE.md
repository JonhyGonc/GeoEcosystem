# Etapa 1 — Auditoria inicial do GeoAutoEngine (Chat 02)

## 1. Identificação
- **Agente:** Chat 02 — especialista GeoAutoEngine
- **Repositório:** `JonhyGonc/GeoAutoEngine` (público)
- **Commits auditados:** `main` @ `3acab634ebceb17ec41f256244c3da56e092e6c9` (23/09/2026); `sync-section-names` @ `2937469` (13/10/2026, contém `fix/correction-export-feedback` @ `c2c10e4`); `feature/atualizar-textos-anomalias` @ `1724528` (22/10/2026)
- **Leitura cruzada (somente leitura):** `Geomap@b9be25c` (contrato de importação, Issue #48); `GeoAutoDocs@073191a` e `GeoAutoDocs@5d333d6` (cliente do pipe)
- **Data / corte:** 10/11/2026
- **Estado:** auditoria, **somente leitura**
- **Escopo:** Q1–Q8 e GAE-A…GAE-F do documento `governanca/ETAPA_1_COORDENACAO_AUDITORIA.md`

### Verificação de acesso (regra 2)
| Recurso | Resultado |
|---|---|
| Código e histórico (git) dos 3 repositórios | OK — clone e fetch de todas as branches |
| Issues, PRs e CI do GeoAutoEngine | **Bloqueado.** `gh api` respondeu HTTP 403 ("GitHub access to this repository is not enabled for this session"). O pedido de anexar com credenciais foi negado pela política da sessão. |
| Issues do GeoMap (leitura) | OK (Issue #48 consultada) |
| `dotnet` / Autodesk | Indisponíveis na sessão. Nenhum build ou teste foi executado. |

**Commits mais novos que a referência:** nenhum. A `main` continua em `3acab63`, e as branches auditadas continuam nos SHAs indicados.

## 2. Objetivo e critérios de aceitação
- **Objetivo:** documentar o comportamento real do GeoAutoEngine e de suas integrações, como base para o roadmap.
- **Critérios:**
  - Q1–Q8 e GAE-A…F respondidos;
  - achados com ID, classe, referência `repo@sha7:caminho#Lx` e confiança;
  - nenhuma alteração em repositório;
  - item 10 com até 40 linhas.

## 3. Evidências (por pergunta)

### Q1 — Stack, build, testes e CI
- **Versão:** 3.1.1 (`GeoAutoEngine@3acab63:Directory.Build.props#L10`).
- **SDK:** .NET 8.0.415 (`global.json`).
- **Pacotes:** gestão central (`Directory.Packages.props`): xUnit 2.9.3, System.Text.Json 8.0.6.
- **Projetos:**
  - Domain, Application e Csv: `net8.0`, ou `netstandard2.0` quando o host é 2024;
  - Cad2026 e Plugin: `net8.0-windows` (2025/2026) ou `net48` (2024) (`Directory.Build.props#L21-L22`).
  - Apesar do nome, o projeto `Cad2026` atende aos três hosts.
- **API usada:** só AutoCAD .NET (AcCoreMgd, AcDbMgd, AcMgd, AcWindows, AdWindows: `Directory.Build.targets`). Não há referência a AeccDbMgd (Civil 3D). O bundle declara `AutodeskProduct="AutoCAD"` (`build/GeoAutoEngine.bundle/PackageContents.xml#L4`) e carrega também no AutoCAD puro.
- **Build:** um build por host, com `-p:AutodeskAutoCADVersion=2024|2025|2026`. Exige as DLLs Autodesk locais (`Directory.Build.targets`, alvo `ValidateAutodeskAutoCADReferences`).
- **Testes:**
  - 203 métodos `[Fact]/[Theory]` em 26 arquivos (`tests/GeoAutoEngine.Tests`);
  - o projeto de testes referencia só Domain, Application e Csv;
  - suítes em `scripts/test.ps1` (fast, regression, legacy, cad, full).
- **CI:**
  - `.github/workflows/geomap-export.yml` e `video-inspection.yml`, em ubuntu com .NET 8;
  - disparam só em PR/push que tocam certos caminhos;
  - rodam só classes filtradas.
  - **Status da CI: Lacuna (API bloqueada).**

### Q2 / GAE-A — Branches e versões
- **Fora da `main` há apenas duas branches.** As demais estão contidas nela (`ahead=0`): `ChatGPT`, `backup-main`, `feat/geomap-polyline-export`, `diag/video-inspection-json-performance`, `fix/video-inspection-callouts` e `feat/palette-redesign`.
- **`sync-section-names`** (9 commits, de 23/09 a 13/10):
  - exportação de correções pela paleta, sem travar a interface (`513286a`, `559d8b1`, `c2c10e4`);
  - novo comando `GEOAUTO3VISYNC`, que sincroniza nomes e diâmetros dos trechos (`67e1fd1`…`2937469`).
  - Muda só `Plugin` e `Cad2026`. O `PackageContents.xml` **não** recebeu `GEOAUTO3VISYNC`.
- **`feature/atualizar-textos-anomalias`** (2 commits, 22/10):
  - `GEOAUTO3VIUPDATETEXTS`, `InspectionTextRefreshPlanner` e `MTextContentMerger`;
  - +10 testes;
  - o comando foi incluído no `PackageContents.xml`.
- **As duas partem de `3acab63` e conflitam entre si** (`git merge-tree`) em 5 arquivos: `AutoCadInspectionRenderer.cs`, `GeoAutoEngineUi.cs`, `VideoInspectionController.cs`, `VideoInspectionRuntime.cs` e `VideoInspectionViewModel.cs`.
- Nenhuma das duas altera o protocolo do pipe, o schema ou o exportador GeoMap.
- **Versão nunca alterada:**
  - a versão `3.1.1` e o `AppVersion` estão no commit inicial `427ccac` (25/08) e nunca mudaram em 210 commits (`git log -G'<Version>'`);
  - o log de inicialização grava só `AssemblyVersion` (`src/GeoAutoEngine.Plugin/PluginEntry.cs#L31-L33`).
- **Distribuição:**
  - um gerador portátil compila na máquina do desenhista a partir de um snapshot do código (`tools/GeoAutoEngineBundleBuilder.cs#L60-L98`), ou é usado `scripts/deploy.ps1`;
  - o destino é `%AppData%\Autodesk\ApplicationPlugins`;
  - o commit usado não é registrado.
  - **Portanto, não há evidência de qual build está instalada na equipe (Lacuna → usuário).**
- **README × COMPATIBILITY:**
  - o `README.md#L3` diz "AutoCAD 2026 e Civil 3D 2026" e está desatualizado;
  - `COMPATIBILITY.md#L3-L5`, `PackageContents.xml#L13,L41,L69` (R24.3/R25.0/R25.1) e `Directory.Build.props` suportam 2024, 2025 e 2026;
  - no 2024, o smoke em CAD é declarado pendente.
- **`../GeoAutoEngine.Legacy` e `../reference`:**
  - **não** são necessários para o build, para as suítes `fast`/`regression` nem para a CI;
  - são necessários para `scripts/test.ps1 -Suite legacy|full` (`#L54-L60`), `tools/update-reference-manifest.ps1#L4-L5` e os oráculos `tools/capture-*.py`;
  - os scripts esperam o .NET portátil em `.\.dotnet\dotnet.exe` (`scripts/test.ps1#L12`), que não é versionado.

### Q3 — Modelo de dados e persistência
- Não há banco de dados. A persistência fica no próprio DWG e em arquivos locais.

**No DWG (XData):**
| Aplicação registrada | Usada por | Conteúdo |
|---|---|---|
| `GEOAUTOENGINE` | entidades do fluxo CSV | `CODIGO`, `ORIGEM`, `REGISTRO`, `TIPO`, `SOURCE_FILE`, `SOURCE_LINE` (`src/GeoAutoEngine.Application/DrawingPlanBuilder.cs#L69,L643-L655`) |
| `GEOAUTOENGINE_VI` | anotações de vídeo | `SCHEMA_VERSION`, `REPORT_ID`, `SECTION_ID`, `OCCURRENCE_ID`, `STABLE_KEY` etc. (`src/GeoAutoEngine.Cad2026/VideoInspection/AutoCadInspectionRenderer.cs#L800-L825`) |
| `GEOAUTOENGINE_VI_COVERAGE_V3` | coberturas | lê também a V2 (`AutoCadInspectionCoverageController.cs#L29-L32`) |

**No DWG (dicionários NOD):**
- `GEOAUTOENGINE_VIDEO_INSPECTION_ASSOCIATIONS` (`AutoCadInspectionAssociationStore.cs#L8`);
- `GEOAUTOENGINE_VIDEO_INSPECTION_COVERAGE_V3`;
- evolução por `STORAGE_VERSION` e por nomes com sufixo de versão.

**Em memória:**
- a sessão de inspeção (relatório, trechos, ocorrências) e as correções ainda não confirmadas (`src/GeoAutoEngine.Plugin/VideoInspection/VideoInspectionRuntime.cs#L28`).

**Em arquivos locais:**
- configurações da paleta em `%AppData%` (`ViewModels/PaletteSettings.cs#L19`);
- log em `%LocalAppData%` (`PluginLog.cs#L10`).

### Q4 / GAE-D — Identificadores
- **Associação trecho ↔ Polyline:**
  - fica em `CadSectionAssociation(ReportId, SectionId, GeometryHandle, Direction, LastKnownGeometryLength, StorageVersion)` (`src/GeoAutoEngine.Application/VideoInspection/VideoInspectionSession.cs#L473-L479`);
  - **é persistida no DWG** num Xrecord com chave `R_{reportId}_S_{sectionId}` (`AutoCadInspectionAssociationStore.cs#L29-L36,L163-L164`);
  - o handle é resolvido por `Database.GetObjectId` (`#L119-L137`).
- **Ao reabrir o desenho:**
  - o Xrecord sobrevive a salvar e reabrir;
  - **mas só é reidratado quando a inspeção chega de novo** (push do GeoAutoDocs ou arquivo `.geoadocs.json`) (`VideoInspectionRuntime.cs#L255-L270,L316-L352`).
  - Sem a sessão, o DWG contém só o vínculo, sem o conteúdo do relatório.
- **Ocorrências:**
  - o `OccurrenceId` não se associa a handle; vai no XData das anotações.
  - As correções de metragem e descrição pendentes ficam só em memória.
- **Limites da associação:**
  - o handle é gravado como texto (`DxfCode.Text`), não como ponteiro, e por isso não é traduzido em WBLOCK, INSERT ou cópia entre DWGs;
  - `LastKnownGeometryLength` é gravado mas só exibido (`VideoInspectionViewModel.cs#L363`), então uma edição da geometria não é detectada.
- **BREAK de cobertura:** a Polyline original mantém o handle com a parte inspecionada, e o restante vira uma Polyline nova (`AutoCadInspectionCoverageController.cs#L20-L23,L172-L181`).
- **Chaves do fluxo CSV:**
  - `REGISTRO = SourceFileId:índice:tipo:regra:discriminador`, em que `SourceFileId` vem do **caminho absoluto + SHA-256 do CSV** (`src/GeoAutoEngine.Domain/Identifiers.cs#L32-L38`; `src/GeoAutoEngine.Csv/CsvSourceImporter.cs#L74`; `DrawingPlanBuilder.cs#L640-L641`).

### Q5 — Contratos
1. **GeoMap Import Contract 0.1** (produtor): `GEOAUTO3GEOMAPEXPORT` (GAE-B).
2. **Pipe GADX v1 + `GeoAutoDocs.Inspection` v1** (servidor e consumidor), duplicado no GeoAutoDocs (GAE-C).
3. **Arquivo `.geoadocs.json`:** mesmo schema, importado por `GEOAUTO3VIIMPORT` (`PluginEntry.cs#L199-L202`).
4. **Importação Multi-CSV de levantamento:** `docs/IMPORT_BATCH_CONTRACT.md`.
5. **Contrato implícito no DWG:** nomes de aplicações registradas, dicionários e `STORAGE_VERSION` (Q3).
- O GeoMap cita a PR #3 do GeoAutoEngine (`Geomap@b9be25c:docs/CIVIL3D_EXPORTER.md#L7`), mas ela não foi verificada (API bloqueada).

### GAE-B — Exportador GeoMap
- **Escopo real:**
  - seleção manual filtrada por `LWPOLYLINE` (`src/GeoAutoEngine.Plugin/GeoMapExportCommands.cs#L108-L111`);
  - só `Polyline` (`src/GeoAutoEngine.Cad2026/GeoMapExport/AutoCadGeoMapPolylineReader.cs#L29-L32`);
  - **qualquer bulge cancela o arquivo inteiro** (`#L63-L75`);
  - o Z é descartado, com `projectedTo2D` e `cadElevation` como metadados (`#L44-L57`).
- **Contrato e limites:**
  - `ContractVersion = "0.1"` fixo, com limites 10.000/100.000 iguais aos do contrato (`src/GeoAutoEngine.Application/GeoMapExport/GeoMapExportSerializer.cs#L9-L12`);
  - `properties` contém só metadados CAD (`#L189-L195`).
- **Metadados da rede:**
  - CRS digitado à mão, sem padrão nem verificação de coerência (`GeoMapExportCommands.cs#L50,L175-L181`);
  - uma rede por arquivo, inferida do layer (`#L138-L150`).
- **Lacunas em relação ao contrato GeoMap:**
  - **0.2:** não lê a cor da layer e não emite `network.color`. Além disso, a cor é por rede, mas a seleção pode misturar layers.
  - **0.3:** blocos/INSERT não são selecionáveis e não há `Point`/`MultiPoint` nem ATTRIB.
  - **Issue #48:** a tesselação foi atribuída ao exportador definitivo ([Geomap #48](https://github.com/JonhyGonc/Geomap/issues/48)), mas hoje o GAE rejeita toda a seleção, o que é mais restritivo que a ponte DXF (que rejeita por camada).
  - Também ficam de fora Polyline2d/3d, Line e Arc, além de atributos técnicos (diâmetro e material; #48 cita o layer `RD_<diâmetro>_<material>`).
- **Fixture:**
  - `samples/geomap/polyline-export.geomap.json` é idêntica à fixture do GeoMap (JSON normalizado);
  - **nenhum teste do GAE lê esse arquivo** (`tests/GeoAutoEngine.Tests/GeoMapExportTests.cs`: 4 testes que só verificam campos).

### GAE-C — Protocolo GADX v1 (documentado a partir do código)
**Transporte:**
- Named Pipe `GeoAutoEngine.Inspection`;
- servidor no GAE, com **1 instância**, modo byte e sem `PipeSecurity` explícita (`src/GeoAutoEngine.Plugin/VideoInspection/Transport/NamedPipeInspectionServer.cs#L219-L224`).

**Frame:**
| Bytes | Conteúdo |
|---|---|
| 0–3 | `GADX` |
| 4 | `u16` versão |
| 6 | `u16` tipo |
| 8 | `i32` tamanho do payload |
| 12 | `i32` requestId |

- Little-endian, com cabeçalho de 16 bytes.
- Payload JSON UTF-8 em camelCase (`JsonSerializerDefaults.Web`), máximo de 16 MiB.
- Tipo desconhecido gera `InvalidDataException` (`src/GeoAutoEngine.Application/VideoInspection/InspectionPipeProtocol.cs#L6-L15,L25-L29,L87-L90`).

**Mensagens:**
| Tipo | Sentido | Conteúdo |
|---|---|---|
| 1 `Hello` | cliente → GAE | `application`, `applicationVersion`, `protocolVersion`, `inspectionSchemaVersion` |
| 2 `HelloAck` | GAE → cliente | mesmos campos + `cadProduct`, `cadVersion` |
| 5 `Error` | GAE → cliente | `status`: `IncompatibleVersion` / `CommunicationError` / `InvalidPayload` (`Server#L287-L337`) |
| 3 `InspectionPush` | cliente → GAE | documento `GeoAutoDocs.Inspection` v1 (`InspectionExchangeContracts.cs`) |
| 4 `InspectionAck` | GAE → cliente | `accepted`, `status`, `message` (`Server#L416-L456`) |
| 6 `CorrectionPush` | **GAE → cliente** | `reportId`, `generatedAtUtc`, `corrections[sectionId, occurrenceId, locationMode, meterage, endMeterage, meteragePoints, description]` |
| 7 `CorrectionAck` | cliente → GAE | `accepted`, `status`, `message`, contagens, `results[]`; mesmo `requestId` (requestIds do servidor a partir de 100001; timeout de 5 min) (`InspectionCorrectionContracts.cs`; `Server#L129-L204,L458-L491`) |

- O handshake só é aceito com protocolo 1 e schema 1.

**Negociação de capacidade:**
- **Não existe.** O `Hello` só traz as versões.
- As mensagens 6/7 entraram sob a mesma versão 1 (`docs/VIDEO_INSPECTION_CORRECTIONS.md#L15-L18`).

**Cliente GeoAutoDocs `main` (sem as mensagens 6/7):**
- Conecta, envia e desconecta a cada operação (`GeoAutoDocs@073191a:src/GeoAutoDocs.Infrastructure/NamedPipeInspectionTransport.cs#L28-L33,L57-L59`).
- **Caso normal:** o GAE quase nunca vê a conexão pronta e recusa o envio com "O GeoAutoDocs não está conectado" (`Server#L148-L153`).
- **Corrida:** se o `CorrectionPush` sair durante uma conexão curta, o cliente lê o tipo 6 e falha com `InvalidDataException` (`GeoAutoDocs@073191a:…/InspectionPipeProtocol.cs#L64-L65`). Ele então reporta `CommunicationError` (`NamedPipeInspectionTransport.cs#L104-L107`) para uma inspeção que o GAE já aceitou (`Server#L423-L424`), e o GAE espera o `CorrectionAck` até o timeout.
- Na `main` do GAE, essa espera bloqueia a interface do Civil 3D por até 5 min (`src/GeoAutoEngine.Plugin/VideoInspection/VideoInspectionCorrectionRuntime.cs#L106-L110`). Isso foi corrigido só em `sync-section-names` (`559d8b1`).

**Cliente GeoAutoDocs `fix/section-edit-save-stability`:**
- mantém uma sessão persistente para receber correções;
- como o servidor só tem 1 instância, essa sessão precisa ser liberada antes de cada push (`GeoAutoDocs@5d333d6:…/NamedPipeInspectionTransport.cs#L90-L93`).

### GAE-E — Autoridade sobre os dados
**Nascem no GAE** (interpretação de CSV de campo):
- textos de profundidade, diâmetro e código;
- blocos (PV, registro, poste, hidrante);
- contornos de caixas e grelhas;
- alertas;
- anotações e coberturas de vídeo;
- correções de metragem e descrição (devolvidas ao GeoAutoDocs);
- o arquivo GeoMap.

**Vêm de fora:**
- coordenadas, códigos, profundidades e diâmetros do levantamento (CSV);
- **as Polylines da rede, desenhadas pelo projetista** (o GAE lê, associa e, com BREAK, divide; `DrawingItemKind` não tem rede: `src/GeoAutoEngine.Domain/DrawingPlan.cs#L87-L94`);
- inspeção, trechos, ocorrências, anomalias, nomes e cliente (GeoAutoDocs).

### GAE-F / Q7 — Dados em repositório público (só caminhos)
**Provavelmente reais:**
- `docs/audit/cad_gis_raw/GF-02-EP01-18-530.26-001.raw.json` e `docs/audit/cad_gis_raw/GF-02-EP01-22-530.26-001.raw.json`: auditoria bruta de DWG, com milhares de coordenadas tipo UTM, textos de atributos e `source_path` com pasta de usuário Windows;
- `docs/audit/cad_gis_inventory.json`: coordenadas agregadas;
- `docs/audit/RELATORIO_AUDITORIA_CAD_GIS.md`: sem coordenadas detectadas; não revisado a fundo.

**Caminhos locais de usuário:**
- `docs/evidence/environment.json`;
- `docs/evidence/stage1/cad/plugin_probe_2026_acad.json`;
- `docs/evidence/stage1/cad/plugin_probe_2026_c3d.json`.

**Não abertos (sem ferramenta DWG) — Lacuna:**
- `docs/evidence/cad/*.dwg`;
- `docs/evidence/stage4/cad/*.dwg`.

**Sintéticos:**
- `samples/*.csv`;
- `samples/geomap/polyline-export.geomap.json`;
- `tests/GeoAutoEngine.Tests/Fixtures/sample.geoadocs.json`.

- Todos esses arquivos estão versionados desde `427ccac`. Não há exclusões no histórico.

### Q6 — Testes
- **Executado:** nada (não há `dotnet` na sessão).
- **Por branch:** 203 testes na `main` e em `sync-section-names`; 213 em `feature/atualizar-textos-anomalias`.
- **Sem teste automatizado** (só smokes PowerShell que exigem Civil 3D local):
  - o servidor do pipe;
  - o leitor CAD do exportador;
  - o store de associações;
  - BREAK e coberturas;
  - o renderer.
- **Sem teste cruzado:** contra o validador do GeoMap e contra o cliente do GeoAutoDocs.

### Q8 — Riscos e inconsistências
- Consolidados nos itens 4 (achados GAE-01…17) e 8 (riscos e decisões).

## 4. Achados — real × esperado

| ID | Achado | Classe | Confiança |
|---|---|---|---|
| GAE-01 | Versão 3.1.1 nunca alterada; a build instalada não é identificável por evidência | Fato / Lacuna | alta |
| GAE-02 | O README diz só 2026; o código e o bundle suportam 2024–2026; o 2024 não tem smoke CAD; não há API Civil 3D | Fato | alta |
| GAE-03 | Legacy/reference só para as suítes legacy/full e os oráculos; o build e a CI não dependem deles | Fato | alta |
| GAE-04 | Duas branches não mescladas, que conflitam entre si; `GEOAUTO3VISYNC` está fora do `PackageContents` | Fato | alta |
| GAE-05 | `main` bloqueia a interface até 5 min ao exportar correções; correção só fora da `main` | Fato | alta |
| GAE-06 | Exportador: só LWPOLYLINE reta, 0.1, sem atributos técnicos, CRS manual; um arco cancela tudo | Fato | alta |
| GAE-07 | Faltam 0.2 (cor), 0.3 (blocos), tesselação/arcos (#48) e outros tipos de entidade | Fato | alta |
| GAE-08 | A fixture GeoMap no GAE não é testada; a sincronização com o GeoMap depende de cópia manual | Fato | alta |
| GAE-09 | GADX v1 documentado acima; o contrato está duplicado no GeoAutoDocs | Fato | alta |
| GAE-10 | Sem negociação de capacidade; 6/7 sob a mesma versão; corrida com o cliente da `main` do GeoAutoDocs | Fato (código) / Hipótese (execução) | alta / média |
| GAE-11 | Associação persistida no DWG, mas o conteúdo da inspeção e as correções ficam só em memória | Fato | alta |
| GAE-12 | Handle como texto, edição de geometria não detectada, BREAK divide a Polyline (afeta o `sourceId` do GeoMap) | Fato / Hipótese (efeito no GeoMap) | alta / média |
| GAE-13 | Autoridade compartilhada: o GAE corrige metragem/descrição do GeoAutoDocs; a geometria da rede nasce com o projetista | Fato / interpretação | média |
| GAE-14 | `REGISTRO` depende do caminho do CSV: mover o arquivo pode duplicar entidades ao reprocessar | Fato (código) / Hipótese (efeito) | média |
| GAE-15 | Dados de projeto real e caminhos de usuário em repositório público | Hipótese forte (não aberto em detalhe) | alta |
| GAE-16 | CAD, Plugin e pipe sem teste automatizado; CI filtrada; status da CI desconhecido | Fato / Lacuna | alta |
| GAE-17 | Pipe sem ACL explícita nem autenticação, com nome fixo (risco de outro processo ocupar o nome) | Hipótese | média |

## 5. Alterações feitas
- **Nenhuma nos repositórios:** sem branch, commit, Issue, PR, comentário, label ou release.
- Só clones e fetches locais nesta sessão.
- Este relatório foi salvo no Project GeoEcosystem como `governanca/auditorias/ETAPA_1_CHAT_02_GEOAUTOENGINE.md`.

## 6. Testes e validações
**Executado:**
- `git` (log, merge-base, rev-list, `merge-tree`, diff --stat, `log -G`);
- leitura de código e documentação;
- comparação JSON das fixtures;
- inspeção estrutural dos arquivos de dados (chaves e contagens, sem copiar valores);
- `gh api` na Issue #48 do GeoMap.

**Não executado:**
- build, testes xUnit e smokes no Civil 3D. Não há `dotnet` nem Autodesk na sessão.

**Não acessível:**
- Issues, PRs e CI do GeoAutoEngine (403).

**Nada foi validado no Civil 3D.**

## 7. Impacto em outros projetos e contratos
- **GeoMap:**
  - o exportador cobre só 0.1;
  - para 0.2/0.3/#48 é preciso decidir quem tessela e como mapear layer → atributos;
  - o BREAK e o handle como `sourceId` afetam a identidade das feições na reimportação.
- **GeoAutoDocs:**
  - o GADX v1 tem variantes incompatíveis;
  - a `main` do GeoAutoDocs não recebe correções;
  - a branch do GeoAutoDocs disputa a única instância do pipe;
  - a semântica de `description` (sempre enviada pelo GAE) × `HasDescription` (GeoAutoDocs) deve ser verificada pelo Chat 03.
- **Contratos sem dono formal:** GADX/Inspection e as chaves XData/NOD do DWG.

## 8. Riscos, dependências e decisões necessárias
**Perguntas ao usuário:**
- Qual build está instalada? Dá para verificar sem o código: no Civil 3D, ver se os comandos `GEOAUTO3VISYNC` e `GEOAUTO3VIUPDATETEXTS` existem, e a data de `%AppData%\Autodesk\ApplicationPlugins\GeoAutoEngine.bundle\Contents\<ano>\GeoAutoEngine.Plugin.dll`.
- Os arquivos de GAE-15 são de cliente? Torná-los privados ou limpar o histórico é operação destrutiva e exige autorização humana.

**Decisões para o Chat 00:**
- dono e versionamento do GADX (negociação de capacidade × v2);
- ordem de merge das duas branches (conflito);
- escopo do exportador definitivo (#48, 0.2, 0.3);
- política de identidade (handle × GUID persistido em XData).

**Riscos:**
- interface bloqueada na `main`;
- perda de correções não sincronizadas ao fechar o Civil 3D;
- duplicação ao reprocessar CSV movido;
- exposição de dados.

## 9. Próximo passo
O usuário informa a build instalada (verificação acima). Em seguida, o Chat 00 decide a ordem de merge das branches `sync-section-names` e `feature/atualizar-textos-anomalias`.

## 10. Resumo para o Chat 00

```
[Chat 02 → Chat 00 | Etapa 1 — GeoAutoEngine | 10/11/2026 | somente leitura]
Base: GeoAutoEngine@3acab63 (main) + sync-section-names@2937469 + feature/atualizar-textos-anomalias@1724528.
Acesso: git OK; Issues/PRs/CI do GAE bloqueados (gh 403). Sem dotnet/Civil 3D: nada compilado/testado. Sem alterações.
GAE-01 Fato/alta: Version 3.1.1 fixa desde 427ccac (210 commits); bundle compilado na máquina do usuário sem
  registrar commit (tools/GeoAutoEngineBundleBuilder.cs#L60-L98) -> build da equipe = Lacuna (perguntar usuário).
GAE-02 Fato/alta: README.md#L3 "2026" desatualizado; PackageContents.xml#L13,L41,L69 = R24.3/R25.0/R25.1
  (net48/net8). Só API AutoCAD (sem AeccDbMgd). 2024 sem smoke CAD (COMPATIBILITY.md#L3).
GAE-03 Fato/alta: ../GeoAutoEngine.Legacy e ../reference só p/ scripts/test.ps1#L54-L60 (legacy/full) e
  tools/update-reference-manifest.ps1#L4-L5; build, fast/regression e CI independem.
GAE-04 Fato/alta: só 2 branches fora da main; ambas de 3acab63 e CONFLITAM em 5 arquivos (merge-tree).
  Nenhuma altera pipe/schema/exportador. GEOAUTO3VISYNC fora do PackageContents.xml.
GAE-05 Fato/alta: main bloqueia UI até 5 min ao exportar correções
  (VideoInspectionCorrectionRuntime.cs#L106-L110); corrigido só em sync-section-names (559d8b1).
GAE-06/07 Fato/alta: exportador = LWPOLYLINE reta, v0.1 fixo (GeoMapExportSerializer.cs#L9), 1 bulge cancela
  o arquivo inteiro (AutoCadGeoMapPolylineReader.cs#L63-L75), CRS manual, properties só metadados CAD.
  Sem 0.2 color, sem 0.3 Point/blocos/ATTRIB, sem tesselação (Geomap#48 atribui ao exportador definitivo).
  GAE-08: samples/geomap/*.json idêntica à fixture do GeoMap, mas nenhum teste do GAE a lê.
GAE-09 Fato/alta: GADX v1 = header 16B LE (GADX|u16 ver|u16 tipo|i32 len|i32 reqId), JSON camelCase ≤16 MiB,
  pipe "GeoAutoEngine.Inspection", servidor GAE com 1 instância, sem PipeSecurity (NamedPipeInspectionServer.cs#L219-L224).
  1 Hello/2 HelloAck/5 Error/3 InspectionPush/4 InspectionAck; 6 CorrectionPush (GAE->GAD) / 7 CorrectionAck.
GAE-10 Fato/alta + Hipótese/média: SEM negociação de capacidade (Hello só versões); 6/7 sob v1.
  GAD@073191a conecta por operação -> normalmente GAE recusa "não conectado" (Server#L148-L153). Em corrida,
  GAD lê tipo 6 -> InvalidDataException (GAD InspectionPipeProtocol.cs#L64-L65) e reporta falha de uma inspeção
  que o GAE já aceitou (Server#L423-L424); GAE aguarda ACK até 5 min.
GAE-11 Fato/alta: associação SectionId<->handle persiste no DWG (NOD GEOAUTOENGINE_VIDEO_INSPECTION_ASSOCIATIONS,
  AutoCadInspectionAssociationStore.cs#L8,L29-L36) e sobrevive reabrir, mas só reidrata quando a inspeção chega
  de novo; sessão e correções pendentes só em memória (VideoInspectionRuntime.cs#L28). OccurrenceId fica no XData GEOAUTOENGINE_VI.
GAE-12 Fato/alta: handle salvo como texto; LastKnownGeometryLength não usado p/ detectar edição; BREAK divide a
  Polyline da rede (AutoCadInspectionCoverageController.cs#L172-L181) -> afeta sourceId no GeoMap (Hipótese).
GAE-13 Fato/média: GAE gera textos/blocos/contornos/alertas a partir de CSV de campo e corrige metragem/descrição
  do GAD; Polylines da rede nascem com o projetista (DrawingPlan.cs#L87-L94); inspeção nasce no GAD.
GAE-14 Fato+Hipótese/média: REGISTRO depende do caminho do CSV (Identifiers.cs#L32-L38) -> reprocessar
  CSV movido pode duplicar entidades.
GAE-15 Hipótese forte/alta: dados reais em repo público: docs/audit/cad_gis_raw/*.raw.json,
  docs/audit/cad_gis_inventory.json; caminhos de usuário em docs/evidence/environment.json e
  docs/evidence/stage1/cad/plugin_probe_*.json; docs/evidence/**/*.dwg não abertos (Lacuna).
GAE-16 Fato/alta: 203 testes (213 na feature); Cad2026/Plugin/pipe sem teste automatizado; CI filtrada por path.
  GAE-17 Hipótese/média: pipe sem ACL/autenticação, nome fixo. Completo: Project, governanca/auditorias/ETAPA_1_CHAT_02_GEOAUTOENGINE.md
Decisões Chat 00: dono/versão GADX; ordem de merge; escopo do exportador (#48/0.2/0.3); identidade (handle x GUID).
  Usuário: build instalada (existem GEOAUTO3VISYNC/GEOAUTO3VIUPDATETEXTS? data da DLL) e destino dos dados GAE-15.
```
