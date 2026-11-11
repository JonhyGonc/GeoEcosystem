# Etapa 2 — Consolidação da auditoria e matriz de integração (Chat 00)

## 1. Identificação
- **Agente:** Chat 00 — coordenador GeoEcosystem
- **Data:** 10/11/2026
- **Estado:** consolidação de auditoria — **somente leitura**
- **Entradas:**
  - `governanca/ETAPA_1_RELATORIO_CHAT_01_GEOMAP.md` (achados GM-01…20)
  - `governanca/auditorias/ETAPA_1_CHAT_02_GEOAUTOENGINE.md` (GAE-01…17)
  - `governanca/ETAPA_1_RELATORIO_CHAT_03_GEOAUTODOCS.md` (GAD-01…20)
  - `governanca/ETAPA_1_COORDENACAO_AUDITORIA.md` (C00-01…11)
- **Commits de referência:** os mesmos da Etapa 1, sem commits novos até o corte:
  - `Geomap@b9be25c`
  - `GeoAutoEngine@3acab63`, `@2937469`, `@1724528`
  - `GeoAutoDocs@073191a`, `@5d333d6`, `@61a47b4`

## 2. Objetivo e critérios de aceitação
**Objetivo:** transformar os três relatórios num diagnóstico único e verificado, que sirva de base para as Etapas 3 e 4.

**Critérios:**
- (a) achados de maior impacto conferidos contra o código;
- (b) separação entre fatos, hipóteses e lacunas;
- (c) matriz de integração em 11 dimensões;
- (d) decisões e perguntas consolidadas;
- (e) **nenhum mecanismo de integração escolhido** (regra 12).

## 3. Verificação das alegações pelo Chat 00
Conferi por amostragem as alegações com maior impacto no roadmap.

| Alegação | Como verifiquei | Resultado |
|---|---|---|
| GM-05: produtores com `sourceSystem` diferentes geram projetos duplicados | `geomap_dxf.py#L711` = `"Civil 3D DXF"`; `GeoMapExportSerializer.cs#L10` = `"Civil 3D"`; `DeterministicGeoMapIds.cs#L13-L14` usa `SourceSystem` no UUID | ✅ Confirmado |
| GM-06: reimportação apaga feições sem histórico | `PostgresGeoMapImportStore.cs`: `DELETE FROM network_features WHERE network_id = …` | ✅ Confirmado |
| GM-07: dados reais entraram pela ponte DXF | `docs/REAL_DATA_VALIDATION.md` descreve DXF ASCII → ponte; não há uso registrado de `GEOAUTO3GEOMAPEXPORT` | ✅ Confirmado |
| GAE-01: versão 3.1.1 nunca alterada | `git log -G'<Version>' -- Directory.Build.props` → só `427ccac` | ✅ Confirmado |
| GAE-04: branches do GAE conflitam entre si | `git merge-tree` → conflito nos 5 arquivos citados | ✅ Confirmado |
| GAE-05: `main` do GAE bloqueia a interface ao exportar correções | `VideoInspectionCorrectionRuntime.cs#L106-L110`: `SendCorrectionsAsync(…, 5 min).GetAwaiter().GetResult()` | ✅ Espera síncrona confirmada; a thread de UI é inferida pelo contexto (alta) |
| GAE-09/17: servidor do pipe com 1 instância e sem ACL | `NamedPipeServerStream(…, InOut, 1, Byte, Asynchronous)`, sem `PipeSecurity` | ✅ Confirmado |
| GAE-11: associação trecho↔Polyline persistida no DWG | `AutoCadInspectionAssociationStore.cs`: dicionário `GEOAUTOENGINE_VIDEO_INSPECTION_ASSOCIATIONS` | ✅ Confirmado |
| GAD-05: `main` + fix do GAD conflitam | `git merge-tree` → conflito em `MainWindow.xaml.cs` | ✅ Confirmado |
| GAD-10: abrir projeto compartilhado sobrescreve o cache | `SharedProjectService.cs#L64-L85`: o oficial é lido e gravado no cache sem comparar com o local | ✅ Lógica confirmada; perda efetiva em uso = hipótese (média) |
| GAD-12a: não há "banco sincronizado" | 0 arquivos com `Sqlite`/`EntityFramework` em `GeoAutoEngine/src` | ✅ Confirmado |
| C00-02 / GAE-10 / GAD-13: GADX "v1" em duas variantes | Enum do GAD `main` sem 6/7; GAD fix e GAE `main` com 6/7; ambos `CurrentVersion = 1`; leitura de tipo desconhecido lança exceção | ✅ Confirmado |
| **GAD-14** (deixada em aberto pelos Chats 02 e 03): semântica de "campo não alterado" | O GAE (`VideoInspectionSession.cs#L229-L242`) envia o **estado completo** da ocorrência editada, com todos os campos e nulos serializados (`JsonSerializerDefaults.Web`). No GAD fix, cada setter marca `HasX = true` (`InspectionCorrections.cs`), e o desserializador chama o setter de toda propriedade presente, inclusive `null`. | ⚠️ **Resolvida como fato (alta):** com o produtor atual, todos os `Has*` são sempre verdadeiros, ou seja, a semântica de presença do GAD nunca é exercitada. O efeito na aplicação (sobrescrever com nulo ou vazio) é hipótese (média) e precisa de teste cruzado. |

