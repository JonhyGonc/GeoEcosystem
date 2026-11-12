\set ON_ERROR_STOP on

DO $$
DECLARE
    imported_network_id uuid;
    imported_color varchar(7);
    point_count integer;
    multipoint_count integer;
    first_point geometry;
    tile_x integer;
    tile_y integer;
    tile_z constant integer := 18;
    tile_bytes bytea;
BEGIN
    SELECT network.id, network.color
    INTO imported_network_id, imported_color
    FROM networks network
    JOIN projects project ON project.id = network.project_id
    WHERE project.name = 'Projeto exemplo de blocos'
      AND network.name = 'Conexões (blocos)';

    IF imported_network_id IS NULL THEN
        RAISE EXCEPTION 'contract 0.3 block network was not imported';
    END IF;

    IF imported_color IS DISTINCT FROM '#00A0FF' THEN
        RAISE EXCEPTION 'expected block network color #00A0FF, found %', imported_color;
    END IF;

    SELECT
        count(*) FILTER (WHERE GeometryType(geometry) = 'POINT'),
        count(*) FILTER (WHERE GeometryType(geometry) = 'MULTIPOINT')
    INTO point_count, multipoint_count
    FROM network_features
    WHERE network_id = imported_network_id;

    IF point_count <> 1 OR multipoint_count <> 1 THEN
        RAISE EXCEPTION 'expected 1 point and 1 multipoint, found % and %',
            point_count, multipoint_count;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM network_features
        WHERE network_id = imported_network_id
          AND properties ->> 'bloco' = 'TE'
          AND source_type = 'BlockReference'
    ) THEN
        RAISE EXCEPTION 'block name was not kept in feature properties';
    END IF;

    SELECT geometry
    INTO first_point
    FROM network_features
    WHERE network_id = imported_network_id
      AND GeometryType(geometry) = 'POINT';

    tile_x := floor((ST_X(first_point) + 180.0) / 360.0 * (2 ^ tile_z));
    tile_y := floor(
        (1.0 - ln(tan(radians(ST_Y(first_point))) + 1.0 / cos(radians(ST_Y(first_point)))) / pi())
        / 2.0 * (2 ^ tile_z));

    SELECT ST_AsMVT(tile, 'network_features')
    INTO tile_bytes
    FROM (
        SELECT ST_AsMVTGeom(
                   ST_Transform(feature.geometry, 3857),
                   ST_TileEnvelope(tile_z, tile_x, tile_y)) AS geom
        FROM network_features feature
        WHERE feature.network_id = imported_network_id
          AND ST_Intersects(
                  feature.geometry,
                  ST_Transform(ST_TileEnvelope(tile_z, tile_x, tile_y), 4326))
    ) tile
    WHERE tile.geom IS NOT NULL;

    IF tile_bytes IS NULL OR length(tile_bytes) = 0 THEN
        RAISE EXCEPTION 'block point did not render in MVT tile %/%/%', tile_z, tile_x, tile_y;
    END IF;
END
$$;

\echo 'Block points import checks passed.'
