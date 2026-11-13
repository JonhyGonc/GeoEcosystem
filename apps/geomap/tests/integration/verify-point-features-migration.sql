\set ON_ERROR_STOP on

BEGIN;

INSERT INTO projects (id, name)
VALUES ('6e000000-0000-4000-8000-000000000001', 'Point migration check');

INSERT INTO networks (id, project_id, name, type)
VALUES ('6e100000-0000-4000-8000-000000000001', '6e000000-0000-4000-8000-000000000001', 'Blocos', 'blocks');

INSERT INTO network_features (id, network_id, geometry, source_type, properties)
VALUES
    ('6e200000-0000-4000-8000-000000000001', '6e100000-0000-4000-8000-000000000001',
     ST_GeomFromText('POINT(-48.85 -26.30)', 4326), 'BlockReference', '{"bloco":"TE"}'),
    ('6e200000-0000-4000-8000-000000000002', '6e100000-0000-4000-8000-000000000001',
     ST_GeomFromText('MULTIPOINT((-48.85 -26.30),(-48.84 -26.29))', 4326), 'BlockReference', '{}'),
    ('6e200000-0000-4000-8000-000000000003', '6e100000-0000-4000-8000-000000000001',
     ST_GeomFromText('LINESTRING(-48.85 -26.30,-48.84 -26.29)', 4326), 'Polyline', '{}');

DO $$
BEGIN
    BEGIN
        INSERT INTO network_features (id, network_id, geometry, source_type, properties)
        VALUES ('6e200000-0000-4000-8000-000000000004', '6e100000-0000-4000-8000-000000000001',
                ST_GeomFromText('POLYGON((0 0,1 0,1 1,0 0))', 4326), 'Polygon', '{}');
        RAISE EXCEPTION 'polygon was accepted';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

ROLLBACK;

\echo 'Point features migration checks passed.'