**Divergências entre relatórios:** só números de linha. O Chat 02 cita `NamedPipeInspectionServer.cs#L148-L153` e o Chat 03 `#L146-L151` para a mesma verificação "não conectado". O conteúdo é o mesmo. Nenhuma contradição de fato.

**Correção de interpretação do Chat 00 (C00-06):** o GAE **não cria** a geometria da rede. As Polylines são desenhadas pelo projetista no Civil 3D (GAE-13, `DrawingPlan.cs#L87-L94`). A fonte autoritativa da geometria é o **DWG**. O GAE e a ponte DXF são dois **extratores** concorrentes do mesmo DWG. A tensão com a regra 13 é, portanto, sobre qual extrator é o canônico, e não sobre quem cria a geometria.

## 4. Diagnóstico consolidado

### 4.1 Fatos (verificados em código, commit ou documento)
**F-01 — Integrações existentes:** são duas, pontuais, ambas locais ou por arquivo.
- (a) GAE → GeoMap: arquivo `.geomap.json` v0.1, que **nunca foi usado com dados reais**;
- (b) GAD ↔ GAE: Named Pipe GADX v1 + sidecar `.geoadocs.json`.
- O GeoMap e o GAD não se conhecem.
- *(C00-01, GM-07, GM-20, GAD-12a)*

**F-02 — Ponte DXF:** a ponte em Python, declarada descartável (DEC-024), é o único caminho real de dados CAD para o GeoMap e o único produtor de 0.2/0.3. Concentra regras de domínio CAD que o GAE não tem: LINE, cor, blocos, ATTRIB, posição do símbolo, filtros de levantamento. *(GM-07, GM-08, GM-14)*

**F-03 — Exportador do GAE:** só LWPOLYLINE com segmentos retos, v0.1. Um bulge cancela o arquivo inteiro. O CRS é digitado à mão, e `properties` traz só metadados CAD. *(GAE-06/07)*

**F-04 — Protocolo GADX:** "v1" existe em duas variantes incompatíveis, sem negociação de capacidade, com código duplicado nos dois repositórios e sem teste cruzado. A semântica do payload de correção diverge entre produtor e consumidor (GAD-14, verificado acima). *(C00-02/03, GAE-09/10, GAD-12b/13/14)*

**F-05 — Linhas de base indefinidas:**
- GAD: 319 commits fora da `main`, com conflito no merge;
- GAE: 2 branches fora da `main`, em conflito entre si;
- os números de versão nunca mudam (GAE 3.1.1, GAD 1.0.4), então **não é possível saber qual build a equipe usa**.
- *(GAD-03/05/06, GAE-01/04)*

**F-06 — GeoMap:** é o único projeto com governança madura (Issues, PRs, DECISIONS, CI verde, contrato versionado com validador). *(GM-01)*

**F-07 — Esquemas sem controle de versão aplicada:** o GeoMap aplica migrations à mão, sem tabela de registro; o GAD usa `EnsureCreated` + `ALTER TABLE` ad hoc. *(GM-03, GAD-07)*

**F-08 — Ausência de histórico:**
- GeoMap: snapshot substituído, sem origem persistida;
- GAD: `SaveAsync` substitui o agregado e apaga o que falta;
- GAE: sessão e correções pendentes só em memória.
- *(GM-04/06, GAD-08, GAE-11)*

