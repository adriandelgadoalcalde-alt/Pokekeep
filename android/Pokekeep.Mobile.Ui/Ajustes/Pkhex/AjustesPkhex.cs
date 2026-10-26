// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.ComponentModel;
using System.Text.Json.Serialization;
using PKHeX.Core;
using Pokekeep.Sprites;

namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

/// <summary>
/// LOS AJUSTES DE PKHeX EN EL MÓVIL (F10, ADR 0072). Mismo árbol que <c>PKHeXSettings</c> de PKHeX.WinForms, con los mismos nombres de propiedad y los mismos valores por defecto, para que cada fila
/// <c>PK-SET-*</c> del censo tenga su ruta (<c>SlotWrite.SetUpdateDex</c>, <c>Legality.Handler.Restrictions.AllowHandleOTGen6</c>...) y una prueba generada desde el censo pueda recorrerlas todas:
/// <list type="bullet">
/// <item>Los grupos que viven en el Core (<c>Backup</c>, <c>LocalResources</c>, <c>Legality</c>, <c>Converter</c>, <c>Import</c>, <c>SlotWrite</c>, <c>Privacy</c>, <c>SaveLanguage</c>, <c>Sounds</c>, <c>Advanced</c>,
/// <c>EntityEditor</c>, <c>EntityDb</c>, <c>EncounterDb</c>, <c>MysteryDb</c>, <c>Report</c>, <c>BattleTemplate</c>) son las MISMAS clases del Core: ni una copia que se pueda desviar.</item>
/// <item>Los que PKHeX define en WinForms (<c>Startup</c>, <c>Display</c>, <c>Sprite</c>, <c>Hover</c>, <c>Draw</c>, <c>SlotExport</c>) se espejan aquí; la prueba <c>AjustesContraWinFormsTests</c> los compara por reflexión
/// con las clases reales (nombres, tipos, valores por defecto) y falla si falta, sobra o difiere alguno.</item>
/// <item>Implementa <see cref="IProgramSettings"/>: <c>StartupUtil.ReloadSettings</c> aplica al Core exactamente lo que aplica PKHeX (rutas de copias, idioma de guardados antiguos, escritura de ranuras,
/// importación de sets, legalidad y conversión). Se guardan en <c>ajustes-pkhex.json</c> en la carpeta privada de la app (<see cref="AlmacenAjustesPkhex"/>); nada sale del dispositivo (regla 15).</item>
/// </list>
/// Un ajuste solo aparece en la pantalla cuando un bloque declara quién lo consume (<see cref="ConsumidoresPkhex"/>): ningún ajuste que no haga nada.
/// </summary>
public sealed class AjustesPkhex : IProgramSettings
{
    public InicioPkhex Startup { get; set; } = new();
    public BackupSettings Backup { get; set; } = new();
    public LocalResourceSettings LocalResources { get; set; } = new();

    public LegalitySettings Legality { get; set; } = new();
    public EntityConverterSettings Converter { get; set; } = new();
    public SetImportSettings Import { get; set; } = new();
    public SlotWriteSettings SlotWrite { get; set; } = new();
    public PrivacySettings Privacy { get; set; } = new();
    public SaveLanguageSettings SaveLanguage { get; set; } = new();

    public PantallaPkhex Display { get; set; } = new();
    public SpritePkhex Sprite { get; set; } = new();
    public SoundSettings Sounds { get; set; } = new();
    public DetallePkhex Hover { get; set; } = new();
    public BattleTemplateSettings BattleTemplate { get; set; } = new();

    public DibujoPkhex Draw { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();
    public EntityEditorSettings EntityEditor { get; set; } = new();
    public EntityDatabaseSettings EntityDb { get; set; } = new();
    public EncounterDatabaseSettings EncounterDb { get; set; } = new();
    public MysteryGiftDatabaseSettings MysteryDb { get; set; } = new();
    public ReportGridSettings Report { get; set; } = new();

    public ExportacionPkhex SlotExport { get; set; } = new();

    [JsonIgnore] IStartupSettings IProgramSettings.Startup => Startup;
}

/// <summary>Espejo de <c>PKHeX.WinForms.StartupSettings</c>. Adaptaciones: <c>Language</c> es «es» por defecto (la app es en castellano de España) y <c>DarkMode</c> parte de «claro» (el tema sigue al sistema hasta que el usuario lo fije; bloque 10-1).</summary>
public sealed class InicioPkhex : IStartupSettings
{
    [Browsable(false)]
    [LocalizedDescription("Last version that the program was run with.")]
    public string Version { get; set; } = string.Empty;

