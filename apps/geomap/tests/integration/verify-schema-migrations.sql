\set ON_ERROR_STOP on

DO $$
DECLARE
    registered text;
BEGIN
    SELECT string_agg(version || ':' || name, ',' ORDER BY version)
    INTO registered
    FROM schema_migrations;

    IF registered IS DISTINCT FROM
        '001:initial_spatial_model,002:network_color,003:point_features,004:schema_migrations' THEN
        RAISE EXCEPTION 'unexpected schema_migrations content: %', registered;
    END IF;

    IF EXISTS (SELECT 1 FROM schema_migrations WHERE applied_at IS NULL) THEN
        RAISE EXCEPTION 'schema_migrations has rows without applied_at';
    END IF;

    BEGIN
        INSERT INTO schema_migrations (version, name) VALUES ('5', 'bad_version');
        RAISE EXCEPTION 'invalid version was accepted';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;

    BEGIN
        INSERT INTO schema_migrations (version, name) VALUES ('004', 'duplicate');
        RAISE EXCEPTION 'duplicate version was accepted';
    EXCEPTION
        WHEN unique_violation THEN NULL;
    END;
END
$$;

\echo 'Schema migrations registry checks passed.'
