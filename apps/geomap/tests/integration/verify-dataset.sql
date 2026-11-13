\set ON_ERROR_STOP on

CREATE TEMP TABLE expected_dataset_context AS
SELECT
    lower(:'expected_dataset')::text AS dataset,
    :expected_seed::integer AS seed,
    :expected_count::integer AS expected_count,
    -48.95::double precision AS min_longitude,
    -26.40::double precision AS min_latitude,
    -48.75::double precision AS max_longitude,
    -26.20::double precision AS max_latitude;

DO $verify$
DECLARE
    context expected_dataset_context%ROWTYPE;
    selected_network_id uuid;
    actual_count integer;
    actual_min_longitude double precision;
    actual_min_latitude double precision;
    actual_max_longitude double precision;
    actual_max_latitude double precision;
    invalid_count integer;
BEGIN
    SELECT * INTO STRICT context FROM expected_dataset_context;

    SELECT n.id
    INTO STRICT selected_network_id
    FROM networks n
    WHERE n.name = format(
        'GeoMap Synthetic %s - seed %s',
        upper(context.dataset),
        context.seed);

    SELECT
        count(*)::integer,
        ST_XMin(Box3D(ST_Extent(f.geometry))),
        ST_YMin(Box3D(ST_Extent(f.geometry))),
        ST_XMax(Box3D(ST_Extent(f.geometry))),
        ST_YMax(Box3D(ST_Extent(f.geometry)))
    INTO
        actual_count,
        actual_min_longitude,
        actual_min_latitude,
        actual_max_longitude,
        actual_max_latitude
    FROM network_features f
    WHERE f.network_id = selected_network_id;

    IF actual_count <> context.expected_count THEN
        RAISE EXCEPTION 'Expected % features, found %', context.expected_count, actual_count;
    END IF;

    IF actual_min_longitude < context.min_longitude
       OR actual_min_latitude < context.min_latitude
       OR actual_max_longitude > context.max_longitude
       OR actual_max_latitude > context.max_latitude THEN
        RAISE EXCEPTION 'Dataset extent is outside the declared bounds';
    END IF;

    IF actual_max_longitude - actual_min_longitude < 0.18
       OR actual_max_latitude - actual_min_latitude < 0.18 THEN
        RAISE EXCEPTION 'Dataset does not cover the expected test area';
    END IF;

    SELECT count(*)::integer
    INTO invalid_count
    FROM network_features f
    WHERE f.network_id = selected_network_id
      AND (
          ST_SRID(f.geometry) <> 4326
          OR GeometryType(f.geometry) <> 'LINESTRING'
          OR ST_IsEmpty(f.geometry)
          OR f.source_type <> 'SyntheticLineString'
          OR f.source_layer <> 'SYNTHETIC-' || upper(context.dataset)
          OR f.properties ->> 'dataset' <> context.dataset
          OR (f.properties ->> 'seed')::integer <> context.seed
          OR jsonb_typeof(f.properties -> 'diameter_mm') <> 'number'
          OR jsonb_typeof(f.properties -> 'material') <> 'string'
      );

    IF invalid_count <> 0 THEN
        RAISE EXCEPTION 'Found % invalid synthetic features', invalid_count;
    END IF;

    IF (
        SELECT count(DISTINCT f.source_id)
        FROM network_features f
        WHERE f.network_id = selected_network_id
    ) <> context.expected_count THEN
        RAISE EXCEPTION 'Synthetic source identifiers are not unique';
    END IF;

    RAISE NOTICE '% dataset verified: % features, extent [%, %, %, %]',
        upper(context.dataset),
        actual_count,
        actual_min_longitude,
        actual_min_latitude,
        actual_max_longitude,
        actual_max_latitude;
END
$verify$;

DROP TABLE expected_dataset_context;
