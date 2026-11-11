# Glossário

| Termo | Significado | Onde aparece |
|---|---|---|
| **Rede** | Conjunto de tubulações de um tipo (água, esgoto, pluvial) | GeoMap (`networks`), DWG (layers) |
| **Feição** | Elemento geográfico importado no GeoMap (linha ou ponto) | GeoMap (`network_features`) |
| **Trecho** | Segmento de tubulação entre dois pontos inspecionado num vídeo | GAD (`InspectionSection`), GAE (associação com Polyline) |
| **PV** | Poço de visita; extremidade típica de trecho | GAD (texto livre), DWG (bloco) |
| **Ocorrência / anomalia** | Defeito ou observação registrada num trecho, com metragem | GAD (`Occurrence`), GAE (anotações no DWG) |
| **Metragem** | Distância ao longo do trecho a partir do início do vídeo | GAD, GAE (correções) |
| **Estaqueamento** | Marcação de distâncias ao longo da rede no desenho | GAE |
| **Handle** | Identificador hexadecimal de uma entidade dentro de um DWG; estável só na linhagem do mesmo arquivo | GAE, GeoMap (`sourceId`) |
| **XData / NOD** | Dados estendidos de entidade e dicionário nomeado do DWG, onde o GAE grava vínculos | GAE |
| **GADX** | Protocolo binário do Named Pipe `GeoAutoEngine.Inspection` entre GAD e GAE | [`contratos/gadx-inspecao`](contratos/gadx-inspecao/SPEC.md) |
| **`.geoadocs.json`** | Arquivo de intercâmbio de inspeção (schema `GeoAutoDocs.Inspection`) | GAD → GAE |
| **`.geomap.json`** | Documento do GeoMap Import Contract | GAE / ponte DXF → GeoMap |
| **Ponte DXF** | Conversor Python `tools/dxf-converter` do GeoMap, de DXF para `.geomap.json` | GeoMap |
| **Build** | Versão compilada e instalada de uma aplicação numa máquina | [`status/COMPATIBILIDADE.md`](status/COMPATIBILIDADE.md) |

Siglas: **GM** = GeoMap, **GAE** = GeoAutoEngine, **GAD** = GeoAutoDocs.
