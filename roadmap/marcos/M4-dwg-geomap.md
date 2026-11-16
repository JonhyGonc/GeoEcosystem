# M4 — Caminho oficial DWG → GeoMap

- **Estado:** M4.1 ✅ (PR #19) e M4.2 ✅ (PR #21), mesclados em 16/11/2026. M4.3a ✅ (PR #22), M4.3b ✅ (PR #23), M4.3c ✅ (PR #24), M4.5 ✅ (PR #26), M4.3d ✅ (PR #25) e M4.3e ✅ (PR #27), mesclados em 16/11/2026. M4.4 ✅ (descarte) e ponte DXF aposentada neste PR: **M4 concluído** com o merge. Acompanhamento: Issue [#20](https://github.com/JonhyGonc/GeoEcosystem/issues/20).
- **Decisão:** [DEC-ECO-0003](../../decisoes/DEC-ECO-0003-caminho-dwg-geomap-e-identidade.md)
- **Dono:** agente único (Emenda 2); decisões de negócio, merge e testes no Civil 3D: usuário
- **Objetivo:** um único caminho reproduzível para levar a rede do DWG ao GeoMap, em que reimportar a mesma rede atualiza, sem duplicar.

## Passos
| Passo | Entrega | Verificação | Estado |
|---|---|---|---|
| M4.1 | DEC-ECO-0003: D-03 (b) e D-04 (código próprio por feição) | PR mesclado pelo usuário | ✅ PR #19 |
| M4.2 | GeoMap: identidade do projeto independente do `sourceSystem` (DEC-032, GM-05); documentação | Teste: a mesma obra por produtores diferentes gera o mesmo UUID de projeto e de rede | ✅ PR #21 |
| M4.3a | GAE: `FEATURE_ID` em XData (`GEOAUTOENGINE_FEATURE`), regra de duplicatas, `sourceId` = GUID, `sourceSystem` = `GeoAutoEngine` | CI (regras na camada Application); roteiro no Civil 3D: exportar, reexportar, mover, copiar e BREAK | ✅ PR #22 |
| M4.3b | GAE: cor da layer (contrato 0.2) | Fixture gerada pelo GAE validada pelo validador do GeoMap na CI; mesma regra de cor da ponte DXF (ACI 7 → preto); roteiro no Civil 3D | ✅ PR #23 |
| M4.3c | GAE: blocos como pontos (contrato 0.3), numa rede e num arquivo separados (DEC-029 do GeoMap) | Amostra 0.3 gerada pelo GAE validada pelo validador do GeoMap na CI; mesmas regras da ponte (posição pelo símbolo, `_TG*`, `SIMB_PONTO`); roteiro no Civil 3D | ✅ PR #24 |
| M4.3d | GAE: LINE, ARC e arcos de Polyline (tesselação, máximo 5 mm) | Amostra gerada pelo GAE validada pelo validador do GeoMap na CI; teste de flecha ≤ 5 mm; roteiro no Civil 3D e conferência no mapa pelo botão Importar | ✅ PR #25 |
| M4.3e | GAE: seleção por layer (curinga), uma rede por layer; `diametroMm`/`material` do nome `RD_<diâmetro>[_<material>]`; atributos dos blocos | Amostra 0.3 com atributos gerada pelo GAE validada pelo validador do GeoMap na CI; testes da leitura do nome e da divisão por layer; roteiro no Civil 3D e conferência no mapa | ✅ PR #27 |
| M4.5 | GeoMap: importar `.geomap.json` pelo site (botão **Importar** + `POST /api/imports`, mesmo código do importador do terminal); sempre ligado, sem login, só uso local ([DEC-033 do GeoMap](../../apps/geomap/docs/DECISIONS.md)) | Testes da rota na API (válido, inválido, vazio, > 50 MiB, falha no banco); CI importa pelo HTTP o exemplo de blocos do GAE duas vezes e confere o histórico no banco; testes do botão; roteiro no navegador | ✅ PR #26 |
| M4.4 | Destino do script privado da 3ª rede (GM-09): versionar ou descartar | Decisão registrada | ✅ descartado (DEC-034 do GeoMap) |
| — | Aposentar a ponte DXF, depois do M4.3e | Comparação final dispensada pelo usuário; código, testes, DXF sintético e passos da CI removidos | ✅ DEC-034 do GeoMap |

**Critério de aceitação (roadmap):** reimportar a mesma rede pelo caminho oficial não cria projeto duplicado; o teste cruzado roda na CI.

## Decisões tomadas neste marco
| Data | Decisão | Quem |
|---|---|---|
| 11/11/2026 | D-03 (b): exportador do GAE oficial; ponte DXF aposentada depois | Usuário (aprovação do roadmap) |
| 16/11/2026 | D-04: código próprio (GUID em XData) gravado pelo GAE em cada feição | Usuário |
| 16/11/2026 | `sourceSystem` único `GeoAutoEngine`; cópia/BREAK: o handle mais antigo fica com o código | Agente (DEC-ECO-0003) |
| 16/11/2026 | M4.4: descartar o script privado (GM-09); aposentar a ponte DXF sem a comparação final | Usuário |
| 16/11/2026 | M4.3e: uma rede por layer, como a ponte DXF; diâmetro e material lidos do nome `RD_<diâmetro>[_<material>]` | Usuário |
| 16/11/2026 | M4.5 (pedido novo, fora do roadmap original): botão Importar no site para testar visualmente; sempre ligado, risco de não ter login aceito | Usuário |
| 16/11/2026 | M4.3d: só LINE, ARC e arcos de Polyline (sem SPLINE, CIRCLE, Polyline 3D/2D antiga); tesselação com no máximo 5 mm de diferença | Usuário |
| 16/11/2026 | M4.3c: uma seleção com linhas e blocos gera dois arquivos, um por rede (blocos com `:blocos` e `-blocos`) | Usuário |
| 16/11/2026 | M4.3b: uma cor por rede, a da layer com mais Polylines (empate: ordem alfabética); só a cor da layer conta; sem cor, o arquivo fica na 0.1 | Agente (dentro do contrato 0.2, que já tem uma cor por rede) |

## Pontos de atenção
- A CI não compila o plugin. Cada passo do M4.3 que mexe na leitura do DWG precisa de roteiro no Civil 3D 2026.
- A partir do M4.3a, o exportador passa a **gravar** no desenho (uma XData por entidade exportada). O roteiro deixa isso explícito.
