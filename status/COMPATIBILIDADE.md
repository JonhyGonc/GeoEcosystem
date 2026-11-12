# Matriz de compatibilidade e builds instaladas

**Por que existe:** as versões declaradas nunca mudam (GAE 3.1.1, GAD 1.0.4), os desenhistas instalaram em épocas diferentes, e nenhuma build registra o commit de origem. Este é o único registro do que está em uso (risco R-04).

## Como identificar a build numa máquina
**GeoAutoEngine** (Civil 3D):
1. Rodar `GEOAUTO3VIUPDATETEXTS` e `GEOAUTO3VISYNC` e anotar quais respondem.
2. Anotar a data de `%AppData%\Autodesk\ApplicationPlugins\GeoAutoEngine.bundle\Contents\<ano>`.

| `GEOAUTO3VIUPDATETEXTS` | `GEOAUTO3VISYNC` | Build provável |
|---|---|---|
| ❌ | ❌ | `main@3acab63` ou anterior |
| ✅ | ❌ | `main` + `feature/atualizar-textos-anomalias@1724528` |
| ❌ | ✅ | `main` + `sync-section-names` (algum commit até `2937469`) |
| ✅ | ✅ | combinação não existente em nenhuma branch (montagem local) |

**GeoAutoDocs:** existe a janela de revisão de correções vindas do Civil 3D?
- Sim → build da `fix/section-edit-save-stability`.
- Não → `main` (ou anterior).

## Variante GADX por combinação
| GAE \ GAD | GAD `main` (tipos 1–5) | GAD fix (tipos 1–7) |
|---|---|---|
| GAE `main` ou `feature/atualizar-textos-anomalias` | ⚠️ Inspeção OK; correções recusadas ("não conectado") ou corrida → falha falsa + espera de 5 min | ✅ Correções funcionam; ⚠️ Civil 3D espera de forma síncrona (até 5 min) enquanto o usuário revisa (hipótese alta) |
| GAE `sync-section-names` | ⚠️ Igual à célula acima, sem bloquear a UI | ✅ Correções sem bloquear a UI |

## Versões oficiais
| Aplicação | Versão | Commit | Tag |
|---|---|---|---|
| GeoAutoEngine | 3.2.0 | `d762359` | `v3.2.0` (a criar pelo Chat 02) |
| GeoAutoDocs | 1.1.0 | `64081d5` | `v1.1.0` (a criar pelo Chat 03) |

Com as versões oficiais, a forma de identificar a build é a versão do arquivo (GAE: `FileVersion` da `GeoAutoEngine.Plugin.dll` = `3.2.0.0`; GAD: versão exibida pelo instalador/programa = `1.1.0`).

## Decisão de instalação (usuário, 12/11/2026)
- **GeoAutoDocs 1.1.0:** instalar em todas as máquinas, pelo instalador da Release `v1.1.0`.
- **GeoAutoEngine 3.2.0:** só em máquinas com **Civil 3D 2026** (o único host homologado). Os desenhistas que usam 2024 ou 2025 passarão para o Civil 3D 2026 e recebem o 3.2.0 depois disso. Até lá, continuam com o GeoAutoEngine que já têm.
- Os hosts 2024 e 2025 continuam no código, mas **não são compilados nem testados** para a 3.2.0.

## Máquinas registradas
| Máquina / desenhista | Data da verificação | Civil 3D | Build GAE | Build GAD | Evidência |
|---|---|---|---|---|---|
| Usuário (`JonhyGonc`) | 12/11/2026 | 2026 | **3.2.0** (`main@d762359`, homologada) | **1.1.0** (`main@64081d5`, homologada) | Homologação conjunta aprovada pelo usuário em 12/11/2026 09:46: roteiro `GAE_3.2.0_ROTEIRO.md`, passos 9–17 OK |
| Usuário (`JonhyGonc`) — antes | 10/11/2026 | 2026 | `main@3acab63` + `feature/atualizar-textos-anomalias@1724528` | derivada da `fix/section-edit-save-stability` | situação anterior ao M1 |
| Demais desenhistas | — | — | **desconhecida** (instalações em épocas diferentes) | **desconhecida** | — |
