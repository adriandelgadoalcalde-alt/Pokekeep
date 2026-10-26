-- 0001 Esquema base v1: copia literal de DISENO-BASE-DE-DATOS.sql (SHA-256 294F5D9A1F4697847552B8C866C11611B0AE259D19272EEF7515E2790B68784B)
-- sin las sentencias PRAGMA, que se aplican por conexion (no se pueden cambiar dentro de una transaccion).
-- SPDX-License-Identifier: GPL-3.0-or-later

CREATE TABLE schema_version (
 version INTEGER PRIMARY KEY CHECK(version > 0),
 applied_utc TEXT NOT NULL,
 migration_sha256 TEXT NOT NULL CHECK(length(migration_sha256)=64)
) STRICT;

CREATE TABLE source (
 source_id TEXT PRIMARY KEY,
 name TEXT NOT NULL,
 url TEXT NOT NULL,
 category TEXT NOT NULL CHECK(category IN ('OfficialCore','PublishedEvents','GOResearch','CommunityResearch','LocalCorpus')),
 license_note TEXT NOT NULL,
 auto_import INTEGER NOT NULL DEFAULT 0 CHECK(auto_import IN (0,1))
) STRICT;

CREATE TABLE source_version (
 source_version_id TEXT PRIMARY KEY,
 source_id TEXT NOT NULL REFERENCES source(source_id),
 revision TEXT NOT NULL,
 retrieved_utc TEXT NOT NULL,
 manifest_sha256 TEXT NOT NULL CHECK(length(manifest_sha256)=64),
 manifest_json TEXT NOT NULL CHECK(json_valid(manifest_json)),
 UNIQUE(source_id, revision)
) STRICT;

CREATE TABLE artifact (
 artifact_sha256 TEXT PRIMARY KEY CHECK(length(artifact_sha256)=64),
 format TEXT NOT NULL,
 byte_length INTEGER NOT NULL CHECK(byte_length>=0),
 payload BLOB NOT NULL,
 CHECK(length(payload)=byte_length)
) STRICT;

CREATE TABLE artifact_origin (
 artifact_sha256 TEXT NOT NULL REFERENCES artifact(artifact_sha256),
 source_version_id TEXT NOT NULL REFERENCES source_version(source_version_id),
 relative_path TEXT NOT NULL,
 release_state TEXT NOT NULL CHECK(release_state IN ('Published','Unreleased','Unknown')),
 evidence_class TEXT NOT NULL CHECK(evidence_class IN ('AuthenticDump','DocumentedReconstruction','ResearchOnly','Unknown')),
 approved_for_generation INTEGER NOT NULL DEFAULT 0 CHECK(approved_for_generation IN (0,1)),
 evidence_ref TEXT,
 approval_note TEXT,
 PRIMARY KEY(artifact_sha256,source_version_id,relative_path),
 CHECK(approved_for_generation=0 OR
   (release_state='Published' AND evidence_class IN ('AuthenticDump','DocumentedReconstruction')
     AND evidence_ref IS NOT NULL AND approval_note IS NOT NULL))
) STRICT;

CREATE TABLE dataset_snapshot (
 snapshot_id TEXT PRIMARY KEY,
 created_utc TEXT NOT NULL,
 manifest_sha256 TEXT NOT NULL CHECK(length(manifest_sha256)=64),
 manifest_json TEXT NOT NULL CHECK(json_valid(manifest_json)),
 state TEXT NOT NULL CHECK(state IN ('Staging','Verified','Retired'))
) STRICT;

CREATE TABLE snapshot_source (
 snapshot_id TEXT NOT NULL REFERENCES dataset_snapshot(snapshot_id),
 source_version_id TEXT NOT NULL REFERENCES source_version(source_version_id),
 PRIMARY KEY(snapshot_id,source_version_id)
) STRICT;

