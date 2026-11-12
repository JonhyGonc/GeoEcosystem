\set ON_ERROR_STOP on

-- Confere o histórico de importações de uma rede (Issue #64). Variáveis:
-- network_name, contract_version, source_system, source_srid, units,
-- sha256, imports (linhas esperadas) e features (feições por importação).
-- As variáveis do psql não são expandidas dentro de DO; por isso viram
-- configurações da sessão.
\o /dev/null
SELECT
    set_config('geomap.network_name', :'network_name', false),
    set_config('geomap.contract_version', :'contract_version', false),
    set_config('geomap.source_system', :'source_system', false),
    set_config('geomap.source_srid', :'source_srid', false),
    set_config('geomap.units', :'units', false),
    set_config('geomap.sha256', :'sha256', false),
    set_config('geomap.imports', :'imports', false),
    set_config('geomap.features', :'features', false);
\o

DO $$
DECLARE
    expected_network text := current_setting('geomap.network_name');
    expected_imports integer := current_setting('geomap.imports')::integer;
    expected_features integer := current_setting('geomap.features')::integer;
    target_network uuid;
    found_imports integer;
    mismatched integer;
    broken_chain integer;
BEGIN
    SELECT id INTO STRICT target_network FROM networks WHERE name = expected_network;

    SELECT count(*) INTO found_imports
    FROM network_imports
    WHERE network_id = target_network;
    IF found_imports <> expected_imports THEN
        RAISE EXCEPTION '% has % import records; expected %',
            expected_network, found_imports, expected_imports;
    END IF;

    SELECT count(*) INTO mismatched
    FROM network_imports
    WHERE network_id = target_network
      AND NOT (
          contract_version = current_setting('geomap.contract_version')
          AND source_system = current_setting('geomap.source_system')
          AND source_srid = current_setting('geomap.source_srid')::integer
          AND units = current_setting('geomap.units')
          AND source_file_sha256 = current_setting('geomap.sha256')
          AND imported_features = expected_features
          AND exported_at <= imported_at
      );
    IF mismatched > 0 THEN
        RAISE EXCEPTION '% has % import records with unexpected provenance',
            expected_network, mismatched;
    END IF;

    -- A primeira importação cria a rede; cada reimportação substitui
    -- exatamente as feições gravadas pela importação anterior.
    SELECT count(*) INTO broken_chain
    FROM (
        SELECT
            replaced_features,
            lag(imported_features, 1, 0) OVER (ORDER BY id) AS previous_features
        FROM network_imports
        WHERE network_id = target_network
    ) chain
    WHERE replaced_features <> previous_features;
    IF broken_chain > 0 THEN
        RAISE EXCEPTION '% import records do not chain replaced and imported counts',
            expected_network;
    END IF;
END
$$;

\echo 'Network import provenance checks passed for' :'network_name'
