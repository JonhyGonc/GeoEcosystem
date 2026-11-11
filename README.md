# GeoEcosystem

Repositório central de documentação e coordenação do ecossistema de engenharia e geoprocessamento:

| Aplicação | Repositório | Papel |
|---|---|---|
| GeoMap | [`JonhyGonc/Geomap`](https://github.com/JonhyGonc/Geomap) | Visualização e gestão de redes geográficas (PostGIS, API, MVT, MapLibre) |
| GeoAutoEngine | [`JonhyGonc/GeoAutoEngine`](https://github.com/JonhyGonc/GeoAutoEngine) | Plugin AutoCAD/Civil 3D: automação de desenho, estaqueamento, vídeo-inspeção no DWG, exportação GeoMap |
| GeoAutoDocs | [`JonhyGonc/GeoAutoDocs`](https://github.com/JonhyGonc/GeoAutoDocs) | Desktop WPF: vídeo-inspeções, anomalias e relatórios técnicos |

Este repositório **não contém código das aplicações**. Ele é a única fonte da documentação **compartilhada** entre os três projetos: decisões entre projetos, registro de contratos, roadmap, status e auditorias. A documentação específica de cada aplicação continua no repositório dela.

## Mapa
| Pasta | Conteúdo |
|---|---|
| [`governanca/`](governanca/) | Protocolo dos agentes, papéis e processo de PR |
| [`decisoes/`](decisoes/) | Decisões entre projetos (`DEC-ECO-NNNN`) |
| [`contratos/`](contratos/) | Registro de todos os contratos entre projetos e specs que não têm outro dono |
| [`roadmap/`](roadmap/) | Marcos, dependências e critérios de aceitação |
| [`status/`](status/) | Estado de cada projeto, builds instaladas e riscos |
| [`auditorias/`](auditorias/) | Relatórios de auditoria (somente leitura, datados) |
| [`glossario.md`](glossario.md) | Termos do domínio |

## Como os agentes usam este repositório
- **Chat 00 (coordenador):** administra este repositório; revisa e registra a aprovação dos PRs daqui.
- **Chats 01–03 (especialistas):** consultam decisões, contratos e roadmap; propõem mudanças por PR ou Issue. Não alteram documentos de outros projetos.
- **Usuário:** é o único que faz merge (ver [`governanca/PROCESSO_PR.md`](governanca/PROCESSO_PR.md)).

Ao retomar qualquer trabalho, a regra 18 do protocolo vale: revalidar o estado no GitHub e não confiar só na memória do chat.
