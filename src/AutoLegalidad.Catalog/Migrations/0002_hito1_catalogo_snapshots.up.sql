-- 0002 Hito 1: extensiones ADITIVAS sobre el esquema base v1 (no se modifica ninguna columna existente).
--   * game: grupos de versiones (para que encounter.origin_game pueda apuntar a un grupo del Core), generacion, formato, tabla personal.
--   * availability: almacenabilidad de la forma (Core: FormInfo).
--   * artifact_origin: estado del parseo oficial y corroboracion con el MGDB embebido del Core.
--   * encounter_game: resolucion de cada encuentro a juegos concretos (GameVersion.Contains del Core).
--   * coverage_case: estado Planned/Untested.
--   * Disparadores: snapshots inmutables, artefactos inmutables, y barrera de aprobacion para la generacion.
-- SPDX-License-Identifier: GPL-3.0-or-later

ALTER TABLE game ADD COLUMN is_group INTEGER NOT NULL DEFAULT 0 CHECK(is_group IN (0,1));
ALTER TABLE game ADD COLUMN generation INTEGER CHECK(generation IS NULL OR generation > 0);
ALTER TABLE game ADD COLUMN entity_format INTEGER CHECK(entity_format IS NULL OR entity_format > 0);
ALTER TABLE game ADD COLUMN personal_table TEXT;
ALTER TABLE game ADD COLUMN availability_basis TEXT NOT NULL DEFAULT 'Unknown'
  CHECK(availability_basis IN ('PersonalTable','CoreEncounters','Unknown','NotApplicable'));

ALTER TABLE availability ADD COLUMN form_storage TEXT NOT NULL DEFAULT 'NotApplicable'
  CHECK(form_storage IN ('Storable','PartyOnly','BattleOnly','TotemOnly','LordOnly','NotApplicable'));

ALTER TABLE artifact_origin ADD COLUMN parse_status TEXT NOT NULL DEFAULT 'NotChecked'
  CHECK(parse_status IN ('Parsed','ParseFailed','NoOfficialParser','NotAPokemonTemplate','NotChecked'));
ALTER TABLE artifact_origin ADD COLUMN core_corroboration TEXT NOT NULL DEFAULT 'NotChecked'
  CHECK(core_corroboration IN ('ByteIdenticalInCoreMgdb','NotInCoreMgdb','NotApplicable','NotChecked'));
ALTER TABLE artifact_origin ADD COLUMN allowlist_rule_id TEXT;

ALTER TABLE coverage_case ADD COLUMN status TEXT NOT NULL DEFAULT 'Planned' CHECK(status IN ('Planned','Untested'));
ALTER TABLE coverage_case ADD COLUMN scope_note TEXT;

CREATE TABLE encounter_game (
 encounter_key TEXT NOT NULL REFERENCES encounter(encounter_key) ON DELETE CASCADE,
 game_id INTEGER NOT NULL REFERENCES game(game_id),
 PRIMARY KEY(encounter_key, game_id)
) STRICT, WITHOUT ROWID;

CREATE INDEX ix_encounter_game_game ON encounter_game(game_id, encounter_key);
CREATE INDEX ix_artifact_origin_class ON artifact_origin(evidence_class, approved_for_generation);
CREATE INDEX ix_coverage_status ON coverage_case(status, expected);
CREATE INDEX ix_coverage_game ON coverage_case(game_id, encounter_kind);

-- Un source_version esta "congelado" cuando pertenece a un snapshot que ya no esta en Staging (Verified o Retired).
CREATE VIEW v_frozen_source_version AS
  SELECT DISTINCT ss.source_version_id
    FROM snapshot_source ss JOIN dataset_snapshot d ON d.snapshot_id = ss.snapshot_id
   WHERE d.state <> 'Staging';

-- UNICA via por la que la generacion (Hito 2+) puede leer artefactos y encuentros: solo lo aprobado.
CREATE VIEW v_artifact_generable AS
  SELECT * FROM artifact_origin
   WHERE approved_for_generation = 1 AND release_state = 'Published'
     AND evidence_class IN ('AuthenticDump','DocumentedReconstruction') AND parse_status = 'Parsed';

CREATE VIEW v_encounter_generable AS
  SELECT e.* FROM encounter e
   WHERE e.approved_for_generation = 1
     AND (e.artifact_sha256 IS NULL OR EXISTS (
           SELECT 1 FROM v_artifact_generable a
            WHERE a.artifact_sha256 = e.artifact_sha256 AND a.source_version_id = e.source_version_id));

