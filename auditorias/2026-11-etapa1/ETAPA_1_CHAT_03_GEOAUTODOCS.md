# Etapa 1 — Relatório de auditoria do GeoAutoDocs (Chat 03)

## 1. Identificação
- **Agente:** Chat 03 — especialista GeoAutoDocs
- **Repositório:** `JonhyGonc/GeoAutoDocs` (público)
- **Commits auditados:**
  - `main` @ `073191a5a726a48c204616e4f0fcacf4329f5085` (13/10/2026)
  - `fix/section-edit-save-stability` @ `5d333d67f39433ac1b797af38a05acb61cbc5f63` (23/09/2026)
  - `Testes-ChatGPT` @ `61a47b4606bb23121f6e67d576b30236b889ae72` (21/08/2026)
- **Comparação cruzada:** `JonhyGonc/GeoAutoEngine` `main` @ `3acab634ebceb17ec41f256244c3da56e092e6c9`
- **Data de corte:** 10/11/2026. Não há commits mais novos nas três branches.
- **Escopo:** perguntas Q1–Q8 e GAD-A…GAD-G da instrução do Chat 00.
- **Estado:** auditoria **somente leitura**.

Convenções:
- Referências no formato `GAD@sha7:caminho#Lx`, com `GAD@073191a` = `main`, `GAD@5d333d6` = `fix/section-edit-save-stability` e `GAE@3acab63` = GeoAutoEngine `main`.
- **Classe:** F = Fato, H = Hipótese, L = Lacuna. **Confiança:** A = alta, M = média, B = baixa.

### Verificação de acesso (regra 2)

| Recurso | Resultado |
|---|---|
| Código e branches | ✅ Leitura via clone anônimo (repositório público). |
| Issues, PRs, Actions | ❌ API do GitHub retornou 403 ("access not enabled for this session"). A anexação com credenciais (`add_repo` com push) foi **negada pela política da sessão**. As páginas web do GitHub estão bloqueadas por `robots`. |
| PRs via git | Único dado obtido: `git ls-remote` mostra **PR #1** (`refs/pull/1/head` = `5d333d6`, `refs/pull/1/merge` = `2d9b13c`, de 23/09). Título, estado e comentários **não são acessíveis**. |
| Build e testes | ❌ O ambiente é Linux, sem .NET SDK, sem instalação de pacotes, e WPF exige Windows. |

## 2. Objetivo e critérios de aceitação
- **Objetivo:** descrever o estado real do GeoAutoDocs (stack, branches, persistência, integrações, identificadores, mídia, testes, documentação e dados sensíveis) para a consolidação do Chat 00.
- **Critérios de aceitação:**
  - Q1–Q8 e GAD-A…G respondidas, cada achado com ID, classe, referência e confiança;
  - nenhuma alteração remota;
  - item 10 com até 40 linhas.

## 3. Evidências

### Q1 — Stack, build e CI
- **GAD-01 (F, A) — Stack:**
  - App WPF `net8.0-windows`, `<Version>1.0.4</Version>` (`GAD@073191a:src/GeoAutoDocs.App/GeoAutoDocs.App.csproj#L4-L9`).
  - Core e Infrastructure em `net8.0`.
  - Pacotes: EF Core SQLite 8.0.23, DocumentFormat.OpenXml 3.3.0, LibVLCSharp 3.10.0 + VideoLAN.LibVLC.Windows 3.0.23.1, System.Drawing.Common 8.0.29 (`GAD@073191a:src/GeoAutoDocs.Infrastructure/GeoAutoDocs.Infrastructure.csproj`).
  - Testes em xUnit 2.9.3.
  - Publicação autocontida win-x64 (`GAD@073191a:DISTRIBUICAO.md#L6`) e instalador Inno Setup (`installer/GeoAutoDocs.iss`).
  - A branch fix acrescenta `UseWindowsForms` ao App.
