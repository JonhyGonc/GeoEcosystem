\set ON_ERROR_STOP on

CREATE TEMP TABLE vector_tile_plan (
    plan jsonb NOT NULL
);

DO $verify$
DECLARE
    large_project_id uuid;
    large_network_id uuid;
    explain_result json;
BEGIN
    SELECT project_id, id
    INTO STRICT large_project_id, large_network_id
    FROM networks
    WHERE name = 'GeoMap Synthetic LARGE - seed 424242';

    FOR explain_result IN EXECUTE $query$
        EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)
        WITH bounds AS (
            SELECT
                ST_TileEnvelope(15, 11938, 18866) AS tile_envelope,
                ST_Transform(
                    ST_TileEnvelope(15, 11938, 18866, 0.015625),
                    4326) AS query_envelope
        ),
        tile_rows AS (
            SELECT
                feature.id::text AS id,
                ST_AsMVTGeom(
                    ST_Transform(feature.geometry, 3857),
                    bounds.tile_envelope,
                    4096,
                    64,
                    true) AS geom
            FROM network_features feature
            JOIN networks network ON network.id = feature.network_id
            CROSS JOIN bounds
            WHERE network.project_id = $1
              AND feature.network_id = $2
              AND feature.geometry && bounds.query_envelope
              AND ST_Intersects(feature.geometry, bounds.query_envelope)
        )
        SELECT COALESCE(
            ST_AsMVT(tile_rows, 'network_features', 4096, 'geom'),
            '\x'::bytea)
        FROM tile_rows
        WHERE geom IS NOT NULL
    $query$ USING large_project_id, large_network_id
    LOOP
        INSERT INTO vector_tile_plan (plan)
        VALUES (explain_result::jsonb);
    END LOOP;

    IF NOT EXISTS (
        SELECT 1
        FROM vector_tile_plan
        WHERE plan::text LIKE '%ix_network_features_geometry%'
    ) THEN
        RAISE EXCEPTION 'Critical LARGE vector tile plan did not use ix_network_features_geometry';
    END IF;
END
$verify$;

SELECT
    (plan -> 0 ->> 'Planning Time')::numeric(10, 3) AS planning_ms,
    (plan -> 0 ->> 'Execution Time')::numeric(10, 3) AS execution_ms,
    plan::text LIKE '%ix_network_features_geometry%' AS uses_geometry_gist
FROM vector_tile_plan;

SELECT jsonb_pretty(plan) AS explain_analyze_buffers
FROM vector_tile_plan;