-- ---- Barrera de aprobacion (regla 8: Unknown/ResearchOnly/Unreleased nunca para generar) ----
-- El CHECK de la tabla ya exige Published + AuthenticDump|DocumentedReconstruction + evidence_ref + approval_note.
-- Estos disparadores anaden: parseo oficial correcto, y corroboracion del Core para reconstrucciones documentadas.
CREATE TRIGGER trg_artifact_origin_approval_ins BEFORE INSERT ON artifact_origin
WHEN NEW.approved_for_generation = 1 AND
     (NEW.parse_status <> 'Parsed' OR
      (NEW.evidence_class = 'DocumentedReconstruction' AND NEW.core_corroboration <> 'ByteIdenticalInCoreMgdb'))
BEGIN SELECT RAISE(ABORT, 'aprobacion denegada: requiere parseo oficial correcto y, si es reconstruccion, corroboracion byte a byte del Core'); END;

CREATE TRIGGER trg_artifact_origin_approval_upd BEFORE UPDATE ON artifact_origin
WHEN NEW.approved_for_generation = 1 AND
     (NEW.parse_status <> 'Parsed' OR
      (NEW.evidence_class = 'DocumentedReconstruction' AND NEW.core_corroboration <> 'ByteIdenticalInCoreMgdb'))
BEGIN SELECT RAISE(ABORT, 'aprobacion denegada: requiere parseo oficial correcto y, si es reconstruccion, corroboracion byte a byte del Core'); END;

CREATE TRIGGER trg_encounter_approval_ins BEFORE INSERT ON encounter
WHEN NEW.approved_for_generation = 1 AND NEW.artifact_sha256 IS NOT NULL AND NOT EXISTS (
       SELECT 1 FROM v_artifact_generable a WHERE a.artifact_sha256 = NEW.artifact_sha256 AND a.source_version_id = NEW.source_version_id)
BEGIN SELECT RAISE(ABORT, 'encuentro aprobado sin artefacto aprobado para generar'); END;

CREATE TRIGGER trg_encounter_approval_upd BEFORE UPDATE ON encounter
WHEN NEW.approved_for_generation = 1 AND NEW.artifact_sha256 IS NOT NULL AND NOT EXISTS (
       SELECT 1 FROM v_artifact_generable a WHERE a.artifact_sha256 = NEW.artifact_sha256 AND a.source_version_id = NEW.source_version_id)
BEGIN SELECT RAISE(ABORT, 'encuentro aprobado sin artefacto aprobado para generar'); END;

-- ---- Inmutabilidad de contenido direccionable y de snapshots ----
CREATE TRIGGER trg_source_version_no_update BEFORE UPDATE ON source_version
BEGIN SELECT RAISE(ABORT, 'source_version es inmutable'); END;

CREATE TRIGGER trg_artifact_no_update BEFORE UPDATE ON artifact
BEGIN SELECT RAISE(ABORT, 'artifact es inmutable (direccionado por contenido)'); END;

CREATE TRIGGER trg_snapshot_identity_no_update BEFORE UPDATE OF snapshot_id, created_utc ON dataset_snapshot
BEGIN SELECT RAISE(ABORT, 'identidad del snapshot inmutable'); END;

CREATE TRIGGER trg_snapshot_manifest_frozen BEFORE UPDATE OF manifest_sha256, manifest_json ON dataset_snapshot
WHEN OLD.state <> 'Staging'
BEGIN SELECT RAISE(ABORT, 'snapshot inmutable: el manifest ya no se puede cambiar'); END;

CREATE TRIGGER trg_snapshot_state_transition BEFORE UPDATE OF state ON dataset_snapshot
WHEN NOT ((OLD.state = 'Staging'  AND NEW.state IN ('Verified','Retired')) OR
          (OLD.state = 'Verified' AND NEW.state = 'Retired') OR
          (OLD.state = 'Retired'  AND NEW.state = 'Verified') OR
          (OLD.state = NEW.state))
BEGIN SELECT RAISE(ABORT, 'transicion de estado de snapshot no permitida'); END;

CREATE TRIGGER trg_snapshot_no_delete BEFORE DELETE ON dataset_snapshot
WHEN OLD.state <> 'Staging'
BEGIN SELECT RAISE(ABORT, 'un snapshot Verified/Retired no se borra (se conserva para rollback)'); END;

CREATE TRIGGER trg_snapshot_source_ins BEFORE INSERT ON snapshot_source
WHEN (SELECT state FROM dataset_snapshot WHERE snapshot_id = NEW.snapshot_id) <> 'Staging'
BEGIN SELECT RAISE(ABORT, 'snapshot inmutable: no admite mas fuentes'); END;