- **GAD-02 (F, A / L) — CI:**
  - Nenhuma branch tem CI. A `main` não tem `.github/`.
  - A branch fix criou e apagou **38 workflows temporários** (`windows-latest`, `dotnet restore/build/test` em Release; ex.: `.github/workflows/temporary-correction-validation.yml`, commit `b9398e5`). O último foi removido em `5d333d6`.
  - Resultados das execuções: **L** (API de Actions inacessível).
  - Comandos de build/teste do projeto: `dotnet build GeoAutoDocs.sln -c Release` e `dotnet test GeoAutoDocs.sln -c Release`.

### Q2 / GAD-A — Branches
- **GAD-03 (F, A) — Divergência:** as três branches partem de `13aefe4` (21/08).

  | Branch | Commits exclusivos | Conteúdo |
  |---|---|---|
  | `main` | 1 (`073191a`, 13/10, autor "Claude") | Renumeração de seções: `SectionNumbering.cs` + `MainWindow.xaml.cs` + 5 testes |
  | `fix/section-edit-save-stability` | 319 (27/08–23/09, autor João) | 81 arquivos, +11.866/−733 |
  | `Testes-ChatGPT` | 5 (21/08) | Só UI (`MainWindow.xaml`, `MainWindow.xaml.cs`, `GeoAutoDocsTheme.xaml`) |

  - Em `Testes-ChatGPT`, o 1º commit (`4cd32ba`, design tokens) é patch-equivalente a um commit da fix (`git cherry`). Os outros 4 não estão na fix.
- **GAD-04 (F, A) — O que a fix muda em relação à `main`:**
  - **Funcionalidades:**
    - recepção de correções do GeoAutoEngine: `Core/InspectionCorrections.cs` (571 linhas) + `CorrectionReviewWindow`;
    - motor de revisão de relatório: `Core/Review/*`, 11 regras, `ReportReviewWindow` e `ReportGenerationReviewDialog`;
    - janelas de capturas pendentes e de ordenação de locais e trechos;
    - nomeação automática de trechos;
    - segurança de reprodução de mídia (`MediaPlaybackSafety`, `MediaLoadPolicy`);
    - salvamento parcial de trecho (`IProjectStore.SaveSectionAsync`) + semáforo de salvamento (`GAD@5d333d6:src/GeoAutoDocs.Infrastructure/ProjectStore.cs#L13`, `#L140`);
    - sessões compartilhadas serializadas, com `AbandonAsync` e cópia de mídia apenas se alterada.
  - **Esquema SQLite:** **nenhuma** tabela ou coluna nova. O diff de `ProjectStore.cs` tem 0 ocorrências de `AddColumnIfMissing`/`CREATE TABLE`.
  - **Esquema de domínio:** novo valor de enum `AnomalyPosition.UpperAndLowerGenerators = 15` (`GAD@5d333d6:src/GeoAutoDocs.Core/Domain.cs#L220`).
  - **Protocolo:** enum ganha `CorrectionPush = 6` e `CorrectionAck = 7` (`GAD@5d333d6:src/GeoAutoDocs.Infrastructure/InspectionPipeProtocol.cs#L14-L15`). O transporte passa a manter conexão aberta e escutar correções (`GAD@5d333d6:src/GeoAutoDocs.Infrastructure/NamedPipeInspectionTransport.cs#L358`).
  - **Testes:** passam de 110 para 240 atributos (ver GAD-19).
- **GAD-05 (F, A) — Mesclagem:**
  - O PR #1 aponta para `5d333d6`. O ref `refs/pull/1/merge` (`2d9b13c`, 23/09) indica que, naquela data, a fusão com `13aefe4` era limpa.
  - Uma simulação local somente leitura (`git merge-tree --write-tree origin/main origin/fix/...`) hoje dá **conflito em `src/GeoAutoDocs.App/MainWindow.xaml.cs`**, causado por `073191a`.
- **GAD-06 (L) — Versão em uso, obsolescência e motivo de não mesclar:**
  - **Qual build a equipe usa:** sem evidência. As duas branches declaram `1.0.4` no csproj e no `.iss`, então o número de versão **não distingue** os builds. Pergunta ao usuário.
  - **A `main` está obsoleta?** **H/M:** funcionalmente, sim. A `main` tem 1 commit exclusivo contra 319 da fix, e não recebe correções do GeoAutoEngine (GAD-12). Não está confirmado que a fix seja a versão distribuída.
  - **Por que não foi mesclada:** **L**. Não há evidência em commits, e o PR #1 não é legível.