**F-09 — Identificadores:** não existe identificador comum. O único elo é o **handle CAD**, válido dentro da linhagem de um mesmo DWG:
- gravado como texto no GAE;
- muda com WBLOCK, cópia ou recriação;
- o BREAK do GAE divide Polylines.
- O GAD usa GUIDs v4 próprios, estáveis, sem vínculo espacial.
- *(GM-C, GAE-12, GAD-15/16)*

**F-10 — Possíveis dados de cliente ou projeto real em repositórios públicos:** GAE e GAD. *(GAE-15, GAD-18)*

**F-11 — Testes:** nenhum teste cruzado entre repositórios. Sem cobertura automatizada:
- CAD, pipe e renderer no GAE;
- UI no GAD;
- navegador real no GeoMap.
- *(GM-10/18, GAE-08/16, GAD-20)*

**F-12 — Runtimes distintos:** GeoMap .NET 10; GAE .NET 8 e .NET Framework 4.8 (Civil 3D 2024, sem smoke); GAD .NET 8 WPF. O GAE não usa API Civil 3D (só AutoCAD). *(C00-11, GAE-02)*

### 4.2 Hipóteses (inferidas, a confirmar)
- **H-01 — Perda de dados no GAD:** alterações locais não sincronizadas se perdem ao reabrir um projeto compartilhado (GAD-10). Lógica confirmada, efeito não reproduzido.
- **H-02 — Corrida no pipe:** um `CorrectionPush` chega durante um push do GAD `main`, a inspeção já aceita é reportada como falha, e o GAE espera 5 min (GAE-10, GAD-13).
- **H-03 — Correções sobrescrevendo campos:** a aplicação de correções no GAD fix pode sobrescrever campos com nulo ou vazio, porque todos os `Has*` são verdadeiros (GAD-14).
- **H-04 — Identidade no GeoMap:** BREAK e recriação de entidades mudam o `sourceId`, gerando feições novas e órfãs na reimportação (GAE-12, GM-C).
- **H-05 — Duplicação no GAE:** reprocessar um CSV movido de pasta duplica entidades (GAE-14).
- **H-06 — Lock do GAD:** a recuperação do lock obsoleto não é atômica entre dois clientes (GAD-09).

### 4.3 Lacunas (não verificáveis nesta sessão)
- **L-01:** build instalada na equipe (GAE e GAD).
- **L-02:** estado e motivo do PR #1 do GAD; PR #3 do GAE.
- **L-03:** Issues, PRs e CI do GAE e do GAD (API bloqueada para os três chats).
- **L-04:** nenhum build ou teste .NET executado localmente. Só a CI do GeoMap e os 13 testes Python foram confirmados.
- **L-05:** conteúdo real dos arquivos suspeitos (não foram abertos, por segurança).
- **L-06:** comportamento em Civil 3D 2024.
- **L-07:** reprodutibilidade da 3ª rede real (script privado, GM-09).

## 5. Matriz de integração
Legenda: **GM** = GeoMap, **GAE** = GeoAutoEngine, **GAD** = GeoAutoDocs.

