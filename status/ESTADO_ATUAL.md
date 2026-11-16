# Estado atual

**Atualizado em:** 16/11/2026 (sessão da manhã), pelo agente (Claude). Atualize ao fim de cada sessão.

## Onde estamos
- **M1 — Versão oficial:** ✅ concluído. GeoAutoEngine 3.2.0 (`d762359`) e GeoAutoDocs 1.1.0 (`64081d5`), homologados pelo usuário em 12/11/2026.
- **M2 — Monorepo:** quase concluído. As três aplicações estão em `apps/` com histórico (PR #7, mesclado em 13/11/2026), com CI 6/6 verde.
- **Governança:** um único agente (Emenda 2, 16/11/2026).
- **Próximo passo:** CI + roteiro + merge do PR #10 → M2.5 (usuário) → M3.1 (spec GADX v1.1 + `DEC-ECO-0002`).
- **Sessão de 16/11/2026:** ajustes pós-monorepo das três aplicações no [PR #10](https://github.com/JonhyGonc/GeoEcosystem/pull/10), aguardando CI, roteiro manual e merge do usuário.

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

## Ajustes pós-monorepo → PR #10
Feitos no [PR #10](https://github.com/JonhyGonc/GeoEcosystem/pull/10) (aberto; não mesclado):
- **GeoMap:** README/AGENTS.md (comandos em `apps/geomap`, CI na raiz, `#NN` antigos), contrato v0.1–v0.3 no README, links em `docs/BACKLOG.md` e `docs/CIVIL3D_EXPORTER.md`.
- **GeoAutoDocs:** README, `DISTRIBUICAO.md` (clone do monorepo, `cd apps\geoautodocs`, tags `geoautodocs/vX.Y.Z`), `docs/INTEGRACAO_GEOAUTOENGINE.md`.
- **GeoAutoEngine:** `scripts/test.ps1`, `tools/update-reference-manifest.ps1`, `tools/capture-*.py` e o teste do logo legado procuram `reference/` e `GeoAutoEngine.Legacy/` subindo pastas; `Stage4CadSmokeFixture` lê `samples/` e passou a rodar na CI; README e `docs/INSTALACAO_EQUIPE.md`.

Falta: CI verde do PR #10, o usuário rodar o roteiro manual do PR e fazer o merge.

Para depois (fora do PR #10): `governanca/PROCESSO_PR.md` e `.github/PULL_REQUEST_TEMPLATE.md` ainda falam em `APROVADO-CHAT-00` (anterior à Emenda 2).

## Próximo marco: M3 — Contrato GADX estável
Ver `roadmap/ROADMAP.md`, seção M3:
- especificação normativa do protocolo GADX v1.1 com capacidades no `Hello`;
- biblioteca compartilhada em `libs/` usada pelo GeoAutoEngine e pelo GeoAutoDocs, no lugar das duas cópias;
- correções aplicadas como "estado completo".

Notas já registradas para o M3:
- o `Hello` do GeoAutoDocs envia `applicationVersion` = 1.0.0.0 (versão do assembly Infrastructure);
- como só o Civil 3D 2026 é homologado, vale reavaliar se a biblioteca precisa mesmo de `netstandard2.0` (exigência do host 2024).
