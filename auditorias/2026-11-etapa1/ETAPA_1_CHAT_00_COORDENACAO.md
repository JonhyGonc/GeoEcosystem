# Etapa 1 — Coordenação da auditoria (Chat 00)

## 1. Identificação
- **Agente:** Chat 00 — coordenador GeoEcosystem
- **Data:** 10/11/2026
- **Estado:** auditoria (reconhecimento) — **somente leitura**
- **Escopo:** reconhecimento dos três repositórios para fixar o protocolo comum e instruir os Chats 01–03. Não é a auditoria profunda, que cabe aos especialistas.
- **Commits de referência (data de corte 10/11/2026):**

| Repositório | Branch | Commit de referência | Data do commit |
|---|---|---|---|
| `JonhyGonc/Geomap` (privado) | `main` | `b9be25c94e5a2ca20e8c62a6d0616b76048d0ae0` | 10/11/2026 |
| `JonhyGonc/GeoAutoEngine` (público) | `main` | `3acab634ebceb17ec41f256244c3da56e092e6c9` | 23/09/2026 |
| | `sync-section-names` (não mesclada) | `2937469d135df612fe4cb41846bf479f32f99ec3` | 13/10/2026 |
| | `feature/atualizar-textos-anomalias` (não mesclada) | `17245284e3d948637da390ab4ea24d9dc1acbc38` | 22/10/2026 |
| | `fix/correction-export-feedback` (não mesclada; contida em `sync-section-names`) | `c2c10e4856ab8a5866059ad1b067c1dc0f6af2c8` | 23/09/2026 |
| `JonhyGonc/GeoAutoDocs` (público) | `main` | `073191a5a726a48c204616e4f0fcacf4329f5085` | 13/10/2026 |
| | `fix/section-edit-save-stability` (não mesclada) | `5d333d67f39433ac1b797af38a05acb61cbc5f63` | 23/09/2026 |
| | `Testes-ChatGPT` (não mesclada) | `61a47b4606bb23121f6e67d576b30236b889ae72` | 21/08/2026 |
| `GeoEcosystem` | — | não existe (confirmado pelo usuário em 10/11/2026) | — |

## 2. Objetivo e critérios de aceitação
Objetivo: dar aos Chats 01–03 uma base comum e verificável para auditar em paralelo.
Critérios: (a) commit de referência e data de corte por repositório; (b) perguntas comuns e específicas; (c) formato de evidências; (d) três instruções prontas para encaminhar.

## 3. Evidências do reconhecimento
Todas as referências estão nos commits de referência acima.