| Dimensão | GM | GAE | GAD | Integração hoje | Lacuna principal |
|---|---|---|---|---|---|
| **Dados (domínio)** | Projetos, redes, feições linha/ponto + `properties` JSONB | Entidades geradas de CSV de campo (textos, blocos, contornos), anotações e coberturas de vídeo; lê as Polylines da rede | Projetos, relatórios, locais, trechos, ocorrências, anomalias, capturas, vídeos (caminho) | GAD→GAE (inspeção); GAE→GAD (correções); GAE→GM (só fixture) | Nenhum dado de inspeção chega ao GM; atributos técnicos (diâmetro, material) não saem do GAE |
| **Identificadores** | UUID v5 (`sourceSystem`+`externalId`; feição = `sourceType`+handle) | Handle CAD (texto); `REGISTRO` (caminho+SHA do CSV); XData com GUIDs do GAD | GUID v4 para todas as entidades | Handle (GM↔DWG) e GUIDs do GAD (GAD↔GAE, via XData/NOD) | Nenhum ID atravessa GM↔GAD; handle frágil; `sourceSystem` divergente duplica projetos |
| **Geometrias** | EPSG:4326, 2D, LineString/MultiLineString/Point/MultiPoint | DWG (WCS, metros); exportador só LWPOLYLINE reta; BREAK divide Polylines | Nenhuma; ponto inicial/final em texto livre | Ponte DXF e exportador GAE leem o mesmo DWG | Curvas (bulge/arco), Polyline2d/3d, blocos no GAE; CRS digitado à mão |
| **Inspeções / anomalias** | Inexistente (fora do MVP) | Sessão em memória; anotações no DWG; correções devolvidas ao GAD | Fonte autoritativa (SQLite + JSON compartilhado) | Pipe GADX + `.geoadocs.json` | Sem caminho para o GM; correções pendentes se perdem ao fechar o CAD |
| **Histórico** | Nenhum (snapshot); origem não persistida | Nenhum (só versões de storage no DWG) | Nenhum (substituição do agregado); backup zip parcial | — | Sem rastreabilidade de quem mudou o quê e quando |
| **Importação/exportação** | Importa `.geomap.json`/`.ndjson` (CLI); API só GET + MVT | Importa CSV e `.geoadocs.json`; exporta `.geomap.json` | Exporta `.docx` + sidecar `.geoadocs.json`; push via pipe | 3 fluxos unidirecionais | Nenhuma saída do GM; o exportador do GAE nunca foi usado com dados reais |
| **Formatos** | GeoMap Import Contract 0.1/0.2/0.3 (JSON/NDJSON, estrito) | Produz 0.1; consome GADX/Inspection v1 | Produz Inspection v1; consome correções | Contratos com validador só no GM | Contrato GADX sem especificação formal, só código e um doc no GAE |
| **Interfaces** | API REST local, sem autenticação | Comandos AutoCAD + paleta WPF + servidor de pipe | WPF + cliente de pipe + pasta de rede | Pipe local (mesma máquina) | Pipe sem ACL e com 1 instância; API do GM sem autenticação para hospedagem |
| **Versionamento** | `contractVersion` por documento; DECISIONS; sem releases | Versão fixa 3.1.1; `STORAGE_VERSION` no DWG; GADX v1 sem negociação | Versão fixa 1.0.4; schema compartilhado sem versão | Só o GM versiona o contrato de forma explícita | Ruptura silenciosa no GADX; builds indistinguíveis |
| **Performance** | Medida: MVT LARGE p95 32 ms; NDJSON 1M em 50 s/121 MiB; pontos não medidos | Não medida no CAD; limites 10.000/100.000 do contrato | Não medida (muitos vídeos/trechos); `Find` por nó no save | — | Exportador GAE limitado a 10.000 feições, sem NDJSON |
| **Riscos** | Migrations manuais; sem autenticação; ponte vira produção sem decisão | UI bloqueada (`main`); dados reais públicos; branches em conflito | Perda local (H-01); save destrutivo; dados de cliente públicos; `main` defasada | GADX em duas variantes; zero testes cruzados | Ver seção 7 |

### 5.1 Fluxos atuais (só o que existe no código)
```
 levantamento (CSV) ──► GAE ──► DWG (textos/blocos/anotações, XData/NOD)
                                  ▲      │
         projetista desenha rede ─┘      ├──(DXF)──► ponte Python (GM) ──► .geomap.json 0.1–0.3 ──► importador GM ──► PostGIS ──► API/MVT ──► navegador
                                         └──GEOAUTO3GEOMAPEXPORT──► .geomap.json 0.1 (só fixture sintética)
 GAD ──(pipe GADX v1: Hello/InspectionPush)──► GAE ──► anotações no DWG
 GAD ◄──(pipe GADX v1: CorrectionPush 6 / Ack 7; só GAE main + GAD fix)── GAE
 GAD ──(.docx + sidecar .geoadocs.json)──► GAE (GEOAUTO3VIIMPORT)
 GAD ◄─► pasta de rede (project.geoadocs.json + lock.json) ◄─► outras instâncias do GAD
```

### 5.2 Autoridade de dados: regra 13 × realidade
| Dado | Regra 13 | Realidade auditada | Divergência |
|---|---|---|---|
| Geometria da rede | GAE | DWG/projetista; dois extratores (ponte DXF, GAE) | Extrator canônico indefinido (D-03) |
| Dados técnicos do levantamento | GAE | GAE gera entidades a partir do CSV de campo | Coerente |
| Inspeções, vídeos, relatórios, anomalias | GAD | GAD; o GAE corrige metragem/descrição com revisão do usuário e ack | Coerente, desde que o ack seja o "fluxo autorizado"; precisa de registro formal |
| Agregação geográfica | GM | GM só recebe geometria e não sobrescreve fontes | Coerente; o GM não agrega inspeções |

