\set ON_ERROR_STOP on

SELECT n.project_id::text || '|' || n.id::text
FROM networks n
WHERE n.name = format(
    'GeoMap Synthetic %s - seed %s',
    upper(:'expected_dataset'),
    :expected_seed::integer);
