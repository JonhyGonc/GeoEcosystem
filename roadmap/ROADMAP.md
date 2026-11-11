# Roadmap — proposta da Etapa 4

> **Estado: PROPOSTA, aguardando aprovação explícita do usuário** (regra 3). Nenhuma implementação começa antes dessa aprovação. Depois de aprovado, cada marco gera Issues nos repositórios e um arquivo em `marcos/` quando começar.

- **Autor:** Chat 00
- **Data:** 11/11/2026
- **Base:** [`auditorias/2026-11-etapa1/`](../auditorias/2026-11-etapa1/), [`status/`](../status/), [`contratos/REGISTRO.md`](../contratos/REGISTRO.md)
- **Commits de referência:** inalterados desde a auditoria (revalidados em 11/11/2026): `Geomap@b9be25c`, `GeoAutoEngine@3acab63`, `GeoAutoDocs@073191a`

## 1. Diagnóstico em uma página
| Situação | Evidência | Consequência |
|---|---|---|
| Ninguém sabe qual versão cada desenhista tem | Versões fixas (3.1.1 / 1.0.4), sem commit registrado; instalações em épocas diferentes | Impossível diagnosticar erro em campo (R-04) |
| A `main` do GAE e do GAD não é o que está instalado | Máquina do usuário: GAE `main` + `feature/atualizar-textos-anomalias`; GAD da `fix` (319 commits fora da `main`) | Trabalho real fora da linha oficial; branches em conflito |
| O Civil 3D provavelmente trava enquanto o usuário revisa correções | Espera síncrona de 5 min na `main` do GAE (GAE-05); a correção está numa branch não instalada | Risco operacional diário (R-03) |
| A comunicação GAE ↔ GAD tem duas variantes sob a mesma versão | Enum 1–5 × 1–7; sem negociação; payload de correção com semântica divergente | Ruptura silenciosa (R-03, R-09) |
| O GeoMap recebe dados CAD por uma ponte "descartável" | Todos os dados reais vieram da ponte DXF; o exportador do GAE nunca foi usado | Caminho oficial indefinido (R-05, R-07) |
| Nenhum teste atravessa projetos | Fixtures copiadas à mão; contratos duplicados | Qualquer mudança pode quebrar o vizinho sem aviso (R-08) |

## 2. Arquitetura atual (resumo)
Três aplicações independentes, com integração pontual e local:
- GAD ⇄ GAE por Named Pipe (GADX) e arquivo `.geoadocs.json`;
- DWG → GeoMap por arquivo `.geomap.json`, gerado pela ponte DXF (real) ou pelo GAE (só testado com dados fictícios).

