// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios;

/// <summary>
/// Un boton de «Específicos del juego»: el equivalente de un boton de las herramientas del guardado de PKHeX (<c>SAVEditor.B_*</c>). <see cref="Habilitada"/> es la regla EXACTA con la que WinForms lo muestra
/// (<c>SAVEditor.ToggleViewSubEditors</c>, <see cref="Regla"/> dice la linea). <see cref="Fabrica"/> construye el editor declarativo para ese guardado y devuelve null si todavia no se ha hecho (lo hace el bloque que
/// dice <see cref="Bloque"/>); la pantalla solo enseña los habilitados que ya tienen editor.
/// </summary>
public sealed record EntradaEspecificaP(string Id, string ClaveTitulo, string ClaveDescripcion, string Glifo, string Bloque, string[] Censo, string Regla, Func<SaveFile, bool> Habilitada, Func<SaveFile, bool> Aplica, Func<SaveFile, EditorDefP?> Fabrica, bool EnPartida = true)
{
    public string Titulo => IdiomaInterfaz.T(ClaveTitulo);
    public string Descripcion => IdiomaInterfaz.T(ClaveDescripcion);

    /// <summary>La app ya sabe editarla en este guardado. Usa el predicado barato <see cref="Aplica"/>, nunca construye el editor.</summary>
    public bool Implementada(SaveFile sav) => Aplica(sav);
}

/// <summary>
/// «Partida > Específicos del juego» (plan 6.2): los formularios del censo <c>PARTIDA-GEN*</c> que PKHeX enseña para el formato abierto, ni uno mas ni uno menos. Cada bloque de la F8 rellena SU fichero
/// (<c>CatalogoEspecificos.Gen*.cs</c>) con los editores de su generacion; este fichero no se toca para anadir un formulario (ver «Como anadir un formulario» en docs\android\plan-f8.md).
/// </summary>
public static partial class CatalogoEspecificos
{
    private const string Win = "SAVEditor.cs ToggleViewSubEditors";

    /// <summary>Todas las entradas, por orden alfabetico del titulo (WinForms ordena sus botones por texto).</summary>
    public static IReadOnlyList<EntradaEspecificaP> Todas { get; } = Construir();

    private static EditorDefP? Crear(string id, SaveFile sav) => EditoresGen1a3(id, sav) ?? EditoresGen4(id, sav) ?? EditoresGen5(id, sav) ?? EditoresGen6y7(id, sav) ?? EditoresGen8y9(id, sav);

    private static EntradaEspecificaP E(string id, string titulo, string desc, string glifo, string bloque, string[] censo, string regla, Func<SaveFile, bool> habilitada, bool enPartida = true)
        => new(id, titulo, desc, glifo, bloque, censo, regla, habilitada, sav => AplicaEditor(id, sav), sav => Crear(id, sav), enPartida);

    /// <summary>Cada bloque tiene su <c>Aplica&lt;Gen&gt;</c> en su fichero; el primero que diga que si, decide (una entrada multiformato tiene un caso en cada bloque).</summary>
    private static bool AplicaEditor(string id, SaveFile sav) => AplicaGen1a3(id, sav) || AplicaGen4(id, sav) || AplicaGen5(id, sav) || AplicaGen6y7(id, sav) || AplicaGen8y9(id, sav);

