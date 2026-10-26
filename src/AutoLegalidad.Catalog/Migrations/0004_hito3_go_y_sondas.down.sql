-- Rollback de 0004.
DROP VIEW IF EXISTS v_coverage_probe_latest;
DROP INDEX IF EXISTS ix_coverage_probe_state;
DROP TABLE IF EXISTS coverage_probe;
ALTER TABLE pogo_window DROP COLUMN core_route;
ALTER TABLE pogo_window DROP COLUMN core_match;
