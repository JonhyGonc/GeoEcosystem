\set ON_ERROR_STOP on

DO $verify$
DECLARE
    estimated_rows bigint;
    relation_size_bytes bigint;
    analyzed_at timestamptz;
    valid_spatial_indexes integer;
    valid_relational_indexes integer;
    analyzed_columns integer;
BEGIN
    SELECT
        c.reltuples::bigint,
        pg_total_relation_size(c.oid)
    INTO STRICT estimated_rows, relation_size_bytes
    FROM pg_class c
    WHERE c.oid = 'public.network_features'::regclass;

    IF estimated_rows < 1000000 THEN
        RAISE EXCEPTION 'ANALYZE estimated only % rows; expected at least 1000000', estimated_rows;
    END IF;

    PERFORM pg_stat_clear_snapshot();

    SELECT s.last_analyze
    INTO analyzed_at
    FROM pg_stat_user_tables s
    WHERE s.relid = 'public.network_features'::regclass;

    IF analyzed_at IS NULL THEN
        RAISE EXCEPTION 'network_features has no recorded manual ANALYZE';
    END IF;

    SELECT count(*)::integer
    INTO valid_spatial_indexes
    FROM pg_index i
    JOIN pg_class index_relation ON index_relation.oid = i.indexrelid
    JOIN pg_class table_relation ON table_relation.oid = i.indrelid
    JOIN pg_am access_method ON access_method.oid = index_relation.relam
    WHERE table_relation.oid = 'public.network_features'::regclass
      AND index_relation.relname = 'ix_network_features_geometry'
      AND access_method.amname = 'gist'
      AND i.indisvalid
      AND i.indisready;

    IF valid_spatial_indexes <> 1 THEN
        RAISE EXCEPTION 'Expected one valid GiST geometry index, found %', valid_spatial_indexes;
    END IF;

    SELECT count(*)::integer
    INTO valid_relational_indexes
    FROM pg_index i
    JOIN pg_class index_relation ON index_relation.oid = i.indexrelid
    JOIN pg_class table_relation ON table_relation.oid = i.indrelid
    JOIN pg_am access_method ON access_method.oid = index_relation.relam
    WHERE table_relation.oid = 'public.network_features'::regclass
      AND index_relation.relname = 'ix_network_features_network_id'
      AND access_method.amname = 'btree'
      AND i.indisvalid
      AND i.indisready;

    IF valid_relational_indexes <> 1 THEN
        RAISE EXCEPTION 'Expected one valid B-tree network index, found %', valid_relational_indexes;
    END IF;

    SELECT count(*)::integer
    INTO analyzed_columns
    FROM pg_statistic statistics
    JOIN pg_attribute attribute
      ON attribute.attrelid = statistics.starelid
     AND attribute.attnum = statistics.staattnum
    WHERE statistics.starelid = 'public.network_features'::regclass
      AND attribute.attname IN ('network_id', 'geometry');

    IF analyzed_columns <> 2 THEN
        RAISE EXCEPTION 'Expected statistics for network_id and geometry, found % columns', analyzed_columns;
    END IF;

    RAISE NOTICE 'LARGE database state verified: estimated rows %, relation plus indexes % bytes, analyzed at %',
        estimated_rows,
        relation_size_bytes,
        analyzed_at;
END
$verify$;