Não há identificador comum além do handle CAD. Detalhes: [matriz de integração](../auditorias/2026-11-etapa1/ETAPA_2_CONSOLIDACAO.md#5-matriz-de-integração).

## 3. Princípios deste roadmap
1. **Primeiro estabilizar o que já é usado, depois integrar mais.** Os marcos M0–M2 não adicionam nenhuma integração nova.
2. **Passos pequenos e reversíveis**, cada um com verificação objetiva.
3. **Nenhum mecanismo de integração novo é escolhido sem alternativas** (regra 12): as decisões abaixo trazem opções e recomendação, e quem decide está indicado.
4. **Compatibilidade retroativa** em todo contrato (regra 8).
5. **O usuário é o testador no Civil 3D e quem instala na equipe:** nenhuma sessão de agente tem Windows, .NET ou Civil 3D.

## 4. Pré-requisitos de execução
| ID | Pré-requisito | Dono | Por quê |
|---|---|---|---|
| P-1 | Os Chats 02 e 03 conseguirem escrever nos seus repositórios | Usuário | A política das sessões negou acesso de escrita na Etapa 1. Funcionou no GeoEcosystem quando o usuário pediu explicitamente: "conecte o <repositório> com permissão de escrita para …". |
| P-2 | Roteiro de teste manual no Civil 3D para cada entrega do GAE/GAD | Chat 02/03 escrevem; usuário executa | A CI não testa a camada CAD nem a UI WPF |
| P-3 | Instalação na equipe só com autorização do usuário | Usuário | Regra 14: alterar a operação real exige autorização humana |

## 5. Marcos

### M0 — Rastreabilidade de builds (resolve R-04)
**Objetivo:** toda build instalada diz de qual commit veio.

| Passo | Projeto / dono | Entrega | Verificação |
|---|---|---|---|
| M0.1 | GAE / Chat 02 | Versão informativa = `3.1.x+<sha7>` gravada no build (`InformationalVersion`), mostrada no log de inicialização e num comando `GEOAUTO3VERSAO`; o gerador de bundle registra o commit | Teste unitário do formato; usuário roda o comando no Civil 3D |
| M0.2 | GAD / Chat 03 | Mesma versão informativa no título/"Sobre" e no campo `applicationVersion` do `Hello` (já existe no protocolo, sem mudar o contrato) | Teste unitário; usuário confere na tela |
| M0.3 | GAD / Chat 03 | CI permanente no GitHub (`windows-latest`, `dotnet build` + `dotnet test`); hoje não há nenhuma | CI verde no PR |
| M0.4 | Usuário | Preencher [`status/COMPATIBILIDADE.md`](../status/COMPATIBILIDADE.md) para cada desenhista, com o guia atual | Tabela completa |

**Critério de aceitação:** as builds novas de GAE e GAD exibem o SHA; a tabela de máquinas está completa.
**Risco:** baixo (aditivo; não muda contrato nem dados).

### M1 — Linha de base única (D-01; resolve parte de R-03)
**Objetivo:** a `main` de cada projeto passa a ser o que a equipe usa.

| Passo | Projeto / dono | Entrega | Verificação |
|---|---|---|---|
| M1.1 | GAD / Chat 03 | Mesclar `fix/section-edit-save-stability` na `main` (PR #1 do GAD), resolvendo o conflito em `MainWindow.xaml.cs` para manter a renumeração de `073191a` | CI (M0.3) verde; 240+ testes; roteiro manual executado pelo usuário |
| M1.2 | GAE / Chat 02 | Mesclar `sync-section-names` na `main`. Ela traz a correção do travamento de 5 min (`559d8b1`) e o `GEOAUTO3VISYNC`; adicionar o comando ao `PackageContents.xml` | CI verde; usuário confirma no Civil 3D que revisar correções não trava mais |
| M1.3 | GAE / Chat 02 | Mesclar `feature/atualizar-textos-anomalias` sobre a nova `main`, resolvendo os conflitos nos 5 arquivos | CI verde; 213+ testes; `GEOAUTO3VIUPDATETEXTS` e `GEOAUTO3VISYNC` funcionam juntos |
| M1.4 | Usuário | Instalar a nova `main` primeiro na própria máquina (homologação) e, aprovada, nos demais desenhistas | `COMPATIBILIDADE.md` mostra a mesma build (com SHA) em todos |
| M1.5 | GAD / Chat 03; GAE / Chat 02 | Arquivar `Testes-ChatGPT`, `ChatGPT` e `backup-main` como tags `arquivo/<nome>` e apagar as branches (autorizado em 11/11/2026) | Tags existem e apontam para os SHAs originais; branches removidas |

**Alternativas para o GAD (D-01):**
- (a) **mesclar a fix na `main`** — recomendado: a fix é o que está instalado, e o conflito é de 1 arquivo;
- (b) recriar a `main` a partir da fix e reaplicar `073191a` — reescreve histórico, então é destrutivo;
- (c) manter as duas — perpetua a divergência.

**Ordem no GAE:** `sync-section-names` primeiro, porque carrega a correção do travamento (o risco mais alto) e é a mais antiga. A alternativa inversa deixaria o travamento ativo por mais tempo.

**Branches residuais:** `Testes-ChatGPT` (GAD; 4 commits só de UI que não estão na fix), `ChatGPT` e `backup-main` (GAE; já contidas na `main`). **Autorizado pelo usuário em 11/11/2026 (Q-C):** depois do M1, arquivá-las como tag (`arquivo/<nome>`) e apagar as branches. Passo **M1.5** (Chats 02/03), executado só depois que o M1.4 for confirmado.

**Critério de aceitação:** `main` = build instalada em todos os desenhistas; nenhuma branch funcional fora da `main`.

### M2 — Contrato GADX estável (D-02, D-06; resolve R-03, R-08 parcial, R-09)
**Objetivo:** a comunicação GAE ↔ GAD tem uma especificação única, compatível com versões antigas e testada nos dois lados.

**Decisão D-02 — como versionar** (Chat 00 decide após aprovação; registrada como `DEC-ECO-0001`):

| Opção | Como funciona | Compatibilidade | Custo |
|---|---|---|---|
| **(a) Capacidades no `Hello`** — recomendada | O `Hello` ganha um campo opcional `capabilities` (ex.: `["corrections"]`). O GAE só envia 6/7 a quem declarou a capacidade. Clientes antigos, sem o campo, continuam funcionando sem correções. | Total, nos dois sentidos | Baixo: campo aditivo |
| (b) Protocolo v2 | 6/7 passam a existir só na v2; o servidor aceita v1 e v2 | Boa, mas exige manter dois caminhos no servidor | Médio |
| (c) Pacote compartilhado de contrato | Uma biblioteca única usada pelos dois repositórios | Elimina a duplicação, mas exige `netstandard2.0` para o Civil 3D 2024 e um processo de publicação | Alto |

**Semântica das correções (R-09):** registrar na especificação que o GAE envia o **estado completo** da ocorrência. Alternativas:
- (i) o GAD aplica como estado completo — recomendado: alinha com o produtor real;
- (ii) o GAE passa a omitir campos não alterados — exige mudar o produtor.

| Passo | Dono | Entrega | Verificação |
|---|---|---|---|
| M2.1 | Chat 00 | Especificação **normativa** GADX v1.1 em `contratos/gadx-inspecao/SPEC.md` + `DEC-ECO-0001`, via Issue `mudanca-de-contrato` | PR aprovado pelo usuário |
| M2.2 | Chat 00 | Fixtures de referência em `contratos/gadx-inspecao/fixtures/`: frames binários e JSON de cada mensagem, inclusive `Hello` com e sem capacidades | Fixtures validadas contra o código atual dos dois lados |
| M2.3 | GAE / Chat 02 | Servidor aceita `Hello` com e sem `capabilities`; só envia 6/7 a quem declarou; testes leem as fixtures do GeoEcosystem (checkout na CI, repositório público) | CI verde com as fixtures |
| M2.4 | GAD / Chat 03 | Cliente declara `capabilities`; aplica as correções como estado completo; testes com as mesmas fixtures | CI verde; roteiro manual: correção de metragem e descrição chega ao GAD sem apagar campos |
| M2.5 | Usuário | Instalação coordenada (servidor primeiro: GAE novo funciona com GAD antigo) | Todas as combinações de `COMPATIBILIDADE.md` em ✅ |

**Critério de aceitação:** as duas CIs testam contra as mesmas fixtures; nenhuma combinação de builds instaladas produz falha silenciosa.

### M3 — Caminho oficial DWG → GeoMap (D-03, D-04; resolve R-05, R-07)
**Objetivo:** um único caminho reproduzível para levar a rede do DWG ao GeoMap.

**Decisão D-03.** O requisito de negócio foi respondido em 11/11/2026 (Q-A, seção 7): anomalias devem aparecer no mapa, na metragem do trecho. **Recomendação: opção (b)**, porque só o GAE conhece a associação trecho ↔ Polyline e a direção do trecho, necessárias para posicionar a metragem. A ponte DXF segue em uso até o exportador do GAE cobrir o que ela faz; depois é aposentada.

| Opção | Vantagens | Desvantagens | Quando faz sentido |
|---|---|---|---|
| (a) Ponte DXF vira oficial (revisar a DEC-024) | Já funciona com dados reais e já gera 0.2/0.3; custo mínimo | Python fora do CAD; não tem acesso aos vínculos de inspeção (XData/NOD); repete regras que o GAE já conhece | Se o GeoMap só precisa mostrar a rede (descartada pelo requisito) |
| (b) Exportador do GAE ganha 0.2, 0.3 e curvas; ponte aposentada | Alinhado à regra 13; tem acesso a `SectionId` ↔ handle, base para levar inspeções ao mapa | Maior esforço (cor, blocos, ATTRIB, tesselação, seleção por layer) | **Recomendada**: anomalias no mapa confirmadas |
| (c) Coexistência com `sourceSystem` e identidade unificados | Transição gradual | Duas implementações das mesmas regras | Como transição de (a) para (b) |

| Passo | Dono | Entrega | Verificação |
|---|---|---|---|
| M3.1 | Usuário + Chat 00 | Decisão D-03 e D-04 (`DEC-ECO-0002`) | Decisão registrada |
| M3.2 | Chat 01 | Política única de `sourceSystem` (GM-05). Como o GeoMap ainda não é usado (Q-B), não há migração de dados de produção: os bancos de desenvolvimento podem ser recriados | Teste: reimportar a mesma rede gera o mesmo UUID |
| M3.3 | Chat 02 | Exportador do GAE com 0.2 (cor), 0.3 (blocos/ATTRIB), LINE e curvas (tesselação), seleção por layer e atributos técnicos; em passos pequenos, um por capacidade | Para cada capacidade: fixture gerada pelo GAE validada pelo validador do GeoMap na CI; comparação com a saída da ponte DXF para o mesmo desenho |
| M3.4 | Chat 01 | Destino do script privado da 3ª rede (GM-09): versionar ou descartar | Dado reproduzível ou descarte registrado |

**Critério de aceitação:** reimportar a mesma rede pelo caminho oficial não cria projeto duplicado; o teste cruzado roda na CI.

### M4 — Anomalias no mapa do GeoMap (D-07) — **confirmado, último na fila**
**Requisito do usuário (11/11/2026):**
- cada anomalia aparece como uma marcação sobre a rede, na **metragem** registrada no GeoAutoDocs (ex.: "a 5 m, tubo deslocado" → marcação a 5 m do início do trecho);
- ao clicar na marcação, o GeoMap mostra a **imagem da anomalia** capturada no GeoAutoDocs;
- **sem prazo**: começa depois que M0–M3 estiverem prontos.

**O que já existe e o que falta** (da auditoria):
- O GAE sabe ligar trecho ↔ Polyline (`CadSectionAssociation`, com direção) e já posiciona metragens no desenho. É a base para calcular a coordenada da anomalia.
- O GAD tem a metragem, a descrição e as capturas PNG das ocorrências (evidência `isPrimary`). As imagens ficam em pastas locais do GAD e não trafegam hoje para lugar nenhum.
- O GeoMap só aceita geometria pelo contrato de importação e não tem armazenamento de imagens nem endpoint de escrita.

**Ainda não decidido** (regra 12; será proposto com alternativas quando o M4 começar):
- quem calcula a coordenada;
- como a anomalia e a imagem chegam ao GeoMap (contrato novo ou versão nova do contrato atual);
- onde as imagens ficam guardadas;
- o identificador que liga anomalia, trecho e feição (D-04).

## 6. Melhorias internas autorizadas com o roadmap (autonomia, regra 5)
Pequenas, seguras e restritas a cada projeto. Cada uma vira uma Issue no repositório do projeto. Não dependem dos marcos.

| Projeto | Itens |
|---|---|
| GeoMap (Chat 01) | Atualizar a documentação desatualizada do contrato (GM-12); tabela de controle de migrations aplicadas (GM-03); persistir a origem da importação (GM-04); filtrar blocos anônimos `*U` na ponte (GM-15) |
| GAE (Chat 02) | Corrigir o README sobre compatibilidade 2024–2026 (GAE-02); remover caminhos de usuário Windows de `docs/evidence` (R-01) |
| GAD (Chat 03) | Criar README e documentação mínima (GAD-19); incluir `Cache\` no backup (GAD-11); tabela de versão de esquema SQLite (GAD-07) |

**Não autorizados sem nova decisão:**
- hospedagem e autenticação do GeoMap (GM-16, PEND-007);
- Shapefile (#50);
- curvas na ponte (#48), que depende de D-03;
- correção da pasta compartilhada do GAD (R-02), só se ela for adotada.

## 7. Sequência e dependências
```
M0 (GAE ‖ GAD) ──► M1 (GAD ‖ GAE) ──► M2 (spec → GAE → GAD → instalação)
                                         │
M3.1 decisão (pode correr em paralelo) ──┴──► M3.2 (Chat 01, em paralelo) / M3.3 (Chat 02, após M2) / M3.4
                                                                        │
                                                                        └──► M4 (anomalias no mapa; último)
Melhorias internas: em paralelo, a qualquer momento
```
O GAE e o GAD avançam em paralelo dentro de M0 e M1. O M2 exige M1 concluído nos dois.

**Respostas do usuário (11/11/2026):**
- **Q-A:** sim, as anomalias devem aparecer no mapa (marcação na metragem; clique mostra a imagem do GeoAutoDocs). Sem prazo; fazer por último. → D-03 (b), M4 confirmado.
- **Q-B:** ninguém usa o GeoMap ainda; ele não está pronto. → M3 sem migração de dados de produção; R-07 cai para baixa.
- **Q-C:** pode arquivar as branches residuais. → passo M1.5.

## 8. Riscos do próprio roadmap
| Risco | Mitigação |
|---|---|
| Os agentes não conseguem escrever nos repositórios (P-1) | Autorização explícita do usuário em cada chat; plano B: o agente entrega patch e o usuário aplica |
| Teste manual no Civil 3D vira gargalo | Roteiros curtos (até 10 passos) por entrega; agrupar M1.2 + M1.3 numa única sessão de teste |
| Mesclar a fix do GAD (319 commits) introduz regressão | CI (M0.3) antes do merge; o usuário já usa essa build, então o risco real é o conflito de 1 arquivo |
| Instalação parcial na equipe recria combinações incompatíveis | M2 com capacidades torna todas as combinações seguras; M0 torna as combinações visíveis |

## 9. Como aprovar
Responda ao Chat 00 com uma das opções:
- **"Roadmap aprovado"** — aprova M0–M4 nesta ordem, as recomendações (D-01 opção a; D-02 opção a com semântica de estado completo; D-03 opção b) e as melhorias internas da seção 6;
- **"Aprovado com ajustes: …"**;
- **"Não aprovado: …"**.

Em seguida faça o merge deste PR. O merge sozinho não conta como aprovação: a regra 3 exige aprovação explícita.
