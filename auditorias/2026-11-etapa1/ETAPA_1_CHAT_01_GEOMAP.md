# Etapa 1 — Relatório de auditoria do GeoMap (Chat 01)

## 1. Identificação
- **Agente:** Chat 01, especialista GeoMap
- **Repositório:** `JonhyGonc/Geomap` (privado), branch `main`, commit `b9be25c94e5a2ca20e8c62a6d0616b76048d0ae0` (10/11/2026). Reconfirmado no início desta etapa: `origin/main` = `b9be25c`.
- **Data / corte:** 10/11/2026
- **Estado:** auditoria **somente leitura**
- **Escopo:** Q1–Q8 e GM-A a GM-E da instrução do Chat 00. Leitura de apoio do GeoAutoEngine (`3acab63`) feita só nos pontos de integração.

**Acesso verificado (regra 2):**
- GeoMap: leitura, escrita e API REST (permissão admin).
- GeoAutoEngine e GeoAutoDocs: só leitura pelo git, sem acesso a Issues, PRs e CI.
- `GeoEcosystem`: inexistente.
- O GraphQL do GitHub está bloqueado na sessão; todas as consultas usaram a REST.
- Proteção de branch não consultável (plano gratuito em repositório privado).

Todas as referências abaixo usam o prefixo `Geomap@b9be25c`, omitido para encurtar.

## 2. Objetivo e critérios de aceitação
- **Objetivo:** dar ao Chat 00 um diagnóstico verificável do GeoMap para consolidar o roadmap.
- **Critérios:**
  - Q1–Q8 e GM-A…E respondidos com classe, referência e confiança;
  - testes executados e não executados declarados;
  - nenhuma alteração no repositório;
  - item 10 com até 40 linhas.

## 3. Evidências por pergunta

