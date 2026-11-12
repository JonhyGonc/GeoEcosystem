# Exportador de Polyline do Civil 3D

Status: exportador inicial implementado pela Issue #18.

## Componente

O exportador faz parte do [GeoAutoEngine](https://github.com/JonhyGonc/GeoAutoEngine), aplicativo C# já integrado ao AutoCAD/Civil 3D. A implementação entrou pela [PR #3 do GeoAutoEngine](https://github.com/JonhyGonc/GeoAutoEngine/pull/3). O GeoMap não carrega DLLs Autodesk: a integração ocorre exclusivamente pelo contrato JSON v0.1.

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
| Entidade | somente `LWPOLYLINE` |
| Segmentos | somente retos; bulges cancelam o arquivo |
| Aberta | `LineString` na ordem original |
| Fechada | repete a primeira posição ao final |
| Identidade | handle em `sourceId` e `cadHandle` |
| Layer | preservado em `sourceLayer` |
| Z | saída X/Y; perda declarada em `projectedTo2D` e elevação em `cadElevation` |
| CRS | obrigatório, no formato EPSG |
| Unidade | obrigatória e restrita ao contrato v0.1 |
| Limites | 10.000 feições; 100.000 posições por feição |

Ficam fora desta versão: `Polyline2d`, `Polyline3d`, Feature Line, Pipe Network, Pressure Network, arcos, sincronização bidirecional e edição no site.

## Validação cruzada

O arquivo `contracts/geomap/0.1/examples/valid-geoautoengine-polyline.geomap.json` é uma cópia sintética da saída de referência mantida no GeoAutoEngine. Os testes verificam:

- aceitação pelo validador estrito v0.1;
- leitura de projeto, rede, handle, layer e propriedades pelo importador;
- persistência e transformação `EPSG:31982` → `EPSG:4326` no PostGIS;
- listagem da rede pelo catálogo da API;
- consulta dos atributos da feição;
- geração de um MVT válido contendo a feição importada.

DWG, DXF, contratos reais e coordenadas privadas continuam fora do Git. A fixture versionada é sintética e não contém dados do desenho autorizado da Issue #17.
