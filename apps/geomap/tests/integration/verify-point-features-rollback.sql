\set ON_ERROR_STOP on

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_network_features_linear_geometry'
    ) OR EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_network_features_supported_geometry'
    ) THEN
        RAISE EXCEPTION 'linear geometry constraint was not restored by the 003 rollback';
    END IF;
END
$$;

\echo 'Point features rollback checks passed.'
