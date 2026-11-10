BEGIN;

-- O rollback não apaga dados: se já existirem feições pontuais, ele falha e
-- pede que sejam removidas explicitamente antes de restaurar a regra linear.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM network_features
        WHERE GeometryType(geometry) IN ('POINT', 'MULTIPOINT')
    ) THEN
        RAISE EXCEPTION 'Point features exist; remove them explicitly before rolling back migration 003.';
    END IF;
END
$$;

ALTER TABLE network_features
    DROP CONSTRAINT ck_network_features_supported_geometry,
    ADD CONSTRAINT ck_network_features_linear_geometry
        CHECK (GeometryType(geometry) IN ('LINESTRING', 'MULTILINESTRING'));

COMMIT;