### Q3 / GAD-B — Persistência
- **GAD-07 (F, A) — Banco SQLite local:**
  - Caminho: `%LOCALAPPDATA%\GeoAutoDocs\database\GeoAutoDocs.db` (`GAD@073191a:src/GeoAutoDocs.Infrastructure/AppDataPaths.cs#L11`). A raiz pode ser trocada pela variável `GEOAUTODOCS_DATA_ROOT` (`App.xaml.cs#L27`).
  - Evolução do esquema:
    - `EnsureCreatedAsync` seguido de 7 rotinas `Ensure*SchemaAsync` (`ProjectStore.cs#L25-L41`);
    - essas rotinas detectam colunas via `PRAGMA table_info` e aplicam `ALTER TABLE … ADD COLUMN` ou `CREATE TABLE IF NOT EXISTS` (`#L623-L641`, `#L634`);
    - **sem** tabela de versão de esquema, migrations ou rollback;
    - cópias `.bak` únicas em 3 transições históricas (`#L335-L356`).
  - Só suporta mudanças aditivas. Renomear, mudar tipo ou remover coluna não tem caminho previsto.
- **GAD-08 (F, A / H, M) — Salvamento do agregado:**
  - `SaveAsync` grava o projeto inteiro (`#L95-L129`): apaga do banco tudo o que **não** está no grafo em memória (`#L201-L295`) e depois apaga os arquivos gerenciados (`#L128`).
  - Não há token de concorrência no banco local, e há um `Find` por nó (custo cresce com o tamanho do projeto).
  - **Risco (H):** qualquer chamada com agregado parcial remove dados e arquivos de forma irreversível. Os chamadores auditados usam `GetAsync` completo.
- **GAD-09 (F, A) — Projeto compartilhado** (`GAD@073191a:src/GeoAutoDocs.Infrastructure/SharedProjectService.cs`):
  - **Arquivos na pasta de rede:** `project.geoadocs.json` (oficial) + `lock.json`.
  - **Lock:**
    - criado com `FileMode.CreateNew`;
    - heartbeat a cada 1 min (`MainWindow.xaml.cs#L135`);
    - considerado obsoleto após 5 min (`#L26`);
    - a recuperação do lock obsoleto, com confirmação do usuário, faz `File.Delete` + `CreateNew` (`#L52-L61`). Isso não é atômico entre dois clientes que recuperam ao mesmo tempo (**H**).
  - **Versão otimista:** `ProjectDataVersion` é comparada no checkpoint a cada 15 s (`#L96-L110`; `MainWindow.xaml.cs#L133`).
  - **Conflito de versão:** gera exceção, e a UI mostra "Servidor indisponível — alterações protegidas localmente" para **qualquer** erro (`MainWindow.xaml.cs#L835`). Não há fluxo de resolução nem merge.
  - **Formato do JSON:** `ReferenceHandler.Preserve` (`$id`/`$ref`) e nenhum campo de versão de esquema (`#L32`).
  - **Mídia:** só capturas e evidências externas são replicadas para `captures/` e `evidence/` na pasta de rede (`#L164-L188`). **Vídeos não.**
- **GAD-10 (F na lógica, H/M no efeito) — Perda silenciosa de alterações locais não sincronizadas:**
  - Ao abrir um projeto compartilhado (`MainWindow.xaml.cs#L443-L445`), o arquivo oficial **sempre** sobrescreve o cache local (`SharedProjectService.cs#L67-L85`). No próximo salvamento, o SQLite é regravado a partir desse grafo (`MainWindow.xaml.cs#L798`), e o `SaveAsync` apaga as linhas locais ausentes (GAD-08).
  - Alterações que ficaram só no local são descartadas na reabertura, sem aviso. Isso vale para checkpoint falho por rede, conflito de versão ou lock perdido.
  - A fix tem o mesmo comportamento (`GAD@5d333d6:…/SharedProjectService.cs#L67-L85`).
  - Nenhum teste cobre "reabrir após checkpoint falho" (`tests/GeoAutoDocs.Tests/SharedProjectTests.cs`).