CREATE TABLE release (
 release_id TEXT PRIMARY KEY,
 core_version TEXT NOT NULL,
 core_commit TEXT NOT NULL,
 core_dll_sha256 TEXT NOT NULL CHECK(length(core_dll_sha256)=64),
 alm_version TEXT NOT NULL,
 alm_commit TEXT NOT NULL,
 adapter_version TEXT NOT NULL,
 policy_version TEXT NOT NULL,
 protocol_version INTEGER NOT NULL CHECK(protocol_version>0),
 schema_version INTEGER NOT NULL CHECK(schema_version>0),
 rule_profile_sha256 TEXT NOT NULL CHECK(length(rule_profile_sha256)=64),
 manifest_json TEXT NOT NULL CHECK(json_valid(manifest_json))
) STRICT;

CREATE TABLE game (
 game_id INTEGER PRIMARY KEY,
 version_name TEXT NOT NULL UNIQUE,
 display_name_es TEXT NOT NULL,
 context TEXT NOT NULL,
 save_runtime_type TEXT,
 is_storage INTEGER NOT NULL DEFAULT 0 CHECK(is_storage IN (0,1)),
 is_transfer_origin_only INTEGER NOT NULL DEFAULT 0 CHECK(is_transfer_origin_only IN (0,1))
) STRICT;

CREATE TABLE species (
 species_id INTEGER PRIMARY KEY CHECK(species_id BETWEEN 1 AND 65535),
 canonical_name TEXT NOT NULL,
 introduced_generation INTEGER CHECK(introduced_generation>0),
 core_resource_sha256 TEXT NOT NULL CHECK(length(core_resource_sha256)=64)
) STRICT;

CREATE TABLE species_name (
 species_id INTEGER NOT NULL REFERENCES species(species_id),
 language_code TEXT NOT NULL,
 name TEXT NOT NULL,
 PRIMARY KEY(species_id,language_code)
) STRICT;

CREATE TABLE species_form (
 species_id INTEGER NOT NULL REFERENCES species(species_id),
 form_id INTEGER NOT NULL CHECK(form_id>=0),
 canonical_form_name TEXT,
 descriptor_json TEXT NOT NULL CHECK(json_valid(descriptor_json)),
 PRIMARY KEY(species_id,form_id)
) STRICT;

CREATE TABLE availability (
 release_id TEXT NOT NULL REFERENCES release(release_id),
 game_id INTEGER NOT NULL REFERENCES game(game_id),
 species_id INTEGER NOT NULL,
 form_id INTEGER NOT NULL,
 state TEXT NOT NULL CHECK(state IN ('Present','Absent','Unknown')),
 reason_ref TEXT NOT NULL,
 PRIMARY KEY(release_id,game_id,species_id,form_id),
 FOREIGN KEY(species_id,form_id) REFERENCES species_form(species_id,form_id)
) STRICT;

CREATE TABLE encounter (
 encounter_key TEXT PRIMARY KEY,
 release_id TEXT NOT NULL REFERENCES release(release_id),
 source_version_id TEXT NOT NULL REFERENCES source_version(source_version_id),
 artifact_sha256 TEXT REFERENCES artifact(artifact_sha256),
 species_id INTEGER NOT NULL,
 form_id INTEGER NOT NULL,
 origin_game INTEGER NOT NULL REFERENCES game(game_id),
 context TEXT NOT NULL,
 runtime_type TEXT NOT NULL,
 kind TEXT NOT NULL CHECK(kind IN ('Wild','Static','Egg','Trade','Gift','Event','Raid','GO','Other')),
 internal_id TEXT,
 descriptor_sha256 TEXT NOT NULL CHECK(length(descriptor_sha256)=64),
 descriptor_json TEXT NOT NULL CHECK(json_valid(descriptor_json)),
 constraints_json TEXT NOT NULL CHECK(json_valid(constraints_json)),
 source_ref TEXT NOT NULL,
 approved_for_generation INTEGER NOT NULL DEFAULT 0 CHECK(approved_for_generation IN (0,1)),
 FOREIGN KEY(species_id,form_id) REFERENCES species_form(species_id,form_id)
) STRICT;
-- El importer aplica allowlist y evidencia. Los descriptores se reconstruyen con Core.

