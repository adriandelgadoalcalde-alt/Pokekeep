# Créditos

*[English](CREDITS.md)*

Pokekeep lo diseña y desarrolla **IncrediBad**. Es software libre bajo la GPL-3.0-or-later (`LICENSE`). Las licencias, la
procedencia y la huella de cada componente están en [`NOTICE.md`](NOTICE.md) y [`docs/LICENCIAS.md`](docs/LICENCIAS.md).

## PKHeX

Pokekeep se basa en **[PKHeX](https://github.com/kwsch/PKHeX)**, de Kurt «kwsch» y colaboradores (GPL-3.0-or-later). `PKHeX.Core`
lee y escribe las partidas, y su análisis de legalidad es el que decide qué es «legal» en Pokekeep. Pokekeep no cambia ninguna de
sus reglas. Gracias al equipo de PKHeX y a la comunidad de [Project Pokémon](https://projectpokemon.org/).

## Motor de legalización

| Componente | Autores | Licencia | Uso |
|---|---|---|---|
| [Auto Legality Mod (PKHeX-Plugins)](https://github.com/santacrab2/PKHeX-Plugins) | santacrab2, architdate y colaboradores | MIT | Generador de Pokémon, encapsulado tras un adaptador propio; cada resultado se vuelve a comprobar con el Core. |
| [Project Pokémon: EventsGallery](https://github.com/projectpokemon/EventsGallery) | Comunidad de Project Pokémon | Sin licencia declarada | Datos de los eventos (solo metadatos y huellas en el repositorio). |
| [Project Pokémon: PoGoEncTool](https://github.com/projectpokemon/PoGoEncTool) | Comunidad de Project Pokémon | AGPL-3.0 | Solo sus datos (ventanas de Pokémon GO). |

## App de Android

| Componente | Licencia | Uso |
|---|---|---|
| [.NET](https://github.com/dotnet/runtime) y .NET para Android | MIT | Plataforma de la app. |
| [Avalonia UI](https://github.com/AvaloniaUI/Avalonia) 12 | MIT | Interfaz. |
| [SkiaSharp](https://github.com/mono/SkiaSharp) | MIT | Dibujo. |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MIT | Modelo de vistas. |
| [QRCoder](https://github.com/codebude/QRCoder) | MIT | Códigos QR de los Pokémon. |
| [ZXing.Net](https://github.com/micjahn/ZXing.Net) | Apache-2.0 | Lectura de códigos QR. |
| AndroidX Core SplashScreen | Apache-2.0 | Pantalla de arranque. |
| [Microsoft.Data.Sqlite](https://github.com/dotnet/efcore) y SQLite | MIT / dominio público | Catálogo y caché de soluciones. |
| [Roboto](https://fonts.google.com/specimen/Roboto) | Apache-2.0 | Tipografía. |
| [Noto Symbols](https://github.com/notofonts/symbols) | SIL OFL 1.1 | Iconos de la interfaz (`PokekeepSymbols`). |

Los decodificadores de WebP y PNG de la app (`WebpLite`, `PngLite`) y el escalador de pixel art (Scale2x/EPX, algoritmo de dominio
público de Andrea Mazzoleni y Eric Johnston) son implementaciones propias.

## Imágenes y sonidos

Todo el contenido Pokémon (imágenes, sonidos, nombres y datos) es **© Nintendo, Creatures Inc. y GAME FREAK inc.** y de The
Pokémon Company. Pokekeep es una herramienta **no oficial** y no está afiliada a ninguno de ellos.

- **Vistas previas, iconos de caja, huevos y logos de los títulos:** extraídos de las copias de los juegos del autor, sin
  modificar el arte; los colores de los tipos se midieron en Pokémon Escarlata. No se distribuye ninguna ROM, clave ni partida.
- **Sprites e iconos que trae PKHeX:** los del propio PKHeX.
- **Carátulas de los juegos:** de [WikiDex](https://www.wikidex.net/) (ediciones españolas), con su página y URL en el manifiesto.
- **Gritos:** [PokeAPI/cries](https://github.com/PokeAPI/cries) (repositorio CC0; audio © The Pokémon Company).
- **Iconos de objetos adicionales:** [PokeAPI/sprites](https://github.com/PokeAPI/sprites), [msikma/pokesprite](https://github.com/msikma/pokesprite),
  [pokesprites/all](https://github.com/pokesprites/all) y los proyectos de descompilación de [pret](https://github.com/pret)
  (arte © The Pokémon Company).

La procedencia de cada imagen y sonido (fuente, revisión y SHA-256) está en `docs/android/*procedencia*.tsv`.

## Herramientas de desarrollo

[ILSpy](https://github.com/icsharpcode/ILSpy), [Ghidra](https://github.com/NationalSecurityAgency/ghidra),
[libwebp](https://chromium.googlesource.com/webm/libwebp) y [oxipng](https://github.com/shssoichiro/oxipng) se usaron para investigar
y para compactar las imágenes sin pérdida; no forman parte del producto.
