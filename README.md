# GeoEcosystem

Repositório central de documentação e coordenação do ecossistema de engenharia e geoprocessamento:

| Aplicação | Pasta | Papel | Repositório de origem (arquivado após o M2) |
|---|---|---|---|
| GeoMap | [`apps/geomap/`](apps/geomap/) | Visualização e gestão de redes geográficas (PostGIS, API, MVT, MapLibre) | `JonhyGonc/Geomap` |
| GeoAutoEngine | [`apps/geoautoengine/`](apps/geoautoengine/) | Plugin AutoCAD/Civil 3D: automação de desenho, estaqueamento, vídeo-inspeção no DWG, exportação GeoMap | `JonhyGonc/GeoAutoEngine` |
| GeoAutoDocs | [`apps/geoautodocs/`](apps/geoautodocs/) | Desktop WPF: vídeo-inspeções, anomalias e relatórios técnicos | `JonhyGonc/GeoAutoDocs` |

Desde o M2 ([DEC-ECO-0001](decisoes/DEC-ECO-0001-monorepo.md)) este é o **repositório único** do ecossistema: o código de cada aplicação fica em `apps/<aplicação>/` (com o histórico completo do repositório de origem) e a documentação **compartilhada** fica na raiz. A documentação específica de cada aplicação continua dentro da pasta dela. A CI de cada aplicação está em [`.github/workflows/`](.github/workflows/) e roda só quando a pasta dela muda.

## Mapa
| Pasta | Conteúdo |
|---|---|
| [`apps/`](apps/) | Código das três aplicações |
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
