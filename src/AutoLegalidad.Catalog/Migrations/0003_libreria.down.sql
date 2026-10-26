-- 0003 (rollback): elimina la libreria derivada y su indice de busqueda.
-- SPDX-License-Identifier: GPL-3.0-or-later
DROP TRIGGER IF EXISTS trg_library_addable_upd;
DROP TRIGGER IF EXISTS trg_library_addable_ins;
DROP TABLE IF EXISTS library_search;
DROP INDEX IF EXISTS ix_library_anchor;
DROP INDEX IF EXISTS ix_library_event_lang;
DROP INDEX IF EXISTS ix_library_event_date;
DROP INDEX IF EXISTS ix_library_generation;
DROP INDEX IF EXISTS ix_library_name;
DROP INDEX IF EXISTS ix_library_dex;
DROP INDEX IF EXISTS ix_library_game_kind;
DROP INDEX IF EXISTS ix_library_game_species;
DROP TABLE IF EXISTS library_entry;
