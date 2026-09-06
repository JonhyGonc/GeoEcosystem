BEGIN;

CREATE EXTENSION IF NOT EXISTS postgis;

CREATE TABLE projects (
    id uuid PRIMARY KEY,
    name varchar(200) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_projects_name_not_blank CHECK (btrim(name) <> '')
);

CREATE TABLE networks (
    id uuid PRIMARY KEY,
    project_id uuid NOT NULL,
    name varchar(200) NOT NULL,
    type varchar(100) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_networks_project
        FOREIGN KEY (project_id)
        REFERENCES projects (id)
        ON DELETE CASCADE,
    CONSTRAINT ck_networks_name_not_blank CHECK (btrim(name) <> ''),
    CONSTRAINT ck_networks_type_not_blank CHECK (btrim(type) <> '')
);

CREATE TABLE network_features (
    id uuid PRIMARY KEY,
    network_id uuid NOT NULL,
    geometry geometry(Geometry, 4326) NOT NULL,
    source_id varchar(255),
    source_layer varchar(255),
    source_type varchar(100) NOT NULL,
    properties jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_network_features_network
        FOREIGN KEY (network_id)
        REFERENCES networks (id)
        ON DELETE CASCADE,
    CONSTRAINT ck_network_features_source_type_not_blank
        CHECK (btrim(source_type) <> ''),
    CONSTRAINT ck_network_features_linear_geometry
        CHECK (GeometryType(geometry) IN ('LINESTRING', 'MULTILINESTRING')),
    CONSTRAINT ck_network_features_geometry_not_empty
        CHECK (NOT ST_IsEmpty(geometry)),
    CONSTRAINT ck_network_features_properties_object
        CHECK (jsonb_typeof(properties) = 'object')
);

CREATE INDEX ix_networks_project_id
    ON networks (project_id);

CREATE INDEX ix_network_features_network_id
    ON network_features (network_id);

CREATE INDEX ix_network_features_geometry
    ON network_features
    USING gist (geometry);

CREATE UNIQUE INDEX ux_network_features_network_source
    ON network_features (network_id, source_type, source_id)
    WHERE source_id IS NOT NULL;

COMMIT;
