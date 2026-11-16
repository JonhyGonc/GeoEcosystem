# DEC-ECO-0003 — Caminho oficial DWG → GeoMap e identidade das feições (D-03, D-04)

- **Estado:** aprovada pelo usuário. D-03 com o roadmap (11/11/2026). D-04 respondida em 16/11/2026 ("Código próprio"). Passa a valer com o merge do PR que a introduz.
- **Origem:** roadmap M4.1; auditoria Etapa 2 (F-09, H-04, GM-05, R-05, R-07)
- **Contratos:** CT-01 (GeoMap Import), CT-05 (dados do GAE no DWG)

## Contexto
- Hoje há dois produtores do contrato GeoMap:
  - a **ponte DXF** em Python, usada com todos os dados reais e que já gera 0.1, 0.2 e 0.3;
  - o **exportador do GAE** (`GEOAUTO3GEOMAPEXPORT`), que só gera 0.1 e só foi testado com dados fictícios.
- A identidade de cada feição no GeoMap é o **handle** do AutoCAD (`sourceId`). O handle muda com cópia, WBLOCK, recriação e BREAK. Reimportar a rede depois de editar o desenho cria feições novas e deixa as antigas órfãs (H-04).
- Os dois produtores usam `sourceSystem` diferentes (`"Civil 3D DXF"` × `"Civil 3D"`), e por isso a mesma obra vira dois projetos no GeoMap (GM-05).
- O M5 vai prender cada anomalia num tubo do mapa. Se a identidade do tubo mudar, a anomalia se solta.
- O GeoMap ainda não é usado (Q-B): não há dados de produção para migrar.

## D-03 — Quem leva a rede ao GeoMap
| Opção | Avaliação |
|---|---|
| (a) A ponte DXF vira oficial | Não tem acesso aos vínculos de inspeção (XData/NOD), necessários no M5 |
| **(b) O exportador do GAE vira oficial; a ponte é aposentada** | **Escolhida** (roadmap aprovado em 11/11/2026). Só o GAE conhece trecho ↔ Polyline e a direção do trecho |
| (c) Os dois convivem | Duas implementações das mesmas regras |

A ponte continua em uso até o exportador do GAE cobrir o que ela faz (M4.3): cor (0.2), blocos/pontos (0.3), LINE, curvas e seleção por layer.

> **Atualização (16/11/2026):** o M4.3 foi concluído (PRs #22 a #25 e #27) e a ponte foi aposentada (DEC-034 do GeoMap). O usuário dispensou a comparação final no mesmo desenho, porque testou cada passo no Civil 3D e no mapa. O script privado da GM-09 foi descartado (M4.4).

## D-04 — Identidade de cada feição
| Opção | Avaliação |
|---|---|
| Handle do AutoCAD (atual) | Sem esforço; muda com cópia, WBLOCK, recriação e BREAK → duplicatas e órfãs |
| **Código próprio gravado pelo GAE no objeto (XData)** | **Escolhida pelo usuário (16/11/2026).** Sobrevive a mover e editar. O que precisa de cuidado (cópia e BREAK) tem regra abaixo |
| Nome do trecho | Legível, mas renomear quebra o vínculo, e nem toda linha tem nome único |

## Decisão
1. **Produtor oficial:** o exportador do GAE. A ponte DXF é aposentada quando o M4.3 cobrir as capacidades dela. Até lá, só é usada para comparação.
2. **Código da feição:** GUID v4 gravado pelo GAE em XData, na RegApp nova `GEOAUTOENGINE_FEATURE`, com os campos `SCHEMA_VERSION` = 1 e `FEATURE_ID` = GUID.
   - É gravado na **primeira exportação** de cada entidade. **Isso muda o comportamento do exportador:** hoje ele só lê o desenho; passa a gravar uma XData por entidade exportada, na mesma transação da leitura.
   - Uma entidade que já tem `FEATURE_ID` mantém o código.
3. **Cópias e BREAK:** se duas ou mais entidades exportadas juntas têm o mesmo `FEATURE_ID`, a de **handle mais antigo** (menor número) fica com o código e as outras recebem um GUID novo. O resumo da exportação informa quantas foram renovadas.
   - Isso cobre o COPY e o BREAK nativos do AutoCAD, que copiam a XData, e o BREAK do próprio GAE.
4. **No contrato GeoMap (CT-01):**
   - `sourceId` = `FEATURE_ID`;
   - o handle continua indo como atributo (`properties.cadHandle`), para rastreio;
   - o contrato não muda de versão, porque `sourceId` já é um texto opaco de até 255 caracteres. Muda só o que o produtor GAE coloca nele, e isso fica documentado.
5. **`sourceSystem` (GM-05):**
   - o produtor oficial usa `"GeoAutoEngine"` (M4.3a);
   - no GeoMap, a identidade do projeto deixa de depender do `sourceSystem`, que vira só o registro da origem (M4.2, DEC-032 do GeoMap). Assim, a mesma obra não vira dois projetos mesmo enquanto a ponte DXF ainda for usada;
   - os bancos de desenvolvimento do GeoMap podem ser recriados, porque não há produção (Q-B).
6. **Vínculo com a vídeo-inspeção (M5):** hoje a associação trecho ↔ Polyline (`CadSectionAssociation`) guarda o handle. Passar a guardar também o `FEATURE_ID` é avaliado no início do M5, com alternativas (regra 12). Não faz parte do M4.

## Impacto por aplicação
| Aplicação | O que muda | Passo |
|---|---|---|
| GAE | grava e lê `GEOAUTOENGINE_FEATURE`; trata duplicatas; `sourceId` = GUID; `sourceSystem` = `"GeoAutoEngine"` | M4.3 (primeiro passo) |
| GeoMap | política única de `sourceSystem`; documentação de `sourceId` | M4.2 |
| GAD | nada | — |

## Compatibilidade, testes e rollback
- **Desenhos antigos:** sem `FEATURE_ID`, ganham o código na primeira exportação nova. Nenhum dado existente é apagado.
- **GeoMap:** a primeira importação com GUIDs cria feições novas em relação às importadas por handle. Isso é aceitável porque os bancos são de desenvolvimento (Q-B).
- **Testes:**
  - regra de duplicatas e geração do `sourceId` na CI do GAE (camada Application, sem AutoCAD);
  - roteiro no Civil 3D 2026 cobrindo as cinco situações de identidade: exportar, reexportar sem mudança, mover, copiar e BREAK. O código só se renova na cópia e no BREAK.
- **Rollback:** a XData nova é ignorada por versões antigas do GAE. Voltar ao exportador por handle é reinstalar a versão anterior do plugin.