## 6. Decisões técnicas a arbitrar (insumos da Etapa 4; nenhuma tomada aqui)
| ID | Decisão | Alternativas já identificadas | Depende de |
|---|---|---|---|
| D-01 | Linha de base de cada projeto | GAD: `main` × fix (319 commits); GAE: ordem de merge de `sync-section-names` e `feature/atualizar-textos-anomalias` | L-01, L-02 (usuário) |
| D-02 | Dono, especificação e versionamento do contrato GADX | (a) declarar v1 = com 6/7 e exigir as duas pontas atualizadas; (b) v2 com negociação de capacidade no Hello; (c) fonte única do contrato compartilhada entre os repos | D-01 |
| D-03 | Extrator canônico DWG → GeoMap | (a) revisar a DEC-024 e assumir a ponte como produto; (b) levar 0.2/0.3/curvas para o GAE e aposentar a ponte; (c) coexistência com `sourceSystem`/identidade unificados | Usuário (uso real), GM-09 |
| D-04 | Estratégia de identidade entre sistemas | Handle CAD (atual); GUID persistido em XData pelo GAE; chave de negócio (nome de trecho/PV) | D-03 |
| D-05 | Rastreabilidade e histórico mínimos | Persistir origem da importação no GM; versionar esquema GM/GAD; versão de build por commit | — |
| D-06 | Testes cruzados de contrato | Fixtures geradas pelo produtor e validadas pelo consumidor em CI; ou fonte única | D-02, D-03 |
| D-07 | Escopo da integração GAD ↔ GM | Nenhuma (manter); inspeções no GM; só indicadores | Requisito de negócio (usuário). **Mecanismo não escolhido** (regra 12) |

## 7. Riscos priorizados
| # | Risco | Severidade | Natureza | Observação |
|---|---|---|---|---|
| R-01 | Dados de cliente/projeto em repositórios públicos (GAE-15, GAD-18) | **Alta** | Segurança | Tornar privado é decisão do usuário, reversível e sem perda. Limpar o histórico é destrutivo e exige plano. |
| R-02 | Perda silenciosa de alterações no GAD compartilhado (H-01) | **Alta** | Integridade de dados | Afeta a produção se a equipe usa projetos compartilhados |
| R-03 | GADX em duas variantes + UI do CAD bloqueada até 5 min (F-04, GAE-05) | **Alta** | Integração | Depende de qual combinação de builds está instalada |
| R-04 | Builds não rastreáveis (F-05) | Média | Operação | Impede diagnosticar R-02/R-03 em campo |
| R-05 | Ponte "descartável" em produção, com script privado (F-02, GM-09) | Média | Arquitetura | O dado no banco não é reproduzível |
| R-06 | Esquemas sem controle de versão aplicada (F-07) | Média | Dados | Divergência silenciosa entre máquinas |
| R-07 | Duplicação de projetos no GM por `sourceSystem` (GM-05) | Média | Dados | Aparece assim que o exportador do GAE for usado com dados reais |
| R-08 | Ausência de testes cruzados (F-11) | Média | Qualidade | Qualquer mudança de contrato pode quebrar outro projeto sem aviso |

## 8. Perguntas ao usuário (consolidadas e sem duplicatas)
1. **Builds em uso** (L-01):
   - GAE: os comandos `GEOAUTO3VISYNC` e `GEOAUTO3VIUPDATETEXTS` existem no Civil 3D da equipe? Qual a data de `%AppData%\Autodesk\ApplicationPlugins\GeoAutoEngine.bundle\Contents\<ano>\GeoAutoEngine.Plugin.dll`?
   - GAD: a janela de revisão de correções existe no GeoAutoDocs instalado?