- **GAD-11 (F, A) — Backup e restauração** (`BackupService.cs`, `App.xaml.cs#L37-L58`):
  - Na inicialização, se houve mudança, gera um zip com o SQLite (cópia via backup online da SQLite) + `projects\**`, com retenção de 5 (`#L18`, `#L197-L213`).
  - Se o banco estiver ausente ou corrompido, oferece restaurar e preserva o banco atual.
  - **Fora do backup:** `Cache\` (capturas, evidências e JSON de projetos compartilhados), a pasta compartilhada, vídeos, `.docx` gerados e logos.

### GAD-C — "Banco sincronizado" com o GeoAutoEngine
- **GAD-12a (F, A) — Refutado:**
  - O GeoAutoEngine não tem SQLite nem EF Core (busca em `GAE@3acab63:src`).
  - O que é realmente compartilhado:
    1. Named Pipe local `GeoAutoEngine.Inspection`. O GeoAutoDocs é o cliente e o GeoAutoEngine (Civil 3D) o servidor (`InspectionPipeProtocol.cs#L24`).
    2. Arquivo `*.geoadocs.json`: sidecar gerado junto de cada `.docx` (`FileInspectionTransport.cs#L49`; `WordReportService.cs#L127`) ou exportação manual. O GeoAutoEngine importa por diálogo (`GAE@3acab63:src/GeoAutoEngine.Plugin/PluginEntry.cs#L202`).
    3. Os GUIDs dentro desses payloads.
  - A "pasta compartilhada" (GAD-09) sincroniza **instâncias do GeoAutoDocs entre usuários**, não com o GeoAutoEngine.

### Q5 / GAD-D — Contrato do pipe
- **GAD-12b (F, A) — Formato no fio idêntico:**
  - Os dois lados usam: magic `GADX`, cabeçalho de 16 bytes LE, protocolo v1, limite de 16 MiB, JSON `JsonSerializerDefaults.Web`.
  - Os DTOs `Hello`/`HelloAck`/`Ack`/`Error` são iguais.
  - O schema `GeoAutoDocs.Inspection` v1 tem a mesma estrutura nos dois repositórios (`GAD@073191a:src/GeoAutoDocs.Core/InspectionExchange.cs` × `GAE@3acab63:src/GeoAutoEngine.Application/VideoInspection/InspectionExchangeContracts.cs`).
  - Diferenças:
    - o enum (falta 6/7 na `main` do GeoAutoDocs);
    - os tipos locais `InspectionTransportStatus`/`Result`, que não trafegam;
    - a implementação: BinaryPrimitives no GeoAutoDocs × bytes manuais no GeoAutoEngine (compatibilidade com netfx).
  - É código duplicado, sem teste cruzado (confirma C00-03).
- **GAD-13 (F no código, H/M em execução) — Comportamento da `main` do GeoAutoDocs ao receber 6/7:**
  - O cliente é de curta duração: fecha o pipe após cada envio ou verificação (`NamedPipeInspectionTransport.cs#L59`, `#L32`).
  - Por isso, o `SendCorrectionsAsync` do GeoAutoEngine normalmente falha antes, com "GeoAutoDocs não está conectado" (`GAE@3acab63:src/GeoAutoEngine.Plugin/VideoInspection/Transport/NamedPipeInspectionServer.cs#L146-L151`).
  - Se um `CorrectionPush` chegar enquanto o GeoAutoDocs aguarda um `InspectionAck`:
    - `ReadFrameAsync` lança "Tipo de mensagem desconhecido: 6" (`InspectionPipeProtocol.cs#L64-L65`);
    - o transporte converte isso em `CommunicationError` (`#L104-L107`) e informa falha mesmo que a inspeção tenha sido aceita;
    - o GeoAutoEngine espera o ack até o timeout.
  - O handshake não negocia capacidades: os dois lados anunciam protocolo 1 / schema 1. **Confirma C00-02.**
