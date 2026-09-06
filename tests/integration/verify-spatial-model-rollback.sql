\set ON_ERROR_STOP on

DO $$
BEGIN
    IF to_regclass('public.network_features') IS NOT NULL
        OR to_regclass('public.networks') IS NOT NULL
        OR to_regclass('public.projects') IS NOT NULL THEN
        RAISE EXCEPTION 'Spatial model rollback left application tables behind';
    END IF;
END
$$;