2. **PR #1 do GeoAutoDocs** (L-02): está aberto? Por que não foi mesclado?
3. **Dados nos repositórios públicos** (R-01): os arquivos listados em GAE-15 e GAD-18 são de cliente ou projeto real? Quer tornar os dois repositórios privados já?
4. **Uso do GeoMap:** ele é usado fora de máquinas de desenvolvimento? O script privado da 3ª rede deve virar código versionado ou ser descartado?
5. **Projetos compartilhados do GAD:** a equipe usa a pasta de rede? Isso define a urgência de R-02.
6. **Escopo de negócio** (D-07): inspeções e anomalias devem aparecer no mapa do GeoMap? Há prazo ligado a algum contrato?

### 8.1 Respostas do usuário (10/11/2026)
- **P3:** os arquivos dos repositórios públicos **não são dados de cliente**. O R-01 cai para **baixa**. Fica só a recomendação de remover caminhos de usuário Windows (GAE-15) em uma limpeza futura, sem urgência.
- **P4:** a equipe **não usa** projetos compartilhados em pasta de rede hoje. Resposta literal: "devemos criar uma pasta nova". O R-02 cai para **média**: não afeta a produção agora, mas **vira pré-requisito** antes de adotar a pasta compartilhada.
- **P1 (GAE), evidência parcial:** captura de tela da pasta `GeoAutoEngine.bundle\Contents` na máquina do usuário.
  - A pasta `2026` foi modificada em **22/10/2026 09:07**.
  - O último commit de `feature/atualizar-textos-anomalias` (`1724528`) é de **22/10/2026 09:01**, ou seja, 6 minutos antes.
  - Nenhum outro commit de qualquer branch do GAE é desse dia.
  - **Hipótese forte (alta):** a build instalada é `main@3acab63` + `feature/atualizar-textos-anomalias`, **sem** `sync-section-names`. Isso implica que a máquina do usuário tem `GEOAUTO3VIUPDATETEXTS`, não tem `GEOAUTO3VISYNC` e ainda tem o bloqueio de até 5 min ao exportar correções (GAE-05).
  - **Confirmado por teste do usuário (10/11/2026 15:38):** `GEOAUTO3VIUPDATETEXTS` existe (respondeu "Nenhuma inspeção carregada") e `GEOAUTO3VISYNC` não existe ("Unknown command"). Na máquina do usuário, a build é `main@3acab63` + `feature/atualizar-textos-anomalias@1724528` → **Fato (alta)**. Ainda não se sabe se as outras máquinas da equipe têm a mesma build.
  - Há várias pastas `2026.backup-*` de 26/08 a 11/09, o que indica implantações frequentes sem registro de commit (confirma GAE-01).
- **P1 (GAD), respondida em 10/11/2026 20:58:** a janela de revisão de correções **aparece** no GeoAutoDocs instalado. Ela (`CorrectionReviewWindow`) só existe em `fix/section-edit-save-stability`, então a build do GAD em uso deriva da fix, e não da `main` → **Fato (alta)** para a máquina do usuário.
  - Consequência: na máquina do usuário, as duas pontas falam a variante "v1 com 6/7" do GADX. A incompatibilidade da `main` do GAD não afeta essa máquina.
  - Por outro lado, a combinação GAE `main` (espera síncrona de 5 min, GAE-05) + GAD fix (o usuário revisa as correções numa janela) torna **provável que o Civil 3D fique travado enquanto o usuário revisa** → Hipótese (alta), a confirmar com o usuário.
- **P4, revisada:** a equipe **não** pretende usar a pasta compartilhada do GAD. O R-02 cai para **baixa** e fica registrado como defeito conhecido.
- **P5, equipe:** os desenhistas instalaram o GAE em **épocas diferentes**. Combinado com GAE-01 (versão fixa, sem commit registrado), isso significa builds diferentes e não identificáveis na equipe. O R-04 sobe para **alta**: nas outras máquinas pode haver o GAD `main` (sem 6/7) ou o GAE sem `GEOAUTO3VIUPDATETEXTS`.

## 9. Alterações feitas
Nenhuma em repositório. Este documento foi salvo no Project em `governanca/ETAPA_2_CONSOLIDACAO.md`.

## 10. Próximo passo
1. O usuário responde as perguntas da seção 8, principalmente 1, 3 e 5.
2. Com as respostas, o Chat 00 entrega a **Etapa 3**: proposta de estrutura do repositório `GeoEcosystem`, com pedido de autorização para criá-lo.
3. Em seguida, a **Etapa 4**: roadmap para aprovação. Nenhuma implementação começa antes da aprovação.