### Q1 — Stack, build e CI
- **Backend e ferramentas:** 8 projetos `net10.0`, com Npgsql 8.0.9 e xUnit 2.9.3 (`*.csproj`). O .NET 10 substituiu o .NET 8 pela DEC-003 (`docs/DECISIONS.md#L17-L23`, Issue #38).
- **Banco:** `postgis/postgis:16-3.5` (`docker-compose.yml#L5`).
- **Web:**
  - React 19.2.8, MapLibre 6.8.0, Vite 8.2.2, Vitest 5, TypeScript 6.0.3 (`apps/web/package.json`);
  - Node 24 na CI.
- **Ponte DXF:** Python só com a biblioteca padrão (`tools/dxf-converter/geomap_dxf.py`).
- **Build e teste:** `dotnet build/test GeoMap.sln`, `npm ci && npm run lint && npm run test && npm run build`, `python3 -m unittest discover -s tests/dxf-converter`.
- **CI:**
  - `.github/workflows/backend-ci.yml` tem 2 jobs. O segundo sobe o Compose, aplica as migrations up/down/up, importa as fixtures 0.1/0.2/0.3 e a ponte DXF, carrega SMALL/MEDIUM/LARGE, verifica a API e o MVT e roda os benchmarks.
  - `frontend-ci.yml` roda lint, testes, build e a verificação do worker do MapLibre.
  - Estado em `b9be25c`: **3 check-runs `success`** (runs 42838167056 e 42838166784, 10/11/2026).

### Q2 — Branches e distribuição
- **Fato:** as 29 branches remotas além da `main` têm 0 commits à frente da `main` (API `compare`). Os 29 PRs (#19–#57) estão todos mesclados.
- **Fato:** não há releases nem tags.
- **Fato:** não existe artefato distribuível.
  - O `docker-compose.yml` sobe só o banco e a API, com `ASPNETCORE_ENVIRONMENT: Development` (`#L28`).
  - O frontend roda por `vite` com proxy `/api` (`apps/web/vite.config.ts`).
- **Lacuna:** se alguém da equipe usa o GeoMap fora de uma máquina de desenvolvimento. Pergunta ao usuário.

### Q3 — Modelo de dados e persistência
- **Tabelas:** `projects`, `networks` e `network_features` (`database/migrations/001_initial_spatial_model.up.sql#L5-L47`).
  - Geometria `geometry(Geometry,4326)`, `properties jsonb`, `source_id/source_layer/source_type`.
  - Índices: GiST `#L55-L57` e único `(network_id, source_type, source_id)` `#L59-L61`.
- **Migrations:**
  - `002` adiciona `networks.color` com CHECK `^#[0-9A-F]{6}$` (`002_network_color.up.sql#L5-L8`);
  - `003` aceita POINT e MULTIPOINT (`003_point_features.up.sql#L15-L18`), com rollback que falha se houver pontos (`003…down.sql#L25-L35`).
- **Evolução:** SQL up/down aplicado à mão com `psql` (DEC-012, `docs/DECISIONS.md#L75-L79`). **Não há tabela de controle das migrations aplicadas** (GM-03).
- **Histórico:** inexistente. Cada importação apaga e reinsere as feições da rede (GM-06).

### Q4 — Identificadores
UUID v5 (SHA-1, namespace fixo), gerado pelo importador (`tools/importer/GeoMap.Importer/DeterministicGeoMapIds.cs#L8-L25`):

| Entidade | Composição do UUID |
|---|---|
| projeto | `sourceSystem` + `project.externalId` |
| rede | projeto + `network.externalId` |
| feição | rede + `sourceType` + `sourceId`; sem `sourceId`, a posição no documento |

`sourceId` é o handle CAD, tanto na ponte (`geomap_dxf.py#L274-L280`, `#L313`, `#L590`) quanto no exportador do GeoAutoEngine. Esses IDs persistem só no banco do GeoMap. Detalhes em GM-C.

### Q5 — Contratos
- **Contrato:** GeoMap Import Contract 0.1/0.2/0.3.
  - Especificação: `docs/IMPORT_CONTRACT.md`.
  - Exemplos: `contracts/geomap/{0.1,0.2,0.3}/examples`.
  - Validador: `tools/importer/GeoMap.Contract/GeoMapContractValidator.cs#L10-L39`.
- **Transportes:** `.geomap.json` e `.geomap.ndjson`.
- **Produtores:**
  - a ponte DXF gera 0.1, 0.2 ou 0.3 (`geomap_dxf.py#L708`);
  - o GeoAutoEngine gera só 0.1 (`GeoAutoEngine@3acab63:src/GeoAutoEngine.Application/GeoMapExport/GeoMapExportSerializer.cs#L9-L10`).
- **Cópia em outro repositório:** a fixture `valid-geoautoengine-polyline.geomap.json` (GM-B).
- **Saída:** só a API REST de leitura e o MVT (`docs/API.md`). Não existe contrato de saída para outros sistemas.

### Q6 — Testes
| Suíte | Quantidade |
|---|---|
| .NET | 45 `[Fact]` + 16 `[Theory]`: Api 7+7, Contract 13+6, DatasetGenerator 5+3, Importer 20 |
| Web | 41 casos em 11 arquivos `*.test.ts(x)` |
| Ponte DXF | 13 testes Python |
| Integração | 34 scripts em `tests/integration` (SQL, shell, Python), executados na CI |

**Sem cobertura:**
- mapa em navegador real (Issue #44);
- limites do contrato além de 128 propriedades: 50 MiB, 10.000 feições, 100.000 vértices, 64 KiB, profundidade 4 e 4.096 caracteres não têm teste unitário;
- desempenho com pontos;
- regeneração cruzada da fixture do GeoAutoEngine.

### Q7 — Dados sensíveis ou reais versionados
- **Fato:** nenhum DWG ou DXF real está versionado. O `.gitignore` bloqueia `*.dwg`, `*.dxf` e `private-data/`, com exceção só para `tests/fixtures/dxf/synthetic-network.dxf`, que é sintético.
- **Fato:** os exemplos de contrato são sintéticos. Não há credencial real; `change-me-local-only` aparece só como exemplo local.
- **Baixo risco:**
  - `docs/REAL_DATA_VALIDATION.md#L157-L235` traz agregados (fuso UTM, extensão aproximada, km por diâmetro);
  - o corpo do PR #54 cita identificadores internos de desenhos.
  - Não copiados aqui; o repositório é privado.

### Q8 — Riscos e inconsistências
Ver GM-03 a GM-20 (item 4) e o item 8.

## 4. Achados (real × esperado)

| ID | Achado | Classe | Referência | Confiança |
|---|---|---|---|---|
| GM-01 | `main` estável e coerente. CI verde nos 3 jobs, nenhuma branch pendente, Issues #1–#57 rastreadas. | Fato | check-runs `b9be25c`; PRs #19–#57 | alta |
| GM-02 | Não há build distribuída nem hospedagem; PEND-007 continua aberta. | Fato | `docs/DECISIONS.md#L246-L250`; `docker-compose.yml#L28` | alta |
| GM-03 | Migrations manuais sem registro do que foi aplicado em cada banco, o que permite divergência silenciosa. A API exige a 002 (`database/migrations/README.md#L87`). | Fato | `database/migrations/README.md`; `001…up.sql` | alta |
| GM-04 | A origem da importação não é persistida: `sourceSystem`, `contractVersion`, `exportedAt`, `sourceCrs`, `units` e o hash do arquivo ficam fora do banco. `created_at` é reiniciado a cada importação, mas a API o expõe como `createdAt`. | Fato | `PostgresGeoMapImportStore.cs#L121-L133`, `#L243-L258`; `001…up.sql#L12-L34`; `FeatureDetailsQuery.cs#L21` | alta |
| GM-05 | O mesmo projeto importado pelos dois caminhos gera **dois projetos**: a ponte usa `sourceSystem = "Civil 3D DXF"` e o GeoAutoEngine `"Civil 3D"`, e o UUID depende desse valor. | Fato | `geomap_dxf.py#L711`; `DeterministicGeoMapIds.cs#L13-L14`; `GeoMapExportSerializer.cs#L10` (GeoAutoEngine) | alta |
| GM-06 | A reimportação apaga as feições da rede (`DELETE`) sem histórico. Não há caminho para remover projeto ou rede, então trocar o `externalId` deixa órfãos. Nas importações por rede, o nome do projeto é o da última importada. | Fato | `PostgresGeoMapImportStore.cs#L222-L234`, `#L121-L124` | alta |
| GM-07 | Todos os dados reais (3 redes: #17, #46 e o projeto de água, validado na Issue #17, com cores na #52 e blocos na #49/#54/#55) entraram pela **ponte DXF**. O exportador `GEOAUTO3GEOMAPEXPORT` só foi exercitado com a fixture sintética. A pendência manual do PR #36 foi fechada pelo PR #46, usando a ponte. | Fato | `docs/REAL_DATA_VALIDATION.md#L17-L52`, `#L183-L187`; PR #36; PR #46 | alta |
| GM-08 | A DEC-024 diz que a ponte é "descartável", mas ela virou o único produtor de 0.2 e 0.3, e as DEC-028/029 a citam como produtora. A Issue #48 propõe ampliá-la. A DEC-024 nunca foi revista. Isso tensiona a regra 13 (autoridade do GeoAutoEngine sobre a geometria). | Fato (docs) / interpretação | `docs/DECISIONS.md#L183-L200`, `#L230-L244`; Issue #48 | alta |
| GM-09 | Na terceira rede, um script privado fora do repositório uniu 17 layers e extraiu diâmetro e material do nome da layer. O dado no banco não é reproduzível com o código versionado. | Fato | `docs/REAL_DATA_VALIDATION.md#L183-L187` | alta |
| GM-10 | O "byte a byte" da fixture do GeoAutoEngine vale só em 09/09. Ela teve um único commit (`13735b8`) e hoje é idêntica, em JSON normalizado, a `GeoAutoEngine@3acab63:samples/geomap/polyline-export.geomap.json`. Nenhum dos dois repositórios regenera e compara. A doc afirma que a CI "confirma a compatibilidade entre os dois repositórios", o que é exagero. | Fato (GeoMap) / Hipótese (GeoAutoEngine: os testes não citam o sample) | `docs/IMPORT_CONTRACT.md#L87`; `docs/CIVIL3D_EXPORTER.md#L59-L66`; `GeoAutoEngine@3acab63:tests/GeoAutoEngine.Tests/GeoMapExportTests.cs` | alta / média |
| GM-11 | Os limites do contrato coincidem entre a doc e o validador. Os testes unitários não cobrem a maioria deles, e o exemplo 0.2 não é carregado pelos testes unitários (só pela CI de integração). | Fato | `GeoMapContractValidator.cs#L13-L22`; `tests/GeoMap.Contract.Tests/GeoMap.Contract.Tests.csproj` | alta |
| GM-12 | Documentação desatualizada (lista abaixo). | Fato | indicadas abaixo | alta |
| GM-13 | `units` é validado, mas não é usado nem comparado com o EPSG declarado; o PostGIS usa só o SRID. | Fato | `GeoMapImportDocument.cs#L45`; `PostgresGeoMapImportStore.cs#L250-L252` | alta |
| GM-14 | Diferenças de capacidade entre os dois produtores (lista abaixo). | Fato | `geomap_dxf.py#L27`, `#L359-L363`, `#L425-L426`, `#L468`, `#L566-L615`, `#L674-L678`, `#L692-L696`; `GeoAutoEngine@3acab63:src/GeoAutoEngine.Plugin/GeoMapExportCommands.cs#L20-L50` | alta |
| GM-15 | Blocos dinâmicos `*U…` não são filtrados pela ponte e entrariam com nome anônimo. A DEC-029 os declara fora do escopo, mas não diz que devem ser rejeitados. | Fato (código) / efeito: hipótese | `geomap_dxf.py#L660-L669`; `docs/DECISIONS.md#L244` | média |
| GM-16 | API sem autenticação ou autorização, sem CORS, com `AllowedHosts: *`. O importador exige connection string direta ao banco. Aceitável em uso local, bloqueante para qualquer hospedagem. | Fato | `apps/api/GeoMap.Api/Program.cs#L10-L51`; `appsettings.json#L11` | alta |
| GM-17 | Desempenho medido só para linhas. Pontos (0.3) não foram medidos (o gerador de datasets só cria linhas). Os baselines das Issues #5–#7 são da época do .NET 8. | Fato | `docs/PERFORMANCE.md#L118`, `#L137`, `#L153`; `DatasetLoader.cs#L236-L237`; PR #54 | alta |
| GM-18 | O mapa não é testado em navegador real. O bug #42 passou com CI verde (Issue #44). | Fato | Issue #44 | alta |
| GM-19 | Teste com volume de pontos (#49) e importação de Shapefile (#50) sem decisão. A #50 introduziria tecnologia nova (DECISIONS). | Fato | Issues #49, #50 | alta |
| GM-20 | O GeoMap não tem modelo para inspeções, anomalias ou histórico: o MVP exclui vídeo-inspeção e "histórico complexo". | Fato | `docs/MVP.md#L41-L51`; `docs/REAL_DATA_VALIDATION.md#L154-L155` | alta |

**GM-12 — documentação desatualizada:**
- `docs/IMPORT_CONTRACT.md`:
  - `#L15` diz "valor exato `0.1`";
  - `#L142` descreve `GMC_VERSION_UNSUPPORTED` como "diferente de 0.1";
  - `#L91` e `#L93` têm media types só da v0.1.
- `GeoMapContractValidator.cs#L189`: mensagem "version 0.1".
- `docs/BACKLOG.md#L13-L30` lista só as Issues #1–#18.
- `docs/PERFORMANCE.md` registra os baselines em .NET 8.

**GM-14 — capacidades por produtor:**
- **Só a ponte faz:**
  - `LINE`;
  - cor da layer (v0.2);
  - `INSERT` → `Point` (v0.3);
  - `ATTRIB` → `properties`;
  - posição pelo símbolo (heurística de 50×);
  - filtros `_TG*` e `SIMB_PONTO`;
  - descarte de Paper Space;
  - `inputSha256`, mas só no resumo, não no documento.
- **Só o GeoAutoEngine faz:**
  - seleção manual das entidades no desenho;
  - `externalId` da rede inferido.
- **Os dois:** recusam bulge e arco e ficam limitados a 10.000 feições, sem NDJSON.

### GM-A — Entrada de dados CAD
Ver GM-05, GM-07, GM-08, GM-09 e GM-14.

**Como os dados reais carregaram:**
- **#17 (1ª rede):** DXF → ponte → v0.1.
- **#46 (2ª rede):** DXF → ponte → v0.1.
- **Água (3ª rede):** DXF → ponte → script privado → v0.1.
- **#52 (cores):** ponte → v0.2.
- **#49 (blocos):** ponte → v0.3. A conversão real foi conferida em privado, e a validação de posição dos tês está no PR #55 e na DEC-029.

**Confronto com a DEC-024 e a Issue #48:**
- A DEC-024 continua dizendo que a ponte é "descartável" e "não se torna uma segunda arquitetura de importação" (`#L198-L200`).
- Hoje a ponte não é uma segunda arquitetura no importador: tudo passa pelo mesmo validador. Mas ela concentra regras de domínio CAD (cor, blocos, posição, filtros de levantamento) que o produtor oficial não tem.
- A #48 aumentaria essa diferença: excluir curvas com relatório, ou tesselar, o que conflita com "a tesselação pertence ao exportador definitivo" (`geomap_dxf.py#L361-L362`).

### GM-B — Contrato
**Regras por versão** (`GeoMapContractValidator.cs#L33-L39`, `#L205-L207`, `#L362-L371`):
- **0.1:** linhas; `color` recusado.
- **0.2:** linhas e `color` opcional.
- **0.3:** linhas, pontos e `color`.

**Limites** — doc (`IMPORT_CONTRACT.md#L116-L129`) = validador (`#L13-L22`):
- 50 MiB;
- 10.000 feições;
- 100.000 vértices;
- 128 propriedades / 64 KiB / profundidade 4 / 4.096 caracteres;
- NDJSON: 64 KiB de metadados, 16 MiB por linha, 5.000.000 feições.

**Testes:** cobrem versões, cor, pontos, desconhecidos, duplicados e 128 propriedades. Os demais limites não têm teste (GM-11).

**Fixture do GeoAutoEngine:** cópia manual (GM-10). Hoje, uma divergência do produtor só seria percebida:
1. por alguém recopiando o sample;
2. por falha no import de um arquivo real.

Não há verificação automática que alcance a `main` do GeoAutoEngine.

### GM-C — Identidade e reimportação

| Cenário | Efeito no GeoMap |
|---|---|
| Edição no mesmo DWG (vértice, layer) | Mesmo handle, então o mesmo UUID. Geometria e `properties` são substituídas por inteiro; `created_at` é reiniciado. Mudar a layer não muda o UUID: a identidade é `sourceType`+`sourceId`. |
| Entidade apagada no DWG | Some na reimportação, sem marca nem histórico. |
| Entidade recriada (copiar/colar, `WBLOCK`, explodir/refazer) | Novo handle, novo UUID (hipótese sobre o comportamento do AutoCAD, confiança média). |
| DWG copiado ("Salvar como") | Mesmos handles. Com o mesmo `project.externalId`, **sobrescreve** a rede; com outro, cria outro projeto. |
| Reexportação pelo outro caminho (ponte × GeoAutoEngine) | Projeto e rede duplicados (GM-05). |
| Mudança do `network.externalId` | Rede nova; a anterior fica órfã (GM-06). |

O handle também é usado pelo GeoAutoEngine (`CadSectionAssociation`, segundo o Chat 00). Isso torna o handle o único elo possível hoje, válido só dentro da linhagem do mesmo DWG.

### GM-D — Pontos de extensão existentes (só descrição)
Elementos que já existem sem violar a DEC-008 (sem tipos Autodesk) nem a DEC-021 (estrutura estrita, extensível só em `properties`):
- `properties` (JSONB, até 128 campos e 64 KiB) aceita chaves novas sem mudar a versão do contrato. É sobrescrito a cada snapshot, então dado de outra origem gravado ali seria apagado pela próxima importação CAD.
- `sourceType` e `networks.type` são strings neutras.
- Uma rede pode conter pontos (0.3).
- O detalhe da feição devolve `properties` inteiro (`docs/API.md#L106-L134`).
- O versionamento por `contractVersion`, com validador por versão, já existe.

Não há:
- endpoint de escrita: a API só tem `GET` (`Program.cs#L38-L49`);
- tabela de dados não geométricos;
- vínculo entre feições além da geometria.

### GM-E — Operação
- **Autenticação:** nenhuma (GM-16).
- **Hospedagem:** PEND-007 aberta (`docs/ARCHITECTURE.md#L165`).
- **Configuração por ambiente:**
  - `Database__ConnectionString` obrigatória, validada no início (`Program.cs#L12-L18`);
  - variáveis `VITE_*` para o mapa base e o projeto inicial (`.env.example#L8-L20`);
  - o mapa base OSM público é permitido só para uso interno de baixo tráfego (DEC-016).

**Desempenho registrado** (`docs/PERFORMANCE.md`, `docs/benchmarks/*`):

| Medição | Resultado |
|---|---|
| MVT LARGE, zoom 15 | 171.200 B, p95 de 32,4 ms; guarda de 512 KiB / 100 ms na CI |
| NDJSON LARGE (1 milhão de feições) | 50,26 s, cerca de 121 MiB de RSS |
| NDJSON MEDIUM | 7,35 s |
| `extent` LARGE | p95 de 284 ms; guarda de 2 s |

**Issues abertas:**
- **#44:** Playwright; sugestão não priorizada.
- **#48:** curvas na ponte; opções a decidir, contradiz a DEC-024.
- **#50:** Shapefile; ferramenta, interface e mapeamento a decidir.

## 5. Alterações feitas
Nenhuma no repositório: sem branch, commit, Issue, PR, comentário, label ou release. Só foram feitos clones rasos locais e consultas REST de leitura. Este relatório foi salvo apenas no Project do Claude.

## 6. Testes e validações
- **Executado:**
  - `python3 -m unittest discover -s tests/dxf-converter -p 'test_*.py'` → **13 testes OK** (Python 3.13.18, sessão Linux);
  - comparação JSON normalizada das fixtures → idênticas;
  - leitura dos check-runs da CI → 3/3 `success`.
- **Não executado:**
  - `dotnet build/test`: SDK ausente e instalação bloqueada;
  - `npm ci/test/build`: sem `node_modules` e sem rede para pacotes;
  - `docker compose`: não tentado, sem rede para baixar imagens.
- O resultado verde desses itens vem apenas da CI do GitHub.

## 7. Impacto em outros projetos e contratos
- **GeoAutoEngine (Chat 02):**
  - o exportador está atrás da ponte em 0.2/0.3 e em `LINE`;
  - `sourceSystem` diferente da ponte (GM-05);
  - a fixture não é verificada nos dois sentidos (GM-10);
  - qualquer mudança de `sourceSystem` muda os UUIDs (ruptura silenciosa, regra 8).
- **GeoAutoDocs (Chat 03):** nada no GeoMap consome ou produz dados de inspeção hoje (GM-20).
- **Contrato envolvido:** GeoMap Import Contract 0.1–0.3. Dono: GeoMap; produtores: ponte e GeoAutoEngine.

## 8. Riscos, dependências e decisões necessárias
- **Chat 00:**
  - destino da ponte DXF: revisar a DEC-024 ou dar ao GeoAutoEngine as capacidades 0.2/0.3;
  - política de `sourceSystem` e identidade entre produtores (GM-05);
  - verificação cruzada da fixture (GM-10);
  - estratégia de identificador comum, que hoje é o handle CAD por DWG.
- **Usuário:**
  - o GeoMap é usado fora de máquinas de desenvolvimento (GM-02)?
  - o script privado da 3ª rede deve virar código versionado ou ser descartado (GM-09)?
  - prioridade entre #48, #49 (volume de pontos), #50 e #44.
- **Riscos:**
  - perda de rastreabilidade da origem (GM-04);
  - divergência entre bancos por migrations manuais (GM-03);
  - exposição da API sem autenticação se for hospedada (GM-16).

## 9. Próximo passo pequeno e verificável
Após o roadmap aprovado, abrir uma Issue no GeoMap para corrigir a documentação desatualizada do contrato (GM-12). É só documentação, sem mudança de contrato, e se verifica pelo diff.

## 10. Resumo para encaminhar ao Chat 00
```
[Chat 01 → Chat 00 | Etapa 1 — auditoria GeoMap | 10/11/2026 | SOMENTE LEITURA]
Repo JonhyGonc/Geomap @ main b9be25c (reconfirmado). Nenhuma alteração feita.
Acesso: GeoMap leitura+escrita+REST; GAE/GAD só leitura git; GeoEcosystem inexistente.
Testes: CI de b9be25c 3/3 success; local só ponte DXF 13/13 OK; dotnet/npm/compose não executados (sem SDK/rede).
Q1 .NET 10 (8 csproj), Npgsql 8.0.9, PostGIS 16-3.5, React 19.2.8, MapLibre 6.8.0, Vite 8.2.2; ponte em Python stdlib.
Q2 29 branches todas mescladas (ahead 0); sem release/tag; sem build distribuída; compose em Development.
Q3 3 tabelas; migrations 001–003 manuais, sem tabela de controle (GM-03); sem histórico (snapshot).
Q4 UUID v5: projeto=sourceSystem+externalId; rede=projeto+externalId; feição=rede+sourceType+handle.
Q6 .NET 45 Fact+16 Theory; web 41; Python 13; 34 scripts de integração. Sem teste de navegador real (#44).
Q7 nenhum CAD real versionado; agregados em REAL_DATA_VALIDATION.md; PR #54 cita IDs internos de desenho.
ACHADOS-CHAVE
GM-05 Mesmo projeto por 2 caminhos = 2 projetos: ponte "Civil 3D DXF" × GAE "Civil 3D"
  (Geomap@b9be25c:tools/dxf-converter/geomap_dxf.py#L711; DeterministicGeoMapIds.cs#L13-L14).
GM-07 TODOS os dados reais (#17, #46, água, #52, #49) entraram pela ponte DXF; GEOAUTO3GEOMAPEXPORT
  só foi usado com fixture sintética (docs/REAL_DATA_VALIDATION.md#L17-L52,#L183-L187; PR #36/#46).
GM-08 DEC-024 "descartável" (docs/DECISIONS.md#L183-L200) não revista; ponte é o único produtor
  de 0.2/0.3 (DEC-028/029) e a #48 a amplia → tensão com a regra 13.
GM-14 Só a ponte: LINE, cor de layer, INSERT→Point, ATTRIB, posição pelo símbolo (50×),
  filtros _TG*/SIMB_PONTO. Ambos: sem curvas, ≤10.000 feições, sem NDJSON.
GM-09 3ª rede passou por script privado (uniu 17 layers + diâmetro/material) → não reproduzível.
GM-10 Fixture GAE = cópia manual (commit único 13735b8); idêntica hoje ao sample GAE@3acab63;
  nenhuma CI regenera/compara; IMPORT_CONTRACT.md#L87 superestima a garantia.
GM-04 Origem não persiste (sourceSystem, versão, exportedAt, CRS, units, hash);
  created_at é reiniciado a cada importação, mas a API o expõe como createdAt.
GM-06 Reimportação apaga feições sem histórico; projeto/rede nunca são removidos (órfãos).
GM-11/12 Limites doc=validador, mas sem teste unitário (exceto 128 props); docs desatualizadas
  (IMPORT_CONTRACT.md#L15,#L91,#L93,#L142; BACKLOG só #1–#18; PERFORMANCE em .NET 8).
GM-13 `units` validado mas ignorado e não comparado com o EPSG.
GM-15 Blocos dinâmicos *U… não filtrados pela ponte (geomap_dxf.py#L660-L669).
GM-16 API sem auth/CORS, AllowedHosts * (apps/api/GeoMap.Api/Program.cs#L10-L51); PEND-007 aberta.
GM-17 Desempenho só de linhas (MVT LARGE z15 171 KB/p95 32 ms; NDJSON 1M 50 s/121 MiB); pontos sem medição.
GM-D Extensão hoje: só properties JSONB (sobrescrito a cada snapshot), tipos neutros, versão de contrato;
  API só GET; nenhum modelo de inspeção/anomalia/histórico (MVP.md#L41-L51).
Issues abertas: #44 Playwright (sugestão), #48 curvas (contradiz DEC-024), #50 Shapefile (tecnologia nova).
DECISÕES PEDIDAS
Chat 00: destino da ponte × GAE (0.2/0.3); política de sourceSystem/identidade; verificação cruzada da fixture.
Usuário: GeoMap usado fora do dev? script privado da 3ª rede vira código? prioridade #48/#49/#50/#44.
Próximo passo (pós-roadmap): Issue de documentação do contrato (GM-12), sem mudança de contrato.
Relatório completo: Project GeoEcosystem → governanca/ETAPA_1_RELATORIO_CHAT_01_GEOMAP.md
```
