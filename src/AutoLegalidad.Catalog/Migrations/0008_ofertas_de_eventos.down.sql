-- 0008 (rollback): quita la tabla de ofertas de eventos.
-- SPDX-License-Identifier: GPL-3.0-or-later
DROP INDEX IF EXISTS ix_event_offer_game_via;
DROP TABLE IF EXISTS event_offer;
