BEGIN;

-- Remove somente o registro; projetos, redes e feições são preservados.
-- Reverta as migrations em ordem decrescente: a 004 antes da 003.
DROP TABLE schema_migrations;

COMMIT;