    [LocalizedDescription("Use the Dark color mode for the application on startup.")]
    public bool DarkMode { get; set; }

    [LocalizedDescription("Force HaX mode on Program Launch")]
    public bool ForceHaXOnLaunch { get; set; }

    [LocalizedDescription("Toggles a higher Dpi rendering mode for the application on startup.")]
    public bool HighDpiText { get; set; }

    [LocalizedDescription("Skips displaying the splash screen on Program Launch.")]
    public bool SkipSplashScreen { get; set; }

    [LocalizedDescription("Automatically locates the most recently saved Save File when opening a new file.")]
    public bool TryDetectRecentSave { get; set; } = true;

    [LocalizedDescription("Automatically Detect Save File on Program Startup")]
    public SaveFileLoadSetting AutoLoadSaveOnStartup { get; set; } = SaveFileLoadSetting.RecentBackup;

    [LocalizedDescription("Show the changelog when a new version of the program is run for the first time.")]
    public bool ShowChangelogOnUpdate { get; set; } = true;

    [LocalizedDescription("Loads plugins from the plugins folder, assuming the folder exists.")]
    public bool PluginLoadEnable { get; set; } = true;

    [LocalizedDescription("Loads any plugins that were merged into the main executable file.")]
    public bool PluginLoadMerged { get; set; }

    [Browsable(false)]
    public List<string> RecentlyLoaded { get; set; } = new(DefaultMaxRecent);

    public const int DefaultMaxRecent = 10;

    [LocalizedDescription("Amount of recently loaded save files to remember.")]
    public uint RecentlyLoadedMaxCount
    {
        get;
        set => field = Math.Clamp(value, 1, 1000);   // como el original: que nadie se ralentice sin querer
    } = DefaultMaxRecent;

    [Browsable(false)]
    public string Language
    {
        get;
        set
        {
            if (!GameLanguage.IsLanguageValid(value))
            {
                // migra los códigos antiguos como el original
                field = value switch { "zh" => "zh-Hans", "zh2" => "zh-Hant", _ => field };
                return;
            }
            field = value;
        }
    } = "es";

    [Browsable(false)]
    public GameVersion DefaultSaveVersion
    {
        get;
        set
        {
            if (!value.IsValidSavedVersion()) return;
            field = value;
        }
    } = Latest.Version;
}

/// <summary>Espejo de <c>PKHeX.WinForms.DisplaySettings</c>.</summary>
public sealed class PantallaPkhex
{
    [LocalizedDescription("Show Unicode gender symbol characters, or ASCII when disabled.")]
    public bool Unicode { get; set; } = true;

    [LocalizedDescription("Don't show the Legality popup if Legal!")]
    public bool IgnoreLegalPopup { get; set; }

    [LocalizedDescription("Display all properties of the encounter (auto-generated) when exporting a verbose report.")]
    public bool ExportLegalityVerboseProperties { get; set; }

    [LocalizedDescription("Always displays the verbose legality report, and inverts the hotkey behavior to instead disable.")]
    public bool ExportLegalityAlwaysVerbose { get; set; }

    [LocalizedDescription("Always skips the prompt option asking if you would like to export a legality report to clipboard.")]
    public bool ExportLegalityNeverClipboard { get; set; }

    [LocalizedDescription("Flag Illegal Slots in Save File")]
    public bool FlagIllegal { get; set; } = true;

    [LocalizedDescription("Focus border indentation for custom drawn image controls.")]
    public int FocusBorderDeflate { get; set; } = 1;

