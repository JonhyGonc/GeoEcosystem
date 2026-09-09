DO $$
DECLARE
    target_project_id uuid;
    target_network_id uuid;
BEGIN
    SELECT id INTO target_project_id
    FROM projects
    WHERE name = 'DXF Fixture Project';

    IF target_project_id IS NULL THEN
        RAISE EXCEPTION 'DXF fixture project was not imported';
    END IF;

    SELECT id INTO target_network_id
    FROM networks
    WHERE project_id = target_project_id
      AND name = 'DXF Fixture Network'
      AND type = 'stormwater';

    IF target_network_id IS NULL THEN
        RAISE EXCEPTION 'DXF fixture network was not imported';
    END IF;

    IF (SELECT count(*) FROM network_features WHERE network_id = target_network_id) <> 2 THEN
        RAISE EXCEPTION 'Expected exactly two converted DXF features';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM network_features
        WHERE network_id = target_network_id
          AND (
              source_id NOT IN ('A1', 'A2')
              OR source_layer <> 'TEST-NETWORK'
              OR source_type <> 'Polyline'
              OR properties ->> 'cadEntityType' <> 'LWPOLYLINE'
              OR properties ->> 'projectedTo2D' NOT IN ('true', 'false')
              OR ST_SRID(geometry) <> 4326
              OR GeometryType(geometry) <> 'LINESTRING'
              OR NOT ST_IsValid(geometry)
          )
    ) THEN
        RAISE EXCEPTION 'Converted DXF metadata or geometry is invalid';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM network_features
        WHERE network_id = target_network_id
          AND source_id = 'A1'
          AND (properties ->> 'cadElevation')::double precision = 12.5
          AND (properties ->> 'vertexCount')::integer = 3
          AND (properties ->> 'projectedTo2D')::boolean
    ) THEN
        RAISE EXCEPTION 'Projected elevation attributes were not preserved';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM network_features
        WHERE network_id = target_network_id
          AND source_id = 'A1'
          AND ST_Equals(
              geometry,
              ST_Transform(
                  ST_GeomFromText(
                      'LINESTRING(700000 7100000,700010 7100010,700025 7100015)',
                      31982
                  ),
                  4326
              )
          )
    ) THEN
        RAISE EXCEPTION 'DXF coordinates were not transformed from EPSG:31982';
    END IF;
END
$$;
