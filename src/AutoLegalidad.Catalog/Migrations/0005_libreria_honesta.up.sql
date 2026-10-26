-- 0005 Hito 3c: la Libreria solo OFRECE lo que el motor genera (R-003, R-009).
--   * library_entry.home_required: la entrada solo existe pasando por Pokemon HOME (GO hacia los juegos de Switch). Sigue siendo addable (el motor la genera con el ajuste
--     explicito R-009 «sin matricula»); la interfaz la ofrece solo con ese ajuste activado y, si no, la atenua con su motivo.
--   * library_entry.dim_source: de donde sale el motivo de una atenuacion o de un bloqueo (catalog = regla sobre los datos del catalogo; measured = imposibilidad demostrada por
--     el banco del Hito 3b, docs/datos/libreria-no-generables.tsv).
-- SPDX-License-Identifier: GPL-3.0-or-later

ALTER TABLE library_entry ADD COLUMN home_required INTEGER NOT NULL DEFAULT 0 CHECK(home_required IN (0,1));
ALTER TABLE library_entry ADD COLUMN dim_source TEXT CHECK(dim_source IS NULL OR dim_source IN ('catalog','measured'));
-- Hito 3c (titulos de eventos en español, sin inventar nada): de donde sale event_title_es. card = la propia tarjeta esta en español; spanish-marks = el titulo lleva los signos propios del
-- castellano (¡ ¿); same-event = otra tarjeta del MISMO evento (mismo tipo, id, especie y forma) esta en español. Si no hay fuente fiable, event_title_es queda vacio.
ALTER TABLE library_entry ADD COLUMN event_title_es_source TEXT CHECK(event_title_es_source IS NULL OR event_title_es_source IN ('card','spanish-marks','same-event'));
-- Hito 3c (sprite correcto): lo que cambia el sprite de un evento. shiny_rule = regla de variocolor de la tarjeta (Never, Random, Always, AlwaysStar, AlwaysSquare); fixed_gender = sexo fijo de la
-- tarjeta (0 macho, 1 hembra, 2 sin sexo; NULL = no fijo); gigantamax = la tarjeta reparte un Pokemon Gigamax. Solo los eventos los traen: el resto sale con el sprite base (variocolor y sexo aleatorios).
ALTER TABLE library_entry ADD COLUMN shiny_rule TEXT;
ALTER TABLE library_entry ADD COLUMN fixed_gender INTEGER CHECK(fixed_gender IS NULL OR fixed_gender IN (0,1,2));
ALTER TABLE library_entry ADD COLUMN gigantamax INTEGER NOT NULL DEFAULT 0 CHECK(gigantamax IN (0,1));