- **GAD-14 (H, M) — Payload de correção (fix × GeoAutoEngine `main`):**
  - Os nomes dos campos são compatíveis.
  - O GeoAutoEngine envia `GeneratedAtUtc` (ignorado pelo GeoAutoDocs) e sempre serializa todos os campos (`Description` com `""` por padrão).
  - O GeoAutoDocs usa semântica de presença (`HasMeterage`, `HasDescription`…; `GAD@5d333d6:src/GeoAutoDocs.Core/InspectionCorrections.cs#L22-L75`).
  - O significado de "campo não alterado" depende dessa interpretação, sem teste cruzado. Verificação pendente com o Chat 02.

### Q4 / GAD-E — Identificadores e vínculo espacial
- **GAD-15 (F, A) — GUIDs:**
  - GUIDs v4 gerados no cliente para Project, Report, Location, InspectionSection, VideoReference, Occurrence, Capture, Evidence etc. (`Domain.cs#L10`, `#L64`, …).
  - Persistem no SQLite e no JSON compartilhado e são exportados no schema de intercâmbio. Um teste garante que o ID do trecho se preserva ao editar (`ProjectStoreTests.cs#L77`).
  - A pasta `ProjectPath` usa **outro** GUID, diferente de `Project.Id` (`ProjectStore.cs#L77`).
  - O catálogo de anomalias tem IDs fixos.
- **GAD-16 (F, A / H) — Sem vínculo geométrico:**
  - Não há coordenada, CRS, handle CAD nem geometria em nenhuma branch (busca em `src`).
  - `StartPoint`, `EndPoint` e `Reference` são texto livre opcional (`Domain.cs#L172-L174`; `NewSectionWindow.xaml#L11-L13`).
  - O `InspectionAck` não devolve handle nem ID do CAD.
  - Sem o GeoAutoEngine, só seria possível ligar trechos a geometria por casamento heurístico de texto (nomes de PV, por exemplo), sem garantia (**H**).

### GAD-F — Mídia e relatórios
- **GAD-17 (F, A) — Onde ficam os arquivos:**
  - **Vídeos:** referenciados por caminho absoluto, local ou UNC (`Domain.cs#L187-L205`). Nunca são copiados. Em projeto compartilhado, um caminho local fica inacessível aos outros usuários.
  - **Capturas PNG:**
    - projeto local: `projects\<guid>\captures`;
    - projeto compartilhado: `Cache\<projectId>\captures` (`FrameCaptureService.cs#L122-L123`).
  - **Evidências externas:** copiadas para `evidence\` (`OccurrenceService.cs#L114-L161`).
  - **Relatórios:** o `.docx` é gravado no caminho escolhido pelo usuário, com sufixo único (`WordReportService.cs#L78-L84`), mais o sidecar `.geoadocs.json` ao lado.
  - **Modelo:** `MODELO DE RELATORIO.docx` é copiado para `Templates\` da saída (csproj). A geração depende de âncoras literais do modelo (`"TRECHO 01"`, `"- Juntas de ligação"`, `"Figura 4.1"`, `media/image23.png`; `WordReportService.cs#L381-L386`), então qualquer edição do modelo pode quebrar a geração.
  - `Templates/modelo folha.docx` está versionado, mas só é reconhecido pelo nome (`#L182-L185`).
  - **LibVLC:** nativo empacotado; o publish remove x86/arm64.
- **GAD-18 (F na presença, H/M no conteúdo) — Dados sensíveis (Q7), só caminhos:**
  - Arquivos:
    - `MODELO DE RELATORIO.docx`;
    - `artifacts/Etapa25/Relatorio_Buckman_001.docx`;
    - `artifacts/Etapa25/Relatorio_Buckman_002.docx`;
    - `artifacts/Relatorio_Prova_Etapa2.docx`;
    - `Templates/modelo folha.docx`.
  - Características verificadas: os 4 primeiros têm 28 imagens embutidas e campos de cliente/contrato; todos têm metadado de autor preenchido; os nomes em `artifacts/` sugerem uma empresa real.
  - `artifacts/` foi versionado **apesar** de `.gitignore#L10`.
  - Inspeção automatizada sem extrair conteúdo: 0 CNPJ e 0 padrões de coordenada no texto. As imagens não foram abertas.
  - Não há credenciais no código. Os testes usam CNPJ fictício. Não há `.db` nem vídeos no histórico.

