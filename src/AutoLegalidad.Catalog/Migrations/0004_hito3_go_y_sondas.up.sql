-- 0004 Hito 3: cruce de las ventanas de GO con las tablas GO del Core fijado y resultados de las sondas de la matriz de cobertura.
--   * pogo_window.core_match / core_route: si el Core fijado reproduce la ventana (Identical | DatesOffByOneDay) o no (PoGoOnly) y por que ruta (HOME, LGPE o ambas).
--     El Core es la autoridad (regla 12): una ventana PoGoOnly NO se usa para generar.
--   * coverage_probe: resultado MEDIDO de una tupla de la matriz (sonda del Hito 3) con el vocabulario de estados del proyecto. Las tuplas no medidas no tienen fila
--     (nunca se rellena una laguna); coverage_result sigue vacia (el Hito 5 certificara con validaciones completas).
-- SPDX-License-Identifier: GPL-3.0-or-later

ALTER TABLE pogo_window ADD COLUMN core_match TEXT NOT NULL DEFAULT 'NotChecked' CHECK(core_match IN ('NotChecked','Identical','DatesOffByOneDay','PoGoOnly'));
ALTER TABLE pogo_window ADD COLUMN core_route TEXT;

CREATE TABLE coverage_probe (
 case_key TEXT NOT NULL REFERENCES coverage_case(case_key),
 run_id TEXT NOT NULL,
 action TEXT NOT NULL CHECK(action IN ('Legalize','Optimize')),
 state TEXT NOT NULL CHECK(state IN ('OBSERVED','NOT_OBSERVED','INCONCLUSIVE','NOT_APPLICABLE','BLOCKED','ERROR')),
 result TEXT NOT NULL,
 elapsed_ms INTEGER NOT NULL CHECK(elapsed_ms>=0),
 encounter_type TEXT,
 encounter_key TEXT,
 reason TEXT,
 measured_utc TEXT NOT NULL,
 PRIMARY KEY(case_key, run_id, action)
) STRICT;

CREATE INDEX ix_coverage_probe_state ON coverage_probe(run_id, state);

-- Ultima medicion de cada tupla y accion.
CREATE VIEW v_coverage_probe_latest AS
  SELECT p.* FROM coverage_probe p
   WHERE p.measured_utc = (SELECT max(q.measured_utc) FROM coverage_probe q WHERE q.case_key = p.case_key AND q.action = p.action);
