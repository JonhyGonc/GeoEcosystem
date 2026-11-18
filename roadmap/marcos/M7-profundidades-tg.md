# M7 — Profundidades das tubulações pelos pontos _TG (emergencial)

- **Estado:** M7.1 (entrega 1) em revisão no PR #34.
- **Origem:** pedido emergencial do usuário em 17/11/2026. Tem prioridade sobre o M5 e sobre melhorias não relacionadas. O M6 (Civil 3D 2027, PR #33) e o conversor da ortofoto (PR #32) já estão prontos e não são interrompidos.
- **Decisão:** [DEC-ECO-0005](../../decisoes/DEC-ECO-0005-profundidades-pontos-tg.md) (aprovada em 17/11/2026; limite de 10 m ainda proposto).
- **Dono:** agente único (Emenda 2). Homologação no Civil 3D, conferência no mapa, merge e instalação: usuário.
- **Objetivo:** processar em lote um DWG com milhares de tubulações, levar ao GeoMap as profundidades medidas nos pontos `_TG` e deixar só as exceções para o operador.

## Passos
| Passo | Entrega | Verificação | Estado |
|---|---|---|---|
| M7.1 | Entrega 1: leitura dos pontos `_TG`, interpretação pelo interpretador existente, associação pelo NODE (vértice e trecho), conflitos, diagnóstico CSV (`GEOAUTO3DEPTHSCAN`) e exportação provisória (`GEOAUTO3GEOMAPEXPORT`) | CI: casos sintéticos, 25.000 linhas × 50.000 pontos, dois desenhos reais da auditoria com contagens conferidas por outra implementação, amostra validada pelo GeoMap. Usuário: roteiro numa cópia de uma amostra do projeto real e valores no mapa | 🔄 |
| M7.2 | Entrega 2: profundidades gravadas nas linhas (Object Data `GEO_PROFUNDIDADE`, visível e editável; cópia escondida em XRecord), correção manual protegida (`ORIGEM`), atualização sem duplicar, exportação lendo do desenho | Testes da regra de gravação na CI; roteiro: gravar, editar à mão, reexecutar, salvar, reabrir, reexportar; GUIDs preservados | 🔄 no PR #34 |

**Critério de aceitação:** numa amostra representativa do projeto real, as profundidades associadas conferem com as descrições dos pontos (conferência manual); cobertura, ambíguos, ausentes e divergências contados no diagnóstico; os valores aparecem no GeoMap.

## Decisões tomadas neste marco
| Data | Decisão | Quem |
|---|---|---|
| 17/11/2026 | Prioridade emergencial, implementação autorizada (merge continua com o usuário) | Usuário |
| 17/11/2026 | Tolerância de 1 mm (só NODE); ponta de duas linhas vale para as duas; profundidade até a geratriz superior; gravação no DWG em XRecord próprio | Usuário |
| 18/11/2026 | No mapa, um resumo: média dos pontos, profundidade inicial e final e quantidade de pontos; o valor de cada ponto fica no CSV | Usuário |
| 18/11/2026 | Entrega 2 com Object Data visível na paleta, edição manual protegida e campo ORIGEM | Usuário |
| 18/11/2026 | Profundidade inicial/final = primeira/última registrada ao longo da linha, mesmo fora das pontas | Usuário |

## Pontos de atenção
- A CI não compila o leitor CAD nem os comandos: só o roteiro no Civil 3D os prova.
- Até o M7.2, as profundidades no GeoMap são provisórias: reexportar respondendo "Não" às profundidades as apaga do mapa.
- Nenhuma precisão de 100% é declarada: os números da CI são de desenhos de teste e da auditoria; o projeto real precisa da conferência manual.
