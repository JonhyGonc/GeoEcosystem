# M5 — Anomalias da inspeção no mapa do GeoMap

- **Estado:** M5.1 (decisões) em revisão. Acompanhamento: Issue [#30](https://github.com/JonhyGonc/GeoEcosystem/issues/30).
- **Decisão:** [DEC-ECO-0004](../../decisoes/DEC-ECO-0004-anomalias-no-mapa.md)
- **Dono:** agente único (Emenda 2); decisões, merge e testes no Civil 3D, no GAD e no mapa: usuário
- **Objetivo:** cada anomalia aparece sobre o tubo, na metragem do GeoAutoDocs; ao clicar, o mapa mostra a foto.

## Passos
| Passo | Entrega | Verificação | Estado |
|---|---|---|---|
| M5.1 | DEC-ECO-0004: posição pelo GAE, pacote `.zip` com fotos, intervalo como trecho destacado, vínculo por códigos próprios | Decisão registrada (usuário escolheu as quatro recomendações) | 🔄 |
| M5.2 | GADX 1.2: `captureFile` opcional em `evidence`; GAD preenche; biblioteca, especificação e fixtures | CI da biblioteca, do GAD e do GAE; fixture antiga (sem o campo) continua válida | ⏳ |
| M5.3 | GAE: posição da ocorrência sobre a Polyline (ponto, intervalo, vários pontos; sentido da filmagem; metragem além do tubo) | Testes na camada Application | ⏳ |
| M5.4 | GAE: comando que gera o pacote `.zip` (rede de anomalias 0.3 + `imagens/`) | Amostra de pacote gerada pelo GAE; roteiro no Civil 3D com uma inspeção real | ⏳ |
| M5.5 | GeoMap: importar o `.zip` (site e terminal), guardar as imagens, rota da imagem, foto no painel, destaque das graves | Testes da API e do site; a CI importa a amostra do GAE e lê uma imagem pela API; roteiro no navegador | ⏳ |

**Critério de aceitação:** numa inspeção real, cada anomalia cai sobre o tubo certo, na metragem do GAD (conferida no CAD); ao clicar, aparece a foto principal; reimportar o pacote não duplica.

## Decisões tomadas neste marco
| Data | Decisão | Quem |
|---|---|---|
| 16/11/2026 | As quatro decisões da DEC-ECO-0004 (opções recomendadas) | Usuário |

## Pontos de atenção
- A CI não compila o plugin. O cálculo da posição fica na camada Application, que a CI testa; a leitura do DWG precisa de roteiro no Civil 3D.
- As fotos são dados do cliente: ficam fora do Git, em pasta própria do GeoMap, como as ortofotos.
- Mostrar ao cliente depende de hospedagem e login, pendentes (PEND-007, DEC-033 do GeoMap).
