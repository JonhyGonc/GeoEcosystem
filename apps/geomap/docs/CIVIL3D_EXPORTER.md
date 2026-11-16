# Exportador de Polyline do Civil 3D

Status: exportador inicial implementado pela Issue #18.

## Componente

O exportador faz parte do GeoAutoEngine, aplicativo C# já integrado ao AutoCAD/Civil 3D, que fica em `apps/geoautoengine` neste mesmo monorepo. A implementação entrou pela [PR #3 do repositório antigo do GeoAutoEngine](https://github.com/JonhyGonc/GeoAutoEngine/pull/3), hoje arquivado. O GeoMap não carrega DLLs Autodesk: a integração ocorre exclusivamente pelo contrato JSON: v0.3 para os blocos (pontos, numa rede separada, M4.3c), v0.2 para linhas com cor de layer (`network.color`, M4.3b) e v0.1 para linhas sem cor.

## Instalação

Compile ou obtenha o bundle do GeoAutoEngine para a versão instalada do Civil 3D. Instale a pasta `GeoAutoEngine.bundle` em:

~~~text
%AppData%\Autodesk\ApplicationPlugins\GeoAutoEngine.bundle
~~~

Reinicie o Civil 3D após atualizar o bundle. O manifesto registra o comando para os hosts 2024, 2025 e 2026.

## Uso

1. Abra o DWG e confirme o sistema de coordenadas e a unidade do desenho.
2. Execute `GEOAUTO3GEOMAPEXPORT`.
3. Selecione as `Polyline` 2D da mesma rede e pressione Enter.
4. Confirme o identificador e o nome do projeto.
5. Confirme o identificador, nome e tipo da rede. Layers do catálogo GeoAutoEngine recebem uma sugestão automática.
6. Informe o EPSG de origem, como `EPSG:31982`; também é aceito informar somente `31982`.
7. Confirme `metre`, `foot`, `us-survey-foot` ou `degree`.
8. Salve o arquivo `*.geomap.json`.
9. Importe o arquivo com `GeoMap.Importer`.

Exemplo no PowerShell, com o banco local iniciado:

~~~powershell
$env:GEOMAP_DB_CONNECTION_STRING = "Host=localhost;Port=5432;Database=geomap;Username=geomap;Password=change-me-local-only"

dotnet run --project tools/importer/GeoMap.Importer -- `
  --file "C:\caminho\rede.geomap.json"
~~~

## Política da versão 0.1

| Item | Comportamento |
|---|---|
| Entidade | `LWPOLYLINE`, `LINE` e `ARC` (linhas) e `INSERT` (blocos como pontos, numa segunda rede e num segundo arquivo `-blocos`) |
| Segmentos | retos; arcos (bulge e `ARC`) viram trechos retos com no máximo 5 mm de diferença (M4.3d) |
| Aberta | `LineString` na ordem original |
| Fechada | repete a primeira posição ao final |
| Identidade | código próprio da Polyline (GUID em XData, gravado pelo GeoAutoEngine) em `sourceId`; handle em `cadHandle` ([DEC-ECO-0003](https://github.com/JonhyGonc/GeoEcosystem/blob/main/decisoes/DEC-ECO-0003-caminho-dwg-geomap-e-identidade.md)) |
| Layer | preservado em `sourceLayer` |
| Cor | cor da layer com mais Polylines em `network.color` (versão 0.2); sem cor, o arquivo fica na 0.1 |
| Z | saída X/Y; perda declarada em `projectedTo2D` e elevação em `cadElevation` |
| CRS | obrigatório, no formato EPSG |
| Unidade | obrigatória e restrita ao contrato v0.1 |
| Limites | 10.000 feições; 100.000 posições por feição |

Ficam fora desta versão: `Polyline2d`, `Polyline3d`, `SPLINE`, `CIRCLE`, Feature Line, Pipe Network, Pressure Network, sincronização bidirecional e edição no site.

## Validação cruzada

Os arquivos `contracts/geomap/0.1/examples/valid-geoautoengine-polyline.geomap.json`, `contracts/geomap/0.2/examples/valid-geoautoengine-color.geomap.json` e `contracts/geomap/0.3/examples/valid-geoautoengine-blocks.geomap.json` e `contracts/geomap/0.1/examples/valid-geoautoengine-curves.geomap.json` são cópias sintéticas das saídas de referência mantidas no GeoAutoEngine (`apps/geoautoengine/samples/geomap/`). Um teste do GeoAutoEngine confere, na CI, que o serializador gera esses arquivos e que as cópias são idênticas byte a byte. Os testes do GeoMap verificam:

- aceitação pelo validador estrito v0.1;
- leitura de projeto, rede, handle, layer e propriedades pelo importador;
- persistência e transformação `EPSG:31982` → `EPSG:4326` no PostGIS;
- listagem da rede pelo catálogo da API;
- consulta dos atributos da feição;
- geração de um MVT válido contendo a feição importada.

DWG, DXF, contratos reais e coordenadas privadas continuam fora do Git. A fixture versionada é sintética e não contém dados do desenho autorizado da Issue #17.
