# Exportação de Polylines e blocos para o GeoMap

O comando `GEOAUTO3GEOMAPEXPORT` exporta as redes do desenho ativo para o contrato GeoMap (`*.geomap.json`), **uma rede por layer** (desde o M4.3e), cada uma no seu arquivo. A versão é a menor que aceita o arquivo: 0.3 para os blocos (pontos), 0.2 para linhas com cor de layer e 0.1 para linhas sem cor. Ele está disponível no bundle do GeoAutoEngine para Civil 3D 2024, 2025 e 2026.

## Escopo desta versão

- entidades `Polyline` 2D (`LWPOLYLINE`), abertas ou fechadas;
- blocos (`INSERT`) como pontos, numa rede separada (desde o M4.3c, ver "Blocos");
- `LINE` e `ARC` (desde o M4.3d);
- seleção por layer, com curinga, e uma rede por layer (desde o M4.3e, ver "Redes por layer");
- diâmetro e material lidos do nome da layer `RD_<diâmetro>[_<material>]` e atributos dos blocos (desde o M4.3e, ver "Atributos");
- arcos (bulge) nas Polylines e o `ARC` viram trechos retos, com no máximo 5 mm de diferença (ver "Arcos");
- até 10.000 Polylines por arquivo e 100.000 posições por feição;
- código próprio de cada Polyline (`sourceId`), gravado no desenho (ver "Identidade das feições");
- preservação de handle (atributo `cadHandle`), layer, quantidade de vértices, fechamento e elevação CAD;
- coordenadas X/Y no CRS declarado pelo operador;
- unidades `metre`, `foot`, `us-survey-foot` ou `degree`.

`Polyline2d`, `Polyline3d`, `SPLINE`, `CIRCLE`, Feature Line, Pipe Network e Pressure Network não são exportados (decisão do usuário no M4.3d: só LINE, ARC e arcos de Polyline). Quando a seleção contém geometria não suportada, o comando cancela o arquivo inteiro e informa o handle ou o tipo problemático.

## Como usar

1. Instale ou atualize o bundle `GeoAutoEngine.bundle` normalmente.
2. Abra o DWG no Civil 3D e confirme o sistema de coordenadas e a unidade do desenho.
3. Execute `GEOAUTO3GEOMAPEXPORT` na linha de comando.
4. Em "Selecionar por [Layers/Objetos]", pressione Enter para **Layers** e digite as layers, por exemplo `RD_*,TÊ`. O comando pega todas as linhas e blocos dessas layers no espaço do modelo. Ou escolha **Objetos** e selecione na tela.
5. O comando lista as redes que vai criar, uma por layer. Confirme o projeto e o tipo das redes. Quando a layer pertence ao catálogo oficial do GeoAutoEngine, o tipo é sugerido automaticamente.
6. Informe o EPSG de origem, por exemplo `EPSG:31982`. Também é aceito informar somente `31982`.
7. Confirme a unidade e escolha a pasta e o nome base, por exemplo `obra.geomap.json`. Os arquivos saem como `obra-<layer>.geomap.json` e, para os blocos, `obra-<layer>-blocos.geomap.json`. Arquivos com o mesmo nome são substituídos.
8. Importe os arquivos pelo botão **Importar** do site do GeoMap (aceita vários de uma vez) ou pelo importador do terminal.
9. **Salve o DWG** se o comando avisar que o desenho recebeu códigos novos.

## Identidade das feições (desde o M4.3a)
Cada Polyline exportada ganha um código próprio (GUID) na XData `GEOAUTOENGINE_FEATURE` (`SCHEMA_VERSION=1`, `FEATURE_ID=<guid>`), gravado pelo comando na primeira exportação. Esse código é o `sourceId` no GeoMap. Assim, reexportar a rede depois de mover ou editar as Polylines atualiza as mesmas feições, em vez de criar novas ([DEC-ECO-0003](../../../decisoes/DEC-ECO-0003-caminho-dwg-geomap-e-identidade.md)).

