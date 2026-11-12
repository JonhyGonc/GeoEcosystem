\set ON_ERROR_STOP on

DO $$
DECLARE
    imported_project_id uuid;
    imported_network_id uuid;
    feature_count integer;
    invalid_geometry_count integer;
    first_point geometry;
    expected_first_point geometry;
BEGIN
    SELECT id
    INTO STRICT imported_project_id
    FROM projects
    WHERE name = 'GeoMap Issue 15 SMALL';

    SELECT id
    INTO STRICT imported_network_id
    FROM networks
    WHERE project_id = imported_project_id
      AND name = 'Drenagem Issue 15 SMALL'
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

    IF feature_count <> 1000 THEN
        RAISE EXCEPTION 'Expected 1000 imported features, found %', feature_count;
    END IF;
    IF invalid_geometry_count <> 0 THEN
        RAISE EXCEPTION 'Found % invalid imported geometries', invalid_geometry_count;
    END IF;

    SELECT ST_StartPoint(geometry)
    INTO STRICT first_point
    FROM network_features
    WHERE network_id = imported_network_id
      AND source_type = 'Polyline'
      AND source_id = 'small-0000'
      AND source_layer = 'INTEGRATION-SMALL'
      AND properties ->> 'dataset' = 'import-small'
      AND (properties ->> 'sequence')::integer = 0;

    expected_first_point := ST_Transform(
        ST_SetSRID(ST_MakePoint(688000.0, 7086400.0), 31982),
        4326);
    IF ST_Distance(first_point, expected_first_point) > 0.000000001 THEN
        RAISE EXCEPTION 'EPSG:31982 to EPSG:4326 transformation is incorrect';
    END IF;
END
$$;
