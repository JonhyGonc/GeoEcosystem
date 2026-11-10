# Ponte DXF provisória

Esta ferramenta converte uma única camada linear de um DXF ASCII para o contrato
GeoMap v0.1. Ela existe apenas para validar uma rede CAD real na Issue #17; o
exportador Civil 3D definitivo continua pertencendo à Issue #18.

## Uso

~~~bash
python3 tools/dxf-converter/geomap_dxf.py \
  --input private-data/rede.dxf \
  --output validation-output/rede.geomap.json \
  --layer "REDE-PLUVIAL" \
  --source-crs EPSG:31982 \
  --units metre \
  --project-external-id projeto-real \
  --project-name "Projeto real" \
  --network-external-id drenagem-real \
  --network-name "Drenagem real" \
  --network-type stormwater \
  --project-to-2d
~~~

O conversor aceita `LWPOLYLINE` e `LINE`, usa o handle CAD como `sourceId` e
preserva tipo, quantidade de vértices e elevação em `properties`. A opção
`--project-to-2d` é obrigatória quando existe Z ou elevação diferente de zero.
Segmentos com bulge, extrusão não padrão, `POLYLINE`, `ARC` e `SPLINE` são
rejeitados para impedir perda silenciosa de forma. Textos, blocos e outras
anotações da camada são apenas contabilizados como ignorados. Entidades dos
layouts de impressão são contabilizadas e descartadas; somente o Model Space é
convertido.

A cor da layer é lida da tabela LAYER do DXF: o true color (código 420) tem prioridade sobre o índice ACI (código 62), e o ACI 7 vira preto sobre o mapa claro. Com cor, a saída usa o contrato `0.2` com `network.color`; sem a tabela ou sem cor, continua no `0.1`. O resumo informa `networkColor`.

### Blocos (`--blocks`)

Com `--blocks`, a ponte converte as inserções de bloco (`INSERT`) da layer em pontos e gera o contrato `0.3`. Cada bloco vira uma feição `Point` no ponto de inserção, com `sourceType` `BlockReference` e as propriedades `bloco` (nome do bloco), `rotacao`, `escalaX`, `escalaY` e `cadElevation`. Os atributos do bloco (`ATTRIB`) entram como propriedades com o nome da tag; uma tag que coincida com uma propriedade gerada recebe o prefixo `atributo_`. Linhas e outras entidades da layer são apenas contabilizadas como ignoradas, então a mesma layer pode ser convertida duas vezes: uma para as linhas e outra, com `--blocks`, para os blocos, em redes separadas (por exemplo, "RD_50_PVC" e "RD_50_PVC (blocos)"). A cor da layer é preservada do mesmo jeito.

Alguns blocos são criados com o desenho já nas coordenadas reais e o ponto base em 0,0. O AutoCAD mostra o símbolo no lugar certo, mas o ponto de inserção fica a centenas de quilômetros da obra. A ponte lê a seção BLOCKS e, quando o desenho do bloco está a mais de 50 vezes o próprio tamanho do ponto base, usa a posição em que o símbolo aparece: o centro do desenho, com a escala e a rotação da inserção aplicadas. Textos não entram nesse cálculo. Essas feições recebem `posicaoPeloSimbolo: true`, e o resumo informa `symbolPositionedBlocks`. Blocos desenhados em volta do ponto base continuam no ponto de inserção.

Pontos de levantamento topográfico ficam sempre fora do GeoMap (DEC-029). A ponte recusa as layers `_TG*` em qualquer modo e, com `--blocks`, descarta os blocos `SIMB_PONTO`, contabilizados no resumo como `INSERT survey point`.

Blocos dinâmicos aparecem com o nome anônimo que o DXF grava (`*U…`), e não com o nome do bloco original; essa limitação está registrada na DEC-029.

O limite provisório é 512 MiB por DXF e 10.000 feições por documento JSON. Dados
reais devem permanecer em `private-data/` e resultados em `validation-output/`;
DWG e DXF também são ignorados pelo Git. A ferramenta não envia arquivos pela
rede.
