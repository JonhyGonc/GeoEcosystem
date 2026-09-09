DO $$
DECLARE
    target_project_id uuid;
    target_network_id uuid;
    target_feature_id uuid;
BEGIN
    SELECT id INTO target_project_id
    FROM projects
    WHERE name = 'Projeto exemplo';

    IF target_project_id IS NULL THEN
        RAISE EXCEPTION 'GeoAutoEngine project was not imported';
    END IF;

    SELECT id INTO target_network_id
    FROM networks
    WHERE project_id = target_project_id
      AND name = 'Águas pluviais'
      AND type = 'stormwater';

    IF target_network_id IS NULL THEN
        RAISE EXCEPTION 'GeoAutoEngine network was not imported';
    END IF;

    SELECT id INTO target_feature_id
    FROM network_features
    WHERE network_id = target_network_id
      AND source_id = '2A7F'
      AND source_layer = 'GF-Rede Pluvial'
      AND source_type = 'Polyline'
      AND properties ->> 'cadHandle' = '2A7F'
      AND properties ->> 'cadEntityType' = 'Polyline'
      AND (properties ->> 'vertexCount')::integer = 2
      AND NOT (properties ->> 'closed')::boolean
      AND (properties ->> 'cadElevation')::double precision = 0
      AND NOT (properties ->> 'projectedTo2D')::boolean
      AND ST_SRID(geometry) = 4326
      AND GeometryType(geometry) = 'LINESTRING'
      AND ST_IsValid(geometry)
      AND ST_Equals(
          geometry,
          ST_Transform(
              ST_GeomFromText(
                  'LINESTRING(688100.25 7086400.75,688112.5 7086411)',
                  31982
              ),
              4326
          )
      );

    IF target_feature_id IS NULL THEN
        RAISE EXCEPTION 'GeoAutoEngine feature metadata or transformed geometry is invalid';
    END IF;

    IF (SELECT count(*) FROM network_features WHERE network_id = target_network_id) <> 1 THEN
        RAISE EXCEPTION 'Expected exactly one GeoAutoEngine reference feature';
    END IF;
END
$$;
