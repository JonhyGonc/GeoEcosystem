\set ON_ERROR_STOP on

CREATE TEMP TABLE spatial_query_plans (
    dataset text PRIMARY KEY,
    plan jsonb NOT NULL
);

DO $verify$
DECLARE
    target record;
    explain_result json;
BEGIN
    FOR target IN
        SELECT definition.dataset, network.project_id, network.id AS network_id
        FROM (
            VALUES
                ('small', 'GeoMap Synthetic SMALL - seed 42'),
                ('medium', 'GeoMap Synthetic MEDIUM - seed 4242'),
                ('large', 'GeoMap Synthetic LARGE - seed 424242')
        ) AS definition(dataset, network_name)
        JOIN networks network ON network.name = definition.network_name
    LOOP
        FOR explain_result IN EXECUTE $query$
            EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)
            SELECT
                feature.id,
                feature.network_id,
                ST_AsGeoJSON(feature.geometry, 8)
            FROM network_features feature
            JOIN networks network ON network.id = feature.network_id
            WHERE network.project_id = $1
              AND feature.network_id = $2
              AND feature.geometry && ST_MakeEnvelope(-48.90, -26.35, -48.85, -26.30, 4326)
              AND ST_Intersects(
                  feature.geometry,
                  ST_MakeEnvelope(-48.90, -26.35, -48.85, -26.30, 4326))
            LIMIT 501
        $query$ USING target.project_id, target.network_id
        LOOP
            INSERT INTO spatial_query_plans (dataset, plan)
            VALUES (target.dataset, explain_result::jsonb);
        END LOOP;
    END LOOP;

    IF NOT EXISTS (
        SELECT 1
        FROM spatial_query_plans
        WHERE dataset = 'large'
          AND plan::text LIKE '%ix_network_features_geometry%'
    ) THEN
        RAISE EXCEPTION 'Critical LARGE query plan did not use ix_network_features_geometry';
    END IF;
END
$verify$;

SELECT
    dataset,
    (plan -> 0 ->> 'Planning Time')::numeric(10, 3) AS planning_ms,
    (plan -> 0 ->> 'Execution Time')::numeric(10, 3) AS execution_ms,
    plan::text LIKE '%ix_network_features_geometry%' AS uses_geometry_gist
FROM spatial_query_plans
ORDER BY dataset;

SELECT dataset, jsonb_pretty(plan) AS explain_analyze_buffers
FROM spatial_query_plans
ORDER BY dataset;
