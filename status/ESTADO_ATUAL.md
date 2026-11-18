# Estado atual

**Atualizado em:** 18/11/2026, pelo agente (Claude). Atualize ao fim de cada sessão.

## Onde estamos
- **M1 — Versão oficial:** ✅ concluído. GeoAutoEngine 3.2.0 (`d762359`) e GeoAutoDocs 1.1.0 (`64081d5`), homologados pelo usuário em 12/11/2026.
- **M2 — Monorepo:** ✅ concluído em 16/11/2026: tags `geoautoengine/v3.2.0` e `geoautodocs/v1.1.0` criadas (conferidas no GitHub); repositórios antigos arquivados (confirmado pelo usuário).
- **M3 — Contrato GADX:** ✅ concluído em 16/11/2026 (PRs #13–#18). Especificação GADX 1.1 normativa; fixtures nas CIs; biblioteca `libs/GeoEcosystem.Contracts.Inspection` (`net8.0`) usada pelo GAE e pelo GAD; `capabilities` nos dois lados. Homologado na máquina do usuário (GAD 1.1.0 e GAD novo). Issues #11 e #12 fechadas.
- **Governança:** um único agente (Emenda 2, 16/11/2026). Processo de PR e templates já atualizados (PR #13).
- **M4 — DWG → GeoMap:** M4.1 ✅ (DEC-ECO-0003, PR #19). M4.2 ✅: identidade do projeto no GeoMap independente do `sourceSystem` (DEC-032 do GeoMap, PR #21). M4.3a ✅: o GAE grava o código próprio (`FEATURE_ID`) em cada Polyline exportada (PR #22; roteiro no Civil 3D aprovado pelo usuário: reexportar, MOVE, COPY e BREAK). M4.3b ✅: o exportador leva a cor da layer (`network.color`, contrato 0.2), com teste cruzado na CI (PR #23; roteiro aprovado pelo usuário). M4.3c ✅: blocos como pontos (contrato 0.3), numa rede e num arquivo separados (PR #24; roteiro aprovado: 487 tês, coordenadas conferidas, reexportação sem códigos novos). M4.5 ✅: botão Importar no site do GeoMap (PR #26; testado pelo usuário no navegador). M4.3d ✅: LINE, ARC e arcos de Polyline, tesselados com no máximo 5 mm (PR #25; testado pelo usuário: rede inteira exportada e conferida no mapa). M4.3e ✅: seleção por layer, uma rede por layer, diâmetro/material da layer e atributos dos blocos (PR #27; testado pelo usuário). M4.4 ✅: script privado descartado. Ponte DXF aposentada (DEC-034 do GeoMap, PR #28). M4.6 🔄: ortofoto do projeto (DEC-035 do GeoMap, PR #29). Acompanhamento: Issue #20; plano: `roadmap/marcos/M4-dwg-geomap.md`.
- **Prioridade atual (pedido do usuário, 16/11/2026):** ortofoto do projeto no GeoMap (M4.6, DEC-035 do GeoMap; PR #29), porque o chefe do usuário quer ver o mapa com a ortofoto. Depois, o M5.
- **M5 — Anomalias no mapa:** decisões tomadas em 16/11/2026, pelo celular (DEC-ECO-0004, PR #31; plano em `roadmap/marcos/M5-anomalias-mapa.md`; Issue #30). Implementação começa depois da ortofoto (M4.6).
- **Prioridade emergencial (pedido do usuário, 17/11/2026): M7 — profundidades das tubulações pelos pontos `_TG`.** Entregas 1 e 2 no PR #34: associação pelo NODE, diagnóstico `GEOAUTO3DEPTHSCAN`, gravação nas linhas (Object Data `GEO_PROFUNDIDADE`, visível e editável, com `ORIGEM` AUTOMATICA/MANUAL) e exportação do que está gravado (média, pontas e quantidade de pontos). DEC-ECO-0005 aprovada (17 e 18/11/2026); o limite de 10 m segue proposto. Testado pelo usuário em 18/11/2026 no projeto real (2.949 linhas, 84.021 pontos _TG): a importação da entrega 1 no site funcionou e a gravação na tabela `GEO_PROFUNDIDADE` funcionou, com média, inicial/final (primeira/última profundidade da linha), pontos e ORIGEM na paleta Propriedades, depois de duas correções no Map 3D (lista de registros vazia, criação da tabela). Plano: `roadmap/marcos/M7-profundidades-tg.md`.
- **Prontos para o usuário:** PR #32 (conversor da ortofoto, testado em 17/11/2026) e PR #33 (Civil 3D 2027, M6).
- **Próximo passo:** merge do PR #34 pelo usuário. Faltam no roteiro: correção à mão + nova rodada (MANUAL mantido), reabrir o DWG e exportar o gravado. Depois: revisar no CSV os 8.169 pontos "perto, fora da tolerância" e os 4.660 códigos desconhecidos do projeto real; limpeza do log da vídeo-inspeção (393 MB, a decidir pelo usuário) → M5.2.

## Pendências do usuário
1. Merge do PR #28 (aposenta a ponte DXF); roteiro da ortofoto (PR #29) com uma ortofoto real.
2. ~~Instalar na equipe~~ **Adiado** (decisão do usuário, 16/11/2026): só quando o projeto inteiro estiver terminado (depois do M5), numa rodada única com as versões finais. A equipe quase não usa o GeoAutoDocs. Até lá, cada marco é homologado só na máquina do usuário.

## Decisões da sessão de 16/11/2026
- **Transição do GADX (S2):** um cliente sem `capabilities` (GAD 1.1.0) continua recebendo correções de um GAE novo. Decisão do usuário, registrada na `DEC-ECO-0002`.
- **Versão do GADX:** no fio continua `1`; a especificação vira a revisão 1.1.
- **Biblioteca do contrato:** só `net8.0` ("somente com a versão de 2026").
- **Instalação na equipe:** só no fim do projeto, numa rodada única.
- **Identidade no GeoMap (D-04):** código próprio (GUID) gravado pelo GAE em XData em cada feição exportada.
- **Blocos no exportador (M4.3c):** uma seleção com linhas e blocos gera dois arquivos, um por rede.
- **Curvas no exportador (M4.3d):** só LINE, ARC e arcos de Polyline; tesselação com no máximo 5 mm.
- **Ponte DXF (M4):** aposentada sem comparação final; script privado (GM-09) descartado.
- **Redes por layer (M4.3e):** uma rede por layer, como a ponte; diâmetro e material lidos de `RD_<diâmetro>[_<material>]`.
- **Ortofoto (M4.6, DEC-035 do GeoMap):** ortofotos de drone (ECW/JP2, 1–10 GB) convertidas uma vez no QGIS para MBTiles, com o nome do projeto; só no PC do usuário por enquanto, mas o cliente (proprietário) deve ver depois (hospedagem e login pendentes).
- **M5 (DEC-ECO-0004):** posição pelo GAE; pacote `.zip` com pontos e fotos pelo botão do site; intervalo como trecho destacado; vínculo por `occurrenceId` + `FEATURE_ID`.
- **Importação pelo site (M4.5, DEC-033 do GeoMap):** pedido novo; sempre ligada, sem login, só uso local (risco aceito).

## Fatos aprendidos (para não repetir)
- **Máquina do usuário:** não tem as pastas externas `reference` e `GeoAutoEngine.Legacy` nem o .NET portátil `.dotnet`. Os testes `EXTERNAL` e as suítes `legacy`/`full` do GAE não rodam lá; nunca rodaram.
- **Clone do monorepo:** fica em `C:\Users\João\Desktop\GeoEcosystem`. O clone antigo do GAE fica em `Documentos\GitHub\GeoAutoEngine`.
- **Geração do instalador:** o usuário gera o do GAD a partir do monorepo (testado em 16/11/2026).
- **Instalação do GAE para testar:** com o Civil 3D fechado, rodar `apps\geoautoengine\Instalar GeoAutoEngine 3.cmd`. Ele compila o host 2026 e instala, com backup em `Contents\2026.backup-<data>`. Funciona com o .NET do Windows desde o PR #16.
- **GAD para testar:** `dotnet run --project src\GeoAutoDocs.App -c Release` em `apps\geoautodocs`.
- **Sessões do agente:** não têm .NET nem PowerShell; código C# só é verificado pela CI.
- **SDK na máquina do usuário:** .NET 8 SDK global (ele gerou o instalador do GAD com `dotnet publish`).
- **GeoMap na máquina do usuário (16/11/2026):** Docker Desktop e Node funcionando. Sobe com `docker compose up -d --build` e as migrations em `apps\geomap`; o site roda com `npm ci` e `npm run dev` em `apps\geomap\apps\web` (não em `apps\geomap`). Importa pelo botão do site.

## Assuntos em aberto (fora do roadmap, para decidir depois)
- **Civil 3D 2027 e .NET 10** (perguntado pelo usuário em 16/11/2026; "depois vemos isso"):
  - o Civil 3D 2027 exige plugins .NET 10, recompilados contra as DLLs do 2027 (série R26.0), então o GAE precisaria de um host 2027;
  - o GAD e o GeoMap não dependem da versão do Civil 3D;
  - o .NET 8 perdeu o suporte da Microsoft em 10/11/2026: vale migrar o GAD e a biblioteca do contrato para .NET 10;
  - a Autodesk também passa o Civil 3D 2026 para .NET 10 pela atualização 2026.1.2. O plugin atual funcionou na máquina do usuário em 16/11/2026.

## Notas técnicas
- Alvo da biblioteca: **só `net8.0`** (decisão do usuário, 16/11/2026). O host 2024 do GAE não compila mais. `build-release.ps1` precisa de `-Versions 2026`.
- A CI **não compila** o `GeoAutoEngine.Plugin` nem o `Cad2026` (exigem DLLs da Autodesk). Toda mudança neles precisa de compilação na máquina do usuário.
