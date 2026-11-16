# Estado atual

**Atualizado em:** 16/11/2026 (fim da sessão da manhã), pelo agente (Claude). Atualize ao fim de cada sessão.

## Onde estamos
- **M1 — Versão oficial:** ✅ concluído. GeoAutoEngine 3.2.0 (`d762359`) e GeoAutoDocs 1.1.0 (`64081d5`), homologados pelo usuário em 12/11/2026.
- **M2 — Monorepo:** M2.1–M2.4 ✅. Os ajustes pós-monorepo (docs das 3 apps e scripts do GAE) entraram no PR #10, mesclado em 16/11/2026 depois do roteiro manual do usuário. Falta só o **M2.5** (usuário): tags e arquivamento.
- **M3 — Contrato GADX:**
  - M3.1 ✅: especificação normativa GADX 1.1 + `DEC-ECO-0002`, no PR #13, mesclado em 16/11/2026;
  - M3.2 ✅: fixtures + testes nas duas CIs (PR #14, mesclado em 16/11/2026);
  - M3.3a 🔄: biblioteca `libs/GeoEcosystem.Contracts.Inspection` (só `net8.0`), sem mudança de comportamento (PR #15). Falta o usuário compilar o plugin 2026 e fazer o teste de sempre.

  Acompanhamento: Issue #11; mudança de contrato: Issue #12.
- **Governança:** um único agente (Emenda 2, 16/11/2026). Processo de PR e templates já atualizados (PR #13).
- **Próximo passo:** teste manual e merge do PR #15 → M3.3b (payloads das correções na biblioteca) → M3.3c (servidor com `capabilities`, regras S1–S3).

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
- **SDK na máquina do usuário:** .NET 8 SDK global (ele gerou o instalador do GAD com `dotnet publish`).

## Notas para o M3.3/M3.4
- O `Hello` do GeoAutoDocs envia `applicationVersion` = 1.0.0.0 (versão do assembly Infrastructure). Corrigir no M3.4.
- Alvo da biblioteca: **só `net8.0`** (decisão do usuário, 16/11/2026). O host 2024 do GAE não compila mais. `build-release.ps1` precisa de `-Versions 2026`.
- A CI **não compila** o `GeoAutoEngine.Plugin` nem o `Cad2026` (exigem DLLs da Autodesk). Toda mudança neles precisa de compilação na máquina do usuário.
- `locationMode`: o GAE compara o nome exato. O enum do GAD tem aliases. Falta o teste de nome canônico (M3.4).