- **Reexportar** sem mudança, ou depois de mover ou editar: o código é mantido.
- **COPY ou BREAK:** a cópia leva a XData junto. Na exportação, a Polyline de handle mais antigo fica com o código e as outras recebem código novo.
- O comando informa quantos códigos foram criados, renovados e mantidos. Quando grava códigos, o desenho fica alterado e precisa ser salvo; senão, a próxima exportação cria códigos novos.
- O `sourceSystem` do arquivo é `GeoAutoEngine` (antes: `Civil 3D`).

O EPSG nunca recebe um valor silencioso: ele deve ser informado em cada exportação. A unidade é sugerida somente quando `INSUNITS` está configurado como metros, pés ou US survey feet.

## Cor da rede (desde o M4.3b)

O comando lê a cor da layer das Polylines e grava em `network.color` (`#RRGGBB`, maiúsculas), no contrato 0.2. O GeoMap pinta a rede com essa cor.

- A rede tem uma cor só: vale a cor da layer com mais Polylines na seleção. Em empate, a primeira layer em ordem alfabética.
- True color e índice ACI usam a conversão do próprio AutoCAD. O ACI 7 (branco ou preto, conforme o fundo) vira preto (`#000000`), porque o mapa base é claro. É a mesma regra da ponte DXF.
- Se a layer escolhida não tem cor (ByLayer, ByBlock), o arquivo sai na versão 0.1, sem `color`.
- Só a cor da **layer** conta. A cor definida direto na Polyline é ignorada.
- O comando informa a cor e a layer de onde ela veio.

## Blocos (desde o M4.3c)

Os blocos (tês, registros, caps, poços de visita) vão para o GeoMap como pontos (`Point`), no contrato 0.3. Pela DEC-029 do GeoMap, eles ficam numa rede separada das linhas.

- Os blocos de cada layer vão numa rede e num arquivo próprios, com `-blocos` no nome (ver "Redes por layer").
- O nome do bloco vai em `properties.bloco`. No bloco dinâmico, é o nome do bloco, e não o nome interno `*U…`. Também vão a rotação em graus (`rotacao`), a escala (`escalaX`, `escalaY`), a elevação (`cadElevation`) e o handle (`cadHandle`). Os atributos do bloco também vão (ver "Atributos").
- Cada bloco também ganha o código próprio (`FEATURE_ID`), com as mesmas regras das Polylines.
- **Ficam de fora**, com a contagem por motivo na linha de comando: blocos em layers de levantamento `_TG*`, o símbolo de ponto levantado `SIMB_PONTO`, blocos anônimos, referências externas (xref) e `MINSERT`.
- **Posição:** o ponto é o ponto de inserção. Alguns blocos têm o desenho nas coordenadas reais e o ponto base em 0,0; neles, o ponto vai para o centro do símbolo, e `posicaoPeloSimbolo` fica verdadeiro. É a mesma regra da ponte DXF.
- Um bloco fora do plano XY (normal diferente de Z) cancela a exportação.

## Redes por layer (desde o M4.3e)

Cada layer vira uma rede, com a cor da própria layer, como fazia a ponte DXF (decisão do usuário, 16/11/2026). No mapa, cada uma pode ser ligada e desligada separadamente.

- Rede das linhas: identificador `<projeto>:<layer>` e nome `<layer>`, por exemplo `GF-CV-02:RD_150_PVC`.
- Se a layer também tem blocos, eles vão numa rede própria (DEC-029 do GeoMap): `<projeto>:<layer>:blocos` e `<layer> (blocos)`.
- Todas as redes saem com o mesmo projeto, tipo, CRS, unidade e data.
- Cada arquivo aceita até 10.000 feições. Uma layer com mais feições cancela a exportação com aviso.
- Exportações anteriores ao M4.3e usavam uma rede só (por exemplo, "Rede" e "Bloco"). Essas redes antigas continuam no GeoMap até o banco ser recriado.

## Atributos (desde o M4.3e)

- **Diâmetro e material:** numa layer `RD_<diâmetro>[_<material>]`, a feição ganha `diametroMm` e `material` (`RD_150_PVC` → 150 e `PVC`; `RD_200` → só 200). É a mesma convenção da validação com dados reais do GeoMap. Layers fora desse padrão ficam sem esses campos, sem erro.
- **Atributos dos blocos (ATTRIB):** cada atributo vira uma propriedade com o nome da tag, como na ponte DXF. Uma tag com o nome de um campo que o exportador já preenche (por exemplo, `material` ou `bloco`) ganha o prefixo `atributo_`. O atributo multilinha vai como texto simples.
- O contrato aceita até 4.096 caracteres por valor e 128 campos por feição. Um bloco fora disso cancela a exportação com aviso.

