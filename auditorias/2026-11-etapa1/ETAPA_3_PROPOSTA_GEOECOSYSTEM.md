# Etapa 3 — Proposta do repositório central `GeoEcosystem` (Chat 00)

## 1. Identificação
- **Agente:** Chat 00
- **Data:** 10/11/2026
- **Estado:** **proposta.** Nada foi criado.
- **Base:**
  - achados de `governanca/ETAPA_2_CONSOLIDACAO.md`;
  - regras de "Organização documental" e "Gestão de tarefas" do protocolo.

## 2. Objetivo e critérios de aceitação
**Objetivo:** criar a fonte única da documentação compartilhada. Ela guarda:
- decisões entre projetos;
- registro de contratos;
- roadmap e status;
- relatórios de auditoria.

**Critérios:**
- (a) nada do que é específico de um projeto é duplicado aqui;
- (b) cada contrato entre projetos tem dono, produtor, consumidor, versão e local canônico registrados;
- (c) o estado de cada projeto (commit e build em uso) é rastreável;
- (d) mudanças entram só por PR com o relatório padrão.

## 3. Estrutura proposta
```
GeoEcosystem/
├── README.md                     # o que é, mapa do repositório, como cada agente usa
├── governanca/
│   ├── PROTOCOLO.md              # = 00_PROTOCOLO_COMPARTILHADO.md (versão oficial)
│   ├── AGENTES.md                # papéis Chat 00–03, escopos, prompts vigentes
│   └── PROCESSO_PR.md            # como propor, revisar e aprovar mudanças (ver seção 5)
├── decisoes/
│   ├── README.md                 # índice das decisões
│   └── DEC-ECO-0001-<tema>.md    # uma decisão por arquivo: contexto, alternativas, decisão, impacto
├── contratos/
│   ├── REGISTRO.md               # tabela: contrato, dono, produtor, consumidor, versão, local canônico, testes
│   ├── geomap-import/README.md   # ponteiro + regras de compatibilidade (a spec continua no GeoMap)
│   ├── gadx-inspecao/            # spec formal do pipe GADX + GeoAutoDocs.Inspection (hoje só existe em código)
│   └── dwg-xdata/README.md       # chaves XData/NOD que o GAE grava no DWG
├── roadmap/
│   ├── ROADMAP.md                # marcos, dependências, donos
│   └── marcos/M01-<nome>.md      # critérios de aceitação por marco
├── status/
│   ├── STATUS.md                 # por projeto: commit de referência, branches abertas, CI
│   ├── COMPATIBILIDADE.md        # matriz build GAE × build GAD × variante GADX × versão do contrato GM
│   └── RISCOS.md                 # R-01…R-08 com dono e estado
├── auditorias/
│   └── 2026-11-etapa1/           # relatórios dos Chats 00–03 + consolidação da Etapa 2
├── glossario.md                  # trecho, PV, ocorrência, handle, rede, feição…
└── .github/
    ├── PULL_REQUEST_TEMPLATE.md  # relatório padrão de 10 itens
    └── ISSUE_TEMPLATE/
        ├── decisao.md
        ├── mudanca-de-contrato.md  # produtor, consumidor, schema/versão, compatibilidade, migração, testes, rollback
        └── tarefa-interprojetos.md
```

**Labels propostas:**
- `projeto:geomap`, `projeto:geoautoengine`, `projeto:geoautodocs`;
- `tipo:decisao`, `tipo:contrato`, `tipo:risco`, `tipo:marco`;
- `prioridade:alta|media|baixa`.

**Fluxo de Issues:**
- O GeoEcosystem recebe só trabalho entre projetos e marcos.
- Cada tarefa de implementação vira uma Issue no repositório do projeto, com link para a Issue central.

## 4. Decisões de desenho (motivo, alternativas, impacto)

### E3-1 — Especificações de contrato: onde ficam
- **Proposta:**
  - o GeoMap Import Contract **continua** no GeoMap, e o GeoEcosystem só registra e aponta;
  - o GADX ganha sua **primeira** especificação formal no GeoEcosystem.
- **Motivo:**
  - a spec do GeoMap tem validador e CI que leem `contracts/geomap/*/examples` no próprio repositório, e movê-la quebraria a CI;
  - o GADX não tem documento canônico (só código duplicado e `docs/VIDEO_INSPECTION_CORRECTIONS.md` no GAE).
- **Alternativas:**
  - (a) mover tudo para o GeoEcosystem: viola "não reescrever sem necessidade" e quebra a CI;
  - (b) deixar o GADX no GAE: perpetua a assimetria e mantém o contrato sem dono neutro.
