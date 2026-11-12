BEGIN;

-- Origem de cada importação concluída (Issue #64). Uma linha por importação,
-- gravada na mesma transação que troca o snapshot da rede; uma importação que
-- falha não deixa linha. As linhas não são atualizadas nem apagadas pelo
-- importador: reimportar acrescenta uma linha nova.
DO $$
BEGIN
    IF to_regclass('public.schema_migrations') IS NULL THEN
        RAISE EXCEPTION 'migration 004_schema_migrations is not applied';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM schema_migrations WHERE version = '004') THEN
        RAISE EXCEPTION 'migration 004_schema_migrations is not applied';
    END IF;
END
$$;

CREATE TABLE network_imports (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    network_id uuid NOT NULL
        REFERENCES networks (id)
        ON DELETE CASCADE,
    imported_at timestamptz NOT NULL DEFAULT now(),
    contract_version varchar(16) NOT NULL,
    source_system varchar(100) NOT NULL,
    source_srid integer NOT NULL,
    units varchar(32) NOT NULL,
    exported_at timestamptz NOT NULL,
    imported_features integer NOT NULL,
    replaced_features integer NOT NULL,
    source_file_sha256 char(64) NOT NULL,
    CONSTRAINT ck_network_imports_contract_version CHECK (contract_version ~ '^[0-9]+\.[0-9]+$'),
    CONSTRAINT ck_network_imports_source_srid CHECK (source_srid > 0),
    CONSTRAINT ck_network_imports_features CHECK (imported_features > 0 AND replaced_features >= 0),
    CONSTRAINT ck_network_imports_sha256 CHECK (source_file_sha256 ~ '^[0-9a-f]{64}$')
);

CREATE INDEX ix_network_imports_network_id ON network_imports (network_id, id);

COMMENT ON TABLE network_imports IS
    'Origem de cada importação concluída: metadados do arquivo e SHA-256 dos bytes lidos.';

INSERT INTO schema_migrations (version, name) VALUES ('005', 'network_imports');

COMMIT;
