# Requisitos de performance

Performance é requisito arquitetural do GeoMap. O sistema deve continuar utilizável com redes extensas e muitos elementos sem enviar o conjunto inteiro ao navegador.

## Requisitos

### PERF-001 — Sem carga integral

Nenhum endpoint de mapa deve carregar toda uma rede grande por padrão.

### PERF-002 — Índices espaciais

Consultas geográficas devem utilizar índices espaciais quando adequado. Consultas críticas devem ser verificadas com EXPLAIN (ANALYZE, BUFFERS) em ambiente de teste.

### PERF-003 — Região necessária

A visualização deve consultar somente a região e o nível de zoom necessários, por bounding box ou envelope de tile.

### PERF-004 — Datasets

O projeto deve possuir datasets reproduzíveis:

| Dataset | Ordem de grandeza | Uso |
|---|---:|---|
| SMALL | cerca de 1.000 feições | desenvolvimento rápido |
| MEDIUM | cerca de 100.000 feições | integração e regressão |
| LARGE | cerca de 1.000.000 de feições | validação arquitetural |

Os números podem ser ajustados com justificativa registrada.

### PERF-005 — Importação controlada

Importações grandes não devem depender desnecessariamente de carregar todos os dados em memória. Usar lotes, streaming e transações com tamanho controlado.

### PERF-006 — Regressão com LARGE

Alterações relacionadas a mapa, consultas, tiles ou importação devem ser testadas também com o dataset LARGE quando aplicável.

### PERF-007 — Conteúdo do tile

Tiles devem carregar somente atributos necessários para renderização, filtragem e identificação. Atributos detalhados podem ser buscados sob demanda.

### PERF-008 — Níveis de detalhe

Simplificação, recorte e visibilidade por zoom podem ser aplicados quando preservarem a leitura correta da rede.

### PERF-009 — Limites explícitos

Endpoints e importadores devem possuir limites documentados de tamanho, timeout e quantidade quando implementados.

## Práticas de consulta

- filtrar por projeto e rede antes ou junto do filtro espacial;
- usar operador compatível com GiST para reduzir candidatos;
- recortar a geometria ao envelope do tile;
- transformar CRS de forma planejada;
- evitar transformação integral da tabela em tempo de consulta quando isso impedir uso de índice;
- evitar incluir JSONB completo em cada tile;
- medir antes de adicionar cache ou infraestrutura.

## Critérios iniciais de medição

A Issue #4 deve definir geração reproduzível e sementes fixas. As Issues #6 e #7 devem registrar, para SMALL, MEDIUM e LARGE:

- ambiente do teste;
- volume e extensão;
- plano de consulta;
- latência mediana e percentil alto;
- tamanho do tile;
- memória observada;
- limitações encontradas.

Metas numéricas serão definidas após o primeiro baseline, para evitar números arbitrários. A ausência de meta inicial não autoriza regressões sem medição.

## Antipadrões proibidos

- endpoint all-network-features para visualização;
- GeoJSON completo de uma rede grande no carregamento inicial;
- filtro espacial feito apenas em memória na API;
- índice espacial criado sem verificar o plano;
- propriedade pesada repetida em cada tile;
- cache distribuído introduzido antes de medir a necessidade.
