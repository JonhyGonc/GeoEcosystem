# Backlog e roadmap — Protótipo 0.1

## Milestone proposto

**Protótipo 0.1**

Objetivo: uma rede originalmente proveniente do Civil 3D pode ser armazenada no GeoMap e visualizada corretamente pelo navegador sobre um mapa de fundo.

## Ordem recomendada

| # | Issue | Dependências | Resultado verificável |
|---:|---|---|---|
| 1 | Fundação documental e governança | nenhuma | documentação e regras aprovadas |
| 2 | Ambiente local PostgreSQL + PostGIS | #1 | banco espacial inicia via Docker Compose |
| 3 | Modelo espacial inicial | #2 | migrations de Project, Network e NetworkFeature |
| 4 | Gerador de dataset espacial | #3 | SMALL e MEDIUM reproduzíveis |
| 5 | Dataset LARGE | #4 | cerca de 1 milhão de feições carregáveis |
| 6 | Consultas espaciais por região | #3–#5 | consulta usa envelope e índice |
| 7 | Endpoint de Vector Tiles | #6 | endpoint z/x/y retorna MVT |
| 8 | Fundação do frontend MapLibre | #1 | mapa web abre com navegação |
| 9 | Fonte de mapa base configurável | #8 | provedor pode ser trocado por configuração |
| 10 | Visualizar redes do PostGIS | #7–#9 | rede aparece via MVT |
| 11 | Sistema de camadas | #10 | redes podem ser ligadas e desligadas |
| 12 | Selecionar elemento da rede | #10 | clique identifica feição |
| 13 | Mostrar atributos | #12 | painel consulta atributos básicos |
| 14 | Especificar contrato de importação | #3 | contrato v0.1 validável |
| 15 | Importador inicial | #14 | dataset pequeno é importado |
| 16 | Importação em lotes/streaming | #5, #15 | volume alto sem carga integral em memória |
| 17 | Teste com rede real | #10, #16 | posicionamento real validado |
| 18 | Exportador Civil 3D — Polyline | #14, #17 | Polyline gera contrato GeoMap |

## Fases

### Fase A — Fundação e banco

Issues #1 a #3. Entrega: regras do projeto, ambiente local e modelo espacial.

### Fase B — Escala geográfica

Issues #4 a #7. Entrega: datasets grandes, consultas espaciais e MVT medidos.

### Fase C — Visualização

Issues #8 a #13. Entrega: mapa configurável, redes, camadas, seleção e atributos.

### Fase D — Entrada de dados

Issues #14 a #16. Entrega: contrato e importação controlada.

### Fase E — Prova com CAD

Issues #17 e #18. Entrega: rede real validada e exportação inicial de Polyline.

## Regras de execução

- Uma Issue principal por branch e Pull Request.
- A main deve permanecer funcional.
- Toda Issue usa as seções: objetivo, contexto, escopo, fora do escopo, dependências, critérios de aceitação e testes esperados.
- Não avançar ao exportador Civil 3D antes de validar o fluxo web com o dataset LARGE.
- Mudanças arquiteturais exigem atualização de docs/DECISIONS.md.
- Melhorias fora do escopo viram futuras Issues.

## Critério de encerramento do milestone

O milestone termina quando:

- o fluxo PostGIS → API → MVT → MapLibre funciona;
- o dataset LARGE foi usado na validação aplicável;
- uma rede real está corretamente posicionada;
- camadas, seleção e atributos básicos funcionam;
- a importação grande não exige carregar tudo em memória;
- o exportador inicial de Polyline gera dados aceitos pelo GeoMap;
- build, testes e documentação estão atualizados.