- **Impacto:** nenhum código muda nesta etapa. O dono do GADX (D-02) será decidido na Etapa 4.

### E3-2 — Aprovação de PR
- **Limitação:** todos os agentes agem pela **mesma conta GitHub** (`JonhyGonc`). Por isso o GitHub não distingue a "aprovação do Chat 00" do autor do PR, e um autor não pode aprovar o próprio PR.
- **Proposta:**
  - o agente abre o PR com o relatório padrão;
  - o Chat 00 registra a revisão num comentário com o rótulo `APROVADO-CHAT-00` ou `MUDANÇAS-CHAT-00`;
  - **o merge é feito por você.**
- **Alternativas:**
  - (a) contas separadas por agente: mais custo e gestão de credenciais;
  - (b) merge direto pelo Chat 00: sem controle humano.
- **Impacto:** a aprovação fica documentada no PR, e você continua sendo o único com poder de merge.

### E3-3 — Visibilidade
- **Proposta:** repositório **privado**.
- **Motivo:** vai conter a matriz de riscos, caminhos internos e detalhes de clientes e contratos, ainda que agregados. A proteção de branch não está disponível em repositório privado no plano gratuito (GM-01); a regra da E3-2 compensa isso por processo.
- **Alternativa:** público. Não há ganho para uso interno da Geofran.

### E3-4 — O que migra do Project do Claude
- **Migram:** o protocolo, os relatórios das Etapas 1 e 2 e esta proposta. Vão para `governanca/` e `auditorias/` no PR inicial.
- **Depois:** o Project do Claude passa a ter só os prompts dos agentes e cópias de leitura. O GitHub vira o estado oficial (regra 18).

### E3-5 — Status de builds
- **Proposta:** `status/COMPATIBILIDADE.md` registra, por máquina ou desenhista, a build instalada do GAE e do GAD.
- **Motivo:** o R-04 subiu para alta (builds instaladas em épocas diferentes, versões fixas, sem registro de commit).
- **Limite:** esta etapa só cria o lugar do registro. Identificar a build automaticamente, por exemplo com o commit na versão, é decisão de roadmap (D-05).

## 5. Conteúdo do PR inicial (somente documentação)
1. `README.md`, `glossario.md`.
2. `governanca/PROTOCOLO.md`, `AGENTES.md`, `PROCESSO_PR.md`.
3. `contratos/REGISTRO.md` preenchido com os 3 contratos atuais:
   - GeoMap Import 0.1–0.3;
   - GADX/Inspection v1 (2 variantes);
   - XData/NOD do GAE.
4. `contratos/gadx-inspecao/SPEC.md`: descrição do protocolo **como ele é hoje**, a partir de GAE-09, GAD-12b e GAD-14, marcada como "descritiva, não normativa" até a D-02.
5. `status/STATUS.md`, `COMPATIBILIDADE.md` (com a sua máquina já registrada) e `RISCOS.md`.
6. `auditorias/2026-11-etapa1/`: os 5 relatórios.
7. `decisoes/README.md` vazio; `roadmap/ROADMAP.md` como esqueleto, a ser preenchido na Etapa 4.
8. Templates de PR e Issues; labels criadas via API.

**Não entra:** código, mudanças nos três repositórios, decisões D-01…D-07.

## 6. O que é preciso para executar
1. **Sua autorização** para a estrutura (ou ajustes).
2. **Criação do repositório:**
   - você cria `JonhyGonc/GeoEcosystem` no GitHub: **privado**, **vazio** (sem README, sem .gitignore, sem licença);
   - esta sessão não tem ferramenta para criar repositórios.
3. **Acesso de escrita para o Chat 00:**
   - depois de criado, eu peço para conectar o repositório com escrita nesta sessão, para criar a branch e abrir o PR;
   - **atenção:** a política desta sessão já negou anexar com escrita os repositórios GeoAutoEngine e GeoAutoDocs;
   - se negar de novo, a alternativa é eu entregar os arquivos prontos (zip) para você fazer o upload, ou você me dizer explicitamente "conecte o GeoEcosystem com permissão de escrita para abrir o PR inicial", o que deixa a intenção inequívoca.

## 7. Riscos
- **Divergência Project × GitHub durante a transição:** mitigada tratando o GitHub como oficial a partir do merge do PR inicial.
- **Aprovação por convenção, não por proteção de branch:** depende de disciplina; mitigada pelo merge exclusivo do usuário.

## 8. Próximo passo
Você aprova (ou ajusta) a estrutura e cria o repositório vazio. Depois eu abro o PR #1 só de documentação e, em paralelo, preparo a Etapa 4 (roadmap).
