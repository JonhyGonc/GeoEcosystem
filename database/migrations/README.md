# Migrations do banco

As migrations são scripts SQL versionados e executados em ordem. Elas usam psql com ON_ERROR_STOP para interromper a operação na primeira falha.

## Aplicar a migration inicial

Bash:

~~~bash
docker compose exec -T database sh -ec \
  'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' \
  < database/migrations/001_initial_spatial_model.up.sql
~~~

PowerShell:

~~~powershell
Get-Content -Raw database/migrations/001_initial_spatial_model.up.sql |
  docker compose exec -T database sh -ec 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
~~~

## Reverter a migration inicial

A reversão apaga as tabelas projects, networks e network_features e todos os dados nelas. Use apenas quando a perda desses dados for intencional.

Bash:

~~~bash
docker compose exec -T database sh -ec \
  'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' \
  < database/migrations/001_initial_spatial_model.down.sql
~~~

A extensão PostGIS não é removida pelo rollback, pois pode ser compartilhada por outros objetos do banco.

## Convenção

Novas migrations devem usar número crescente, nome descritivo e pares up/down. Não altere uma migration já aplicada em ambientes compartilhados; crie uma nova.