CREATE TABLE encounter_target (
 encounter_key TEXT NOT NULL REFERENCES encounter(encounter_key),
 target_game INTEGER NOT NULL REFERENCES game(game_id),
 final_species_id INTEGER NOT NULL,
 final_form_id INTEGER NOT NULL,
 route_json TEXT NOT NULL CHECK(json_valid(route_json)),
 checked_release_id TEXT NOT NULL REFERENCES release(release_id),
 state TEXT NOT NULL CHECK(state IN ('Candidate','VerifiedRoute','Rejected')),
 reason TEXT NOT NULL,
 PRIMARY KEY(encounter_key,target_game,final_species_id,final_form_id),
 FOREIGN KEY(final_species_id,final_form_id) REFERENCES species_form(species_id,form_id)
) STRICT;
-- VerifiedRoute no significa que cualquier IV/movimiento del input sea válido.

CREATE TABLE pogo_window (
 window_key TEXT PRIMARY KEY,
 encounter_key TEXT NOT NULL REFERENCES encounter(encounter_key),
 species_id INTEGER NOT NULL,
 form_id INTEGER NOT NULL,
 start_day TEXT,
 end_day TEXT,
 encounter_type TEXT NOT NULL,
 shiny_state TEXT NOT NULL CHECK(shiny_state IN ('Never','Possible','Always','Unknown')),
 gender_rule TEXT,
 import_route TEXT NOT NULL,
 constraints_json TEXT NOT NULL CHECK(json_valid(constraints_json)),
 source_ref TEXT NOT NULL,
 FOREIGN KEY(species_id,form_id) REFERENCES species_form(species_id,form_id),
 CHECK(start_day IS NULL OR length(start_day)=10),
 CHECK(end_day IS NULL OR length(end_day)=10),
 CHECK(start_day IS NULL OR end_day IS NULL OR start_day<=end_day)
) STRICT;
-- Días ISO yyyy-mm-dd. Verificar semántica/calendario en importer.
-- NULL=desconocido o extremo abierto según descriptor, no inventar fechas.

CREATE TABLE operation (
 operation_id TEXT PRIMARY KEY,
 release_id TEXT NOT NULL REFERENCES release(release_id),
 snapshot_id TEXT NOT NULL REFERENCES dataset_snapshot(snapshot_id),
 started_utc TEXT NOT NULL,
 finished_utc TEXT,
 target_game INTEGER NOT NULL REFERENCES game(game_id),
 selection_kind TEXT NOT NULL CHECK(selection_kind IN ('Editor','Box')),
 target_context_json TEXT NOT NULL CHECK(json_valid(target_context_json)),
 original_type TEXT NOT NULL,
 original_sha256 TEXT NOT NULL CHECK(length(original_sha256)=64),
 original_entity BLOB NOT NULL,
 candidate_type TEXT,
 candidate_sha256 TEXT CHECK(candidate_sha256 IS NULL OR length(candidate_sha256)=64),
 candidate_entity BLOB,
 status TEXT NOT NULL CHECK(status IN
  ('Running','Success','AlreadyLegal','NoSolution','UnsupportedSave','UnsupportedData','Timeout',
   'Cancelled','VersionMismatch','InvalidInput','TargetChanged','ValidationFailed','InternalError')),
 search_complete INTEGER NOT NULL DEFAULT 0 CHECK(search_complete IN (0,1)),
 attempts INTEGER NOT NULL DEFAULT 0 CHECK(attempts>=0),
 elapsed_ms INTEGER CHECK(elapsed_ms>=0),
 input_request_json TEXT NOT NULL CHECK(json_valid(input_request_json)),
 response_json TEXT CHECK(response_json IS NULL OR json_valid(response_json)),
 applied_utc TEXT,
 reverted_utc TEXT,
 CHECK((candidate_entity IS NULL AND candidate_sha256 IS NULL AND candidate_type IS NULL) OR
       (candidate_entity IS NOT NULL AND candidate_sha256 IS NOT NULL AND candidate_type IS NOT NULL)),
 CHECK(status IN ('Success','AlreadyLegal') OR candidate_entity IS NULL),
 CHECK(applied_utc IS NULL OR (status='Success' AND candidate_entity IS NOT NULL))
) STRICT;
-- Guardar original/candidate y auditar SHA en aplicación.
-- Fallos no incluyen candidato listo para aplicación.
-- Intentos inválidos se registran en validation, no en candidate_entity.