### GAD-G — Documentação
- **GAD-19 (F, A):**
  - Confirmado: **não há** README, `docs/`, ADR, CHANGELOG nem CI.
  - Existe apenas `DISTRIBUICAO.md` (21 linhas: publish + Inno Setup) e `installer/GeoAutoDocs.iss` (v1.0.4, instalação por usuário em `{localappdata}\Programs`).
  - Os mesmos arquivos estão na fix.
  - O histórico começa em `d681e48` (10/08/2026).

### Q6 — Testes
- **GAD-20 (F, A):**

  | Branch | `[Fact]` | `[Theory]` | Arquivos |
  |---|---|---|---|
  | `main` | 108 | 2 | 14 |
  | fix | 228 | 12 | 24 |

  - **Coberto:** regras de ocorrência (45), store (10), projeto compartilhado (6/9), backup, transporte com servidor de pipe simulado, geração Word, captura de vídeo.
  - **Não coberto:**
    - UI WPF (`MainWindow.xaml.cs` tem 2.900 linhas na `main` e 5.605 na fix);
    - reabertura após checkpoint falho (GAD-10);
    - contrato cruzado com o GeoAutoEngine;
    - upgrade a partir de bancos antigos reais (há um único teste de legado);
    - desempenho com muitos vídeos e trechos.

## 4. Real × esperado (Q8)

