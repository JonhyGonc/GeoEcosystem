\set ON_ERROR_STOP on

DO $$
BEGIN
    IF to_regclass('public.schema_migrations') IS NOT NULL THEN
        RAISE EXCEPTION 'schema_migrations still exists';
    END IF;

    IF to_regclass('public.network_features') IS NULL THEN
        RAISE EXCEPTION 'the 004 rollback removed more than the registry';
    END IF;
END
$$;

\echo 'Schema migrations rollback checks passed.'
