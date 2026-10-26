-- 0003 Libreria (requisito R-003): entradas de libreria por destino, DERIVADAS y reconstruibles a partir de la matriz de cobertura, el Core y los eventos.
--   * library_entry: una fila por tupla {destino, especie, forma, clase de origen, variante}, con nombre en español, disponibilidad y motivo si esta atenuada.
--   * library_search: indice FTS5 (sin tildes, por prefijos) por nombre en español/otros idiomas, forma, titulo de evento (en español cuando existe), id de carta y juego.
--   * Barrera: solo una entrada con ancla en v_encounter_generable (o un huevo anclado en el Core) puede ser "addable".
-- SPDX-License-Identifier: GPL-3.0-or-later

CREATE TABLE library_entry (
 entry_key TEXT PRIMARY KEY,
 release_id TEXT NOT NULL REFERENCES release(release_id),
 game_id INTEGER NOT NULL REFERENCES game(game_id),
 game_name TEXT NOT NULL,
 species_id INTEGER NOT NULL,
 form_id INTEGER NOT NULL,
 dex_number INTEGER NOT NULL,
 name_es TEXT NOT NULL,
 names_all TEXT NOT NULL,
 form_name TEXT,
 generation INTEGER,
 kind TEXT NOT NULL,
 variant_key TEXT NOT NULL,
 availability TEXT NOT NULL CHECK(availability IN ('Available','Dimmed')),
 dim_reason TEXT,
 addable INTEGER NOT NULL CHECK(addable IN (0,1)),
 form_storage TEXT NOT NULL,
 status TEXT NOT NULL CHECK(status IN ('Planned','Untested')),
 expected TEXT NOT NULL,
 encounter_key TEXT REFERENCES encounter(encounter_key),
 source_label TEXT NOT NULL,
 event_title TEXT,
 event_title_es TEXT,
 event_card_id TEXT,
 event_language TEXT,
 event_restrict_language TEXT,
 event_start_date TEXT,
 FOREIGN KEY(species_id, form_id) REFERENCES species_form(species_id, form_id),
 CHECK(availability = 'Available' OR (addable = 0 AND dim_reason IS NOT NULL)),
 CHECK(addable = 0 OR (availability = 'Available' AND expected = 'Legalizable' AND status = 'Untested')),
 CHECK(addable = 0 OR encounter_key IS NOT NULL OR kind = 'Egg')
) STRICT;

CREATE INDEX ix_library_game_species ON library_entry(game_id, species_id, form_id);
CREATE INDEX ix_library_game_kind ON library_entry(game_id, kind, availability, species_id);
CREATE INDEX ix_library_dex ON library_entry(dex_number, game_id);
CREATE INDEX ix_library_name ON library_entry(name_es COLLATE NOCASE, game_id);
CREATE INDEX ix_library_generation ON library_entry(generation, kind, species_id);
CREATE INDEX ix_library_event_date ON library_entry(event_start_date) WHERE event_start_date IS NOT NULL;
CREATE INDEX ix_library_event_lang ON library_entry(event_language, game_id) WHERE event_language IS NOT NULL;
CREATE INDEX ix_library_anchor ON library_entry(encounter_key) WHERE encounter_key IS NOT NULL;

CREATE VIRTUAL TABLE library_search USING fts5(
 name_es, names_all, form_name, event_title, event_title_es, event_card_id, game_name,
 content = 'library_entry', content_rowid = 'rowid',
 tokenize = 'unicode61 remove_diacritics 2', prefix = '2 3 4'
);

-- Una entrada "addable" solo puede apoyarse en un encuentro aprobado para generar (misma barrera que la regla 8, tambien en la libreria).
CREATE TRIGGER trg_library_addable_ins BEFORE INSERT ON library_entry
WHEN NEW.addable = 1 AND NEW.encounter_key IS NOT NULL AND NOT EXISTS (SELECT 1 FROM v_encounter_generable e WHERE e.encounter_key = NEW.encounter_key)
BEGIN SELECT RAISE(ABORT, 'entrada de libreria addable sin encuentro aprobado para generar'); END;

CREATE TRIGGER trg_library_addable_upd BEFORE UPDATE ON library_entry
WHEN NEW.addable = 1 AND NEW.encounter_key IS NOT NULL AND NOT EXISTS (SELECT 1 FROM v_encounter_generable e WHERE e.encounter_key = NEW.encounter_key)
BEGIN SELECT RAISE(ABORT, 'entrada de libreria addable sin encuentro aprobado para generar'); END;