| ID | Esperado | Real | Classe / Confiança |
|---|---|---|---|
| GAD-12a | "Banco sincronizado" com o GeoAutoEngine | Pipe + arquivo `.geoadocs.json`; nenhum banco comum | F / A |
| GAD-13 | Regra 8: versões compatíveis e sem ruptura silenciosa | Protocolo "v1" em duas variantes, sem negociação | F / A |
| GAD-03/06 | `main` = versão funcional | 319 commits fora da `main`; versão 1.0.4 idêntica | F / A; uso real = L |
| GAD-10 | Alterações locais "protegidas" (texto da UI) | Descartadas ao reabrir o projeto compartilhado | F (lógica) / H-M (efeito) |
| GAD-07 | Evolução de esquema controlada | `ALTER TABLE` ad hoc, sem versão nem rollback | F / A |
| GAD-11 | Backup cobre os dados do usuário | Não cobre `Cache\` nem a pasta compartilhada | F / A |
| GAD-18 | Repositório público sem dados de cliente | 5 `.docx` com prováveis dados reais | F / H-M |

## 5. Alterações feitas
**Nenhuma no GitHub:** sem branch, commit, Issue, PR, comentário, label nem release.

Feito apenas localmente nesta sessão:
- clones e `git fetch`;
- `git merge-tree`, que gera objetos só no clone local.

Este relatório foi salvo no Project do Claude (`governanca/`), no mesmo local dos documentos do Chat 00.

## 6. Testes e validações
**Executado:**
- `git` (fetch de todas as branches, `log`, `diff --stat`, `merge-base`, `cherry`, `merge-tree`, `ls-remote`, `grep` em `main`/fix);
- comparação estrutural dos contratos GeoAutoDocs × GeoAutoEngine;
- inspeção automatizada dos `.docx` (contagem de imagens e padrões, sem extrair conteúdo).

**Não executado:**
- `dotnet build` e `dotnet test`: .NET SDK ausente, instalação bloqueada e WPF exige Windows;
- testes de UI, mídia (AVI/LibVLC) e pipe real: exigem Windows e Civil 3D.

**Não acessível:** Issues, PRs (título e estado do #1), resultados das execuções dos workflows temporários e proteção de branch.

## 7. Impacto em outros projetos e contratos
- **GeoAutoEngine:**
  - contrato GADX v1 + `GeoAutoDocs.Inspection` v1, duplicado;
  - mensagens 6/7, existentes só na `main` do GeoAutoEngine e na fix do GeoAutoDocs;
  - importação de `.geoadocs.json`.

  Mesclar ou não a fix muda o comportamento visto pelo GeoAutoEngine.
- **GeoMap:** nenhuma relação no código. O GeoAutoDocs não tem dado espacial. Os GUIDs de trecho e ocorrência são os únicos identificadores estáveis disponíveis. Não proponho mecanismo de integração (regra 12).

## 8. Riscos, dependências e decisões necessárias
**Usuário:**
1. Qual build está instalada na equipe (`main` ou fix) e como é distribuída?
2. Qual é o estado do PR #1 (aberto ou fechado) e por que não foi mesclado?
3. Os 5 `.docx` contêm dados reais de cliente? Tornar o repositório privado ou limpar o histórico é operação destrutiva e exige autorização humana.
4. Conceder acesso à API do GitHub (Issues/PRs) ou colar aqui a página do PR #1.

**Chat 00:**
1. Definir o dono e o versionamento do contrato GADX, já que as mensagens 6/7 com o mesmo "v1" são uma ruptura silenciosa, antes de qualquer merge da fix.
2. Definir a estratégia para a fix (conflito em `MainWindow.xaml.cs`).
3. Priorizar GAD-10 (perda local) e GAD-07/11 (esquema e backup).

**Riscos técnicos:**
- GAD-10;
- GAD-08 (salvamento destrutivo por agregado);
- lock obsoleto não atômico;
- mensagem de UI enganosa no conflito de versão;
- vídeos com caminho local em projeto compartilhado;
- dependência de âncoras literais no modelo `.docx`.

## 9. Próximo passo pequeno e verificável
O usuário responde as perguntas 1 e 2 (build instalada e estado do PR #1). Com isso, o Chat 00 decide a linha de base do GeoAutoDocs (`main` ou fix) antes do roadmap.

Opcionalmente, o usuário pode conectar a pasta local do repositório no app desktop. Assim, o Chat 03 roda `dotnet build` e `dotnet test` nas duas branches, no Windows, e registra o resultado.

## 10. Resumo para encaminhar ao Chat 00
```
[Chat 03 → Chat 00 | Etapa 1 — auditoria GeoAutoDocs | 10/11/2026 | SOMENTE LEITURA]
Auditado: GAD@073191a (main), GAD@5d333d6 (fix/section-edit-save-stability), GAD@61a47b4 (Testes-ChatGPT); comparação com GAE@3acab63.
Acesso: código OK (clone público). Issues/PRs/Actions BLOQUEADOS (API 403; add_repo push negado pela política). Só via git: PR #1 head=5d333d6, merge ref 2d9b13c (23/09). Build/testes NÃO executados (sem .NET/Windows).
GAD-01 F/A .NET 8 WPF 1.0.4; EF Core SQLite 8.0.23; OpenXML 3.3.0; LibVLCSharp 3.10; xUnit. Sem CI; 38 workflows temporários (windows-latest) criados e apagados na fix; resultados inacessíveis.
GAD-03 F/A main = 13aefe4 + 1 commit (073191a, renumeração). fix = 319 commits/81 arquivos. Testes-ChatGPT = 5 commits só de UI (1 já absorvido na fix).
GAD-04 F/A fix traz: recepção de correções (msgs 6/7 + conexão persistente), motor de revisão (11 regras), ordenação/nomeação de trechos, segurança AVI, SaveSectionAsync. Sem mudança de tabelas SQLite; novo enum AnomalyPosition=15.
GAD-05 F/A merge-tree local main+fix: CONFLITO em MainWindow.xaml.cs (efeito de 073191a).
GAD-06 L Build usada pela equipe desconhecida: main e fix declaram 1.0.4. Motivo do não-merge desconhecido (PR #1 ilegível).
GAD-07 F/A SQLite %LOCALAPPDATA%\GeoAutoDocs: EnsureCreated + ALTER TABLE ad hoc (ProjectStore.cs#L25-41,#L634); sem versão/migrations/rollback; .bak únicos.
GAD-08 F/A SaveAsync substitui o agregado inteiro e apaga linhas/arquivos ausentes do grafo; sem token de concorrência.
GAD-09 F/A Compartilhado: project.geoadocs.json + lock.json; heartbeat 1 min, obsoleto 5 min, checkpoint 15 s com versão otimista. Conflito = exceção exibida como "Servidor indisponível"; sem resolução. Vídeos não replicados.
GAD-10 F(lógica)/H-M Abrir projeto compartilhado sobrescreve cache e SQLite com o oficial (SharedProjectService.cs#L67-85): alterações locais não sincronizadas se perdem sem aviso. Igual na fix; sem teste.
GAD-11 F/A Backup zip na inicialização (SQLite + projects\, retenção 5). NÃO cobre Cache\ (projetos compartilhados), pasta de rede, vídeos, .docx.
GAD-12a F/A "Banco sincronizado" REFUTADO: o GAE não tem banco. Compartilhado = Named Pipe GeoAutoEngine.Inspection (GAD cliente) + arquivo .geoadocs.json (sidecar do .docx; GAE importa em PluginEntry.cs#L202) + GUIDs.
GAD-12b F/A Pipe: formato no fio, DTOs e schema GeoAutoDocs.Inspection v1 idênticos; só o enum difere (main GAD sem 6/7). Código duplicado, sem teste cruzado (confirma C00-03).
GAD-13 F/H-M main GAD é cliente efêmero: GAE.SendCorrectionsAsync falha com "não conectado" (NamedPipeInspectionServer.cs#L146-151). Se 6 chega durante push: "Tipo de mensagem desconhecido" → CommunicationError, mesmo com inspeção aceita. Sem negociação de capacidade (confirma C00-02).
GAD-14 H/M Payload de correção compatível em nomes; semântica de "campo ausente" (HasX no GAD × GAE serializa todos os campos) sem teste cruzado → validar com Chat 02.
GAD-15 F/A GUIDs v4 no cliente para todas as entidades; estáveis; exportados no intercâmbio. Pasta do projeto usa GUID ≠ Project.Id.
GAD-16 F/A Nenhuma coordenada/CRS/handle no GAD; StartPoint/EndPoint/Reference = texto livre opcional; Ack do GAE não devolve handle. Vínculo espacial só via GAE (ou heurística textual, H).
GAD-17 F/A Vídeos por caminho absoluto (local/UNC), nunca copiados. Capturas/evidências em projects\ ou Cache\. Geração do .docx depende de âncoras literais do modelo ("TRECHO 01", media/image23.png).
GAD-18 F/H-M Repositório público com prováveis dados de cliente (só caminhos): MODELO DE RELATORIO.docx; artifacts/Etapa25/Relatorio_Buckman_00{1,2}.docx; artifacts/Relatorio_Prova_Etapa2.docx; Templates/modelo folha.docx. artifacts/ versionado apesar do .gitignore. Sem credenciais.
GAD-19 F/A Sem README/docs/ADR/CHANGELOG/CI; só DISTRIBUICAO.md e installer/GeoAutoDocs.iss.
GAD-20 F/A Testes: main 108 Fact + 2 Theory; fix 228 + 12. Sem testes de UI (MainWindow 2.900/5.605 linhas), contrato cruzado, reabertura pós-falha, upgrade de banco real.
Decisões — Usuário: build instalada? estado/motivo do PR #1? dados de cliente nos .docx (privatizar/limpar = destrutivo)? liberar API GitHub.
Decisões — Chat 00: dono/versão do contrato GADX antes de mesclar a fix; estratégia da fix (conflito); priorizar GAD-10/07/11.
Próximo passo: usuário responde build instalada + PR #1 → Chat 00 fixa a linha de base do GeoAutoDocs.
Relatório completo: Project GeoEcosystem › governanca/ETAPA_1_RELATORIO_CHAT_03_GEOAUTODOCS.md
```
