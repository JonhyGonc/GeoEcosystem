BEGIN;

-- Blocos do CAD (PVs, conexões, postes) passam a ser feições pontuais
-- (contrato v0.3, Issue #49). Linhas continuam aceitas como antes.
ALTER TABLE network_features
    DROP CONSTRAINT ck_network_features_linear_geometry,
    ADD CONSTRAINT ck_network_features_supported_geometry
        CHECK (GeometryType(geometry) IN ('LINESTRING', 'MULTILINESTRING', 'POINT', 'MULTIPOINT'));

COMMIT;
