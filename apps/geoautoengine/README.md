# GeoAutoEngine 3

Plugin nativo em .NET para Civil 3D 2024, 2025 e 2026.

> **Monorepo.** O GeoAutoEngine vive na pasta `apps/geoautoengine` do repositório [GeoEcosystem](https://github.com/JonhyGonc/GeoEcosystem), junto com o GeoAutoDocs e o GeoMap. Todos os comandos deste README rodam **a partir de `apps/geoautoengine`**. A CI fica na raiz do monorepo, em `.github/workflows/geoautoengine-*.yml`. As tags de versão têm o prefixo da aplicação (`geoautoengine/v3.2.0`). O repositório antigo `JonhyGonc/GeoAutoEngine` está arquivado; números de Issues e PRs antigos se referem a ele, e Issues novas vão para o GeoEcosystem.

| Civil 3D | Release | Runtime |
|---|---|---|
| 2024 | R24.3 | .NET Framework 4.8 |
| 2025 | R25.0 | .NET 8 |
| 2026 | R25.1 | .NET 8 |

O bundle traz um host por versão. O estado da validação de cada uma (build e smoke no CAD) está em [COMPATIBILITY.md](COMPATIBILITY.md).

## Estrutura

- `src/`: projetos de produção;
- `tests/`: testes C# seletivos;
- `docs/`: arquitetura, etapas e evidências;
- `scripts/`: build, testes e gates;
- `tools/`: ferramentas auxiliares de manutenção;
- `build/`: manifesto e estrutura do bundle Autodesk;
- `assets/`: ativos exclusivos da versão 3;
- `samples/`: exemplos exclusivos da versão 3.

## Validação rápida

```powershell
.\.dotnet\dotnet.exe restore GeoAutoEngine.sln
.\scripts\test.ps1 -Suite fast
.\.dotnet\dotnet.exe build GeoAutoEngine.sln -c Release --no-restore
```

O código legado e seu ambiente Python ficam na pasta `GeoAutoEngine.Legacy`, e os dados de equivalência compartilhados na pasta `reference`. As duas ficam **fora** do controle de versão (veja abaixo).

## Testes que dependem de dados externos

Os testes marcados com `[Trait("Category", "EXTERNAL")]` leem pastas que **não** são versionadas:

- `reference`: CSVs de levantamento e oráculos de regressão (`legacy_csv/`, `regression_data/`). Os CSVs são de obras reais e **nunca** entram no repositório, que é público;
- `GeoAutoEngine.Legacy`: o logo original, para comparação, e as suítes `legacy`/`full` do `scripts\test.ps1`.

Os testes, o `scripts\test.ps1`, o `tools\update-reference-manifest.ps1` e os `tools\capture-*.py` procuram essas pastas **subindo** a partir de `apps/geoautoengine`. Então qualquer um destes lugares funciona:

| Onde colocar | Exemplo |
|---|---|
| Dentro de `apps/` do monorepo | `GeoEcosystem\apps\reference` |
| Na raiz do monorepo | `GeoEcosystem\reference` |
| Ao lado do clone | `C:\Projetos\reference`, com o clone em `C:\Projetos\GeoEcosystem` |

As duas primeiras opções já estão no `.gitignore` da raiz, para não serem enviadas por engano.

A CI (`.github/workflows/geoautoengine-tests.yml`, Windows) roda a suíte inteira **menos** essa categoria. Para rodá-los, coloque as duas pastas num dos lugares acima e execute:

```powershell
.\.dotnet\dotnet.exe test tests\GeoAutoEngine.Tests\GeoAutoEngine.Tests.csproj -c Release --filter "Category=EXTERNAL"
```

Sem essas pastas, os testes `EXTERNAL` falham, inclusive os que também fazem parte da suíte `fast`.

`DrawingPlanBuilderTests.Stage4CadSmokeFixture_ProducesFourFixedAndThreeGeometryPendingItems` lê `samples/stage4_cad_smoke_day*.csv`, que estão versionados. Ele não depende mais de dados externos nem do nome da pasta do clone, e roda na CI.

O fluxo nativo completo executa `CSV(s) -> ImportBatch -> DrawingPlan -> resolução geométrica -> entidades no DWG` pela Managed .NET API. O fechamento final da versão 3.0.15 está em `docs/STAGE_6_FINAL_REPORT.md`.

## Instalação na equipe

Para instalar uma versão já compilada no computador de outro desenhista, com backup, conferência da versão e rollback, veja [docs/INSTALACAO_EQUIPE.md](docs/INSTALACAO_EQUIPE.md).

## Integração GeoMap

O comando `GEOAUTO3GEOMAPEXPORT` exporta Polylines 2D do Civil 3D para o contrato GeoMap v0.1. Consulte [docs/GEOMAP_EXPORT.md](docs/GEOMAP_EXPORT.md) para escopo, instalação e uso.
