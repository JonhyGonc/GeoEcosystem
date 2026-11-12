# Validação da primeira rede CAD real

Status: concluída. A primeira rede foi convertida em setembro de 2026 e uma segunda rede, com várias redes, foi importada e conferida visualmente no mapa em 10 de novembro de 2026 (Issue #17).

## Autorização e privacidade

O fornecedor do arquivo confirmou em 9 de setembro de 2026 a autorização para
uso privado no desenvolvimento e validação do GeoMap. O desenho é proveniente do
Civil 3D e representa uma rede municipal real. Identificação do projeto,
município e endereço permanecem no registro privado.

Nenhum DWG, DXF, contrato gerado ou coordenada precisa foi versionado. Os padrões
`*.dwg`, `*.dxf`, `private-data/` e `validation-output/` estão no `.gitignore`.
Este documento registra somente evidências agregadas. O hash SHA-256 integral é
mantido com o arquivo privado para permitir a conferência da entrada.

## Origem e seleção

| Item | Valor registrado |
|---|---|
| Origem | desenho Autodesk Civil 3D exportado como DXF ASCII 2018 |
| CRS | SIRGAS 2000 / UTM zona 22S (`EPSG:31982`) |
| Unidade | metro |
| Camada | camada pluvial autorizada; nome exato mantido privado |
| Tipo CAD selecionado | `LWPOLYLINE` no Model Space |
| Região conferida | município e quadrante esperados; valores mantidos privados |

O CRS e a unidade foram encontrados nos metadados e nas notas do próprio
desenho. A transformação independente da extensão para WGS 84 permaneceu dentro
da região esperada; isso detecta erros comuns de fuso, hemisfério e unidade sem
publicar pontos de controle precisos. Uma sobreposição privada também confirmou
que a rede acompanha as linhas de guia e alinhamento do CAD; a imagem de
conferência não foi versionada.

## Resultado privado

| Verificação | Resultado |
|---|---:|
| Auditoria estrutural do DXF | 0 erros e 0 correções |
| Polilinhas convertidas | 15 |
| Handles únicos | 15 |
| Vértices 2D | 73 |
| Comprimento projetado agregado | 1.143,60 m |
| Intervalo de elevação preservado | 6,3913 m a 7,7534 m |
| Textos ignorados | 58 |
| Cópias de Paper Space descartadas | 16 |
| Documento GeoMap privado | 12.499 bytes |

Todas as 15 feições receberam `sourceId` estável pelo handle CAD, `sourceLayer`,
`sourceType` e propriedades básicas consultáveis: tipo da entidade, quantidade
de vértices, elevação, fechamento e indicação de projeção 2D. A saída possui
somente posições `[x, y]` finitas e usa o contrato GeoMap v0.1 existente.

## Verificação reproduzível sem o dado real

A CI usa `tests/fixtures/dxf/synthetic-network.dxf` para executar a mesma ponte,
importar seu contrato pelo importador .NET e verificar no PostGIS:

- contagem antes/depois e IDs de origem;
- transformação de `EPSG:31982` para `EPSG:4326`;
- validade e SRID das linhas;
- preservação de elevação e demais atributos básicos;
- continuidade dos testes MVT, seleção, atributos e navegação espacial;
- continuidade do cenário LARGE com 1.000.000 de feições.

## Diferenças e limitações conhecidas

- O contrato v0.1 é 2D. A elevação das polilinhas foi removida da geometria por
  decisão explícita e preservada em `properties.cadElevation`.
- Textos e símbolos do CAD não possuem associação semântica confiável com cada
  trecho; diâmetro e material ainda não são extraídos. O painel já consegue
  consultar os atributos de proveniência disponíveis.
- A ponte provisória rejeita bulges, extrusões não padrão, `POLYLINE`, `ARC` e
  `SPLINE`; o fluxo definitivo de `LWPOLYLINE` pertence ao exportador Civil 3D implementado pela Issue #18.
- Os 15 objetos representam a geometria desenhada na camada, inclusive trechos
  paralelos; a ponte não tenta inferir ou reconstruir eixos.
- A conferência visual interativa sobre o mapa base foi feita em 10 de novembro de
  2026 com a segunda rede, descrita abaixo.

## Segunda rede: ensaio final do Protótipo 0.1

### Autorização e privacidade

Em 10 de novembro de 2026, o responsável pelo projeto confirmou a autorização
para uso interno do desenho no desenvolvimento do GeoMap. Como na primeira rede,
o DXF, os documentos GeoMap gerados, as coordenadas, os nomes de logradouros e a
identificação do empreendimento ficam somente na máquina autorizada, em
`private-data/` e `validation-output/`, ambos ignorados pelo Git. Este registro
traz apenas números agregados.

### Origem

| Item | Valor registrado |
|---|---|
| Origem | desenho Autodesk Civil 3D exportado como DXF ASCII (`AC1032`, formato 2018) |
| CRS | SIRGAS 2000 / UTM zona 22S (`EPSG:31982`) |
| Unidade | metro (`$INSUNITS = 6`) |
| Tipo CAD convertido | `LWPOLYLINE` no Model Space |
| Camadas convertidas | 7 camadas de rede, cada uma importada como uma rede do mesmo projeto |

O desenho não declara o datum nem o fuso. A magnitude das coordenadas indica UTM
no hemisfério sul. A inversão aproximada da posição mediana colocou a rede em terra firme, no
município esperado, somente com o fuso 22S; os fusos 23S e 24S levariam a rede
para o oceano. A conferência visual confirmou o fuso escolhido.

### Resultado

| Rede | Feições | Vértices 2D | Comprimento projetado (m) | Polilinhas fechadas |
|---|---:|---:|---:|---:|
| Pluvial | 32 | 148 | 1.450,33 | 6 |
| Pluvial (vídeo-inspeção) | 6 | 30 | 37,52 | 6 |
| Esgoto | 11 | 23 | 291,99 | 0 |
| Água | 2 | 4 | 9,06 | 0 |
| Fibra óptica | 12 | 60 | 23,57 | 12 |
| Elétrica | 11 | 55 | 23,33 | 11 |
| Sem descrição de uso | 7 | 14 | 21,72 | 0 |
| **Total** | **81** | **334** | **1.857,51** | **35** |

| Verificação | Resultado |
|---|---:|
| Camadas rejeitadas pela ponte DXF | 0 |
| Entidades com bulge, `POLYLINE`, `ARC` ou `SPLINE` | 0 |
| Entidades de Paper Space convertidas | 0 |
| Textos e blocos ignorados nas camadas convertidas | 109 |
| Feições com elevação removida da geometria (`--project-to-2d`) | 80 |
| Documentos GeoMap privados | 7 documentos, 63.233 bytes |

### Conferência no mapa

O ambiente local foi Docker Compose com PostGIS e API em .NET 10, frontend com
`npm run dev` e importação pelo `GeoMap.Importer`. Na conferência:

- o projeto apareceu no seletor (DEC-026), com as 7 redes listadas e visíveis;
- as redes acompanham o eixo do logradouro desenhado no mapa base, e as
  derivações aparecem nas transversais esperadas;
- a seleção de feições e a consulta de atributos funcionaram;
- nenhuma camada ficou em erro ou sem feições na área conferida.

O responsável aprovou o resultado em 10 de novembro de 2026.

### Limitações observadas

- **Símbolos como linhas.** Todas as feições das camadas elétrica e de fibra
  óptica, além de 6 feições pluviais e das 6 de vídeo-inspeção, são polilinhas
  fechadas de poucos metros. Elas representam símbolos do CAD (caixas, postes,
  marcadores), não trechos de rede. O contrato v0.1 só aceita linhas, então esses
  símbolos aparecem no mapa como pequenos contornos. Tipos pontuais ou poligonais
  exigiriam evoluir o contrato.
- **Rótulos não associados.** Diâmetro, material e demais rótulos continuam como
  textos soltos no CAD e não são associados aos trechos.
- **Cores parecidas.** Com 7 redes no mesmo projeto, duas camadas receberam tons
  semelhantes. A paleta automática pode precisar de mais contraste ou de cor por
  tipo de rede.
- **Vídeo-inspeção.** A camada de vídeo-inspeção foi importada apenas como
  geometria. Inspeções, anomalias e fotos continuam fora do MVP (ver `MVP.md`).

## Terceira rede: projeto real grande de abastecimento de água

### Autorização e privacidade

Em 10 de novembro de 2026, o responsável confirmou a autorização para uso interno
do desenho no desenvolvimento do GeoMap. Valem as mesmas regras das redes
anteriores: arquivos, coordenadas, logradouros e município ficam somente na
máquina autorizada.

### Origem

| Item | Valor registrado |
|---|---|
| Origem | desenho Autodesk Civil 3D exportado como DXF ASCII (`AC1032`), com 113 MB e 196 camadas |
| CRS | SIRGAS 2000 / UTM zona 24S (`EPSG:31984`) |
| Unidade | metro |
| Tipo CAD convertido | `LWPOLYLINE` e `LINE` no Model Space |

O cabeçalho não declara unidade (`$INSUNITS = 0`). Uma nota do próprio desenho
informa medidas em metros e coordenadas UTM no datum SIRGAS 2000, sem o fuso.
Desta vez, os três fusos candidatos caíam em terra firme, então o fuso foi
confirmado pelo responsável a partir da região do projeto. A conferência visual
confirmou a posição.

### Seleção e preparação

- As 17 camadas `RD_<diâmetro>[_<material>]` foram convertidas pela ponte DXF e
  unidas, por um script privado de validação, em uma única rede. O diâmetro e o
  material foram extraídos do nome da camada e gravados em `properties.diametroMm`
  e `properties.material`.
- A camada `GF-Rede Água` foi importada como uma segunda rede do mesmo projeto.
- **Exclusões:** a ponte DXF rejeita a camada inteira quando encontra curvas. Para
  a validação, 6 polilinhas com bulge e 1 `ARC` (0,26% dos trechos de rede) foram
  movidas para uma camada de exclusão em uma cópia privada do DXF; a comparação
  com o original mostrou exatamente 9 linhas alteradas. Também foram excluídos 2
  trechos a mais de 20 km da área principal, provavelmente da legenda. O
  tratamento definitivo das curvas é a Issue #48.

### Resultado

| Rede | Feições | Vértices 2D | Comprimento projetado |
|---|---:|---:|---:|
| Rede de água: tubulações (RD) | 2.261 | 12.707 | 175,77 km |
| GF-Rede Água | 412 | 1.857 | 2,11 km |
| **Total** | **2.673** | **14.564** | **177,88 km** |

| Diâmetro (mm) | Extensão (km) | Diâmetro (mm) | Extensão (km) |
|---:|---:|---:|---:|
| 20 | 0,32 | 110 | 7,88 |
| 32 | 1,54 | 150 | 9,12 |
| 50 | 105,08 | 200 | 11,96 |
| 60 | 12,82 | 250 | 0,60 |
| 75 | 2,48 | 300 | 2,76 |
| 80 | 1,17 | 400 | 0,92 |
| 100 | 18,08 | 600 | 1,04 |

A área principal tem cerca de 6,4 km × 7,1 km. Os documentos GeoMap privados
somam 1.327.930 bytes, dentro dos limites do contrato (10.000 feições e 50 MB por
documento).

### Conferência no mapa

- a rede caiu na posição esperada sobre o mapa base;
- a navegação pela área completa ficou fluida;
- o painel de atributos mostra todas as propriedades do trecho, inclusive
  diâmetro e material.

O responsável aprovou o resultado em 10 de novembro de 2026.

### Limitações observadas

- **Curvas** bloqueiam camadas inteiras na ponte DXF (Issue #48).
- **85 mil blocos (INSERT) com 253 mil atributos** não são importados, porque o
  contrato v0.1 só aceita linhas (Issue #49).
- **O mapa não enquadra o projeto.** O centro inicial fica fixo e, para a
  conferência, foi alterado temporariamente sem commit (Issue #47).
- O mapeamento de nome de camada para diâmetro e material é uma convenção deste
  desenho, aplicada fora do repositório. Generalizá-lo exige uma decisão no
  exportador ou na importação de Shapefile (Issue #50).
