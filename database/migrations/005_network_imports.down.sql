BEGIN;

-- Remove somente o histórico de importações; projetos, redes e feições são
-- preservados.
DROP TABLE network_imports;

DELETE FROM schema_migrations WHERE version = '005';

COMMIT;
