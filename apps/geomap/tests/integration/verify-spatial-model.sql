\set ON_ERROR_STOP on

DO $$
BEGIN
    IF to_regclass('public.projects') IS NULL
        OR to_regclass('public.networks') IS NULL
        OR to_regclass('public.network_features') IS NULL THEN
        RAISE EXCEPTION 'Initial spatial tables were not created';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_indexes
        WHERE schemaname = 'public'
          AND indexname = 'ix_network_features_geometry'
          AND indexdef ILIKE '%USING gist%'
    ) THEN
        RAISE EXCEPTION 'GiST geometry index was not created';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_attribute
        WHERE attrelid = 'public.network_features'::regclass
          AND attname = 'geometry'
          AND postgis_typmod_srid(atttypmod) = 4326
          AND postgis_typmod_type(atttypmod) = 'Geometry'
    ) THEN
        RAISE EXCEPTION 'Geometry column is not configured with SRID 4326';
    END IF;
END
$$;

BEGIN;

INSERT INTO projects (id, name)
VALUES ('00000000-0000-0000-0000-000000000001', 'Projeto de teste');

INSERT INTO networks (id, project_id, name, type)
VALUES (
    '00000000-0000-0000-0000-000000000002',
    '00000000-0000-0000-0000-000000000001',
    'Drenagem',
    'drainage'
);

INSERT INTO network_features (
    id,
    network_id,
    geometry,
    source_id,
    source_layer,
    source_type,
    properties
)
VALUES (
    '00000000-0000-0000-0000-000000000003',
    '00000000-0000-0000-0000-000000000002',
    ST_GeomFromText('LINESTRING(-48.5000 -27.6000, -48.4990 -27.5990)', 4326),
    'cad-001',
    'REDE-DRENAGEM',
    'Polyline',
    '{"diameter": 300, "material": "PVC"}'::jsonb
);

DO $$
DECLARE
    stored_srid integer;
    stored_type text;
    stored_diameter integer;
BEGIN
    SELECT
        ST_SRID(geometry),
        GeometryType(geometry),
        (properties ->> 'diameter')::integer
    INTO stored_srid, stored_type, stored_diameter
    FROM network_features
    WHERE id = '00000000-0000-0000-0000-000000000003';

    IF stored_srid <> 4326 THEN
        RAISE EXCEPTION 'Unexpected SRID: %', stored_srid;
    END IF;

    IF stored_type <> 'LINESTRING' THEN
        RAISE EXCEPTION 'Unexpected geometry type: %', stored_type;
    END IF;

    IF stored_diameter <> 300 THEN
        RAISE EXCEPTION 'JSONB properties were not preserved';
    END IF;
END
$$;

DO $$
BEGIN
    BEGIN
        INSERT INTO networks (id, project_id, name, type)
        VALUES (
            '00000000-0000-0000-0000-000000000010',
            '00000000-0000-0000-0000-000000000099',
            'Órfã',
            'test'
        );

        RAISE EXCEPTION 'Foreign key accepted an orphan network';
    EXCEPTION
        WHEN foreign_key_violation THEN
            NULL;
    END;
END
$$;

DO $$
BEGIN
    BEGIN
        INSERT INTO network_features (
            id,
            network_id,
            geometry,
            source_id,
            source_type
        )
        VALUES (
            '00000000-0000-0000-0000-000000000011',
            '00000000-0000-0000-0000-000000000002',
            ST_GeomFromText('LINESTRING(-48.4980 -27.5980, -48.4970 -27.5970)', 4326),
            'cad-001',
            'Polyline'
        );

        RAISE EXCEPTION 'Source uniqueness policy was not enforced';
    EXCEPTION
        WHEN unique_violation THEN
            NULL;
    END;
END
$$;

DO $$
BEGIN
    BEGIN
        INSERT INTO network_features (
            id,
            network_id,
            geometry,
            source_type
        )
        VALUES (
            '00000000-0000-0000-0000-000000000012',
            '00000000-0000-0000-0000-000000000002',
            ST_GeomFromText('POINT(-48.5000 -27.6000)', 4326),
            'Point'
        );

        RAISE EXCEPTION 'Non-linear geometry was accepted';
    EXCEPTION
        WHEN check_violation THEN
            NULL;
    END;
END
$$;

DELETE FROM projects
WHERE id = '00000000-0000-0000-0000-000000000001';

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM networks
        WHERE id = '00000000-0000-0000-0000-000000000002'
    ) OR EXISTS (
        SELECT 1
        FROM network_features
        WHERE id = '00000000-0000-0000-0000-000000000003'
    ) THEN
        RAISE EXCEPTION 'Cascade deletion policy was not enforced';
    END IF;
END
$$;

ROLLBACK;
