# Validação da primeira rede CAD real

Status: conversão privada concluída; pipeline reproduzível automatizada.

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
- A conferência visual interativa sobre o mapa base será repetida no ensaio final
  do protótipo, usando os arquivos privados somente na máquina autorizada.
