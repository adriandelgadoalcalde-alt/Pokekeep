-- 0002 (rollback): deshace las extensiones del Hito 1 y deja el esquema base v1 tal cual.
-- SPDX-License-Identifier: GPL-3.0-or-later
DROP TRIGGER IF EXISTS trg_pogo_window_frozen_del;
DROP TRIGGER IF EXISTS trg_pogo_window_frozen_upd;
DROP TRIGGER IF EXISTS trg_pogo_window_frozen_ins;
DROP TRIGGER IF EXISTS trg_encounter_game_frozen_del;
DROP TRIGGER IF EXISTS trg_encounter_game_frozen_ins;
DROP TRIGGER IF EXISTS trg_encounter_frozen_del;
DROP TRIGGER IF EXISTS trg_encounter_frozen_upd;
DROP TRIGGER IF EXISTS trg_encounter_frozen_ins;
DROP TRIGGER IF EXISTS trg_artifact_origin_frozen_del;
DROP TRIGGER IF EXISTS trg_artifact_origin_frozen_upd;
DROP TRIGGER IF EXISTS trg_artifact_origin_frozen_ins;
DROP TRIGGER IF EXISTS trg_snapshot_source_upd;
DROP TRIGGER IF EXISTS trg_snapshot_source_del;
DROP TRIGGER IF EXISTS trg_snapshot_source_ins;
DROP TRIGGER IF EXISTS trg_snapshot_no_delete;
DROP TRIGGER IF EXISTS trg_snapshot_state_transition;
DROP TRIGGER IF EXISTS trg_snapshot_manifest_frozen;
DROP TRIGGER IF EXISTS trg_snapshot_identity_no_update;
DROP TRIGGER IF EXISTS trg_artifact_no_update;
DROP TRIGGER IF EXISTS trg_source_version_no_update;
DROP TRIGGER IF EXISTS trg_encounter_approval_upd;
DROP TRIGGER IF EXISTS trg_encounter_approval_ins;
DROP TRIGGER IF EXISTS trg_artifact_origin_approval_upd;
DROP TRIGGER IF EXISTS trg_artifact_origin_approval_ins;

DROP VIEW IF EXISTS v_encounter_generable;
DROP VIEW IF EXISTS v_artifact_generable;
DROP VIEW IF EXISTS v_frozen_source_version;

DROP INDEX IF EXISTS ix_coverage_game;
DROP INDEX IF EXISTS ix_coverage_status;
DROP INDEX IF EXISTS ix_artifact_origin_class;
DROP INDEX IF EXISTS ix_encounter_game_game;
DROP TABLE IF EXISTS encounter_game;

ALTER TABLE coverage_case DROP COLUMN scope_note;
ALTER TABLE coverage_case DROP COLUMN status;
ALTER TABLE artifact_origin DROP COLUMN allowlist_rule_id;
ALTER TABLE artifact_origin DROP COLUMN core_corroboration;
ALTER TABLE artifact_origin DROP COLUMN parse_status;
ALTER TABLE availability DROP COLUMN form_storage;
ALTER TABLE game DROP COLUMN availability_basis;
ALTER TABLE game DROP COLUMN personal_table;
ALTER TABLE game DROP COLUMN entity_format;
ALTER TABLE game DROP COLUMN generation;
ALTER TABLE game DROP COLUMN is_group;
