# Estado atual

**Atualizado em:** 16/11/2026 (fim da sessão da manhã), pelo agente (Claude). Atualize ao fim de cada sessão.

## Onde estamos
- **M1 — Versão oficial:** ✅ concluído. GeoAutoEngine 3.2.0 (`d762359`) e GeoAutoDocs 1.1.0 (`64081d5`), homologados pelo usuário em 12/11/2026.
- **M2 — Monorepo:** M2.1–M2.4 ✅. Os ajustes pós-monorepo (docs das 3 apps e scripts do GAE) entraram no PR #10, mesclado em 16/11/2026 depois do roteiro manual do usuário. Falta só o **M2.5** (usuário): tags e arquivamento.
- **M3 — Contrato GADX:**
  - M3.1 ✅: especificação normativa GADX 1.1 + `DEC-ECO-0002`, no PR #13, mesclado em 16/11/2026;
  - M3.2 ✅: fixtures + testes nas duas CIs (PR #14, mesclado em 16/11/2026);
  - M3.3a ✅: biblioteca `libs/GeoEcosystem.Contracts.Inspection` (só `net8.0`), sem mudança de comportamento (PR #15, mesclado em 16/11/2026);
  - M3.3b 🔄: payloads das correções na biblioteca + `deploy.ps1` com o .NET do Windows (PR #16). Roteiro executado pelo usuário em 16/11/2026: "Funcionou certinho" (cobre o M3.3a e o M3.3b). Falta o merge.

  Acompanhamento: Issue #11; mudança de contrato: Issue #12.
- **Governança:** um único agente (Emenda 2, 16/11/2026). Processo de PR e templates já atualizados (PR #13).
- **Próximo passo:** merge do PR #16 → **M3.3c**: o servidor (GAE) lê `capabilities` e responde no `HelloAck` (regras S1–S3). É a primeira mudança de comportamento real do M3.

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
3. ~~Instalar na equipe~~ **Adiado** (decisão do usuário, 16/11/2026): só quando o projeto inteiro estiver terminado (depois do M5), numa rodada única com as versões finais. A equipe quase não usa o GeoAutoDocs. Até lá, cada marco é homologado só na máquina do usuário.

## Decisões da sessão de 16/11/2026
- **Transição do GADX (S2):** um cliente sem `capabilities` (GAD 1.1.0) continua recebendo correções de um GAE novo. Decisão do usuário, registrada na `DEC-ECO-0002`.
- **Versão do GADX:** no fio continua `1`; a especificação vira a revisão 1.1.
- **Biblioteca do contrato:** só `net8.0` ("somente com a versão de 2026").
- **Instalação na equipe:** só no fim do projeto, numa rodada única.

## Fatos aprendidos (para não repetir)
- **Máquina do usuário:** não tem as pastas externas `reference` e `GeoAutoEngine.Legacy` nem o .NET portátil `.dotnet`. Os testes `EXTERNAL` e as suítes `legacy`/`full` do GAE não rodam lá; nunca rodaram.
- **Clone do monorepo:** fica em `C:\Users\João\Desktop\GeoEcosystem`. O clone antigo do GAE fica em `Documentos\GitHub\GeoAutoEngine`.
- **Geração do instalador:** o usuário gera o do GAD a partir do monorepo (testado em 16/11/2026).
- **Instalação do GAE para testar:** com o Civil 3D fechado, rodar `apps\geoautoengine\Instalar GeoAutoEngine 3.cmd`. Ele compila o host 2026 e instala, com backup em `Contents\2026.backup-<data>`. Funciona com o .NET do Windows desde o PR #16.
- **GAD para testar:** `dotnet run --project src\GeoAutoDocs.App -c Release` em `apps\geoautodocs`.
- **Sessões do agente:** não têm .NET nem PowerShell; código C# só é verificado pela CI.
- **SDK na máquina do usuário:** .NET 8 SDK global (ele gerou o instalador do GAD com `dotnet publish`).

## Notas para o M3.3/M3.4
- O `Hello` do GeoAutoDocs envia `applicationVersion` = 1.0.0.0 (versão do assembly Infrastructure). Corrigir no M3.4.
- Alvo da biblioteca: **só `net8.0`** (decisão do usuário, 16/11/2026). O host 2024 do GAE não compila mais. `build-release.ps1` precisa de `-Versions 2026`.
- A CI **não compila** o `GeoAutoEngine.Plugin` nem o `Cad2026` (exigem DLLs da Autodesk). Toda mudança neles precisa de compilação na máquina do usuário.
- `locationMode`: o GAE compara o nome exato. O enum do GAD tem aliases. Falta o teste de nome canônico (M3.4).
