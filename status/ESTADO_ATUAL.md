# Estado atual

**Atualizado em:** 16/11/2026 (fim da sessão da manhã), pelo agente (Claude). Atualize ao fim de cada sessão.

## Onde estamos
- **M1 — Versão oficial:** ✅ concluído. GeoAutoEngine 3.2.0 (`d762359`) e GeoAutoDocs 1.1.0 (`64081d5`), homologados pelo usuário em 12/11/2026.
- **M2 — Monorepo:** ✅ concluído em 16/11/2026: tags `geoautoengine/v3.2.0` e `geoautodocs/v1.1.0` criadas (conferidas no GitHub); repositórios antigos arquivados (confirmado pelo usuário).
- **M3 — Contrato GADX:** ✅ concluído em 16/11/2026 (PRs #13–#18). Especificação GADX 1.1 normativa; fixtures nas CIs; biblioteca `libs/GeoEcosystem.Contracts.Inspection` (`net8.0`) usada pelo GAE e pelo GAD; `capabilities` nos dois lados. Homologado na máquina do usuário (GAD 1.1.0 e GAD novo). Issues #11 e #12 fechadas.
- **Governança:** um único agente (Emenda 2, 16/11/2026). Processo de PR e templates já atualizados (PR #13).
- **M4 — DWG → GeoMap:** 🔄 M4.1 (DEC-ECO-0003) no PR #19. D-04 decidida pelo usuário em 16/11/2026: código próprio por feição. Acompanhamento: Issue #20; plano: `roadmap/marcos/M4-dwg-geomap.md`.
- **Próximo passo:** merge do PR #19 → M4.2 (GeoMap: `sourceSystem` único) e M4.3a (GAE: `FEATURE_ID` em XData).

## Pendências do usuário
1. Mesclar o PR #19 (M3 concluído + DEC-ECO-0003).
2. ~~Instalar na equipe~~ **Adiado** (decisão do usuário, 16/11/2026): só quando o projeto inteiro estiver terminado (depois do M5), numa rodada única com as versões finais. A equipe quase não usa o GeoAutoDocs. Até lá, cada marco é homologado só na máquina do usuário.

## Decisões da sessão de 16/11/2026
- **Transição do GADX (S2):** um cliente sem `capabilities` (GAD 1.1.0) continua recebendo correções de um GAE novo. Decisão do usuário, registrada na `DEC-ECO-0002`.
- **Versão do GADX:** no fio continua `1`; a especificação vira a revisão 1.1.
- **Biblioteca do contrato:** só `net8.0` ("somente com a versão de 2026").
- **Instalação na equipe:** só no fim do projeto, numa rodada única.
- **Identidade no GeoMap (D-04):** código próprio (GUID) gravado pelo GAE em XData em cada feição exportada.

## Fatos aprendidos (para não repetir)
- **Máquina do usuário:** não tem as pastas externas `reference` e `GeoAutoEngine.Legacy` nem o .NET portátil `.dotnet`. Os testes `EXTERNAL` e as suítes `legacy`/`full` do GAE não rodam lá; nunca rodaram.
- **Clone do monorepo:** fica em `C:\Users\João\Desktop\GeoEcosystem`. O clone antigo do GAE fica em `Documentos\GitHub\GeoAutoEngine`.
- **Geração do instalador:** o usuário gera o do GAD a partir do monorepo (testado em 16/11/2026).
- **Instalação do GAE para testar:** com o Civil 3D fechado, rodar `apps\geoautoengine\Instalar GeoAutoEngine 3.cmd`. Ele compila o host 2026 e instala, com backup em `Contents\2026.backup-<data>`. Funciona com o .NET do Windows desde o PR #16.
- **GAD para testar:** `dotnet run --project src\GeoAutoDocs.App -c Release` em `apps\geoautodocs`.
- **Sessões do agente:** não têm .NET nem PowerShell; código C# só é verificado pela CI.
- **SDK na máquina do usuário:** .NET 8 SDK global (ele gerou o instalador do GAD com `dotnet publish`).

## Notas técnicas
- Alvo da biblioteca: **só `net8.0`** (decisão do usuário, 16/11/2026). O host 2024 do GAE não compila mais. `build-release.ps1` precisa de `-Versions 2026`.
- A CI **não compila** o `GeoAutoEngine.Plugin` nem o `Cad2026` (exigem DLLs da Autodesk). Toda mudança neles precisa de compilação na máquina do usuário.