### GeoMap
- Stack: .NET 10 (8 projetos `net10.0`), Npgsql 8.0.9, PostgreSQL/PostGIS via `docker-compose.yml`, React 19 + MapLibre 6.8 + Vite 8 (`apps/web/package.json`).
- Governança própria: `AGENTS.md`, `docs/DECISIONS.md` (DEC-001…DEC-029, PEND-007), `docs/BACKLOG.md`. Issues #1–#57 (29 PRs, todas mescladas); abertas: #44 (Playwright), #48 (curvas na ponte DXF), #50 (Shapefile).
- Contrato de importação versionado: `docs/IMPORT_CONTRACT.md`, `contracts/geomap/0.1|0.2|0.3/`, validador `tools/importer/GeoMap.Contract/GeoMapContractValidator.cs`. v0.2 = `network.color` (DEC-028, #52); v0.3 = `Point`/`MultiPoint` (DEC-029, #49).
- Banco: `database/migrations/001…003` (up/down). CRS canônico EPSG:4326 (DEC-011). IDs determinísticos a partir de `sourceSystem` + `externalId`; `sourceType` + `sourceId` dá a identidade da feição; a reimportação substitui o snapshot da rede (DEC-022).
- Dois caminhos de entrada CAD:
  1. o exportador `GEOAUTO3GEOMAPEXPORT` do GeoAutoEngine (DEC-025; `docs/CIVIL3D_EXPORTER.md`), que só gera v0.1 com `LWPOLYLINE` de segmentos retos;
  2. a ponte DXF em Python (`tools/dxf-converter/geomap_dxf.py`). A DEC-024 a declarou "descartável", mas é ela que gera v0.2/v0.3 (cores e blocos), e a Issue #48 propõe ampliá-la.
- CI da `main` em `b9be25c`: 3 check-runs com `success` (API GitHub). Proteção de branch não consultável (plano gratuito em repositório privado).

### GeoAutoEngine
- Versão 3.1.1 (`Directory.Build.props`); bundle para Civil 3D 2024 (R24.3), 2025 (R25.0) e 2026 (R25.1) (`build/GeoAutoEngine.bundle/PackageContents.xml`). O `README.md` diz "AutoCAD 2026 e Civil 3D 2026", o que diverge de `COMPATIBILITY.md`.
- Camadas: Domain / Application / Csv / Cad2026 / Plugin (`docs/adr/ADR-001-PROJECT-BOUNDARIES.md`).
- Exportador GeoMap: `src/GeoAutoEngine.Application/GeoMapExport/GeoMapExportSerializer.cs` (`ContractVersion = "0.1"`). A amostra `samples/geomap/polyline-export.geomap.json` é idêntica (JSON normalizado) a `Geomap:contracts/geomap/0.1/examples/valid-geoautoengine-polyline.geomap.json`. É uma cópia manual: nenhuma CI cruzada detecta divergência futura.
- Integração com o GeoAutoDocs por **Named Pipe** `GeoAutoEngine.Inspection` (magic `GADX`, protocolo v1, schema `GeoAutoDocs.Inspection` v1): `src/GeoAutoEngine.Application/VideoInspection/InspectionPipeProtocol.cs`, `InspectionExchangeContracts.cs`, `InspectionCorrectionContracts.cs`; servidor em `src/GeoAutoEngine.Plugin/VideoInspection/Transport/NamedPipeInspectionServer.cs`; documento `docs/VIDEO_INSPECTION_CORRECTIONS.md`.
- A `main` já define as mensagens `CorrectionPush = 6` e `CorrectionAck = 7`.
- Associação trecho ↔ Polyline: `CadSectionAssociation` com `GeometryHandle` em `VideoInspectionSession.cs`. Não está verificado se persiste no DWG.
- O `README.md` cita `../GeoAutoEngine.Legacy` e `../reference`, que estão fora do repositório.
- CI: `.github/workflows/geomap-export.yml` e `video-inspection.yml` (`dotnet test`, .NET 8, ubuntu). Status não consultável nesta sessão.
- Possíveis dados reais de projeto em repositório público: `docs/audit/cad_gis_raw/*.raw.json`, `docs/evidence/` (não abertos).

### GeoAutoDocs
- .NET 8 / WPF (`net8.0-windows`), EF Core SQLite 8.0.23, LibVLCSharp 3.10, OpenXML 3.3.
- Não tem README, CI nem pasta docs/ADR; há apenas `DISTRIBUICAO.md` e `installer/GeoAutoDocs.iss`.
- Persistência:
  - banco SQLite local `GeoAutoDocs.db` (`AppDataPaths.cs`), criado com `EnsureCreatedAsync` mais `ALTER TABLE` ad hoc (`ProjectStore.cs:28`, `:634`), sem migrations versionadas;
  - "projeto compartilhado": arquivo `project.geoadocs.json` + `lock.json` em pasta compartilhada, com cache local e controle de versão (`SharedProjectService.cs`).
- Cliente do pipe: `src/GeoAutoDocs.Infrastructure/NamedPipeInspectionTransport.cs` e uma cópia própria de `InspectionPipeProtocol.cs`. Na `main`, o enum **não tem** `CorrectionPush/CorrectionAck`; só a branch `fix/section-edit-save-stability` tem.
- A `main` ganhou 1 commit desde `13aefe4` (21/08). A branch `fix/section-edit-save-stability` acumula **319 commits** (27/08–23/09; 81 arquivos, +11.866/−733) que nunca foram mesclados.
- Artefatos `.docx` versionados em repositório público (`artifacts/Etapa25/Relatorio_*.docx`, `MODELO DE RELATORIO.docx`), com possível conteúdo de cliente (não abertos).

## 4. Achados preliminares (a confirmar na auditoria)
| ID | Achado | Classe | Confiança |
|---|---|---|---|
| C00-01 | As integrações reais são duas e pontuais: (a) arquivo `.geomap.json` v0.1 do GeoAutoEngine para o GeoMap; (b) Named Pipe local entre GeoAutoEngine e GeoAutoDocs. O GeoMap não conhece inspeções, e o GeoAutoDocs não conhece geometria. | Fato | alta |
| C00-02 | O protocolo do pipe "v1" tem duas variantes incompatíveis com o mesmo número de versão: a `main` do GeoAutoEngine tem as mensagens 6/7, a `main` do GeoAutoDocs não. O handshake não detecta a diferença, e uma mensagem desconhecida gera exceção na leitura. É uma ruptura silenciosa (viola a regra 8 do protocolo). | Fato (código); efeito em execução = hipótese | alta / média |
| C00-03 | O código do contrato do pipe está duplicado nos dois repositórios, sem fonte única nem teste cruzado. | Fato | alta |
| C00-04 | A maior parte do trabalho recente do GeoAutoDocs (319 commits) está fora da `main`. Não se sabe qual build a equipe usa. | Fato / lacuna | alta |
| C00-05 | O GeoAutoEngine tem trabalho de vídeo-inspeção não mesclado (`sync-section-names`, `feature/atualizar-textos-anomalias`). | Fato | alta |
| C00-06 | Os dados CAD reais chegam ao GeoMap pela ponte DXF (Python, v0.2/v0.3), e não pelo exportador do GeoAutoEngine (v0.1). Isso tensiona a DEC-024 ("descartável") e a regra 13 (autoridade do GeoAutoEngine sobre a geometria). | Fato (docs) / interpretação | média |
| C00-07 | A fixture GeoAutoEngine → GeoMap é uma cópia manual; a compatibilidade é verificada só no momento da cópia. | Fato | alta |
| C00-08 | Não há identificador comum entre os três sistemas. GeoMap: UUID determinístico + handle CAD; GeoAutoEngine: handle CAD + `SectionId`; GeoAutoDocs: GUIDs próprios. O handle do AutoCAD é estável só dentro do mesmo DWG. | Hipótese | média |
| C00-09 | A memória do usuário fala em "banco sincronizado" entre GeoAutoEngine e GeoAutoDocs. O código mostra pipe + arquivo de projeto compartilhado, sem banco em comum. | Divergência a confirmar | média |
| C00-10 | Possíveis dados reais de cliente/projeto em repositórios públicos (GeoAutoEngine, GeoAutoDocs). | Hipótese (não aberto) | baixa |
| C00-11 | Versões de runtime distintas: GeoMap .NET 10; GeoAutoEngine .NET 8 + .NET Framework 4.8 (Civil 3D 2024); GeoAutoDocs .NET 8. | Fato | alta |

## 5. Alterações feitas
Nenhuma nos repositórios: sem branches, commits, Issues, PRs ou comentários. Só clones locais rasos nesta sessão. No Project do Claude foram salvos `governanca/00_PROTOCOLO_COMPARTILHADO.md`, `governanca/PROMPT_CHAT_00_COORDENADOR.md` e este documento.

## 6. Testes / validações
- Executado: leitura de código e docs; `git` (log, merge-base, diff --stat); `gh api` de Issues/PRs e check-runs do GeoMap; comparação JSON das fixtures (idênticas).
- Não executado: build e testes dos três projetos. O `dotnet` não está instalado na sessão e a instalação de pacotes está bloqueada.
- Não acessível: Issues, PRs e CI do GeoAutoEngine e do GeoAutoDocs. A conexão com credenciais foi negada pela política da sessão, apesar da autorização do usuário.

## 7. Impacto em outros projetos e contratos
Contratos identificados: (1) GeoMap Import Contract 0.1/0.2/0.3 (dono: GeoMap; produtores: GeoAutoEngine e ponte DXF); (2) GeoAutoDocs.Inspection v1 + pipe GADX v1 (definido em dois lugares). Os dois precisam de dono formal e de registro no GeoEcosystem (Etapa 3).

## 8. Riscos, dependências e decisões necessárias
- **Usuário:** qual versão de cada aplicativo está instalada na equipe (`main` ou branch)?
- **Usuário:** os repositórios públicos contêm dados de cliente? Avaliar tornar privado ou limpar o histórico. É operação destrutiva e exige autorização humana.
- **Chat 00 (Etapa 2):** dono do contrato do pipe; destino da ponte DXF; estratégia de identificador comum.

## 9. Próximo passo
Usuário encaminha as três instruções (abaixo) aos Chats 01–03 e me devolve os relatórios.

---

## Protocolo comum de auditoria (Chats 01–03)
1. **Modo:** somente leitura. Não criar branch, commit, Issue, PR, comentário, label nem release. Não rodar comandos que escrevam no repositório remoto.
2. **Commit de referência:** audite os SHAs da tabela da seção 1. Se houver commits mais novos, liste-os à parte (SHA, data, assunto) sem auditá-los a fundo.
3. **Data de corte:** 10/11/2026.
4. **Evidência:** cada achado tem
   - ID (`GM-nn`, `GAE-nn`, `GAD-nn`);
   - classe: **Fato** (verificado em código/commit), **Hipótese** (inferido) ou **Lacuna** (não verificável, com o motivo);
   - referência `repo@sha7:caminho#Lx-Ly` ou símbolo;
   - Issue/PR com URL real, quando houver;
   - confiança (alta / média / baixa).
5. **Testes:** se puder compilar/testar, informe comando, resultado e ambiente. Se não puder, escreva "não executado — motivo". Nunca presuma sucesso.
6. **Segurança:** não copie credenciais, coordenadas reais, nomes de clientes nem conteúdo de documentos de cliente para o relatório. Cite só o caminho.
7. **Integração:** descreva o que existe e as lacunas. Não proponha nem escolha mecanismo de integração (regra 12).
8. **Entrega:** relatório padrão de 10 itens do protocolo, com o item 10 (resumo para o Chat 00) em até 40 linhas.

### Perguntas comuns (responder Q1–Q8 em todo relatório)
- **Q1** Stack e versões reais (runtime, frameworks, hosts), comandos de build/teste e estado da CI.
- **Q2** Estado das branches: o que está na `main` e o que está fora; qual build está distribuída para a equipe (perguntar ao usuário se não houver evidência).
- **Q3** Modelo de dados e persistência: entidades, onde ficam, como evoluem (migrations/esquema).
- **Q4** Identificadores: quais existem, quem gera, se são estáveis e onde persistem.
- **Q5** Contratos de entrada/saída: formatos, versões, validadores, onde estão definidos e se há cópias em outros repositórios.
- **Q6** Testes: quantidade, cobertura funcional e o que não é testado.
- **Q7** Dados sensíveis ou reais versionados (só caminhos).
- **Q8** Riscos, dívida técnica e inconsistências entre docs e código.

---

## Instruções para encaminhar

### Chat 01 — GeoMap
```
[Chat 00 → Chat 01 | Etapa 1 — auditoria inicial do GeoMap | 10/11/2026]
Autorização: o usuário autoriza auditoria SOMENTE LEITURA ao encaminhar esta mensagem. Faça antes a verificação de acesso (regra 2) e reporte bloqueios.
Base: 00_PROTOCOLO_COMPARTILHADO.md + "Protocolo comum de auditoria" e Q1–Q8 do doc governanca/ETAPA_1_COORDENACAO_AUDITORIA.md (Project GeoEcosystem).
Repositório: JonhyGonc/Geomap @ main b9be25c94e5a2ca20e8c62a6d0616b76048d0ae0. Data de corte: 10/11/2026.
Respeite também o AGENTS.md do repositório.

Responda Q1–Q8 e, especificamente:
GM-A. Entrada de dados CAD: compare a ponte DXF (tools/dxf-converter, Python) com o exportador GEOAUTO3GEOMAPEXPORT do GeoAutoEngine. Qual caminho carregou os dados reais (#17, #46, #49, #52)? Liste o que só a ponte faz (v0.2 cor, v0.3 blocos, ATTRIB, posição de bloco, filtros _TG*/SIMB_PONTO) e confronte com a DEC-024 ("descartável") e a Issue #48.
GM-B. Contrato: confirme regras e limites de 0.1/0.2/0.3 entre doc, validador (tools/importer/GeoMap.Contract) e testes. Como a fixture valid-geoautoengine-polyline.geomap.json é mantida (cópia manual?) e como uma divergência do produtor seria detectada.
GM-C. Identidade e reimportação: UUIDs determinísticos, sourceId = handle CAD, substituição de snapshot. O que acontece com IDs/atributos quando o DWG é editado, copiado ou reexportado?
GM-D. Pontos de extensão: o que existiria hoje para receber dados não geométricos (ex.: inspeções/anomalias) sem violar DEC-008/DEC-021? Apenas descreva; não proponha mecanismo.
GM-E. Operação: autenticação, hospedagem (PEND-007), configuração por ambiente, performance registrada (docs/PERFORMANCE.md, benchmarks) e estado das Issues abertas #44, #48, #50.
Entrega: relatório padrão de 10 itens; item 10 com até 40 linhas, IDs GM-nn e referências Geomap@sha7:caminho#Lx.
```

### Chat 02 — GeoAutoEngine
```
[Chat 00 → Chat 02 | Etapa 1 — auditoria inicial do GeoAutoEngine | 10/11/2026]
Autorização: o usuário autoriza auditoria SOMENTE LEITURA ao encaminhar esta mensagem. Faça antes a verificação de acesso (regra 2) e reporte bloqueios (inclusive se Issues/PRs/CI não forem acessíveis).
Base: 00_PROTOCOLO_COMPARTILHADO.md + "Protocolo comum de auditoria" e Q1–Q8 do doc governanca/ETAPA_1_COORDENACAO_AUDITORIA.md (Project GeoEcosystem).
Repositório: JonhyGonc/GeoAutoEngine @ main 3acab634ebceb17ec41f256244c3da56e092e6c9. Não mescladas, auditar também: sync-section-names 2937469 (contém fix/correction-export-feedback c2c10e4) e feature/atualizar-textos-anomalias 1724528. Data de corte: 10/11/2026.

Responda Q1–Q8 e, especificamente:
GAE-A. Versões: qual build (3.1.1? qual branch?) está instalada na equipe; divergência README ("2026") × COMPATIBILITY.md (2024–2026); dependência de ../GeoAutoEngine.Legacy e ../reference para build/testes.
GAE-B. Exportador GeoMap (GeoMapExportSerializer.cs): escopo real (LWPOLYLINE retas, v0.1). Lacunas em relação ao contrato GeoMap 0.2 (cor da layer) e 0.3 (blocos como pontos) e ao tratamento de bulges/arcos (Issue #48 do GeoMap).
GAE-C. Pipe com o GeoAutoDocs: documente o protocolo GADX v1 a partir do código (mensagens 1–7, handshake, schema GeoAutoDocs.Inspection v1, limites). Verifique: o que acontece quando um cliente GeoAutoDocs SEM as mensagens 6/7 conecta e recebe CorrectionPush? Há negociação de capacidade?
GAE-D. Identidade: como SectionId/OccurrenceId (GeoAutoDocs) se associam ao handle da Polyline (CadSectionAssociation). A associação persiste no DWG (XData/dicionário) ou só em memória? Sobrevive a reabrir o desenho?
GAE-E. Autoridade sobre geometria: quais dados técnicos nascem aqui (estaqueamento, textos, diâmetros, anomalias desenhadas) e quais vêm de outros sistemas.
GAE-F. Repositório público: docs/audit/cad_gis_raw, docs/evidence e samples contêm dados reais? Informe só caminhos.
Entrega: relatório padrão de 10 itens; item 10 com até 40 linhas, IDs GAE-nn e referências GeoAutoEngine@sha7:caminho#Lx.
```

### Chat 03 — GeoAutoDocs
```
[Chat 00 → Chat 03 | Etapa 1 — auditoria inicial do GeoAutoDocs | 10/11/2026]
Autorização: o usuário autoriza auditoria SOMENTE LEITURA ao encaminhar esta mensagem. Faça antes a verificação de acesso (regra 2) e reporte bloqueios (inclusive se Issues/PRs não forem acessíveis).
Base: 00_PROTOCOLO_COMPARTILHADO.md + "Protocolo comum de auditoria" e Q1–Q8 do doc governanca/ETAPA_1_COORDENACAO_AUDITORIA.md (Project GeoEcosystem).
Repositório: JonhyGonc/GeoAutoDocs @ main 073191a5a726a48c204616e4f0fcacf4329f5085. Não mescladas, auditar também: fix/section-edit-save-stability 5d333d6 (319 commits fora da main desde 13aefe4) e Testes-ChatGPT 61a47b4. Data de corte: 10/11/2026.

Responda Q1–Q8 e, especificamente:
GAD-A. Branches: o que fix/section-edit-save-stability muda em relação à main (funcionalidades, esquema, protocolo). Qual das duas é a versão usada pela equipe? A main está obsoleta? Por que não foi mesclada?
GAD-B. Persistência: SQLite local (EnsureCreated + ALTER TABLE ad hoc em ProjectStore.cs) e projeto compartilhado (project.geoadocs.json + lock.json em SharedProjectService.cs). Como o esquema evolui, como conflitos/locks são tratados, risco de perda de dados, backup/restauração.
GAD-C. Confirme ou refute: existe "banco sincronizado" com o GeoAutoEngine? Descreva o que realmente é compartilhado (pipe, arquivo, pasta).
GAD-D. Cliente do pipe (NamedPipeInspectionTransport.cs): compare a cópia local de InspectionPipeProtocol/InspectionExchange com a do GeoAutoEngine (main 3acab63) e liste diferenças. Na main faltam CorrectionPush/CorrectionAck (6/7); confirme o comportamento ao recebê-las.
GAD-E. Identificadores e vínculo espacial: GUIDs de projeto/relatório/trecho/ocorrência; StartPoint/EndPoint/Reference como texto. Há algum dado que permita ligar trechos a geometria sem o GeoAutoEngine?
GAD-F. Mídia e relatórios: onde ficam vídeos, capturas e .docx gerados; dependências (LibVLC, OpenXML); modelos versionados. Repositório público: artifacts/*.docx e "MODELO DE RELATORIO.docx" contêm dados de cliente? Informe só caminhos.
GAD-G. Ausência de README, CI e docs: confirme e liste o que existe de documentação (DISTRIBUICAO.md, installer).
Entrega: relatório padrão de 10 itens; item 10 com até 40 linhas, IDs GAD-nn e referências GeoAutoDocs@sha7:caminho#Lx.
```
