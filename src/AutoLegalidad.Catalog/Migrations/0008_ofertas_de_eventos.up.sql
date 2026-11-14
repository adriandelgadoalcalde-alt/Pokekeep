-- 0008 R-014 («Añadir evento» en la casilla vacía): que eventos ofrece cada juego.
--   * event_offer: una fila por {juego de destino, tarjeta}. La tarjeta es una clave de encounter (un evento de Gen 3 a 9, o un reparto de Gen 1-2). via:
--       'nativo'        = el juego puede recibir la tarjeta (los bits de PCD.CardCompatibility, CanBeReceivedByVersion, la version de la plantilla del Core...).
--       'transferencia' = la tarjeta se recibe en un juego anterior y llega al destino convertida con EntityConverter (Pal Park, Poke Transfer, Banco, Consola Virtual, HOME).
--     Entra SOLO si LegalityAnalysis del Core da por valido el Pokemon creado con un entrenador de ese juego: ninguna tabla escrita a mano.
--     home_required = 1 cuando solo es valido con el ajuste R-009 (matricula de HOME ausente), igual que library_entry.home_required.
--     origin_game = el juego en el que se recibe la tarjeta (el propio destino si es nativo). entry_key = la fila de la Libreria que la representa (sin clave foranea: la compactacion
--     movil convierte library_entry en una vista); NULL si la tarjeta no tiene fila en ninguna Libreria (entonces la interfaz no la enseña).
-- SPDX-License-Identifier: GPL-3.0-or-later

CREATE TABLE event_offer (
 game_id INTEGER NOT NULL REFERENCES game(game_id),
 encounter_key TEXT NOT NULL REFERENCES encounter(encounter_key),
 via TEXT NOT NULL CHECK(via IN ('nativo','transferencia')),
 home_required INTEGER NOT NULL DEFAULT 0 CHECK(home_required IN (0,1)),
 origin_game INTEGER NOT NULL REFERENCES game(game_id),
 entry_key TEXT,
 PRIMARY KEY(game_id, encounter_key)
) STRICT, WITHOUT ROWID;

CREATE INDEX ix_event_offer_game_via ON event_offer(game_id, via);