    private static IReadOnlyList<EntradaEspecificaP> Construir() =>
    [
        // ---- Gen. 1-3 (8a-1) ----
        E("eventos1", "Partida.Especifico.eventos1.Titulo", "Partida.Especifico.eventos1.Descripcion", "Map", "8a-1", ["PK-SAV-001"], $"{Win}: B_OpenEventFlags, sav is SAV1", sav => sav is SAV1),
        E("hof", "Partida.Especifico.hof.Titulo", "Partida.Especifico.hof.Descripcion", "Star", "8a-1/8b-1", ["PK-SAV-002", "PK-SAV-005", "PK-SAV-106", "PK-SAV-134"], $"{Win}: B_OpenHallofFame, sav is ISaveBlock6Main or SAV7 or SAV3 {{ IsMisconfiguredSize: false }} or SAV1",
          sav => sav is ISaveBlock6Main or SAV7 or SAV1 || sav is SAV3 { IsMisconfiguredSize: false }),
        E("misc", "Partida.Especifico.misc.Titulo", "Partida.Especifico.misc.Descripcion", "Settings", "8a-1/8a-2/8a-3/8b-2",
          ["PK-SAV-003", "PK-SAV-004", "PK-SAV-006", "PK-SAV-007", "PK-SAV-008", "PK-SAV-009", "PK-SAV-010", "PK-SAV-011", "PK-SAV-012", "PK-SAV-013", "PK-SAV-014", "PK-SAV-015", "PK-SAV-019", "PK-SAV-026", "PK-SAV-036", "PK-SAV-037", "PK-SAV-038", "PK-SAV-039", "PK-SAV-040", "PK-SAV-041", "PK-SAV-042", "PK-SAV-043", "PK-SAV-044", "PK-SAV-045", "PK-SAV-095", "PK-SAV-096", "PK-SAV-097", "PK-SAV-098", "PK-SAV-099", "PK-SAV-100", "PK-SAV-101", "PK-SAV-158", "PK-SAV-159"],
          $"{Win}: B_OpenMiscEditor, sav is SAV2 {{ Version: GameVersion.C }} or SAV3 or SAV4 or SAV5 or SAV8BS", sav => sav is SAV2 { Version: GameVersion.C } or SAV3 or SAV4 or SAV5 or SAV8BS),
        E("roamer", "Partida.Especifico.roamer.Titulo", "Partida.Especifico.roamer.Descripcion", "Map", "8a-1/8b-1", ["PK-SAV-016", "PK-SAV-116"], $"{Win}: B_Roamer, sav is SAV3 or SAV6XY", sav => sav is SAV3 or SAV6XY),
        E("rtc3", "Partida.Especifico.rtc3.Titulo", "Partida.Especifico.rtc3.Descripcion", "Clock", "8a-1", ["PK-SAV-017"], $"{Win}: B_OpenRTCEditor, (sav.Generation == 2 && sav is not SAV2Stadium) || sav is SAV3 {{ SmallBlock: ISaveBlock3SmallHoenn }} (la de Gen. 2 no abre formulario: es la accion «reiniciar reloj» de la F10)",
          sav => sav is SAV3 { SmallBlock: ISaveBlock3SmallHoenn }),
        E("basessecretas", "Partida.Especifico.basessecretas.Titulo", "Partida.Especifico.basessecretas.Descripcion", "Lock", "8a-1/8b-1", ["PK-SAV-018", "PK-SAV-117", "PK-SAV-118", "PK-SAV-119"], $"{Win}: B_OpenSecretBase, sav is SAV6AO or SAV3 {{ LargeBlock: ISaveBlock3LargeHoenn }}",
          sav => sav is SAV6AO || sav is SAV3 { LargeBlock: ISaveBlock3LargeHoenn }),

        // ---- Gen. 4 (8a-2) ----
        E("mielarboles", "Partida.Especifico.mielarboles.Titulo", "Partida.Especifico.mielarboles.Descripcion", "Egg", "8a-2", ["PK-SAV-035"], $"{Win}: B_OpenHoneyTreeEditor, sav is SAV4Sinnoh", sav => sav is SAV4Sinnoh),
        E("subsuelo", "Partida.Especifico.subsuelo.Titulo", "Partida.Especifico.subsuelo.Descripcion", "Package", "8a-2/8b-2", ["PK-SAV-056", "PK-SAV-057", "PK-SAV-058", "PK-SAV-059", "PK-SAV-060", "PK-SAV-182"], $"{Win}: B_OpenUGSEditor, sav is SAV4Sinnoh or SAV8BS", sav => sav is SAV4Sinnoh or SAV8BS),
        E("geonet", "Partida.Especifico.geonet.Titulo", "Partida.Especifico.geonet.Descripcion", "Map", "8a-2", ["PK-SAV-034"], $"{Win}: B_OpenGeonetEditor, sav is SAV4", sav => sav is SAV4),
        E("apricorn", "Partida.Especifico.apricorn.Titulo", "Partida.Especifico.apricorn.Descripcion", "Package", "8a-2", ["PK-SAV-027"], $"{Win}: B_OpenApricorn, sav is SAV4HGSS", sav => sav is SAV4HGSS),
        E("pokeathlon", "Partida.Especifico.pokeathlon.Titulo", "Partida.Especifico.pokeathlon.Descripcion", "Swords", "8a-2", ["PK-SAV-020", "PK-SAV-021", "PK-SAV-022", "PK-SAV-023", "PK-SAV-024", "PK-SAV-025", "PK-SAV-046", "PK-SAV-047", "PK-SAV-048", "PK-SAV-049", "PK-SAV-050", "PK-SAV-051", "PK-SAV-052", "PK-SAV-053"], $"{Win}: B_OpenPokeathlon, sav is SAV4HGSS", sav => sav is SAV4HGSS),
        E("pasesbr", "Partida.Especifico.pasesbr.Titulo", "Partida.Especifico.pasesbr.Descripcion", "IdCard", "8a-2", ["PK-SAV-028", "PK-SAV-029", "PK-SAV-030", "PK-SAV-031", "PK-SAV-032"], $"{Win}: B_OpenBattlePass, sav is SAV4BR", sav => sav is SAV4BR),
        E("equipobr", "Partida.Especifico.equipobr.Titulo", "Partida.Especifico.equipobr.Descripcion", "Sparkles", "8a-2", ["PK-SAV-033"], $"{Win}: B_OpenGear, sav is SAV4BR", sav => sav is SAV4BR),
        E("chatter", "Partida.Especifico.chatter.Titulo", "Partida.Especifico.chatter.Descripcion", "Sparkles", "8a-2", ["PK-SAV-212"], $"{Win}: B_OpenChatterEditor, sav is SAV4 or SAV5", sav => sav is SAV4 or SAV5),
        // Nuevos en PKHeX 26.10.18. «DLC I/O» abre el editor de vídeos de Gen. 4 (SAV_DLC4) en Platino y HeartGold/SoulSilver y el de Gen. 5 (SAV_DLC5); las cintas de regalo valen para Gen. 3 y 4.
        E("dlc4", "Partida.Especifico.dlc4.Titulo", "Partida.Especifico.dlc4.Descripcion", "Download", "8a-2", ["PK-SAV-213"], $"{Win}: B_DLC, sav is SAV4HGSS or SAV4Pt (SAV_DLC4)", sav => sav is SAV4HGSS or SAV4Pt),
        E("cintasregalo", "Partida.Especifico.cintasregalo.Titulo", "Partida.Especifico.cintasregalo.Descripcion", "Star", "8a-2", ["PK-SAV-214"], $"{Win}: B_OpenGiftRibbons, sav is IGiftRibbons", sav => sav is IGiftRibbons),

        // ---- Gen. 5 (8a-3) ----
        E("dlc5", "Partida.Especifico.dlc5.Titulo", "Partida.Especifico.dlc5.Descripcion", "Download", "8a-3", ["PK-SAV-061", "PK-SAV-079", "PK-SAV-080", "PK-SAV-081", "PK-SAV-082", "PK-SAV-083", "PK-SAV-084", "PK-SAV-085", "PK-SAV-086", "PK-SAV-087"], $"{Win}: B_DLC, sav is SAV5 (SAV_DLC5)", sav => sav is SAV5),
        E("globallink", "Partida.Especifico.globallink.Titulo", "Partida.Especifico.globallink.Descripcion", "Share2", "8a-3", ["PK-SAV-088", "PK-SAV-089", "PK-SAV-090", "PK-SAV-091"], $"{Win}: B_OpenGlobalLink, sav is SAV5", sav => sav is SAV5),
        E("torreunion", "Partida.Especifico.torreunion.Titulo", "Partida.Especifico.torreunion.Descripcion", "Layers", "8a-3", ["PK-SAV-103"], $"{Win}: B_OpenUnityTowerEditor, sav is SAV5", sav => sav is SAV5),
        E("avenida", "Partida.Especifico.avenida.Titulo", "Partida.Especifico.avenida.Descripcion", "Heart", "8a-3", ["PK-SAV-062", "PK-SAV-063", "PK-SAV-064", "PK-SAV-065", "PK-SAV-066", "PK-SAV-067", "PK-SAV-068", "PK-SAV-069", "PK-SAV-070", "PK-SAV-071", "PK-SAV-072", "PK-SAV-073", "PK-SAV-074", "PK-SAV-075", "PK-SAV-076", "PK-SAV-077", "PK-SAV-078"], $"{Win}: B_OpenJoinAvenueEditor, sav is SAV5B2W2", sav => sav is SAV5B2W2),
        E("medallas5", "Partida.Especifico.medallas5.Titulo", "Partida.Especifico.medallas5.Descripcion", "Star", "8a-3", ["PK-SAV-092", "PK-SAV-093", "PK-SAV-094"], $"{Win}: B_OpenMedalsEditor, sav is SAV5B2W2", sav => sav is SAV5B2W2),

        // ---- Gen. 6-7 (8b-1) ----
        E("bayas6", "Partida.Especifico.bayas6.Titulo", "Partida.Especifico.bayas6.Descripcion", "Egg", "8b-1", ["PK-SAV-104"], $"{Win}: B_OpenBerryField, sav is SAV6XY", sav => sav is SAV6XY),
        E("link6", "Partida.Especifico.link6.Titulo", "Partida.Especifico.link6.Descripcion", "Share2", "8b-1", ["PK-SAV-107", "PK-SAV-108", "PK-SAV-109", "PK-SAV-110"], $"{Win}: B_LinkInfo, sav is ISaveBlock6Main", sav => sav is ISaveBlock6Main),
        E("poderes6", "Partida.Especifico.poderes6.Titulo", "Partida.Especifico.poderes6.Descripcion", "Sparkles", "8b-1", ["PK-SAV-111"], $"{Win}: B_OpenOPowers, sav is ISaveBlock6Main", sav => sav is ISaveBlock6Main),
        E("pokeblocks6", "Partida.Especifico.pokeblocks6.Titulo", "Partida.Especifico.pokeblocks6.Descripcion", "Package", "8b-1", ["PK-SAV-112"], $"{Win}: B_OpenPokeblocks, sav is SAV6AO", sav => sav is SAV6AO),
        E("pokeflautas", "Partida.Especifico.pokeflautas.Titulo", "Partida.Especifico.pokeflautas.Descripcion", "Package", "8b-1", ["PK-SAV-115"], $"{Win}: B_OpenPokepuffs, sav is ISaveBlock6Main", sav => sav is ISaveBlock6Main),
        E("supertrain", "Partida.Especifico.supertrain.Titulo", "Partida.Especifico.supertrain.Descripcion", "Swords", "8b-1", ["PK-SAV-120"], $"{Win}: B_OpenSuperTraining, sav is ISaveBlock6Main", sav => sav is ISaveBlock6Main),
        E("capturas7gg", "Partida.Especifico.capturas7gg.Titulo", "Partida.Especifico.capturas7gg.Descripcion", "Box", "8b-1", ["PK-SAV-128"], "SAV_PokedexGG.cs:59 (el editor se abre desde la Pokédex de Let's Go: sav is SAV7b)", sav => sav is SAV7b),
        E("festival", "Partida.Especifico.festival.Titulo", "Partida.Especifico.festival.Descripcion", "Sparkles", "8b-1", ["PK-SAV-129", "PK-SAV-130", "PK-SAV-131", "PK-SAV-132", "PK-SAV-133"], $"{Win}: B_FestivalPlaza, sav is SAV7", sav => sav is SAV7),
        E("habichuelas", "Partida.Especifico.habichuelas.Titulo", "Partida.Especifico.habichuelas.Descripcion", "Package", "8b-1", ["PK-SAV-135"], $"{Win}: B_OpenPokeBeans, sav is SAV7", sav => sav is SAV7),
        E("celulas", "Partida.Especifico.celulas.Titulo", "Partida.Especifico.celulas.Descripcion", "Package", "8b-1", ["PK-SAV-148"], $"{Win}: B_CellsStickers, sav is SAV7", sav => sav is SAV7),

        // ---- Gen. 8-9 (8b-2) ----
        E("eventos8b", "Partida.Especifico.eventos8b.Titulo", "Partida.Especifico.eventos8b.Descripcion", "ListChecks", "8b-2", ["PK-SAV-153", "PK-SAV-154", "PK-SAV-155", "PK-SAV-156", "PK-SAV-157"], $"{Win}: B_OpenEventFlags, sav is SAV8BS", sav => sav is SAV8BS),
        E("pozoffins8b", "Partida.Especifico.pozoffins8b.Titulo", "Partida.Especifico.pozoffins8b.Descripcion", "Package", "8b-2", ["PK-SAV-160"], $"{Win}: B_Poffins, sav is SAV8BS", sav => sav is SAV8BS),
        E("pegatinas8b", "Partida.Especifico.pegatinas8b.Titulo", "Partida.Especifico.pegatinas8b.Descripcion", "Package", "8b-2", ["PK-SAV-170"], $"{Win}: B_OpenSealStickers, sav is SAV8BS", sav => sav is SAV8BS),
        E("incursiones", "Partida.Especifico.incursiones.Titulo", "Partida.Especifico.incursiones.Descripcion", "Swords", "8b-2", ["PK-SAV-169", "PK-SAV-195"], $"{Win}: B_Raids, sav is SAV8SWSH or SAV9SV", sav => sav is SAV8SWSH or SAV9SV),
        E("incursiones-dlc1", "Partida.Especifico.incursiones-dlc1.Titulo", "Partida.Especifico.incursiones-dlc1.Descripcion", "Swords", "8b-2", ["PK-SAV-169", "PK-SAV-195"], $"{Win}: B_RaidsDLC1, sav is SAV8SWSH {{ SaveRevision: >= 1 }} or SAV9SV {{ SaveRevision: >= 1 }}", sav => sav is SAV8SWSH { SaveRevision: >= 1 } or SAV9SV { SaveRevision: >= 1 }),
        E("incursiones-dlc2", "Partida.Especifico.incursiones-dlc2.Titulo", "Partida.Especifico.incursiones-dlc2.Descripcion", "Swords", "8b-2", ["PK-SAV-169", "PK-SAV-195"], $"{Win}: B_RaidsDLC2, sav is SAV8SWSH {{ SaveRevision: >= 2 }} or SAV9SV {{ SaveRevision: >= 2 }}", sav => sav is SAV8SWSH { SaveRevision: >= 2 } or SAV9SV { SaveRevision: >= 2 }),
        E("incursiones7", "Partida.Especifico.incursiones7.Titulo", "Partida.Especifico.incursiones7.Descripcion", "Swords", "8b-2", ["PK-SAV-196"], $"{Win}: B_RaidsSevenStar, sav is SAV9SV", sav => sav is SAV9SV),
        E("moda", "Partida.Especifico.moda.Titulo", "Partida.Especifico.moda.Descripcion", "Sparkles", "8b-2", ["PK-SAV-188", "PK-SAV-189"], $"{Win}: B_OpenFashion, sav is SAV9SV or SAV9ZA", sav => sav is SAV9SV or SAV9ZA),
        E("donuts", "Partida.Especifico.donuts.Titulo", "Partida.Especifico.donuts.Descripcion", "Egg", "8b-2", ["PK-SAV-183", "PK-SAV-184", "PK-SAV-186", "PK-SAV-187"], $"{Win}: B_Donuts, sav is SAV9ZA {{ SaveRevision: >= 1 }}", sav => sav is SAV9ZA { SaveRevision: >= 1 }),
        E("eventos9a", "Partida.Especifico.eventos9a.Titulo", "Partida.Especifico.eventos9a.Descripcion", "ListChecks", "8b-2", ["PK-SAV-185", "PK-SAV-190", "PK-SAV-191"], $"{Win}: B_OpenEventFlags, sav is SAV9ZA", sav => sav is SAV9ZA),
        // «Adaptar» (no es un boton de Especificos: vive en Mas > Herramientas > Bloques del guardado, avanzado).
        E("bloques8", "Partida.Especifico.bloques8.Titulo", "Partida.Especifico.bloques8.Descripcion", "Layers", "8b-2", ["PK-SAV-150", "PK-SAV-151", "PK-SAV-152"], $"SAVEditor.cs B_Blocks_Click: ISCBlockArray => SAV_BlockDump8", sav => sav is ISCBlockArray, enPartida: false),
    ];

    /// <summary>Lo que PKHeX WinForms habilitaria para este guardado (aunque aun no tenga editor en la app), por orden alfabetico del titulo.</summary>
    public static IReadOnlyList<EntradaEspecificaP> Habilitadas(SaveFile sav)
        => [.. Todas.Where(e => e.EnPartida && e.Habilitada(sav)).OrderBy(e => e.Titulo, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>Las habilitadas que la app ya sabe editar: es la lista que enseña la pantalla.</summary>
    public static IReadOnlyList<EntradaEspecificaP> Disponibles(SaveFile sav) => [.. Habilitadas(sav).Where(e => e.Implementada(sav))];

    /// <summary>El editor de una entrada para este guardado, o null si no aplica o aun no esta hecho.</summary>
    public static EditorDefP? Editor(string id, SaveFile sav)
        => Todas.FirstOrDefault(e => e.Id == id) is { } e && e.Habilitada(sav) ? e.Fabrica(sav) : null;
}
