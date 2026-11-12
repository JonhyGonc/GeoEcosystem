# MVP — Protótipo 0.1

## Objetivo central

Visualizar no navegador uma rede proveniente do CAD, armazenada em PostGIS, corretamente posicionada sobre um mapa.

Para reduzir risco, o fluxo web será comprovado inicialmente com dados artificiais e depois com uma rede real exportada.

## Demonstração de sucesso

Durante a apresentação será possível:

1. abrir um projeto;
2. visualizar um mapa base;
3. ver uma ou mais redes corretamente posicionadas;
4. usar zoom e pan;
5. ativar ou desativar camadas;
6. selecionar um elemento;
7. consultar atributos básicos;
8. navegar sem baixar toda a rede para o navegador.

## Dentro do MVP

- cadastro e consulta básica de projetos;
- redes vinculadas a projetos;
- armazenamento espacial no PostGIS;
- mapa web;
- fonte de mapa base configurável;
- visualização de redes por tiles vetoriais;
- zoom e pan;
- camadas;
- seleção de elementos;
- consulta básica de atributos;
- contrato GeoMap;
- importação mínima;
- importação em lotes ou streaming para volume alto;
- gerador de datasets SMALL, MEDIUM e LARGE;
- teste com uma rede real;
- exportador inicial de Polyline do Civil 3D, somente ao final do fluxo.

## Fora do MVP

- desenho ou edição de geometria no navegador;
- substituição do Civil 3D;
- relatórios;
- vídeo inspeção;
- permissões avançadas;
- organizações e múltiplos locatários;
- cobrança e assinatura;
- aplicativo mobile;
- histórico complexo;
- workflow de aprovação;
- microserviços;
- Kubernetes;
- alta disponibilidade corporativa;
- suporte completo a todos os tipos do Civil 3D.

## Critérios de conclusão do Protótipo 0.1

- uma rede real proveniente do Civil 3D foi importada;
- a geometria aparece na posição esperada;
- a rede é entregue ao navegador de forma espacialmente segmentada;
- o mapa continua utilizável com o dataset LARGE;
- camadas, seleção e atributos básicos funcionam;
- a configuração local é reproduzível;
- build e testes automatizados passam;
- limitações e decisões pendentes estão documentadas.

## Milestone proposto

Nome: **Protótipo 0.1**

Objetivo: uma rede originalmente proveniente do Civil 3D pode ser armazenada no GeoMap e visualizada corretamente pelo navegador sobre um mapa de fundo.

O milestone deverá agrupar as Issues #1 a #18. A associação no GitHub deve ser feita quando o milestone estiver criado na interface do repositório.
