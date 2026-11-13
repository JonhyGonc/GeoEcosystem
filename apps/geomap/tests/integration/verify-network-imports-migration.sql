\set ON_ERROR_STOP on

DO $$
BEGIN
    IF to_regclass('public.network_imports') IS NULL THEN
        RAISE EXCEPTION 'network_imports was not created';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM schema_migrations WHERE version = '005' AND name = 'network_imports'
    ) THEN
        RAISE EXCEPTION 'migration 005 was not registered';
    END IF;
END
$$;

BEGIN;

INSERT INTO projects (id, name)
VALUES ('6f000000-0000-4000-8000-000000000001', 'Import history migration check');

INSERT INTO networks (id, project_id, name, type)
VALUES ('6f100000-0000-4000-8000-000000000001', '6f000000-0000-4000-8000-000000000001', 'Histórico', 'stormwater');

INSERT INTO network_imports (
    network_id, contract_version, source_system, source_srid, units, exported_at,
    imported_features, replaced_features, source_file_sha256)
VALUES (
    '6f100000-0000-4000-8000-000000000001', '0.3', 'Civil 3D DXF', 31982, 'metre',
    '2026-09-09T09:00:00Z', 2, 0, repeat('a', 64));

DO $$
DECLARE
    invalid record;
BEGIN
    FOR invalid IN
        SELECT * FROM (VALUES
            ('0.x', 31982, 1, 0, repeat('a', 64)),
            ('0.1', 0, 1, 0, repeat('a', 64)),
            ('0.1', 31982, 0, 0, repeat('a', 64)),
            ('0.1', 31982, 1, -1, repeat('a', 64)),
            ('0.1', 31982, 1, 0, repeat('A', 64)),
            ('0.1', 31982, 1, 0, repeat('a', 63) || 'g')
        ) AS candidate (contract_version, source_srid, imported_features, replaced_features, sha256)
    LOOP
        BEGIN
            INSERT INTO network_imports (
                network_id, contract_version, source_system, source_srid, units, exported_at,
                imported_features, replaced_features, source_file_sha256)
            VALUES (
                '6f100000-0000-4000-8000-000000000001', invalid.contract_version, 'Civil 3D',
                invalid.source_srid, 'metre', '2026-09-09T09:00:00Z',
                invalid.imported_features, invalid.replaced_features, invalid.sha256);
            RAISE EXCEPTION 'invalid import record was accepted: %', invalid;
        EXCEPTION
            WHEN check_violation THEN NULL;
        END;
    END LOOP;

    DELETE FROM networks WHERE id = '6f100000-0000-4000-8000-000000000001';
    IF EXISTS (
        SELECT 1 FROM network_imports WHERE network_id = '6f100000-0000-4000-8000-000000000001'
    ) THEN
        RAISE EXCEPTION 'import records were not removed with their network';
    END IF;
END
$$;

ROLLBACK;

\echo 'Network imports migration checks passed.'
