BEGIN;

-- Registro das migrations aplicadas neste banco (Issue #62).
-- As migrations 001 a 003 são anteriores ao registro: esta migration confere
-- que os objetos delas existem e só então as registra. A partir da 005, cada
-- migration insere a sua linha no up e a remove no down.
DO $$
BEGIN
    IF to_regclass('public.network_features') IS NULL THEN
        RAISE EXCEPTION 'migration 001_initial_spatial_model is not applied';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'networks'
          AND column_name = 'color'
    ) THEN
        RAISE EXCEPTION 'migration 002_network_color is not applied';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_network_features_supported_geometry'
    ) THEN
        RAISE EXCEPTION 'migration 003_point_features is not applied';
    END IF;
END
$$;

CREATE TABLE schema_migrations (
    version varchar(3) PRIMARY KEY,
    name varchar(100) NOT NULL,
    applied_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_schema_migrations_version CHECK (version ~ '^[0-9]{3}$'),
    CONSTRAINT ck_schema_migrations_name CHECK (name ~ '^[a-z0-9_]+$')
);

COMMENT ON TABLE schema_migrations IS
    'Migrations de database/migrations aplicadas neste banco. As linhas 001 a 003 recebem a data em que a 004 foi aplicada.';

INSERT INTO schema_migrations (version, name)
VALUES
    ('001', 'initial_spatial_model'),
    ('002', 'network_color'),
    ('003', 'point_features'),
    ('004', 'schema_migrations');

COMMIT;
