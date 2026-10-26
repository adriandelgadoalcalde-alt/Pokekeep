// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.ForzarCarga;

/// <summary>Una opción de una lista del solucionador: el texto que se ve y el valor del Core.</summary>
public sealed record OpcionForzar<T>(string Texto, T Valor, string? Detalle = null);

/// <summary>Lo que devuelve «Continuar»: el guardado reconocido (y, si es de Gen. 3 y hay que elegir versión, las versiones entre las que elegir), o el mensaje de error de PKHeX.</summary>
public sealed record ResultadoForzar(SaveFile? Partida, string? Error, IReadOnlyList<OpcionForzar<GameVersion>> VersionesGen3, int VersionActual = -1)
{
    public bool Hecho => Partida is not null && Error is null;
    public bool PideVersion => Partida is not null && VersionesGen3.Count > 0;
}

/// <summary>
/// EL SOLUCIONADOR DEL GESTOR DE GUARDADO DE PKHeX SIN PANTALLA (<c>SaveHandlerTroubleshooter</c>, «Forzar carga de SAV», Ctrl+Alt+L; bloque 10-4: PK-SUB-021, PK-MENU-024, PK-MENU-025 y PK-MENU-047).
/// Las mismas listas y la misma llamada al Core que el escritorio:
/// <list type="bullet">
/// <item>Tipo de archivo de guardado: <c>SaveFileType</c> sin <c>None</c> (con los juegos de cada tipo en castellano como ayuda);</item>
/// <item>Subversión: <c>GameUtil.GameVersions</c> de ese tipo (vacía = no se pregunta, como PKHeX oculta la lista);</item>
/// <item>Idioma: <c>LanguageID</c>; Manejador: <c>SaveHandlerDefault</c> y después <c>SaveUtil.Handlers</c> (nombre sin el prefijo «SaveHandler»);</item>
/// <item>«Continuar» = <c>SaveUtil.TryGetSaveFileHandler(datos, out sav, ruta, manejador, new SaveTypeInfo(tipo, subversión, idioma))</c> y después lo que hace <c>OpenSAV(sav, ruta, forceOpen: true)</c>: sin la comprobación
/// de versión, nombre del archivo en <c>Metadata</c>, idioma de Gen. 1-3 (<c>SaveLanguage.TryRevise</c>) y, en Gen. 3, la elección de versión de <c>SanityCheckSAV</c> si la Pokédex está corrupta (<c>IsCorruptPokedexFF</c>),
/// si se pide a mano (la tecla Ctrl de PKHeX) o si un Rojo Fuego/Verde Hoja no dice cuál es.</item>
/// </list>
/// </summary>
public static class ModeloForzarCarga
{
    /// <summary>Los tipos de guardado, en el orden de PKHeX. El texto es el nombre del Core (el mismo que enseña PKHeX); el detalle, los juegos de ese tipo en castellano.</summary>
    public static IReadOnlyList<OpcionForzar<SaveFileType>> Tipos()
    {
        var juegos = GameInfo.GetStrings(Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma).gamelist;
        return [.. Enum.GetValues<SaveFileType>().Where(z => z is not SaveFileType.None).Select(t =>
        {
            var deEste = Subversiones(t, juegos).Select(v => v.Texto).ToList();
            return new OpcionForzar<SaveFileType>(t.ToString(), t, deEste.Count == 0 ? null : string.Join(", ", deEste));
        })];
    }

    /// <summary><c>UpdateSubVersionChoices</c>: las versiones de ese tipo (vacía si el tipo no tiene ninguna; entonces la subversión no se pregunta y vale <c>GameVersion.Any</c>).</summary>
    public static IReadOnlyList<OpcionForzar<GameVersion>> Subversiones(SaveFileType tipo) => Subversiones(tipo, GameInfo.GetStrings(Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma).gamelist);

    private static IReadOnlyList<OpcionForzar<GameVersion>> Subversiones(SaveFileType tipo, IReadOnlyList<string> juegos)
        => [.. GameUtil.GameVersions.Where(z => z.SaveFileType == tipo).Distinct().Select(v => new OpcionForzar<GameVersion>(NombreVersion(v, juegos), v))];

    private static string NombreVersion(GameVersion v, IReadOnlyList<string> juegos)
    {
        var i = (int)v;
        var t = i >= 0 && i < juegos.Count ? juegos[i] : null;
        return string.IsNullOrWhiteSpace(t) ? v.ToString() : t;
    }

    private static Dictionary<LanguageID, string> NombresIdioma => new()
    {
        [LanguageID.None] = IdiomaInterfaz.T("ModeloForzarCarga.NingunoDigaGuardado"), [LanguageID.Japanese] = IdiomaInterfaz.T("ModeloForzarCarga.Japones"), [LanguageID.English] = IdiomaInterfaz.T("ModeloForzarCarga.Ingles"), [LanguageID.French] = IdiomaInterfaz.T("ModeloForzarCarga.Frances"), [LanguageID.Italian] = IdiomaInterfaz.T("ModeloForzarCarga.Italiano"),
        [LanguageID.German] = IdiomaInterfaz.T("ModeloForzarCarga.Aleman"), [LanguageID.Spanish] = IdiomaInterfaz.T("ModeloForzarCarga.Espanol"), [LanguageID.Korean] = IdiomaInterfaz.T("ModeloForzarCarga.Coreano"), [LanguageID.ChineseS] = "Chino simplificado", [LanguageID.ChineseT] = "Chino tradicional",
        [LanguageID.SpanishL] = IdiomaInterfaz.T("ModeloForzarCarga.EspanolLatinoamericano"),
    };

