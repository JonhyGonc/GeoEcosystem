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

## Migration 002: cor da rede

Adiciona `networks.color`, opcional, para a cor da layer do CAD (contrato v0.2). Aplique-a depois da 001, pelo mesmo comando, trocando o arquivo para `002_network_color.up.sql`. No PowerShell:

~~~powershell
Get-Content -Raw database/migrations/002_network_color.up.sql |
  docker compose exec -T database sh -ec 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
~~~

O rollback (`002_network_color.down.sql`) remove apenas a coluna de cor e preserva redes e feições. A API passa a exigir a coluna, então a 002 precisa estar aplicada antes de subir a versão que lê a cor.

## Migration 003: pontos de blocos

Troca a restrição `ck_network_features_linear_geometry` por `ck_network_features_supported_geometry`, que aceita também `POINT` e `MULTIPOINT` (contrato v0.3, blocos do CAD). Não altera dados. Aplique-a depois da 002:

~~~powershell
Get-Content -Raw database/migrations/003_point_features.up.sql |
  docker compose exec -T database sh -ec 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
~~~

O rollback (`003_point_features.down.sql`) recusa-se a rodar enquanto existirem feições pontuais, para não apagar dados sem aviso. Remova antes as redes de blocos, se o rollback for realmente necessário.

## Migration 004: registro das migrations aplicadas

Cria a tabela `schema_migrations`, com uma linha por migration aplicada (`version`, `name`, `applied_at`). Antes de criá-la, confere se os objetos das migrations 001, 002 e 003 existem; se faltar algum, falha sem alterar o banco e informa qual migration aplicar. Em seguida registra as quatro. As linhas 001 a 003 recebem a data em que a 004 foi aplicada, porque o banco não guardava a data original. Aplique-a depois da 003:

~~~powershell
Get-Content -Raw database/migrations/004_schema_migrations.up.sql |
  docker compose exec -T database sh -ec 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
~~~

O rollback (`004_schema_migrations.down.sql`) remove apenas a tabela de registro. Para reverter migrations anteriores, reverta primeiro a 004: as reversões seguem a ordem decrescente.

## Verificar o estado de um banco

~~~bash
docker compose exec -T database sh -ec \
  'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT version, name, applied_at FROM schema_migrations ORDER BY version;"'
~~~

Se a tabela não existir, o banco está antes da 004: aplique as migrations que faltarem, em ordem, até a 004. Compare a maior `version` registrada com o maior número em `database/migrations` para saber o que falta aplicar.

## Banco novo

Em um banco vazio, aplique todas as migrations em ordem. Bash:

~~~bash
for migration in database/migrations/[0-9][0-9][0-9]_*.up.sql; do
  docker compose exec -T database sh -ec \
    'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < "$migration"
done
~~~

PowerShell:

~~~powershell
Get-ChildItem database/migrations/*.up.sql | Sort-Object Name | ForEach-Object {
  Get-Content -Raw $_.FullName |
    docker compose exec -T database sh -ec 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
}
~~~

Não use esse laço em um banco que já tem migrations aplicadas: a 001 falharia ao recriar as tabelas.

## Convenção

Novas migrations devem usar número crescente com três dígitos, nome descritivo em minúsculas e pares up/down. Não altere uma migration já aplicada em ambientes compartilhados; crie uma nova.

A partir da 005, cada migration registra a si mesma no fim do up e se remove no fim do down, na mesma transação:

~~~sql
-- no up
INSERT INTO schema_migrations (version, name) VALUES ('005', 'nome_da_migration');
-- no down
DELETE FROM schema_migrations WHERE version = '005';
~~~
