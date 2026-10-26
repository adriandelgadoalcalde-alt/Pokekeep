-- 0007 Hito 5d (ADR 0047): region de distribucion de los eventos, SOLO cuando una fuente la aporta de verdad.
--   * event_region: por tarjeta, los codigos de region entre comas (',US,EU,JP,'; INT = internacional sin Japon en Gen 3), de donde salen (core = tablas de Gen 3 del Core;
--     eventsgallery = columnas US/EU/JP/TW/KO de las hojas de seguimiento de WC6/WC7), la referencia exacta y, si viene de una hoja, el hash del fichero. Ningun texto de EventsGallery (ADR 0005).
--   * library_entry.event_region / event_region_source: lo que enseña la Libreria (vacio = ninguna fuente da la region; nunca se deduce del idioma).
-- SPDX-License-Identifier: GPL-3.0-or-later

CREATE TABLE event_region (
 encounter_key TEXT NOT NULL PRIMARY KEY REFERENCES encounter(encounter_key),
 regions TEXT NOT NULL CHECK(regions GLOB ',*,' AND length(regions) > 2),
 source TEXT NOT NULL CHECK(source IN ('core','eventsgallery')),
 source_ref TEXT NOT NULL,
 file_sha256 TEXT CHECK(file_sha256 IS NULL OR length(file_sha256)=64)
) STRICT;

ALTER TABLE library_entry ADD COLUMN event_region TEXT;
ALTER TABLE library_entry ADD COLUMN event_region_source TEXT;
