# Estado atual

**Atualizado em:** 16/11/2026 (fim da sessão da manhã), pelo agente (Claude). Atualize ao fim de cada sessão.

## Onde estamos
- **M1 — Versão oficial:** ✅ concluído. GeoAutoEngine 3.2.0 (`d762359`) e GeoAutoDocs 1.1.0 (`64081d5`), homologados pelo usuário em 12/11/2026.
- **M2 — Monorepo:** M2.1–M2.4 ✅. Os ajustes pós-monorepo (docs das 3 apps e scripts do GAE) entraram no PR #10, mesclado em 16/11/2026 depois do roteiro manual do usuário. Falta só o **M2.5** (usuário): tags e arquivamento.
- **M3 — Contrato GADX:**
  - M3.1 ✅: especificação normativa GADX 1.1 + `DEC-ECO-0002`, no PR #13, mesclado em 16/11/2026;
  - M3.2 🔄: fixtures + testes nas duas CIs, no PR do branch `contrato/014-gadx-fixtures`.

  Acompanhamento: Issue #11; mudança de contrato: Issue #12.
- **Governança:** um único agente (Emenda 2, 16/11/2026). Processo de PR e templates já atualizados (PR #13).
- **Próximo passo:** CI verde e merge do PR do M3.2 → **M3.3**: biblioteca `libs/GeoEcosystem.Contracts.Inspection` e servidor com `capabilities` (regras S1–S3). Antes de escrever código, decidir o alvo de compilação da biblioteca.

## Pendências do usuário
1. Criar as tags no monorepo (o agente não consegue enviar tags). Conferido em 16/11/2026: ainda não existem.
   ```
   cd C:\Users\João\Desktop\GeoEcosystem
   git checkout main
   git pull
   git tag -a geoautoengine/v3.2.0 d762359 -m "GeoAutoEngine 3.2.0 (M1)"
   git tag -a geoautodocs/v1.1.0 64081d5 -m "GeoAutoDocs 1.1.0 (M1)"
   git push origin geoautoengine/v3.2.0 geoautodocs/v1.1.0
   ```
2. Arquivar `JonhyGonc/Geomap`, `JonhyGonc/GeoAutoEngine` e `JonhyGonc/GeoAutoDocs` (Settings → General → Danger Zone → Archive).
3. Instalar na equipe (sem pressa): GeoAutoDocs 1.1.0 em todos; GeoAutoEngine 3.2.0 só em quem tem Civil 3D 2026.

## Decisões da sessão de 16/11/2026
- **Transição do GADX (S2):** um cliente sem `capabilities` (GAD 1.1.0) continua recebendo correções de um GAE novo. Decisão do usuário, registrada na `DEC-ECO-0002`.
- **Versão do GADX:** no fio continua `1`; a especificação vira a revisão 1.1.

## Fatos aprendidos (para não repetir)
- **Máquina do usuário:** não tem as pastas externas `reference` e `GeoAutoEngine.Legacy` nem o .NET portátil `.dotnet`. Os testes `EXTERNAL` e as suítes `legacy`/`full` do GAE não rodam lá; nunca rodaram.
- **Clone do monorepo:** fica em `C:\Users\João\Desktop\GeoEcosystem`. O clone antigo do GAE fica em `Documentos\GitHub\GeoAutoEngine`.
- **Geração do instalador:** o usuário gera o do GAD a partir do monorepo (testado em 16/11/2026).
- **Sessões do agente:** não têm .NET nem PowerShell; código C# só é verificado pela CI.

## Notas para o M3.3/M3.4
- O `Hello` do GeoAutoDocs envia `applicationVersion` = 1.0.0.0 (versão do assembly Infrastructure). Corrigir no M3.4.
- Alvo de compilação da biblioteca: só o Civil 3D 2026 (.NET 8) é homologado, mas os hosts 2024 (`netstandard2.0`/net48) e 2025 continuam no código.
- `locationMode`: o GAE compara o nome exato. O enum do GAD tem aliases. Falta o teste de nome canônico (M3.4).
