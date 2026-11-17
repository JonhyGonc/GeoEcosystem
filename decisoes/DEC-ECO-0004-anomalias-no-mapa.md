# DEC-ECO-0004 — Anomalias da inspeção no mapa do GeoMap (D-07, M5)

- **Estado:** aprovada pelo usuário em 16/11/2026, escolhendo a opção recomendada nas quatro perguntas. Passa a valer com o merge do PR que a introduz.
- **Origem:** requisito do usuário de 11/11/2026 (roadmap, M5); D-07 da auditoria; [DEC-ECO-0003](DEC-ECO-0003-caminho-dwg-geomap-e-identidade.md), item 6.
- **Contratos:** CT-01 (GeoMap Import), GADX (inspeção GAD ↔ GAE)

## Contexto
- **Requisito:** cada anomalia aparece sobre o tubo, na metragem registrada no GeoAutoDocs (GAD). Ao clicar, o mapa mostra a foto da captura.
- **GAD:** tem a metragem, a descrição, as anomalias e as evidências (capturas PNG, `isPrimary`). As fotos ficam em pastas locais do GAD e hoje não saem dele.
- **GAE:**
  - recebe a inspeção pelo GADX;
  - liga trecho ↔ Polyline com o sentido da filmagem (`CadSectionAssociation`, no NOD do DWG, pelo handle);
  - já posiciona metragens no desenho;
  - desde o M4, cada Polyline exportada tem um código próprio (`FEATURE_ID`).
- **GeoMap:**
  - recebe redes de linhas e pontos (contrato 0.3) pelo terminal e pelo botão **Importar** do site;
  - mostra as propriedades da feição clicada;
  - não guarda imagens.

## Alternativas e escolha
| Pergunta | Opções | Escolha do usuário |
|---|---|---|
| Quem calcula a posição | **GAE**, no desenho, com o sentido da filmagem; GeoMap, sobre a linha já reprojetada, sem o sentido e com medida diferente da do CAD | **GAE** |
| Como as fotos chegam | **Pacote `.zip` gerado pelo GAE**, com pontos e fotos, importado pelo botão; dois passos (pontos pelo GAE, pasta de fotos pelo GAD) | **Pacote do GAE** |
| Ocorrência em intervalo | **Trecho do tubo destacado**; ponto no início | **Trecho destacado** |
| Vínculo anomalia ↔ tubo | **Códigos próprios** (ocorrência do GAD + `FEATURE_ID` do tubo); handle do AutoCAD | **Códigos próprios** |

## Decisão
1. **Posição:** o GAE calcula a posição da ocorrência sobre a Polyline associada ao trecho, a partir do início da filmagem (respeitando `Direction`), com a mesma geometria que ele exporta para o GeoMap.
   - Metragem única (`Point`, `From`) → `Point`.
   - Intervalo (`Range`) → `LineString` com o pedaço do tubo entre as duas metragens.
   - Vários pontos (`MultiPoint`) → `MultiPoint`.
   - Metragem além do comprimento do tubo é ajustada para a ponta e marcada nas propriedades, sem cancelar.
2. **Entrega:** o GAE gera um **pacote `.zip`** por relatório:
   - um `.geomap.json` (contrato 0.3) com uma rede `<relatório> (anomalias)`, no mesmo projeto do mapa;
   - a pasta `imagens/` com as capturas, nomeadas pelo `captureId`.

   O GeoMap aceita o pacote no botão **Importar** (e no importador do terminal). Ele valida o JSON pelas regras de sempre e guarda as imagens numa pasta própria, fora do Git e do banco. O `.zip` é um transporte novo e opcional do CT-01: os `.geomap.json` e `.geomap.ndjson` não mudam.
3. **De onde vêm as fotos:** o GADX ganha, numa nova revisão da especificação (1.2), o campo **opcional** `captureFile` em `evidence`, com o caminho da imagem no PC.
   - O número de versão do GADX no fio continua `1`, e um GAD antigo, sem o campo, continua funcionando: o pacote sai sem a foto daquela evidência, com aviso.
   - Vale a regra de compatibilidade da DEC-ECO-0002.
4. **Identidade:**
   - o `sourceId` da anomalia é o `occurrenceId` do GAD, de modo que reimportar o pacote atualiza, sem duplicar;
   - a propriedade `featureId` guarda o `FEATURE_ID` da Polyline do tubo, lido na exportação pelo handle da associação. Ela é a ponte para o tubo no mapa, mesmo depois de MOVE, reexportação ou BREAK;
   - guardar o `FEATURE_ID` também na associação do NOD fica como melhoria, se o handle se mostrar instável no uso.
5. **No mapa:**
   - a anomalia é uma camada como as outras;
   - ao clicar, o painel mostra a foto principal (`isPrimary`), a descrição efetiva, a metragem, o trecho e as anomalias da ocorrência;
   - ocorrências graves (`isSevere`) ficam destacadas.

## Fora do escopo
- vídeo no mapa;
- edição pelo site;
- visualização pelo cliente, que depende de hospedagem e login (PEND-007, DEC-033 do GeoMap);
- sincronização automática: a atualização é por reexportação e reimportação do pacote.

## Impacto por projeto
- **GAD:** preenche `captureFile` nas evidências enviadas ao GAE.
- **Biblioteca do contrato:** campo opcional, fixtures e testes de compatibilidade.
- **GAE:** cálculo da posição (camada Application, testável na CI) e comando de exportação do pacote.
- **GeoMap:**
  - importação do `.zip`;
  - armazenamento e rota das imagens;
  - foto no painel da feição;
  - teste cruzado com uma amostra de pacote gerada pelo GAE.
