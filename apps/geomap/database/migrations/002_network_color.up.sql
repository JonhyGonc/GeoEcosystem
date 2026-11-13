BEGIN;

-- Cor da layer de origem no CAD (contrato v0.2). Nula para redes importadas
-- pelo contrato v0.1; o visualizador usa então a paleta automática.
ALTER TABLE networks
    ADD COLUMN color varchar(7),
    ADD CONSTRAINT ck_networks_color_hex
        CHECK (color IS NULL OR color ~ '^#[0-9A-F]{6}$');

COMMIT;
