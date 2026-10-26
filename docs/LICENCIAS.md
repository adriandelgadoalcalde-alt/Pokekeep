# Inventario de licencias (Hito 0)

Comprobado el 3-oct-2026 con el `LICENSE` de cada fuente fijada (ver `fuentes.lock.json`), la API de GitHub y los `.nuspec` de la caché NuGet. **Esto es un inventario técnico, no asesoramiento legal.**

## Componentes de terceros

| Componente | Commit/versión fijada | Licencia | Uso en Pokekeep | Obligaciones / cautelas |
|---|---|---|---|---|
| **PKHeX** (kwsch) | 26.10.18 `bfe89c74...` | **GPL-3.0-or-later** (`LICENSE` = GPLv3; `PKHeX.Core.csproj`: `GPL-3.0-or-later`). GitHub la muestra como "Other" porque el fichero está en HTML-escapado, pero el texto es GPLv3. | Base del producto (Core + WinForms). | La distribución del portable es una obra derivada: debe ofrecerse **código fuente correspondiente** y licencia GPLv3, conservar avisos de copyright y marcar los cambios propios. Por tanto **todo el producto distribuido (Pokekeep + PKHeX modificado) queda bajo GPLv3**. Material incluido por PKHeX con licencia propia: QRCoder (MIT) y pokesprite (MIT) según su README. Datos de Pokémon/Nintendo: marcas y contenido de sus propietarios. |
| **ALM / PKHeX-Plugins** (santacrab2, fork de architdate) | 26.10.18 `b13c011b...` | **MIT** (`LICENSE`, "Copyright (c) 2018 Archit Date") | Motor de legalización encapsulado tras un adaptador aislado (`AutoLegalidad.ALMAdapter`, Hito 2). | Conservar aviso MIT y de copyright. Compatible con GPLv3 al combinarlo con PKHeX. Dependencias de su plugin (Costura.Fody, Fody: MIT) solo aplican si se distribuye el plugin WinForms de ALM, que no es el plan. LibUsbDotNet (Injection) NO se usa. |
| PKHeX-Plugins-actual (rama `cherrytree`) y PKHeX-Plugins (architdate) | `90410f26...` / `c8e23a43...` | MIT | Solo lectura/referencia. | No se redistribuyen. |
| **PoGoEncTool** (projectpokemon) | `2ca0b541...` | **AGPL-3.0** (`LICENSE` y API de GitHub) | **Solo como datos o herramienta aparte** (ventanas y datos GO exportables para PKHeX). | NO copiar su código al producto. La AGPL añade la cláusula de red: si se modifica y se ofrece como servicio, hay que ofrecer fuente. Usarla como programa independiente y tratar sus datos de salida con atribución; revisar con calma antes de empaquetar sus ficheros de datos (Hito 1/3). |
| **EventsGallery** (projectpokemon) | `154d81be...` | **Sin licencia declarada** (no hay `LICENSE` en el repo y GitHub no detecta ninguna). | Fuente de datos de eventos (solo `Released`, con allowlist; Unreleased/ResearchOnly/Unknown excluidos). | Por defecto "todos los derechos reservados" del autor/contenido: **no redistribuir** el repo ni sus ficheros sin permiso; usar localmente como entrada de import con hash y procedencia, y no incluirlos en repos públicos ni en el ZIP hasta resolver su estatus (**pendiente de decisión del usuario**: pedir permiso o limitarse a datos reconstruibles desde el Core). Los datos en sí son contenido de juegos Nintendo/Game Freak. |
| **pokemon-showdown** (smogon) | `3661ce40...` | **MIT** | Referencia para importar sets; no valida legalidad de PKHeX. | Aviso MIT si se copia algo (no se prevé). |

## Dependencias de compilación y pruebas (NuGet, Hito 0)

| Paquete | Versión | Licencia | Notas |
|---|---|---|---|
| Microsoft.NET.Test.Sdk (+ CodeCoverage, TestPlatform) | 18.0.1 | MIT | solo pruebas |
| xunit, xunit.core, xunit.assert, xunit.analyzers, xunit.runner.visualstudio | 2.9.3 / 1.18.0 / 3.1.5 | Apache-2.0 | solo pruebas |
| Newtonsoft.Json | 13.0.3 | MIT | dependencia transitiva del TestPlatform |
| Microsoft.Data.Sqlite (+ .Core) | 10.0.12 | MIT | Hito 1: acceso a la base de datos del catalogo (dependencia de produccion, version exacta + lockfile) |
| SQLitePCLRaw.* (bundle_e_sqlite3, lib.e_sqlite3, provider) | 2.1.12 | Apache-2.0 (SQLite: dominio publico) | Hito 1: motor SQLite 3.53.3 con FTS5, JSON y STRICT |
| **FluentAssertions** | 8.8.0 (PKHeX.Core.Tests) / 8.9.0 (ALM AutoModTests) | **Xceed Community License, solo uso NO comercial** (`LICENSE` del paquete) | **Se EXCLUYE de Pokekeep**: usamos `Xunit.Assert`. Se usa únicamente al *ejecutar* las suites de terceros en local; no se distribuye. |
| PKHeX.Core (NuGet) | 26.8.26 | GPL-3.0-or-later | solo para compilar ALM con su referencia original (comparación) |

`PKHeX.Core` en sí no tiene dependencias NuGet.

## Código propio

Pokekeep (`src/`, `tests/`, `docs/`, `herramientas/`): **GPL-3.0-or-later** (decidido en el Hito 1, ADR 0005): `LICENSE` (texto oficial), `NOTICE.md` (atribuciones) y cabecera SPDX en cada fichero de código. Los datos generados desde recursos oficiales del Core heredan GPLv3.

## Marcas y contenido

Pokémon y sus nombres, formas y datos son marcas/contenido de Nintendo, Game Freak y The Pokémon Company. Pokekeep es herramienta no oficial y no afiliada. No se distribuyen ROMs, saves, claves ni binarios de juego.

## Acciones abiertas

1. ~~Decidir licencia final del repo propio~~ **Hecho (Hito 1)**: GPL-3.0-or-later; `LICENSE`, `NOTICE.md` y cabeceras.
2. **EventsGallery** (sin licencia declarada): decidido (ADR 0005): ningún fichero suyo va a Git (solo hashes y metadatos en `docs/datos/eventsgallery-manifest.tsv`); la BD construida con sus datos va en el ZIP y en el APK, con atribución a Project Pokémon. Desde el 24-oct-2026 se publican abiertamente por decisión del propietario (ADR 0104), con un aviso que explica cómo pedir que se retiren.
3. **PoGoEncTool** (AGPL-3.0): solo se usan sus DATOS (`Resources/data.json`), con atribución, y no se copia ni se enlaza su código (Hito 1). Mismo tratamiento que EventsGallery respecto al repositorio y al paquete; revisar el encaje de la AGPL antes de cualquier redistribución pública.
4. Revisar en el Hito 6 el código fuente correspondiente exigido por la GPL para el binario distribuido y los avisos de terceros del paquete.
