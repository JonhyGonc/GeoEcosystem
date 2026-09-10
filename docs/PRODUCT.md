# Produto GeoMap

## Problema

Redes de tubulação são desenhadas e mantidas no AutoCAD Civil 3D, mas sua consulta costuma depender do arquivo CAD, da estação de trabalho e de conhecimento técnico específico. Isso dificulta apresentar, navegar e consultar essas redes em um ambiente web simples.

O GeoMap deve demonstrar que esses dados podem sair do CAD, passar por uma normalização, ser armazenados espacialmente e aparecer corretamente posicionados em um mapa web.

## Público inicial

O público do Protótipo 0.1 é interno:

- dono e gestores da empresa;
- equipe técnica que trabalha com Civil 3D;
- profissionais que precisam consultar redes sem editar o desenho CAD.

Usuários externos, organizações, planos comerciais e permissões avançadas não fazem parte do protótipo.

## Proposta de valor

Permitir que uma rede já desenhada no Civil 3D seja:

1. exportada para um contrato independente da Autodesk;
2. validada e normalizada;
3. armazenada no PostgreSQL/PostGIS;
4. consultada espacialmente;
5. exibida no navegador sobre um mapa base;
6. selecionada para leitura de atributos.

## Responsabilidades

### Civil 3D

É a origem e o ambiente de autoria da geometria. Nele as tubulações são criadas e ajustadas.

### Exportador Civil 3D

O exportador inicial é hospedado no GeoAutoEngine e converte `LWPOLYLINE` com segmentos retos para o contrato GeoMap. Polyline2D, 3D Polyline, Feature Line, Pipe Network e Pressure Network permanecem possibilidades futuras fora do Protótipo 0.1.

### Importador

Valida o contrato GeoMap, normaliza tipos e atributos e grava os dados de forma controlada.

### PostgreSQL/PostGIS

Armazena projetos, redes, geometrias e atributos. Executa filtros e consultas espaciais com índices adequados.

### Backend

Expõe metadados e tiles vetoriais, sem carregar redes grandes integralmente.

### Site

Exibe mapa base e redes; oferece zoom, pan, camadas, seleção e consulta básica. Não cria nem edita tubulações.

## Conceitos do domínio

### Project

Unidade de organização de uma área ou trabalho. Um projeto possui uma ou mais redes.

### Network

Agrupamento lógico de elementos relacionados, como drenagem, esgoto, água ou outra categoria definida pelo projeto.

### NetworkFeature

Elemento espacial normalizado pertencente a uma rede. No MVP, o foco são geometrias lineares com origem identificável e propriedades variáveis.

### GeoMap Import Contract

Contrato independente do Civil 3D que descreve metadados, sistema de referência, geometria e atributos a serem importados.

## Princípios

- visualização, não edição CAD;
- desempenho desde o início;
- carregamento espacial por região e zoom;
- mapa base substituível;
- contrato independente da Autodesk;
- escopo de protótipo;
- decisões registradas e tarefas verificáveis.