    [LocalizedDescription("Disables the GUI scaling based on Dpi on program startup, falling back to font scaling.")]
    public bool DisableScalingDpi { get; set; }

    [LocalizedDescription("Skips the context menu hotkey requirement and instead always presents the option to check legality of a slot.")]
    public bool SlotLegalityAlwaysVisible { get; set; }
}

/// <summary>Espejo de <c>PKHeX.WinForms.SpriteSettings</c> con los tipos de <c>Pokekeep.Sprites</c> (<see cref="PreferenciaSprites"/> = <c>SpriteBuilderPreference</c>, <see cref="FondoSprite"/> = <c>SpriteBackgroundType</c>; mismos valores y orden).</summary>
public sealed class SpritePkhex
{
    [LocalizedDescription("Choice for which sprite building mode to use.")]
    public PreferenciaSprites SpritePreference { get; set; } = PreferenciaSprites.Sugerida;

    [LocalizedDescription("Show fan-made shiny sprites when the PKM is shiny.")]
    public bool ShinySprites { get; set; } = true;

    [LocalizedDescription("Show an Egg Sprite As Held Item rather than hiding the PKM")]
    public bool ShowEggSpriteAsHeldItem { get; set; } = true;

    [LocalizedDescription("Show the required ball for an Encounter Template")]
    public bool ShowEncounterBall { get; set; } = true;

    [LocalizedDescription("Show a background to differentiate an Encounter Template's type")]
    public FondoSprite ShowEncounterColor { get; set; } = FondoSprite.FondoCompleto;

    [LocalizedDescription("Show a background to differentiate the recognized Encounter Template type for PKM slots")]
    public FondoSprite ShowEncounterColorPKM { get; set; }

    [LocalizedDescription("Opacity for the Encounter Type background layer.")]
    public byte ShowEncounterOpacityBackground { get; set; } = 0x3F;

    [LocalizedDescription("Opacity for the Encounter Type stripe layer.")]
    public byte ShowEncounterOpacityStripe { get; set; } = 0x5F;

    [LocalizedDescription("Amount of pixels thick to show when displaying the encounter type color stripe.")]
    public int ShowEncounterThicknessStripe { get; set; } = 4;

    [LocalizedDescription("Show a thin stripe to indicate the percent of level-up progress")]
    public bool ShowExperiencePercent { get; set; }

    [LocalizedDescription("Show a background to differentiate the Tera Type for PKM slots")]
    public FondoSprite ShowTeraType { get; set; } = FondoSprite.FranjaInferior;

    [LocalizedDescription("Amount of pixels thick to show when displaying the Tera Type color stripe.")]
    public int ShowTeraThicknessStripe { get; set; } = 4;

    [LocalizedDescription("Opacity for the Tera Type background layer.")]
    public byte ShowTeraOpacityBackground { get; set; } = 0xFF;

    [LocalizedDescription("Opacity for the Tera Type stripe layer.")]
    public byte ShowTeraOpacityStripe { get; set; } = 0xAF;

    [LocalizedDescription("Opacity of an entity that does not match a filter.")]
    public float FilterMismatchOpacity { get; set; } = 0.40f;

    [LocalizedDescription("Grayscale amount to apply to an entity that does not match a filter (0.0 = no grayscale, 1.0 = fully grayscale).")]
    public float FilterMismatchGrayscale { get; set; } = 0.70f;

