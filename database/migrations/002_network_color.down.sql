BEGIN;

-- Remove somente a cor; redes e feições são preservadas.
ALTER TABLE networks
    DROP CONSTRAINT ck_networks_color_hex,
    DROP COLUMN color;

COMMIT;
