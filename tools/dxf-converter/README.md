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

O limite provisório é 512 MiB por DXF e 10.000 feições por documento JSON. Dados
reais devem permanecer em `private-data/` e resultados em `validation-output/`;
DWG e DXF também são ignorados pelo Git. A ferramenta não envia arquivos pela
rede.
