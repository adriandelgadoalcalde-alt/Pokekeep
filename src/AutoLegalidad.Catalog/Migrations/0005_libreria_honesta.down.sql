-- 0005 (rollback): quita las columnas de la Libreria honesta.
-- SPDX-License-Identifier: GPL-3.0-or-later
ALTER TABLE library_entry DROP COLUMN gigantamax;
ALTER TABLE library_entry DROP COLUMN fixed_gender;
ALTER TABLE library_entry DROP COLUMN shiny_rule;
ALTER TABLE library_entry DROP COLUMN event_title_es_source;
ALTER TABLE library_entry DROP COLUMN dim_source;
ALTER TABLE library_entry DROP COLUMN home_required;