CREATE TRIGGER trg_snapshot_source_del BEFORE DELETE ON snapshot_source
WHEN (SELECT state FROM dataset_snapshot WHERE snapshot_id = OLD.snapshot_id) <> 'Staging'
BEGIN SELECT RAISE(ABORT, 'snapshot inmutable: no se le quitan fuentes'); END;

CREATE TRIGGER trg_snapshot_source_upd BEFORE UPDATE ON snapshot_source
BEGIN SELECT RAISE(ABORT, 'snapshot_source es inmutable'); END;

-- Filas ligadas a un source_version congelado: ni alta, ni cambio, ni baja.
CREATE TRIGGER trg_artifact_origin_frozen_ins BEFORE INSERT ON artifact_origin
WHEN NEW.source_version_id IN (SELECT source_version_id FROM v_frozen_source_version)
BEGIN SELECT RAISE(ABORT, 'source_version congelado por un snapshot: artifact_origin inmutable'); END;
CREATE TRIGGER trg_artifact_origin_frozen_upd BEFORE UPDATE ON artifact_origin
WHEN OLD.source_version_id IN (SELECT source_version_id FROM v_frozen_source_version)
BEGIN SELECT RAISE(ABORT, 'source_version congelado por un snapshot: artifact_origin inmutable'); END;
CREATE TRIGGER trg_artifact_origin_frozen_del BEFORE DELETE ON artifact_origin
WHEN OLD.source_version_id IN (SELECT source_version_id FROM v_frozen_source_version)
BEGIN SELECT RAISE(ABORT, 'source_version congelado por un snapshot: artifact_origin inmutable'); END;

CREATE TRIGGER trg_encounter_frozen_ins BEFORE INSERT ON encounter
WHEN NEW.source_version_id IN (SELECT source_version_id FROM v_frozen_source_version)
BEGIN SELECT RAISE(ABORT, 'source_version congelado por un snapshot: encounter inmutable'); END;
CREATE TRIGGER trg_encounter_frozen_upd BEFORE UPDATE ON encounter
WHEN OLD.source_version_id IN (SELECT source_version_id FROM v_frozen_source_version)
BEGIN SELECT RAISE(ABORT, 'source_version congelado por un snapshot: encounter inmutable'); END;
CREATE TRIGGER trg_encounter_frozen_del BEFORE DELETE ON encounter
WHEN OLD.source_version_id IN (SELECT source_version_id FROM v_frozen_source_version)
BEGIN SELECT RAISE(ABORT, 'source_version congelado por un snapshot: encounter inmutable'); END;

CREATE TRIGGER trg_encounter_game_frozen_ins BEFORE INSERT ON encounter_game
WHEN (SELECT source_version_id FROM encounter WHERE encounter_key = NEW.encounter_key) IN (SELECT source_version_id FROM v_frozen_source_version)
BEGIN SELECT RAISE(ABORT, 'encounter_game congelado por un snapshot'); END;
CREATE TRIGGER trg_encounter_game_frozen_del BEFORE DELETE ON encounter_game
WHEN (SELECT source_version_id FROM encounter WHERE encounter_key = OLD.encounter_key) IN (SELECT source_version_id FROM v_frozen_source_version)
BEGIN SELECT RAISE(ABORT, 'encounter_game congelado por un snapshot'); END;

CREATE TRIGGER trg_pogo_window_frozen_ins BEFORE INSERT ON pogo_window
WHEN (SELECT source_version_id FROM encounter WHERE encounter_key = NEW.encounter_key) IN (SELECT source_version_id FROM v_frozen_source_version)
BEGIN SELECT RAISE(ABORT, 'pogo_window congelado por un snapshot'); END;
CREATE TRIGGER trg_pogo_window_frozen_upd BEFORE UPDATE ON pogo_window
WHEN (SELECT source_version_id FROM encounter WHERE encounter_key = OLD.encounter_key) IN (SELECT source_version_id FROM v_frozen_source_version)
BEGIN SELECT RAISE(ABORT, 'pogo_window congelado por un snapshot'); END;
CREATE TRIGGER trg_pogo_window_frozen_del BEFORE DELETE ON pogo_window
WHEN (SELECT source_version_id FROM encounter WHERE encounter_key = OLD.encounter_key) IN (SELECT source_version_id FROM v_frozen_source_version)
BEGIN SELECT RAISE(ABORT, 'pogo_window congelado por un snapshot'); END;
