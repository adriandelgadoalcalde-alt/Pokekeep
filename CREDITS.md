# Credits

*[Español](CREDITOS.md)*

Pokekeep is designed and developed by **IncrediBad**. It is free software under the GPL-3.0-or-later (`LICENSE`). The licenses,
provenance and hash of each component are in [`NOTICE.md`](NOTICE.md) and [`docs/LICENCIAS.md`](docs/LICENCIAS.md).

## PKHeX

Pokekeep is based on **[PKHeX](https://github.com/kwsch/PKHeX)**, by Kurt “kwsch” and contributors (GPL-3.0-or-later). `PKHeX.Core`
reads and writes the saves, and its legality analysis decides what is “legal” in Pokekeep. Pokekeep does not change any of its
rules. Thanks to the PKHeX team and to the [Project Pokémon](https://projectpokemon.org/) community.

## Legalization engine

| Component | Authors | License | Use |
|---|---|---|---|
| [Auto Legality Mod (PKHeX-Plugins)](https://github.com/santacrab2/PKHeX-Plugins) | santacrab2, architdate and contributors | MIT | Pokémon generator, wrapped behind its own adapter; every result is checked again by the Core. |
| [Project Pokémon: EventsGallery](https://github.com/projectpokemon/EventsGallery) | Project Pokémon community | No license declared | Event data (only metadata and hashes in the repository). |
| [Project Pokémon: PoGoEncTool](https://github.com/projectpokemon/PoGoEncTool) | Project Pokémon community | AGPL-3.0 | Only its data (Pokémon GO windows). |

## Android app

| Component | License | Use |
|---|---|---|
| [.NET](https://github.com/dotnet/runtime) and .NET for Android | MIT | App platform. |
| [Avalonia UI](https://github.com/AvaloniaUI/Avalonia) 12 | MIT | User interface. |
| [SkiaSharp](https://github.com/mono/SkiaSharp) | MIT | Rendering. |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MIT | View models. |
| [QRCoder](https://github.com/codebude/QRCoder) | MIT | Pokémon QR codes. |
| [ZXing.Net](https://github.com/micjahn/ZXing.Net) | Apache-2.0 | QR code reading. |
| AndroidX Core SplashScreen | Apache-2.0 | Splash screen. |
| [Microsoft.Data.Sqlite](https://github.com/dotnet/efcore) and SQLite | MIT / public domain | Catalog and solution cache. |
| [Roboto](https://fonts.google.com/specimen/Roboto) | Apache-2.0 | Typeface. |
| [Noto Symbols](https://github.com/notofonts/symbols) | SIL OFL 1.1 | Interface icons (`PokekeepSymbols`). |

The app’s WebP and PNG decoders (`WebpLite`, `PngLite`) and the pixel-art scaler (Scale2x/EPX, a public-domain algorithm by Andrea
Mazzoleni and Eric Johnston) are original implementations.

## Images and sounds

All Pokémon content (images, sounds, names and data) is **© Nintendo, Creatures Inc. and GAME FREAK inc.** and The Pokémon
Company. Pokekeep is an **unofficial** tool and is not affiliated with any of them.

- **Previews, box icons, eggs and title logos:** extracted from the author’s own copies of the games, without altering the art;
  type colors were measured in Pokémon Scarlet. No ROM, key or save is distributed.
- **Sprites and icons bundled with PKHeX:** PKHeX’s own.
- **Game box art:** from [WikiDex](https://www.wikidex.net/) (Spanish editions), with each page and URL in the manifest.
- **Cries:** [PokeAPI/cries](https://github.com/PokeAPI/cries) (CC0 repository; audio © The Pokémon Company).
- **Extra item icons:** [PokeAPI/sprites](https://github.com/PokeAPI/sprites), [msikma/pokesprite](https://github.com/msikma/pokesprite),
  [pokesprites/all](https://github.com/pokesprites/all) and the [pret](https://github.com/pret) decompilation projects
  (art © The Pokémon Company).

The provenance of every image and sound (source, revision and SHA-256) is in `docs/android/*procedencia*.tsv`.

## Development tools

[ILSpy](https://github.com/icsharpcode/ILSpy), [Ghidra](https://github.com/NationalSecurityAgency/ghidra),
[libwebp](https://chromium.googlesource.com/webm/libwebp) and [oxipng](https://github.com/shssoichiro/oxipng) were used for research
and for lossless image compaction; they are not part of the product.
