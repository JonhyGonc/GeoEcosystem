\set ON_ERROR_STOP on

SELECT set_config('geomap.expected_profile', :'expected_profile', false);
SELECT set_config('geomap.expected_count', :'expected_count', false);

DO $$
DECLARE
    profile text := current_setting('geomap.expected_profile');
    expected_count integer := current_setting('geomap.expected_count')::integer;
    imported_project_id uuid;
    imported_network_id uuid;
    feature_count integer;
    invalid_geometry_count integer;
    sample_count integer;
BEGIN
    SELECT id
    INTO STRICT imported_project_id
    FROM projects
    WHERE name = 'GeoMap Streaming ' || upper(profile);

    SELECT id
    INTO STRICT imported_network_id
    FROM networks
    WHERE project_id = imported_project_id
      AND name = 'Drenagem Streaming ' || upper(profile)
      AND type = 'stormwater';

    SELECT
        count(*)::integer,
        count(*) FILTER (
            WHERE ST_SRID(geometry) <> 4326
               OR GeometryType(geometry) <> 'LINESTRING'
               OR ST_IsEmpty(geometry))::integer
    INTO feature_count, invalid_geometry_count
    FROM network_features
    WHERE network_id = imported_network_id;

    IF feature_count <> expected_count THEN
        RAISE EXCEPTION 'Expected % streaming features, found %', expected_count, feature_count;
    END IF;
    IF invalid_geometry_count <> 0 THEN
        RAISE EXCEPTION 'Found % invalid streaming geometries', invalid_geometry_count;
    END IF;

    SELECT count(*)::integer
    INTO sample_count
    FROM network_features
    WHERE network_id = imported_network_id
      AND source_id = profile || '-0000000'
      AND source_type = 'Polyline'
      AND source_layer = 'STREAMING-' || upper(profile)
      AND properties ->> 'dataset' = 'streaming-' || profile
      AND (properties ->> 'sequence')::integer = 0
      AND ST_X(ST_StartPoint(geometry)) BETWEEN -48.900000001 AND -48.899999999
      AND ST_Y(ST_StartPoint(geometry)) BETWEEN -26.350000001 AND -26.349999999;

    IF sample_count <> 1 THEN
        RAISE EXCEPTION 'Streaming sample feature was not preserved correctly';
    END IF;
END
$$;