CREATE TABLE change (
 operation_id TEXT NOT NULL REFERENCES operation(operation_id) ON DELETE CASCADE,
 ordinal INTEGER NOT NULL CHECK(ordinal>=0),
 field_name TEXT NOT NULL,
 before_json TEXT NOT NULL CHECK(json_valid(before_json)),
 after_json TEXT NOT NULL CHECK(json_valid(after_json)),
 reason_code TEXT NOT NULL,
 source_ref TEXT NOT NULL,
 PRIMARY KEY(operation_id,ordinal)
) STRICT;

CREATE TABLE validation (
 validation_id TEXT PRIMARY KEY,
 operation_id TEXT REFERENCES operation(operation_id) ON DELETE CASCADE,
 release_id TEXT NOT NULL REFERENCES release(release_id),
 snapshot_id TEXT NOT NULL REFERENCES dataset_snapshot(snapshot_id),
 phase TEXT NOT NULL CHECK(phase IN ('Original','Attempt','RoundTrip','PreCommit','Applied','SaveReopen','Fixture')),
 candidate_sha256 TEXT NOT NULL CHECK(length(candidate_sha256)=64),
 encounter_key TEXT REFERENCES encounter(encounter_key),
 legal INTEGER NOT NULL CHECK(legal IN (0,1)),
 destination_compatible INTEGER NOT NULL CHECK(destination_compatible IN (0,1)),
 report_json TEXT NOT NULL CHECK(json_valid(report_json)),
 rule_profile_sha256 TEXT NOT NULL CHECK(length(rule_profile_sha256)=64),
 validated_utc TEXT NOT NULL
) STRICT;

CREATE TABLE coverage_case (
 case_key TEXT PRIMARY KEY,
 release_id TEXT NOT NULL REFERENCES release(release_id),
 game_id INTEGER NOT NULL REFERENCES game(game_id),
 species_id INTEGER NOT NULL,
 form_id INTEGER NOT NULL,
 encounter_kind TEXT NOT NULL,
 variant_key TEXT NOT NULL,
 expected TEXT NOT NULL CHECK(expected IN ('Legalizable','AlreadyLegal','Rejected','Unknown')),
 evidence_ref TEXT NOT NULL,
 fixture_sha256 TEXT REFERENCES artifact(artifact_sha256),
 FOREIGN KEY(species_id,form_id) REFERENCES species_form(species_id,form_id),
 UNIQUE(release_id,game_id,species_id,form_id,encounter_kind,variant_key)
) STRICT;

CREATE TABLE coverage_result (
 case_key TEXT NOT NULL REFERENCES coverage_case(case_key),
 snapshot_id TEXT NOT NULL REFERENCES dataset_snapshot(snapshot_id),
 run_id TEXT NOT NULL,
 result TEXT NOT NULL CHECK(result IN ('Passed','Failed','Skipped','Untested')),
 validation_id TEXT REFERENCES validation(validation_id),
 reason TEXT NOT NULL,
 measured_utc TEXT,
 elapsed_ms INTEGER CHECK(elapsed_ms>=0),
 PRIMARY KEY(case_key,snapshot_id,run_id),
 CHECK(result!='Passed' OR validation_id IS NOT NULL)
) STRICT;

CREATE INDEX ix_encounter_lookup ON encounter(species_id,form_id,origin_game,kind);
CREATE INDEX ix_encounter_source ON encounter(source_version_id,approved_for_generation);
CREATE INDEX ix_availability ON availability(game_id,species_id,form_id,release_id);
CREATE INDEX ix_pogo_window ON pogo_window(species_id,form_id,start_day,end_day);
CREATE INDEX ix_operation_time ON operation(started_utc);
CREATE INDEX ix_validation_operation ON validation(operation_id,phase);
CREATE INDEX ix_coverage_tuple ON coverage_case(species_id,form_id,game_id,encounter_kind);
CREATE INDEX ix_coverage_result ON coverage_result(snapshot_id,run_id,result);
