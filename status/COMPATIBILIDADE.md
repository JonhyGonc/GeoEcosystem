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

## Máquinas registradas
| Máquina / desenhista | Data da verificação | Civil 3D | Build GAE | Build GAD | Evidência |
|---|---|---|---|---|---|
| Usuário (`JonhyGonc`) | 10/11/2026 | 2026 | `main@3acab63` + `feature/atualizar-textos-anomalias@1724528` | derivada da `fix/section-edit-save-stability` | `GEOAUTO3VIUPDATETEXTS` ✅, `GEOAUTO3VISYNC` ❌; pasta `Contents\2026` de 22/10/2026 09:07 (commit `1724528` de 09:01); janela de revisão presente |
| Demais desenhistas | — | — | **desconhecida** (instalações em épocas diferentes) | **desconhecida** | — |
