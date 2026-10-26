# Notices and attributions / Avisos y atribuciones

*[Español](#avisos-y-atribuciones-de-pokekeep)*

# Pokekeep notices and attributions

Pokekeep is free software: **GNU General Public License v3.0 or later** (`GPL-3.0-or-later`, see `LICENSE`). It is a derivative and
combined work with PKHeX, which is GPL-3.0-or-later, so all of Pokekeep’s own code is released under that same license.

Each file of our own code starts with this header:

```
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
```

## Third parties

| Component | License | How it is used |
|---|---|---|
| PKHeX (kwsch and contributors) | GPL-3.0-or-later | Base of the product: `PKHeX.Core` is built from the pinned commit (`fuentes.lock.json`). No change to the legality rules. |
| PKHeX-Plugins / Auto Legality Mod (santacrab2, architdate) | MIT | Legalization engine, wrapped behind an isolated adapter. |
| Microsoft.Data.Sqlite and SQLitePCLRaw | MIT / Apache-2.0 (SQLite is public domain) | Access to the catalog database. |
| xunit | Apache-2.0 | Tests only; the tests are not in the public repository. |
| Project Pokémon: EventsGallery | **No license declared** | Source of event data. The Pokekeep repository **contains none of its files**, only hashes and provenance metadata. The portable package and the APK include a database derived from it, with credit to Project Pokémon. Since 24 October 2026 that derived data is published openly, by the author’s decision; if its authors disagree, it is removed. |
| Project Pokémon: PoGoEncTool | AGPL-3.0 | Only its **data** (Pokémon GO windows, `Resources/data.json`); its code is neither copied nor linked. Credit to Project Pokémon. Same treatment as EventsGallery for the repository and the package. |
| PokeAPI/cries (cries) | Repository: CC0 1.0. **Audio: © The Pokémon Company** (its README says so: “All audio files contents within are Copyright The Pokémon Company”) | The cries the Android app ships with. See the paragraph below. |
| PokeAPI/sprites (extra items) | Repository: CC0 1.0 (`LICENCE.txt`). **Art: © The Pokémon Company** (the first line of its `LICENCE.txt` says so: “All image contents within are Copyright The Pokémon Company”) | TM/HM/TR with the icon of their type, and items PKHeX does not draw (X14). See the paragraph below. |

The pixel-art scaler of `Pokekeep.Sprites` (`EscaladorPixelArt`) is our own implementation of the Scale2x/EPX algorithm (Andrea
Mazzoleni, Eric Johnston; a public-domain algorithm). It includes no third-party code.

The supplementary presentation images come from [PokeAPI/sprites](https://github.com/PokeAPI/sprites), pinned revision
`0dc105d5b12416932f29b594078282ab4be045f5`: a fifth-generation-style collection, with community contributions for later species. They
are not presented as official images of the modern games. The PNG files are kept unmodified, bundled in the app and left out of Git;
their identifiers, URLs and hashes are in `docs/android/sprites-adicionales.tsv`. The credit and the restrictions on Pokémon content
still apply.

The app’s bundled cries (Android, X11) come from [PokeAPI/cries](https://github.com/PokeAPI/cries), pinned revision
`b3a9044bb839b5e3152aaa536c7b463a0fcf0140`, with the identifier tables `pokemon.csv` / `pokemon_forms.csv` from
[PokeAPI/pokeapi](https://github.com/PokeAPI/pokeapi), pinned revision `c80757193bd0889054e36f4209762360bdaa4b95` (the same one the
supplementary images use). The cries repository is distributed under CC0 1.0, **but its own README states that all the audio is ©
The Pokémon Company**: the audio belongs neither to Pokekeep nor to PokeAPI, and it is treated like the sprites that PKHeX carries
(content of The Pokémon Company). Bundling them is a decision of the author (13 October 2026). The files are packed WITHOUT
re-encoding (PokeAPI stores some MP3 and WAV files with an `.ogg` extension; the content is kept as it is) into a package that
`herramientas/generar-gritos-android.ps1` generates locally, outside Git (`datos/` and `compilacion/` are not versioned). The
provenance of each sound (key, PokeAPI identifier, format, SHA-256, revision and URL) is in `docs/android/gritos-procedencia.tsv`,
and the PKHeX forms that PokeAPI does not tell apart (they sound like their species) are in `docs/android/gritos-sin-forma-propia.tsv`.
The classic Gen. 1-5 cries are used only for saves of those generations.

The extra item icons (Android, X14) come from [PokeAPI/sprites](https://github.com/PokeAPI/sprites) (`sprites/items/`: root, `gen5/`,
`gen8/` and `gen9/`), pinned revision `0dc105d5b12416932f29b594078282ab4be045f5` (12 October 2026), matched by name against
`items.csv` / `item_names.csv` from [PokeAPI/pokeapi](https://github.com/PokeAPI/pokeapi), pinned revision
`c80757193bd0889054e36f4209762360bdaa4b95`. The repository is distributed under CC0 1.0, **but its `LICENCE.txt` states that all the
image content is © The Pokémon Company**: the art belongs neither to Pokekeep nor to PokeAPI, and it is treated like PKHeX’s sprites
and the X11 cries. PKHeX always goes first: PokeAPI only fills in what PKHeX does not draw and gives the TYPE icon to TMs, HMs and TRs
(PokeAPI has no TR art: the type’s TM disc is used, a stated approximation). The PNG files are packed WITHOUT re-encoding into a
package generated locally and kept out of Git (`herramientas/generar-objetos-android.ps1`); the provenance of each image (key,
identifier, folder, SHA-256, revisions and URL) is in `docs/android/sprites-objetos-procedencia.tsv`.

X18 extends the item icons with [msikma/pokesprite](https://github.com/msikma/pokesprite) (revision
`c5aaa610ff2acdf7fd8e2dccd181bca8be9fcb3e`, code under MIT; the art is © The Pokémon Company),
[pokesprites/all](https://github.com/pokesprites/all) (revision `48125ae5edc35663170f46cc325a1575185727cc`, HD icons from Scarlet and
Violet, no license declared; art © The Pokémon Company) and the `gen3/` folder of PokeAPI/sprites. Provenance and SHA-256 of each image
are in `docs/android/sprites-objetos-procedencia.tsv`; the categories, row by row, in `docs/android/sprites-objetos-matriz.tsv`. No
permission to redistribute is inferred from the fact that these repositories are publicly accessible.

X27 adds the item art of [pret/pokeemerald](https://github.com/pret/pokeemerald/tree/731ad5bfd6e6f265508d0efcca0ba42f9dcf5881/graphics/items)
and [pret/pokefirered](https://github.com/pret/pokefirered/tree/037335f4c725d7c9aecdac87066f2002b4bd7e14/graphics/items), combining the
PNG indices with the palette that the game’s native table assigns. No ROM has been copied. The graphics remain © The Pokémon Company /
Nintendo / Game Freak; those repositories do not declare a general license for the art. The licenses of the conversion tools are not
attributed to the graphics. Provenance, revisions, graphic/palette/result hashes and an independent pixel reference are in
`android/Pokekeep.Objetos.Build/pret-gen3.tsv`, `docs/android/sprites-objetos-procedencia.tsv` and
`docs/android/sprites-objetos-x27-fuentes.md`. The X18 criterion holds: the art is not in this repository, and no permission to
redistribute is inferred from public access.

X28 adds the Gen. 4 item art of [pret/pokeplatinum](https://github.com/pret/pokeplatinum/tree/fe1ea418a7364c93daf0d07ae55e195452d8803c),
[pret/pokeheartgold](https://github.com/pret/pokeheartgold/tree/6cedc4f57c9ce18821adbfff25c6aefbd0dc1176) and
[pret/pokediamond](https://github.com/pret/pokediamond/tree/b623abef1b4dfe204ca50aab42447ac00c134290), by native id and the game’s own
table. No ROM has been copied. Same conditions as X27: art © The Pokémon Company / Nintendo / Game Freak, no general license declared,
not in this repository, and no permission to redistribute inferred. Provenance is in `docs/android/sprites-objetos-ds-x28.tsv` and
`docs/android/sprites-objetos-x28-fuentes.md`. The conversion tool for the author’s own game dumps is only in the author’s working
repository, not in this one; the dumps are never versioned.

## Android app (1.0.0)

| Component | License | How it is used |
|---|---|---|
| .NET and .NET for Android (Microsoft) | MIT | App platform. |
| Avalonia UI 12.1.3 (AvaloniaUI) | MIT | User interface. |
| SkiaSharp 3.119.4 (Microsoft / Mono) | MIT | Rendering. |
| CommunityToolkit.Mvvm 8.4.2 (.NET Foundation) | MIT | View models. |
| QRCoder 1.7.0 | MIT | Pokémon QR codes. |
| ZXing.Net 0.16.9 | Apache-2.0 | Reading QR codes from an image. |
| Xamarin.AndroidX.Core.SplashScreen (Google / Microsoft) | Apache-2.0 | Splash screen. |
| Roboto (Google) | Apache-2.0 (`android/Pokekeep.Mobile.Ui/Assets/Fonts/LICENSE-Roboto.txt`) | Typeface. |
| Noto Symbols (The Noto Project Authors) | SIL OFL 1.1 (`LICENSE-PokekeepSymbols.txt`) | Interface icons, as the `PokekeepSymbols` font. |

**Content taken from the games.** The previews (pixel sprites of Gen. 1-5 and 3D renders from 3DS and Switch), the box icons, the eggs
and the title logos were extracted from the author’s own copies of the games (private dumps that are never versioned), without
altering the art. The colors of the type labels were measured on screenshots of Pokémon Scarlet
(`docs/android/tipos-colores-escarlata.json`). All of it is © Nintendo, Creatures Inc. and GAME FREAK inc. / The Pokémon Company. The
provenance and hash of the images listed in this repository are in `docs/android/*procedencia*.tsv`; those of the 3D previews and the
other images taken from the dumps are in the manifests that go with each package (generated, outside Git, and not part of this
repository).

**Box art.** The box art of the Spanish editions for GB, GBC, GBA, GameCube, Wii, DS and 3DS comes from
[WikiDex](https://www.wikidex.net/), with the page and URL of each one in the box-art manifest, which is not included in the public
repository. The box art is © Nintendo / The Pokémon Company.

**Distribution.** Pokekeep’s own code is GPL-3.0-or-later and can be redistributed under its terms. The Nintendo and The Pokémon
Company content that the APK and the portable package of the releases carry (images, sounds and data) is not Pokekeep’s and is not in
this repository: the author includes it in the releases by his own decision, together with the sources marked “no license declared”
above. Whoever builds from the source code does not get that content: the scripts download part of it from its sources, and what
comes from the author’s dumps cannot be reproduced.

---

# Avisos y atribuciones de Pokekeep

Pokekeep es software libre: **GNU General Public License v3.0 o posterior** (`GPL-3.0-or-later`, ver `LICENSE`). Es una obra
derivada y combinada con PKHeX, que es GPL-3.0-or-later; por eso todo el código propio de Pokekeep se publica bajo esa misma licencia.

Cada fichero de código propio lleva la cabecera:

```
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
```

## Terceros

| Componente | Licencia | Cómo se usa |
|---|---|---|
| PKHeX (kwsch y colaboradores) | GPL-3.0-or-later | Base del producto: `PKHeX.Core` se compila desde el commit fijado (`fuentes.lock.json`). Sin cambios de legalidad. |
| PKHeX-Plugins / Auto Legality Mod (santacrab2, architdate) | MIT | Motor de legalización encapsulado tras un adaptador aislado. |
| Microsoft.Data.Sqlite y SQLitePCLRaw | MIT / Apache-2.0 (SQLite es dominio público) | Acceso a la base de datos del catálogo. |
| xunit | Apache-2.0 | Solo pruebas; las pruebas no están en el repositorio público. |
| Project Pokémon: EventsGallery | **Sin licencia declarada** | Fuente de datos de eventos. El repositorio de Pokekeep **no contiene ninguno de sus ficheros**; solo hashes y metadatos de procedencia. El paquete portable y el APK incluyen una base de datos derivada de ella, con atribución a Project Pokémon. Desde el 24-oct-2026 esos datos derivados se publican abiertamente por decisión del autor; si sus autores no están de acuerdo, se retiran. |
| Project Pokémon: PoGoEncTool | AGPL-3.0 | Solo sus **datos** (ventanas de Pokémon GO, `Resources/data.json`); no se copia ni se enlaza su código. Atribución a Project Pokémon. Mismo tratamiento que EventsGallery respecto al repositorio y al paquete. |
| PokeAPI/cries (gritos) | Repositorio: CC0 1.0. **Audio: © The Pokémon Company** (lo dice su README: «All audio files contents within are Copyright The Pokémon Company») | Los gritos que la app lleva de serie en Android. Ver el párrafo de abajo. |
| PokeAPI/sprites (objetos adicionales) | Repositorio: CC0 1.0 (`LICENCE.txt`). **Arte: © The Pokémon Company** (lo dice la primera línea de su `LICENCE.txt`: «All image contents within are Copyright The Pokémon Company») | MT/MO/DR con el icono de su tipo y los objetos que PKHeX no dibuja (X14). Ver el párrafo de abajo. |

El escalador de pixel art de `Pokekeep.Sprites` (`EscaladorPixelArt`) es una implementación propia del algoritmo Scale2x/EPX (Andrea
Mazzoleni, Eric Johnston; algoritmo de dominio público); no incluye código de terceros.

Las imágenes suplementarias de presentación proceden de [PokeAPI/sprites](https://github.com/PokeAPI/sprites), revisión fijada
`0dc105d5b12416932f29b594078282ab4be045f5`: colección de estilo de quinta generación, con aportaciones de la comunidad para especies
posteriores. No se presentan como imágenes oficiales de los juegos modernos. Los PNG se conservan sin modificaciones, se empaquetan
en la app y quedan fuera de Git; sus identificadores, URLs y huellas están en `docs/android/sprites-adicionales.tsv`. La atribución
y las restricciones sobre el contenido Pokémon siguen siendo aplicables.

Los gritos de serie de la app (Android, X11) proceden de [PokeAPI/cries](https://github.com/PokeAPI/cries), revisión fijada
`b3a9044bb839b5e3152aaa536c7b463a0fcf0140`, con la tabla de identificadores `pokemon.csv` / `pokemon_forms.csv` de
[PokeAPI/pokeapi](https://github.com/PokeAPI/pokeapi), revisión fijada `c80757193bd0889054e36f4209762360bdaa4b95` (la misma que usan
los sprites adicionales). El repositorio de gritos se distribuye bajo CC0 1.0, **pero su propio README declara que todo el contenido
de audio es © The Pokémon Company**: el audio no es de Pokekeep ni de PokeAPI, y se trata igual que los sprites que PKHeX lleva
(contenido de The Pokémon Company). Incluirlos de serie es una decisión del autor (13-oct-2026). Los archivos se empaquetan SIN
recodificar (PokeAPI guarda algunos MP3 y WAV con extensión `.ogg`; se conserva el contenido tal cual) en un paquete que genera en
local `herramientas/generar-gritos-android.ps1`, fuera de Git (`datos/` y `compilacion/` no se versionan). La procedencia de cada
sonido (clave, identificador de PokeAPI, formato, SHA-256, revisión y URL) está en `docs/android/gritos-procedencia.tsv`, y las formas
de PKHeX que PokeAPI no distingue (suenan como su especie) en `docs/android/gritos-sin-forma-propia.tsv`. Los gritos clásicos de
Gen. 1-5 se usan solo para partidas de aquellas generaciones.

Los iconos de objetos adicionales de la app (Android, X14) proceden de [PokeAPI/sprites](https://github.com/PokeAPI/sprites)
(`sprites/items/`: raíz, `gen5/`, `gen8/` y `gen9/`), revisión fijada `0dc105d5b12416932f29b594078282ab4be045f5` (12-oct-2026),
cruzados por nombre con `items.csv` / `item_names.csv` de [PokeAPI/pokeapi](https://github.com/PokeAPI/pokeapi), revisión fijada
`c80757193bd0889054e36f4209762360bdaa4b95`. El repositorio se distribuye bajo CC0 1.0, **pero su `LICENCE.txt` declara que todo el
contenido de imagen es © The Pokémon Company**: el arte no es de Pokekeep ni de PokeAPI y se trata igual que los sprites de PKHeX y
los gritos de X11. PKHeX va siempre primero: PokeAPI solo rellena lo que PKHeX no dibuja y pone el icono de TIPO a las MT, MO y DR
(PokeAPI no trae arte de DR: se usa el disco de MT del tipo, aproximación declarada). Los PNG se empaquetan SIN recodificar en un
paquete generado en local y fuera de Git (`herramientas/generar-objetos-android.ps1`); la procedencia de cada imagen (clave,
identificador, carpeta, SHA-256, revisiones y URL) está en `docs/android/sprites-objetos-procedencia.tsv`.

X18 amplía los iconos de objetos con: [msikma/pokesprite](https://github.com/msikma/pokesprite) (revisión
`c5aaa610ff2acdf7fd8e2dccd181bca8be9fcb3e`, MIT el código; el arte es © The Pokémon Company),
[pokesprites/all](https://github.com/pokesprites/all) (revisión `48125ae5edc35663170f46cc325a1575185727cc`, iconos HD de
Escarlata/Púrpura, sin licencia declarada; arte © The Pokémon Company) y la carpeta `gen3/` de PokeAPI/sprites. Procedencia y SHA-256
de cada imagen en `docs/android/sprites-objetos-procedencia.tsv`; categorías por fila en `docs/android/sprites-objetos-matriz.tsv`.
No se infiere permiso de redistribución del hecho de que esos repositorios sean de acceso público.

X27 añade el arte de objetos de [pret/pokeemerald](https://github.com/pret/pokeemerald/tree/731ad5bfd6e6f265508d0efcca0ba42f9dcf5881/graphics/items)
y [pret/pokefirered](https://github.com/pret/pokefirered/tree/037335f4c725d7c9aecdac87066f2002b4bd7e14/graphics/items), combinando los
índices PNG y la paleta que asigna la tabla nativa del juego. No se han copiado ROMs. Los gráficos siguen siendo © The Pokémon
Company / Nintendo / Game Freak; estos repositorios no declaran una licencia general del arte. Las licencias de las herramientas de
conversión no se atribuyen a los gráficos. Procedencia, revisiones, hashes de gráfico/paleta/resultado y referencia independiente de
píxeles en `android/Pokekeep.Objetos.Build/pret-gen3.tsv`, `docs/android/sprites-objetos-procedencia.tsv` y
`docs/android/sprites-objetos-x27-fuentes.md`. Se mantiene el criterio de X18: el arte no está en este repositorio y no se infiere
permiso de redistribución del acceso público.

X28 añade el arte de objetos de Gen. 4 de [pret/pokeplatinum](https://github.com/pret/pokeplatinum/tree/fe1ea418a7364c93daf0d07ae55e195452d8803c),
[pret/pokeheartgold](https://github.com/pret/pokeheartgold/tree/6cedc4f57c9ce18821adbfff25c6aefbd0dc1176) y
[pret/pokediamond](https://github.com/pret/pokediamond/tree/b623abef1b4dfe204ca50aab42447ac00c134290), por id nativo y tabla del propio
juego. No se han copiado ROMs. Mismas condiciones de X27: arte © The Pokémon Company / Nintendo / Game Freak, sin licencia general
declarada, fuera de este repositorio y sin inferir permiso de redistribución. Procedencia en `docs/android/sprites-objetos-ds-x28.tsv`
y `docs/android/sprites-objetos-x28-fuentes.md`. La herramienta de conversión de los volcados del autor está solo en su repositorio de
trabajo, no en este; los volcados nunca se versionan.

## App de Android (1.0.0)

| Componente | Licencia | Cómo se usa |
|---|---|---|
| .NET y .NET para Android (Microsoft) | MIT | Plataforma de la app. |
| Avalonia UI 12.1.3 (AvaloniaUI) | MIT | Interfaz. |
| SkiaSharp 3.119.4 (Microsoft / Mono) | MIT | Dibujo. |
| CommunityToolkit.Mvvm 8.4.2 (.NET Foundation) | MIT | Modelo de vistas. |
| QRCoder 1.7.0 | MIT | Generar los códigos QR de los Pokémon. |
| ZXing.Net 0.16.9 | Apache-2.0 | Leer códigos QR de una imagen. |
| Xamarin.AndroidX.Core.SplashScreen (Google / Microsoft) | Apache-2.0 | Pantalla de arranque. |
| Roboto (Google) | Apache-2.0 (`android/Pokekeep.Mobile.Ui/Assets/Fonts/LICENSE-Roboto.txt`) | Tipografía. |
| Noto Symbols (The Noto Project Authors) | SIL OFL 1.1 (`LICENSE-PokekeepSymbols.txt`) | Iconos de la interfaz, como fuente `PokekeepSymbols`. |

**Contenido sacado de los juegos.** Las vistas previas (sprites pixelados de Gen. 1-5 y renders 3D de 3DS y Switch), los iconos de
caja, los huevos y los rótulos de los títulos se extrajeron de las copias de los juegos del autor (volcados privados que nunca se
versionan), sin modificar el arte. Los colores de las etiquetas de tipo se midieron en capturas de Pokémon Escarlata
(`docs/android/tipos-colores-escarlata.json`). Todo es © Nintendo, Creatures Inc. y GAME FREAK inc. / The Pokémon Company. La
procedencia y la huella de las imágenes que figuran en este repositorio están en `docs/android/*procedencia*.tsv`; las de las vistas
3D y las demás imágenes sacadas de los volcados, en los manifiestos que acompañan a cada paquete (generados, fuera de Git y fuera de
este repositorio).

**Carátulas.** Las carátulas de las ediciones españolas de GB, GBC, GBA, GameCube, Wii, DS y 3DS proceden de
[WikiDex](https://www.wikidex.net/), con la página y la URL de cada una en el manifiesto de las carátulas, que no se incluye en el
repositorio público. El arte de las carátulas es © Nintendo / The Pokémon Company.

**Distribución.** El código propio de Pokekeep es GPL-3.0-or-later y se puede redistribuir con sus condiciones. El contenido de
Nintendo y The Pokémon Company que llevan el APK y el paquete portable de las releases (imágenes, sonidos y datos) no es de Pokekeep
ni está en este repositorio: el autor lo incluye en las releases por decisión propia, junto con las fuentes «sin licencia declarada»
de arriba. Quien compile desde el código no recibe ese contenido: los scripts descargan parte de él de sus fuentes, y lo que sale de
los volcados del autor no se puede reproducir.
