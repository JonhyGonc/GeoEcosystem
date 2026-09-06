# Gerador de datasets espaciais

Esta ferramenta gera e carrega dados lineares artificiais para validar o GeoMap antes de existir uma rede CAD real grande.

## Perfis

| Perfil | Feições | Extensão declarada (EPSG:4326) |
|---|---:|---|
| SMALL | 1.000 | `[-48.95, -26.40, -48.75, -26.20]` |
| MEDIUM | 100.000 | `[-48.95, -26.40, -48.75, -26.20]` |

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

Troque `small` por `medium` para carregar 100.000 feições. A conexão também pode ser informada com `--connection-string`, mas a variável de ambiente evita que uma senha fique no histórico do terminal.

O tamanho do lote aceita valores de 1 a 50.000. O padrão é 5.000. A ferramenta gera as feições sob demanda, envia cada lote por `COPY` binário para uma tabela temporária e constrói a geometria no PostGIS. Assim, não mantém o dataset inteiro em memória e não cria arquivos grandes no repositório.

A carga é atômica: falha em qualquer lote reverte Project, Network e NetworkFeatures desse dataset. Uma combinação de perfil e semente já carregada é recusada para não decidir antecipadamente a política de reimportação das Issues #14 e #15.

## Medição inicial

Os tempos abaixo serão preenchidos com a primeira execução completa no GitHub Actions da Issue #4. São uma observação do ambiente de CI, não uma meta de produção.

| Perfil | Feições | Lote | Tempo observado |
|---|---:|---:|---:|
| SMALL | 1.000 | 250 | pendente |
| MEDIUM | 100.000 | 5.000 | pendente |
