# Exportação de Polyline para o GeoMap

O comando `GEOAUTO3GEOMAPEXPORT` exporta uma rede do desenho ativo para o contrato GeoMap (`*.geomap.json`): versão 0.2 quando a layer tem cor, versão 0.1 quando não tem. Ele está disponível no bundle do GeoAutoEngine para Civil 3D 2024, 2025 e 2026.

## Escopo desta versão

- entidades `Polyline` 2D (`LWPOLYLINE`), abertas ou fechadas;
- segmentos retos;
- até 10.000 Polylines por arquivo e 100.000 posições por feição;
- código próprio de cada Polyline (`sourceId`), gravado no desenho (ver "Identidade das feições");
- preservação de handle (atributo `cadHandle`), layer, quantidade de vértices, fechamento e elevação CAD;
- coordenadas X/Y no CRS declarado pelo operador;
- unidades `metre`, `foot`, `us-survey-foot` ou `degree`.

`Polyline2d`, `Polyline3d`, Feature Line, Pipe Network, Pressure Network e Polylines com arcos (bulge) não são exportadas nesta versão. Quando a seleção contém geometria não suportada, o comando cancela o arquivo inteiro e informa o handle ou o tipo problemático.

## Como usar

1. Instale ou atualize o bundle `GeoAutoEngine.bundle` normalmente.
2. Abra o DWG no Civil 3D e confirme o sistema de coordenadas e a unidade do desenho.
3. Execute `GEOAUTO3GEOMAPEXPORT` na linha de comando.
4. Selecione as Polylines da mesma rede e pressione Enter.
5. Confirme projeto, rede e tipo. Quando o layer pertence ao catálogo oficial do GeoAutoEngine, a rede é sugerida automaticamente.
6. Informe o EPSG de origem, por exemplo `EPSG:31982`. Também é aceito informar somente `31982`.
7. Confirme a unidade e escolha o arquivo `*.geomap.json`.
8. Importe o arquivo pelo importador do GeoMap.
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

## Política geométrica

Polylines abertas são exportadas como `LineString` com os vértices na ordem original. Em Polylines fechadas, a primeira posição é repetida no final para representar o fechamento. A coordenada Z não faz parte do contrato v0.1; a propriedade `projectedTo2D` registra quando os pontos CAD possuíam elevação diferente de zero, e `cadElevation` preserva a elevação original como metadado.

O arquivo é um snapshot completo da rede informada. Uma nova importação com o mesmo identificador externo substitui o snapshot anterior no GeoMap.