## Arcos (desde o M4.3d)

O contrato GeoMap só tem linhas retas. Por isso cada arco vira uma sequência de trechos retos (tesselação).

- **Precisão:** a maior distância entre o arco verdadeiro e os trechos retos é de **5 mm** (decisão do usuário). Em desenhos em pés, o limite é convertido para a mesma medida. Um arco de raio 10 m e 90° vira cerca de 25 trechos.
- Os vértices originais continuam no arquivo, exatamente como no desenho; os pontos novos ficam só entre eles. `vertexCount` continua sendo a quantidade de vértices do CAD.
- `arcosTesselados` informa quantos segmentos curvos a feição tinha. Ele só aparece quando há arco.
- **ARC** vira uma linha (`sourceType` = `Arc`) do ponto inicial ao final, já com os trechos retos.
- **LINE** vira uma linha de dois pontos (`sourceType` = `Line`), com a elevação de cada ponta em `cadStartElevation` e `cadEndElevation`, como na ponte DXF.
- Uma Polyline ou ARC espelhado (normal -Z) é aceito, e o sentido do arco é corrigido. Um arco fora do plano XY cancela a exportação.
- LINE e ARC também ganham o código próprio (`FEATURE_ID`).

## Profundidades dos pontos _TG (desde o M7.1, provisório)
Decisão proposta: [DEC-ECO-0005](../../../decisoes/DEC-ECO-0005-profundidades-pontos-tg.md).

- `GEOAUTO3DEPTHSCAN` analisa as tubulações escolhidas (por layer ou por objeto) contra todos os blocos das layers `_TG*` e mostra um resumo. **Não grava nada no desenho.** No fim, oferece salvar o diagnóstico em CSV.
- `GEOAUTO3GEOMAPEXPORT` pergunta "Levar as profundidades dos N pontos _TG?". Com Sim, cada linha recebe `quantidadeMedicoesProfundidade`, `profundidadeInicialM`, `profundidadeFinalM`, `medicoesProfundidade` e `profundidadeOrigem`. O diagnóstico é salvo ao lado dos arquivos (`<nome>-profundidades-diagnostico.csv`).
- A posição de cada ponto é o ponto (DBPoint) de dentro do bloco, onde o OSNAP NODE prende. O ponto vale para a linha num vértice ou sobre um trecho, até 1 mm; de 1 mm a 10 cm, ele é só relatado.
- Só a informação da rede com profundidade (ex.: `AP1.20D300`) conta; caixas e PVs (`CXAP1.50`), a COTA e códigos desconhecidos não viram profundidade da tubulação.
- Valores: como estão no código, em metros, até a geratriz superior (`profundidadeReferencia`), sem média nem interpolação.
- Estados no diagnóstico: `ASSOCIADO`, `ASSOCIADO_COMPARTILHADO`, `PROXIMO_NAO_ASSOCIADO`, `SEM_TUBULACAO`, `AMBIGUO`, `CONFLITO_REDE`, `CONFLITO_DIAMETRO`, `CONFLITO_PONTOS`, `DUPLICADO`, `SEM_PROFUNDIDADE`, `PROFUNDIDADE_DE_OBJETO`, `CODIGO_DESCONHECIDO`, `PROFUNDIDADE_INVALIDA` e `SEM_CODIGO`.
- **Provisório:** até a entrega 2 (M7.2), as medições não ficam gravadas no DWG. Ao reexportar, responda Sim às profundidades para não apagá-las do mapa.

## Política geométrica

Polylines abertas são exportadas como `LineString` com os vértices na ordem original. Em Polylines fechadas, a primeira posição é repetida no final para representar o fechamento. A coordenada Z não faz parte do contrato v0.1; a propriedade `projectedTo2D` registra quando os pontos CAD possuíam elevação diferente de zero, e `cadElevation` preserva a elevação original como metadado.

O arquivo é um snapshot completo da rede informada. Uma nova importação com o mesmo identificador externo substitui o snapshot anterior no GeoMap.
