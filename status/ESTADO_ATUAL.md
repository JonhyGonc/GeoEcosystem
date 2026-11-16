# Estado atual

**Atualizado em:** 16/11/2026, pelo agente (Claude). Atualize ao fim de cada sessão.

## Onde estamos
- **M1 — Versão oficial:** ✅ concluído. GeoAutoEngine 3.2.0 (`d762359`) e GeoAutoDocs 1.1.0 (`64081d5`), homologados pelo usuário em 12/11/2026.
- **M2 — Monorepo:** quase concluído. As três aplicações estão em `apps/` com histórico (PR #7, mesclado em 13/11/2026), com CI 6/6 verde.
- **Governança:** um único agente (Emenda 2, 16/11/2026).

## Pendências do usuário
1. Criar as tags no monorepo (o agente não consegue enviar tags):
   ```
   git clone https://github.com/JonhyGonc/GeoEcosystem.git
   cd GeoEcosystem
   git tag -a geoautoengine/v3.2.0 d762359 -m "GeoAutoEngine 3.2.0 (M1)"
   git tag -a geoautodocs/v1.1.0 64081d5 -m "GeoAutoDocs 1.1.0 (M1)"
   git push origin geoautoengine/v3.2.0 geoautodocs/v1.1.0
   ```
2. Arquivar `JonhyGonc/Geomap`, `JonhyGonc/GeoAutoEngine` e `JonhyGonc/GeoAutoDocs` (Settings → General → Danger Zone → Archive).
3. Instalar na equipe (sem pressa): GeoAutoDocs 1.1.0 em todos; GeoAutoEngine 3.2.0 só em quem tem Civil 3D 2026.

## Ajustes pós-monorepo (pequenos, só documentação e scripts locais)
| App | O que fazer | Origem |
|---|---|---|
| GeoMap | README/AGENTS.md: comandos rodam a partir de `apps/geomap`; workflows agora na raiz; nota de que `#NN` antigos são do repositório `Geomap` arquivado; restos de "v0.1"; link de Issues em `docs/BACKLOG.md` | Validação M2.4 (Chat 01) |
| GeoAutoEngine | Testes `EXTERNAL`, `scripts/test.ps1 -Suite legacy/full`, `update-reference-manifest.ps1` e `capture-*.py`: procurar `reference/` e `GeoAutoEngine.Legacy/` subindo pastas; README do app; `Stage4CadSmokeFixture` (Issue #19 do repositório antigo) | Validação M2.4 (Chat 02) |
| GeoAutoDocs | README (#L5, #L44, "Como contribuir"), `DISTRIBUICAO.md` (#L20–26: clone/cd/tag), `docs/INTEGRACAO_GEOAUTOENGINE.md` (#L5) | Validação M2.4 (Chat 03) |

## Próximo marco: M3 — Contrato GADX estável
Ver `roadmap/ROADMAP.md`, seção M3:
- especificação normativa do protocolo GADX v1.1 com capacidades no `Hello`;
- biblioteca compartilhada em `libs/` usada pelo GeoAutoEngine e pelo GeoAutoDocs, no lugar das duas cópias;
- correções aplicadas como "estado completo".

Notas já registradas para o M3:
- o `Hello` do GeoAutoDocs envia `applicationVersion` = 1.0.0.0 (versão do assembly Infrastructure);
- como só o Civil 3D 2026 é homologado, vale reavaliar se a biblioteca precisa mesmo de `netstandard2.0` (exigência do host 2024).
