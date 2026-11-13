# M2 — Repositório único (monorepo)

- **Estado:** em execução desde 12/11/2026 (M2.1). O M1 foi concluído em 12/11/2026.
- **Decisão:** [DEC-ECO-0001](../../decisoes/DEC-ECO-0001-monorepo.md)
- **Dono:** Chat 00 (M2.1, M2.3); Chats 01–03 (M2.2, M2.4); usuário (merge, M2.5)

## Origens a importar (revalidar antes do M2.3)
| Aplicação | Repositório | `main` em 12/11/2026 | Workflows de CI a mover |
|---|---|---|---|
| GeoMap | `JonhyGonc/Geomap` (privado) | `83259c2` | `backend-ci.yml`, `frontend-ci.yml` |
| GeoAutoEngine | `JonhyGonc/GeoAutoEngine` | `71588ab` | `geomap-export.yml`, `video-inspection.yml`, `tests.yml` |
| GeoAutoDocs | `JonhyGonc/GeoAutoDocs` | `64081d5` | `ci.yml` |

## Passos
| Passo | Dono | Entrega | Verificação | Estado |
|---|---|---|---|---|
| M2.1 | Chat 00 | DEC-ECO-0001 + Emenda 1 do protocolo + este plano | PR aprovado pelo usuário (GeoEcosystem#6) | ✅ |
| M2.2 | Chat 01 | Varredura do histórico do GeoMap | Feita em `83259c2`: nenhuma credencial; itens de privacidade aceitos (12/11/2026). **Revarrer o delta** se a `main` do GeoMap mudar antes do M2.3 | ✅ |
| M2.3 | Chat 00 | Congelamento anunciado → importação com histórico (`git subtree add --prefix=apps/<app>`, sem squash) numa branch → workflows movidos para `.github/workflows/<app>-*.yml` com `paths: apps/<app>/**` e `working-directory` → nenhuma mudança de código além de caminhos | `git diff <origem> HEAD:apps/<app>` vazio para cada app (exceto `.github/`); `git log -- apps/<app>` mostra o histórico | 🔄 congelamento confirmado pelos 3 chats em 12/11/2026; importado `Geomap@83259c2`, `GeoAutoEngine@71588ab`, `GeoAutoDocs@64081d5` — árvores idênticas às origens |
| M2.4 | Chats 01–03 | Cada especialista valida a sua pasta: CI verde; scripts com caminho relativo funcionando | CI das três aplicações verde no PR; o usuário gera o bundle do GAE e o instalador do GAD a partir do monorepo | ⏳ |
| M2.5 | Usuário | Merge; arquivar os 3 repositórios antigos (Settings → Archive) com aviso no README; tags novas com prefixo (`geoautoengine/v3.2.0`, `geoautodocs/v1.1.0`) | Repositórios antigos somente leitura; trabalho novo só no monorepo | ⏳ |

## Congelamento
- **Começa:** quando o Chat 00 anunciar o início do M2.3.
- **Termina:** no M2.5.
- **Durante:** nenhum merge nos repositórios antigos. PRs abertos ficam parados e são refeitos no monorepo depois.

## Pontos de atenção já conhecidos
| Aplicação | Ponto | Origem |
|---|---|---|
| GeoMap | `tests/integration/verify-network-imports.sh` roda `docker compose` a partir da raiz da aplicação; a CI precisa entrar em `apps/geomap` | Relatório Etapa 5 do Chat 01 |
| GeoMap | Manter na CI os passos das migrations 004/005 | Idem |
| GeoMap | A aplicação já tem uma pasta `apps/` própria → caminhos `apps/geomap/apps/api`, `apps/geomap/apps/web` | Estrutura do repositório |
| GAE | Testes `EXTERNAL` e `scripts/test.ps1` usam `../reference` e `../GeoAutoEngine.Legacy`; no monorepo, `..` passa a ser `apps/` | GAE-03; PR #16 |
| GAE | `global.json` (SDK 8.0.415) e o .NET portátil `.\.dotnet` ficam dentro de `apps/geoautoengine` | GAE Q1 |
| GAD | `installer/GeoAutoDocs.iss` e `DISTRIBUICAO.md` usam caminhos relativos à raiz do repositório | GAD-01 |
| Todos | `.gitignore`, `.github/ISSUE_TEMPLATE` e `AGENTS.md` de cada app continuam dentro da pasta dela; só os workflows sobem para a raiz | DEC-ECO-0001 |
