\set ON_ERROR_STOP on

BEGIN;

INSERT INTO projects (id, name)
VALUES
    ('60000000-0000-4000-8000-000000000001', 'Spatial query fixture project'),
    ('60000000-0000-4000-8000-000000000002', 'Spatial query other project');

INSERT INTO networks (id, project_id, name, type)
VALUES
    (
        '61000000-0000-4000-8000-000000000001',
        '60000000-0000-4000-8000-000000000001',
        'Spatial query target network',
        'test'),
    (
        '61000000-0000-4000-8000-000000000002',
        '60000000-0000-4000-8000-000000000001',
        'Spatial query other network',
        'stormwater'),
    (
        '61000000-0000-4000-8000-000000000003',
        '60000000-0000-4000-8000-000000000002',
        'Empty network from other project',
        'test');

INSERT INTO network_features (
    id,
    network_id,
    geometry,
    source_id,
    source_layer,
    source_type,
    properties)
VALUES
    (
        '62000000-0000-4000-8000-000000000001',
        '61000000-0000-4000-8000-000000000001',
        ST_GeomFromText('LINESTRING(-48.85 -26.30, -48.84 -26.29)', 4326),
        'inside',
        'SPATIAL-QUERY-TEST',
        'TestLineString',
        '{"position":"inside"}'::jsonb),
    (
        '62000000-0000-4000-8000-000000000002',
        '61000000-0000-4000-8000-000000000001',
        ST_GeomFromText('LINESTRING(-48.90 -26.35, -48.80 -26.25)', 4326),
        'crossing',
        'SPATIAL-QUERY-TEST',
        'TestLineString',
        '{"position":"crossing"}'::jsonb),
    (
        '62000000-0000-4000-8000-000000000003',
        '61000000-0000-4000-8000-000000000001',
        ST_GeomFromText('LINESTRING(-48.72 -26.15, -48.71 -26.14)', 4326),
        'outside',
        'SPATIAL-QUERY-TEST',
        'TestLineString',
        '{"position":"outside"}'::jsonb),
    (
        '62000000-0000-4000-8000-000000000004',
        '61000000-0000-4000-8000-000000000002',
        ST_GeomFromText('LINESTRING(-48.85 -26.30, -48.84 -26.29)', 4326),
        'other-network',
        'SPATIAL-QUERY-TEST',
        'TestLineString',
        '{"position":"other-network"}'::jsonb);

COMMIT;
