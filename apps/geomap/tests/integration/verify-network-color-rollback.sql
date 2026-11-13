\set ON_ERROR_STOP on

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'networks' AND column_name = 'color'
    ) THEN
        RAISE EXCEPTION 'networks.color still exists after rollback';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables WHERE table_name = 'networks'
    ) THEN
        RAISE EXCEPTION 'networks table was removed by the color rollback';
    END IF;
END
$$;

\echo 'Network color rollback checks passed.'
