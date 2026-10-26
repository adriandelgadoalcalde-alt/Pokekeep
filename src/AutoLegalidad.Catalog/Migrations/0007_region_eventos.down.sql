-- 0007 (rollback): quita la region de los eventos.
-- SPDX-License-Identifier: GPL-3.0-or-later
ALTER TABLE library_entry DROP COLUMN event_region_source;
ALTER TABLE library_entry DROP COLUMN event_region;
DROP TABLE IF EXISTS event_region;
