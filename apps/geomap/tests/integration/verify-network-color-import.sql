\set ON_ERROR_STOP on

DO $$
DECLARE
    imported_color varchar(7);
BEGIN
    SELECT network.color
    INTO imported_color
    FROM networks network
    JOIN projects project ON project.id = network.project_id
    WHERE project.name = 'Projeto exemplo de cor'
      AND network.name = 'Rede com cor do CAD';

    IF imported_color IS DISTINCT FROM '#FF7F00' THEN
        RAISE EXCEPTION 'expected normalized color #FF7F00, found %', imported_color;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM networks network
        JOIN projects project ON project.id = network.project_id
        WHERE project.name = 'Projeto exemplo'
          AND network.color IS NOT NULL
    ) THEN
        RAISE EXCEPTION 'a contract 0.1 import received a color';
    END IF;
END
$$;

\echo 'Network color import checks passed.'
