# Gerador de datasets espaciais

Esta ferramenta gera e carrega dados lineares artificiais para validar o GeoMap antes de existir uma rede CAD real grande.

## Perfis

| Perfil | Feições | Extensão declarada (EPSG:4326) |
|---|---:|---|
| SMALL | 1.000 | `[-48.95, -26.40, -48.75, -26.20]` |
| MEDIUM | 100.000 | `[-48.95, -26.40, -48.75, -26.20]` |
| LARGE | 1.000.000 | `[-48.95, -26.40, -48.75, -26.20]` |

A área artificial cobre aproximadamente Joinville apenas para oferecer coordenadas geográficas realistas. Os dados não representam redes existentes e não devem ser usados como informação de campo.

Cada feição é uma `LineString` 2D em EPSG:4326 e possui diâmetro, material, sequência, perfil e semente. IDs, geometrias e atributos são derivados somente da versão do algoritmo, perfil, semente e posição da feição. A mesma combinação produz a mesma sequência.

## Pré-requisitos

1. PostgreSQL/PostGIS iniciado.
2. Migration `001_initial_spatial_model.up.sql` aplicada.
3. .NET 8 SDK instalado.

## Uso

Bash:

~~~bash
export GEOMAP_DB_CONNECTION_STRING='Host=localhost;Port=5432;Database=geomap;Username=geomap;Password=change-me-local-only'

dotnet run --project tools/importer/GeoMap.DatasetGenerator -- \
  --size small \
  --seed 20260906 \
  --batch-size 5000
~~~

PowerShell:

~~~powershell
$env:GEOMAP_DB_CONNECTION_STRING = 'Host=localhost;Port=5432;Database=geomap;Username=geomap;Password=change-me-local-only'

dotnet run --project tools/importer/GeoMap.DatasetGenerator -- `
  --size small `
  --seed 20260906 `
  --batch-size 5000
~~~

Troque `small` por `medium` ou `large` para carregar 100.000 ou 1.000.000 de feições. A conexão também pode ser informada com `--connection-string`, mas a variável de ambiente evita que uma senha fique no histórico do terminal.

O tamanho do lote aceita valores de 1 a 50.000. O padrão é 5.000. A ferramenta gera as feições sob demanda, envia cada lote por `COPY` binário para uma tabela temporária e constrói a geometria no PostGIS. Assim, não mantém o dataset inteiro em memória e não cria arquivos grandes no repositório.

A carga é atômica: falha em qualquer lote reverte Project, Network e NetworkFeatures desse dataset. Uma combinação de perfil e semente já carregada é recusada para não decidir antecipadamente a política de reimportação das Issues #14 e #15.

## Medição inicial

Medição realizada no GitHub Actions, runner `ubuntu-latest`, no workflow Backend CI nº 7. São tempos totais informados pela ferramenta, incluindo conexão, criação de Project/Network, carga, verificação de contagem e commit. Representam uma observação do ambiente de CI, não uma meta de produção.

| Perfil | Feições | Lote | Tempo observado |
|---|---:|---:|---:|
| SMALL | 1.000 | 250 | 1,188 s |
| MEDIUM | 100.000 | 5.000 | 3,543 s |
| LARGE | 1.000.000 | 10.000 | pendente |

Extensões efetivamente verificadas nessa execução:

- SMALL: `[-48.94905033, -26.39846622, -48.75094667, -26.20188423]`;
- MEDIUM: `[-48.94990531, -26.39984959, -48.75009488, -26.20015090]`.

A execução LARGE também registra o pico de memória do processo .NET e a métrica independente de RSS máximo do GNU `time`. Após a carga, o CI executa `ANALYZE network_features` e confirma as estatísticas, o índice GiST espacial e o índice B-tree relacional.