    /// <summary>Las opciones que entiende el compositor de sprites (F3): una instancia nueva, sin estado compartido.</summary>
    public OpcionesSprites ComoOpciones() => new()
    {
        Preferencia = SpritePreference, SpritesBrillantes = ShinySprites, HuevoComoObjeto = ShowEggSpriteAsHeldItem, BolaDeEncuentro = ShowEncounterBall,
        ColorEncuentro = ShowEncounterColor, ColorEncuentroPkm = ShowEncounterColorPKM, OpacidadFondoEncuentro = ShowEncounterOpacityBackground,
        OpacidadFranjaEncuentro = ShowEncounterOpacityStripe, GrosorFranjaEncuentro = ShowEncounterThicknessStripe, PorcentajeExperiencia = ShowExperiencePercent,
        TipoTera = ShowTeraType, GrosorFranjaTera = ShowTeraThicknessStripe, OpacidadFondoTera = ShowTeraOpacityBackground, OpacidadFranjaTera = ShowTeraOpacityStripe,
        OpacidadFiltroNoCoincide = FilterMismatchOpacity, GrisFiltroNoCoincide = FilterMismatchGrayscale,
    };
}

/// <summary>Un punto entero (espejo de <c>System.Drawing.Point</c>, que no existe en el móvil).</summary>
public readonly record struct PuntoPkhex(int X, int Y);

/// <summary>Espejo de <c>PKHeX.WinForms.HoverSettings</c>. En táctil no hay ratón: ver las filas «no aplica» del censo (brillo, desplazamiento).</summary>
public sealed class DetallePkhex
{
    [LocalizedDescription("Show PKM Slot Preview on Hover")]
    public bool HoverSlotShowPreview { get; set; } = true;

    [LocalizedDescription("Show Encounter Info on Hover")]
    public bool HoverSlotShowEncounter { get; set; } = true;

    [LocalizedDescription("Show all Encounter Info properties on Hover")]
    public bool HoverSlotShowEncounterVerbose { get; set; }

    [LocalizedDescription("Show first Legality Check message if Illegal on Hover")]
    public bool HoverSlotShowLegalityHint { get; set; } = true;

    [LocalizedDescription("Show PKM Slot ToolTip on Hover")]
    public bool HoverSlotShowText { get; set; } = true;

    [LocalizedDescription("Play PKM Slot Cry on Hover")]
    public bool HoverSlotPlayCry { get; set; } = true;

    [LocalizedDescription("Show a Glow effect around the PKM on Hover")]
    public bool HoverSlotGlowEdges { get; set; } = true;

    [LocalizedDescription("Show Showdown Paste in special Preview on Hover")]
    public bool PreviewShowPaste { get; set; } = true;

    [LocalizedDescription("Show a Glow effect around the PKM on Hover")]
    public PuntoPkhex PreviewCursorShift { get; set; } = new(16, 8);
}

/// <summary>
/// Espejo de <c>PKHeX.WinForms.DrawConfig</c> (solo las propiedades que PKHeX guarda). Los colores son texto <c>#rrggbb</c>, como los guarda PKHeX en <c>cfg.json</c>
/// (<c>ColorJsonConverter</c>); DarkSalmon = (233,150,122).
/// </summary>
public sealed class DibujoPkhex
{
    [Category("Pokémon Editor"), LocalizedDescription("Background color of a ComboBox when the selected item is not valid.")]
    public string InvalidSelection { get; set; } = "#e9967a";

    [Category("Pokémon Editor"), LocalizedDescription("Blue colored marking.")]
    public string MarkBlue { get; set; } = "#00bfff";

    [Category("Pokémon Editor"), LocalizedDescription("Pink colored marking.")]
    public string MarkPink { get; set; } = "#ff75b3";

    [Category("Pokémon Editor"), LocalizedDescription("Shiny star when using unicode characters.")]
    public string ShinyUnicode { get; set; } = "☆";

    [Category("Pokémon Editor"), LocalizedDescription("Shiny star when not using unicode characters.")]
    public string ShinyDefault { get; set; } = "*";
}

/// <summary>Espejo de <c>PKHeX.WinForms.SlotExportSettings</c> (en PKHeX no es visible en la ventana de ajustes: <c>Browsable(false)</c>; en el móvil sí, en «Exportación»).</summary>
public sealed class ExportacionPkhex
{
    [LocalizedDescription("Settings to use for box exports.")]
    public BoxExportSettings BoxExport { get; set; } = new();

    [LocalizedDescription("Selected File namer to use for box exports for the GUI, if multiple are available.")]
    public string DefaultBoxExportNamer { get; set; } = "";

    [LocalizedDescription("Allow drag and drop of boxdata binary files from the GUI via the Box tab.")]
    public bool AllowBoxDataDrop { get; set; }
}
