\set ON_ERROR_STOP on

DO $$
BEGIN
    IF to_regclass('public.network_imports') IS NOT NULL THEN
        RAISE EXCEPTION 'network_imports still exists';
    END IF;

    IF EXISTS (SELECT 1 FROM schema_migrations WHERE version = '005') THEN
        RAISE EXCEPTION 'migration 005 is still registered';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM schema_migrations WHERE version = '004') THEN
        RAISE EXCEPTION 'the 005 rollback removed the 004 registration';
    END IF;
END
$$;

\echo 'Network imports rollback checks passed.'
