# DEC-ECO-0005 — Profundidades das tubulações pelos pontos do levantamento (_TG)

- **Estado:** aprovada pelo usuário em 17/11/2026 nos itens 2, 4, 8 (respostas abaixo); o limite de 10 m (item 5) segue como proposta. Passa a valer com o merge do PR #34.
- **Origem:** pedido emergencial do usuário em 17/11/2026: um projeto grande precisa das profundidades das tubulações no GeoMap, sem cadastro linha por linha.
- **Contratos:** CT-01 (GeoMap Import). Só o conteúdo de `properties` muda, o que o contrato aceita sem versão nova (`IMPORT_CONTRACT.md`, "Somente o conteúdo interno de properties é extensível sem nova versão").

## Contexto
- As tubulações são desenhadas com o OSNAP NODE nos pontos do levantamento: blocos `SIMB_PONTO` na layer `_TG`, com os atributos PONTO, DESC e COTA.
- O bloco `SIMB_PONTO` tem um ponto (DBPoint) na origem, onde o NODE prende (conferido na auditoria CAD → GIS, `apps/geoautoengine/docs/audit`).
- O GAE já interpreta os códigos do levantamento nos CSVs (`SurveyCodeNormalizer`, `SurveyCodeInterpreter`): `AP1.20D300` é rede pluvial, 1,20 m, D300.
- **Medição nos dois desenhos reais da auditoria** (redes `GF-Rede*`, sem as cópias `-VIDEO`):

| Desenho | Pontos com profundidade | Associados (vértice ou trecho, até 1 mm) | De 1 mm a 10 cm da linha | Mais de 10 cm |
|---|---|---|---|---|
| GF-02-EP01-18 | 96 | 54 | 32 | 10 |
| GF-02-EP01-22 | 279 | 141 (+1 ambíguo) | 119 | 18 |

  Os pontos entre 1 mm e 10 cm não formam um grupo separado (vão de 5 mm a 10 cm, espalhados). Nesses desenhos, parte das linhas não foi desenhada com o NODE.

## Decisão proposta
1. **Leitura:** o código (DESC) é lido pelo interpretador que já existe. Só a "informação da rede" com profundidade vale como profundidade da tubulação. Profundidade de caixa ou PV (`CXAP1.50`), código desconhecido e valor fora do intervalo vão para o diagnóstico. A COTA nunca é lida como profundidade.
2. **Valor:** a profundidade vai como está no código, em metros, sem conversão, média ou interpolação. **Referência: geratriz superior** (do terreno até o topo do tubo), confirmada pelo usuário em 17/11/2026; registrada aqui e na documentação do exportador.
3. **Posição do ponto:** o ponto (DBPoint) de dentro do bloco, levado para o desenho pela transformação do bloco. A inserção do bloco só é usada quando o bloco não tem ponto, e isso aparece no resumo.
4. **Associação:**
   - o ponto vale para a linha quando está num vértice ou sobre um trecho (reto ou em arco) **até 1 mm** (aprovado pelo usuário: só o desenhado com NODE);
   - de 1 mm a 10 cm, o ponto é relatado como "perto, não associado";
   - a rede do código precisa bater com a rede da layer, quando as duas são conhecidas;
   - o diâmetro do código escolhe a linha certa quando há mais de uma;
   - um ponto na ponta de duas linhas que se continuam vale para as duas (aprovado pelo usuário); três ou mais linhas, ou ponta de uma e meio de outra, é ambíguo e não é associado;
   - pontos repetidos no mesmo lugar com o mesmo valor valem uma vez; valores diferentes no mesmo lugar são conflito e nenhum é associado.
5. **Profundidade válida:** maior que 0 e até 10 m **(a aprovar)**.
6. **Exportação:** `GEOAUTO3GEOMAPEXPORT` pergunta se leva as profundidades. No mapa vai um **resumo** (pedidos do usuário em 18/11/2026, depois de ver a lista detalhada no site). Cada linha com medição recebe nas `properties`:
   - `profundidadeMediaM`: média simples das profundidades dos pontos da linha, arredondada ao centímetro;
   - `profundidadeInicialM` e `profundidadeFinalM`, quando há medição no primeiro ou no último vértice;
   - `quantidadePontosProfundidade`: quantos pontos entraram na média.

   O pedido original evitava a média "como se fosse a profundidade real". Por isso ela vai com esse nome, junto com as pontas e a quantidade de pontos. Os valores de cada ponto (ponto, código, vértice e distância) ficam no CSV de diagnóstico e, na entrega 2, no DWG.

   Uma linha com medições demais para os 64 KiB do GeoMap faz a exportação parar com aviso, em vez de perder medições.
7. **Diagnóstico:** `GEOAUTO3DEPTHSCAN` analisa sem gravar nada no desenho e salva um CSV (`;` e vírgula decimal) com uma linha por ponto e por tubulação. A exportação salva o mesmo CSV ao lado dos arquivos.
8. **Persistência no DWG (entrega 2):** aprovado pelo usuário: gravar as medições num Extension Dictionary/XRecord próprio (`GEOAUTOENGINE_DEPTHS`) em cada linha. Assim o `FEATURE_ID` (XData `GEOAUTOENGINE_FEATURE`) e as associações da vídeo-inspeção (`GEOAUTOENGINE_VI`) não são tocados. Até a entrega 2, as profundidades no GeoMap são provisórias: reexportar sem elas as apaga do mapa.

## Impacto
- **GAE:** novas classes na camada Application (testadas na CI), leitor dos pontos no Cad2026, comando `GEOAUTO3DEPTHSCAN` e pergunta nova na exportação.
- **GeoMap:** nenhuma mudança de código. O site já mostra as `properties`; a lista de medições aparece como texto JSON.
- **GAD:** nenhum.
