-- 0006 Hito 5 (datos): ventanas de distribucion de eventos WC6/WC7 sacadas de las hojas Extras/Tracking de EventsGallery.
--   * event_window: SOLO metadatos derivados (inicio y fin de la distribucion, procedencia y hash del fichero); ningun byte ni texto de EventsGallery (ADR 0005, ADR 0029).
--     Solo para tarjetas cuyo descriptor NO trae fecha de inicio (la tarjeta «full» WC6/WC7 lleva el marcador «hoy» del Core). end_day NULL = fin desconocido o abierto.
--     Es un dato de HISTORIA (R-010): no interviene en la validez de nada ni en que tarjetas se pueden generar.
-- SPDX-License-Identifier: GPL-3.0-or-later

CREATE TABLE event_window (
 encounter_key TEXT NOT NULL PRIMARY KEY REFERENCES encounter(encounter_key),
 start_day TEXT NOT NULL CHECK(start_day GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
 end_day TEXT CHECK(end_day IS NULL OR (end_day GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]' AND end_day >= start_day)),
 source_ref TEXT NOT NULL,
 file_sha256 TEXT NOT NULL CHECK(length(file_sha256)=64)
) STRICT;