    /// <summary>Los idiomas de <c>LanguageID</c> (todos, como PKHeX), con su nombre en castellano y el del Core como detalle.</summary>
    public static IReadOnlyList<OpcionForzar<LanguageID>> Idiomas()
        => [.. Enum.GetValues<LanguageID>().Select(l => new OpcionForzar<LanguageID>(NombresIdioma.TryGetValue(l, out var n) ? n : l.ToString(), l, l.ToString()))];

    /// <summary>Los manejadores: el de por defecto y los del Core, con el nombre que enseña PKHeX (<c>GetHandlerDisplayName</c>).</summary>
    public static IReadOnlyList<OpcionForzar<ISaveHandler>> Manejadores()
    {
        var l = SaveUtil.Handlers.ToList();
        l.Insert(0, new SaveHandlerDefault());
        return [.. l.Select(h => new OpcionForzar<ISaveHandler>(NombreManejador(h), h, h is SaveHandlerDefault ? IdiomaInterfaz.T("ModeloForzarCarga.CabeceraNiPieArchivo") : null))];
    }

    public static string NombreManejador(ISaveHandler h)
    {
        const string Prefijo = "SaveHandler";
        var n = h.GetType().Name;
        return n.StartsWith(Prefijo, StringComparison.Ordinal) ? n[Prefijo.Length..] : n;
    }

    /// <summary>«Continuar». <paramref name="elegirVersion"/> es la tecla Ctrl de PKHeX: pedir la versión de Gen. 3 aunque la Pokédex esté bien. Trabaja sobre una COPIA de <paramref name="datos"/>.</summary>
    public static ResultadoForzar Cargar(byte[] datos, string nombre, SaveFileType tipo, GameVersion subversion, LanguageID idioma, ISaveHandler manejador, bool elegirVersion = false)
    {
        TextosNucleo.Preparar();
        SaveFile? sav;
        try
        {
            if (!SaveUtil.TryGetSaveFileHandler(datos.ToArray(), out sav, nombre, manejador, new SaveTypeInfo(tipo, subversion, idioma)))
                return new(null, $"{MessageStrings.MsgFileLoadSaveFail} «{nombre}»", []);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return new(null, $"{MessageStrings.MsgFileLoadSaveLoadFail} {ex.Message}", []); }
        // OpenSAV(sav, path, forceOpen: true): sin IsVersionValid; SetExtraInfo(path) (aquí el nombre: Android no da rutas); SanityCheckSAV.
        sav.Metadata.SetExtraInfo(Path.GetFileName(nombre));
        if (sav.Generation <= 3) SaveLanguage.TryRevise(sav);
        return new(sav, null, VersionesAElegir(sav, elegirVersion, out var actual), actual);
    }

    /// <summary>Las versiones entre las que <c>SanityCheckSAV</c> hace elegir (vacía = ninguna elección).</summary>
    public static IReadOnlyList<OpcionForzar<GameVersion>> VersionesAElegir(SaveFile sav, bool aMano, out int actual)
    {
        actual = -1;
        if (!sav.State.Exportable || sav is not SAV3 s3) return [];
        var juegos = GameInfo.GetStrings(Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma).gamelist;
        if (aMano || s3.IsCorruptPokedexFF())
        {
            GameVersion[] todas = [GameVersion.R, GameVersion.S, GameVersion.E, GameVersion.FR, GameVersion.LG];
            actual = Array.IndexOf(todas, s3.Version);
            return [.. todas.Select(v => new OpcionForzar<GameVersion>(NombreVersion(v, juegos), v))];
        }
        if (s3 is SAV3FRLG frlg && !frlg.Version.IsValidSavedVersion())
            return [new(NombreVersion(GameVersion.FR, juegos), GameVersion.FR), new(NombreVersion(GameVersion.LG, juegos), GameVersion.LG)];
        return [];
    }

    /// <summary>
    /// La versión de Gen. 3 elegida (<c>SanityCheckSAV</c>): si la Pokédex está corrupta o se pidió a mano, <c>ForceLoad(versión)</c> (otro tipo de guardado con el mismo contenido); si es Rojo Fuego/Verde Hoja,
    /// <c>ResetPersonal</c> (las estadísticas de Deoxys de esa versión). Null si el Core no lo acepta (PKHeX cancela la apertura).
    /// </summary>
    public static SaveFile? AplicarVersion(SaveFile sav, GameVersion version, bool aMano)
    {
        if (sav is not SAV3 s3) return sav;
        if (!VersionesAElegir(sav, aMano, out _).Any(v => v.Valor == version)) return null;
        if (aMano || s3.IsCorruptPokedexFF())
        {
            var s = s3.ForceLoad(version);
            if (s is SAV3FRLG frlg && !frlg.ResetPersonal(version)) return null;
            if (sav.Metadata.FilePath is { } origen) s.Metadata.SetExtraInfo(origen);
            else if (sav.Metadata.FileName is { } nombre) s.Metadata.SetExtraInfo(nombre);
            return s;
        }
        if (s3 is SAV3FRLG f) return f.ResetPersonal(version) ? f : null;
        return sav;
    }
}
