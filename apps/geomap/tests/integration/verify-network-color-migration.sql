\set ON_ERROR_STOP on

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'networks'
          AND column_name = 'color'
          AND data_type = 'character varying'
          AND character_maximum_length = 7
          AND is_nullable = 'YES'
    ) THEN
        RAISE EXCEPTION 'networks.color is missing or has the wrong type';
    END IF;
END
$$;

BEGIN;

INSERT INTO projects (id, name)
VALUES ('6f000000-0000-4000-8000-000000000001', 'Color migration check');

INSERT INTO networks (id, project_id, name, type, color)
VALUES
    ('6f100000-0000-4000-8000-000000000001', '6f000000-0000-4000-8000-000000000001', 'Com cor', 'water', '#1A2B3C'),
    ('6f100000-0000-4000-8000-000000000002', '6f000000-0000-4000-8000-000000000001', 'Sem cor', 'water', NULL);

DO $$
BEGIN
    BEGIN
        INSERT INTO networks (id, project_id, name, type, color)
        VALUES ('6f100000-0000-4000-8000-000000000003', '6f000000-0000-4000-8000-000000000001', 'Inválida', 'water', '#1a2b3c');
        RAISE EXCEPTION 'lowercase color was accepted';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
    BEGIN
        INSERT INTO networks (id, project_id, name, type, color)
        VALUES ('6f100000-0000-4000-8000-000000000004', '6f000000-0000-4000-8000-000000000001', 'Inválida', 'water', 'red');
        RAISE EXCEPTION 'named color was accepted';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

ROLLBACK;

\echo 'Network color migration checks passed.'
