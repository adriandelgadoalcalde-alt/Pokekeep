// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoLegalidad.Catalog.Library;
using AutoLegalidad.Ui;
using AutoLegalidad.Ui.Library;
using PKHeX.Core;
using Pokekeep.Mobile.Ui;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;

namespace Pokekeep.Mobile.Operaciones;

/// <summary>
/// El catálogo de la Librería en el móvil: <see cref="LibraryData"/> del núcleo compartido (solo lectura; <c>addable=1</c> es la única puerta) con los MISMOS textos, filtros, orden y ficha que la pestaña del
/// escritorio (<c>LibraryNames</c>, <c>LibraryQueries</c>, <c>LibrarySheet</c>). Nada de esto sabe de Android: se prueba en el PC con la misma base de datos que viaja en el APK.
/// </summary>
public class CatalogoLibreriaDatos(LibraryData datos) : ICatalogoLibreria, IDisposable
{
    public LibraryData Datos { get; } = datos;

    public bool Disponible => Datos.IsAvailable;
    public int JuegoDe(SaveFile sav) => LibraryData.GameIdOf(sav);
    public int DestinoGc(SaveFile sav) => LibraryData.GcDestinationOf(sav);

    internal static LibraryFilter Convertir(FiltroLibreria f) => new()
    {
        GameId = f.Juego,
        GcDestination = f.DestinoGc,
        Text = string.IsNullOrWhiteSpace(f.Texto) ? null : f.Texto.Trim(),
        Kind = f.Origen,
        Generation = f.Generacion,
        Species = f.Especie,
        Form = f.Forma,
        EventLanguage = f.Idioma,
        EventRegion = f.Region,
        OnlyAvailable = f.SoloDisponibles,
        Limit = f.Limite,
        Offset = f.Desplazamiento,
    };

    public IReadOnlyList<EntradaLibreria> Consultar(FiltroLibreria filtro)
        => [.. Datos.Query(Convertir(filtro)).Select(r => Entrada(r, filtro.PermitirHome))];

    public long Contar(FiltroLibreria filtro) => Datos.Count(Convertir(filtro) with { Limit = 1, Offset = 0 });

    public IReadOnlyList<(string Codigo, string Etiqueta)> Origenes() => [.. LibraryNames.Kinds.Select(k => (k.Kind, IdiomaInterfaz.T("Libreria.Origen." + k.Kind)))];
    public IReadOnlyList<(string Codigo, string Etiqueta)> IdiomasEvento() => [.. Datos.EventLanguages().Select(l => (l, IdiomaEvento(l)))];
    public IReadOnlyList<(string Codigo, string Etiqueta)> Regiones() => [.. LibraryNames.Regions.Select(r => (r.Code, IdiomaInterfaz.T("Libreria.Region." + r.Code)))];
    public IReadOnlyList<(int Primera, int Ultima, string Etiqueta)> RangosPokedex() => [.. LibraryNames.DexRanges.Select(r => (r.First, r.Last, IdiomaInterfaz.T("Libreria.Rango." + r.First)))];
    private static string IdiomaEvento(string codigo)
        => ProgramaPkhex.Idioma == "en" && Enum.TryParse<LanguageID>(codigo, out var id) && (uint)id < ProgramaPkhex.Textos.languageNames.Length
            ? ProgramaPkhex.Textos.languageNames[(int)id] : LibraryNames.Language(codigo);

    public string NombreEspecie(int especie) => LibraryNames.SpeciesName(especie, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);

    public IReadOnlyList<(int Forma, string Nombre)> Formas(int juego, int especie)
        => [.. Datos.FormsOf(juego, especie).Select(f => (f.Form, NombreForma(especie, f.Form, juego, f.Name)))];

    public EntradaLibreria? Entrada(int juego, int destinoGc, string clave, bool permitirHome)
        => Datos.Find(juego, clave) is { } r ? Entrada(r, permitirHome) : null;

    /// <summary>Una fila del catálogo como la pinta la lista: textos del núcleo, atenuada con su motivo cuando no existe, no se puede generar todavía o necesita HOME sin el ajuste.</summary>
    public static EntradaLibreria Entrada(LibraryRow r, bool permitirHome)
    {
        var home = LibraryData.HomeBlockedReason(r, permitirHome);
        var especie = LibraryNames.SpeciesName(r.SpeciesId, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);
        var brillante = r.ShinyRule is "Always" or "AlwaysStar" or "AlwaysSquare";
        return new EntradaLibreria(r.EntryKey, r.SpeciesId, r.FormId, LibraryNames.SpeciesName(r.SpeciesId, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma), r.FormId == 0 ? null : NombreForma(r.SpeciesId, r.FormId, r.GameId, r.FormName ?? IdiomaInterfaz.F("LibreriaTextos.FormaN", r.FormId)),
            Juego(r.GameName, r.GameId), LibraryNames.Origin(r), r.Available, LibraryNames.DimReasonText(r), r.Addable, r.HomeRequired, r.FixedGender == 1 ? 1 : 0, brillante)
        {
            OrigenCodigo = r.Kind,
            Estado = LibraryNames.State(r, home),
            Procedencia = LibraryNames.Provenance(r),
            Region = LibraryNames.Region(r.EventRegion),
            BloqueoHome = home,
            Gigamax = r.Gigantamax,
            JuegoId = r.GameId,
            NumeroPokedex = r.DexNumber,
        };
    }

    private static string Juego(string nombre, int id)
    {
        var juegos = Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Textos.gamelist;
        if ((uint)id < juegos.Length && !string.IsNullOrEmpty(juegos[id])) nombre = juegos[id];
        return nombre.StartsWith("Pokémon ", StringComparison.Ordinal) ? nombre["Pokémon ".Length..] : nombre;
    }

    private static string NombreForma(int especie, int forma, int juego, string respaldo)
    {
        var textos = Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Textos;
        var formas = FormConverter.GetFormList((ushort)especie, textos.types, textos.forms, Pokekeep.Mobile.Ui.Ajustes.Pkhex.PresentacionPkhex.SimbolosGenero, ((GameVersion)juego).Context);
        return (uint)forma < formas.Length && !string.IsNullOrEmpty(formas[forma]) ? formas[forma] : respaldo;
    }

    // ============================================================================================ ficha
    public FichaLibreria? Ficha(EntradaLibreria entrada, bool permitirHome)
    {
        var row = Datos.Find(entrada.JuegoId, entrada.Clave);
        if (row is null) return null;
        var lineas = new List<LineaFicha>();
        void Linea(string t) => lineas.Add(new LineaFicha(t));
        var home = LibraryData.HomeBlockedReason(row, permitirHome);
        var especie = LibraryNames.SpeciesName(row.SpeciesId, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);
        var titulo = especie + (row.FormId != 0 ? $" ({NombreForma(row.SpeciesId, row.FormId, row.GameId, row.FormName ?? IdiomaInterfaz.F("LibreriaTextos.FormaN", row.FormId))})" : string.Empty) + " · " + IdiomaInterfaz.F("LibreriaTextos.FichaNumero", row.DexNumber);
        lineas.Add(new LineaFicha(titulo, Negrita: true));
        Linea($"{Juego(row.GameName, row.GameId)}: {(row.Available ? (home is not null ? IdiomaInterfaz.T("LibreriaTextos.FichaSoloHome") : IdiomaInterfaz.T("LibreriaTextos.FichaDisponible")) : LibraryNames.DimReasonText(row) ?? IdiomaInterfaz.T("LibreriaTextos.FichaNoDisponible"))}");
        Linea(IdiomaInterfaz.F("LibreriaTextos.FichaOrigen", LibraryNames.Origin(row)));
        if (row.HomeRequired) Linea(home ?? Textos.NecesitaHomeConAjuste);
        try
        {
            if (Datos.Detail(row.EntryKey) is { } json)
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                // 0.6.5 (ADR 0052): quién figura como entrenador original (también en la cría, que no tiene encuentro propio en el catálogo).
                if (row.Available && row.Kind is not ("Absent" or "FormNormalization"))
                {
                    var hasEnc = root.TryGetProperty("encounter", out var enc0) && enc0.ValueKind == JsonValueKind.Object;
                    var type = hasEnc && enc0.TryGetProperty("type", out var ty) ? ty.GetString() : null;
                    var traitsJson = hasEnc && enc0.TryGetProperty("fixedTraits", out var ft) ? ft.GetRawText() : null;
                    var rank = row.HomeRequired ? 5 : OwnerRanks.OfKind(row.Kind, OwnerRanks.IsOwnerFixed(row.Kind, type, traitsJson, row.EncounterKey));
                    Linea(LibraryNames.OwnerLine(rank));
                }
                if (root.TryGetProperty("encounter", out var enc) && enc.ValueKind == JsonValueKind.Object)
                {
                    if (row.Kind == "Event")
                    {
                        Linea(IdiomaInterfaz.F("LibreriaTextos.FichaEvento", LibraryNames.EventTitle(row)) + (LibraryNames.EventTitleSourceText(row) is { } src ? $" ({src})" : " " + IdiomaInterfaz.T("LibreriaTextos.FichaSinTitulo")));
                        if (LibraryNames.OriginalEventTitle(row) is { } original)
                            Linea(string.IsNullOrWhiteSpace(row.EventLanguage) ? IdiomaInterfaz.F("LibreriaTextos.FichaTituloOriginal", original) : IdiomaInterfaz.F("LibreriaTextos.FichaTituloOriginalIdioma", LibraryNames.Language(row.EventLanguage!), original));
                        var partes = new List<string>();
                        if (!string.IsNullOrWhiteSpace(row.EventLanguage)) partes.Add(IdiomaInterfaz.F("LibreriaTextos.FichaIdioma", LibraryNames.Language(row.EventLanguage!)));
                        if (!string.IsNullOrWhiteSpace(row.EventStartDate)) partes.Add(IdiomaInterfaz.F("LibreriaTextos.FichaDesde", row.EventStartDate));
                        if (enc.TryGetProperty("restrictLanguage", out var rl) && !string.IsNullOrEmpty(rl.GetString())) partes.Add(IdiomaInterfaz.F("LibreriaTextos.FichaSoloPara", LibraryNames.Language(rl.GetString()!)));
                        if (partes.Count > 0) Linea(string.Join(" · ", partes));
                        // Hito 5d (ADR 0047): la región solo se enseña si el Core o la documentación de la tarjeta la aportan; si no, se dice que no hay dato (nunca se deduce del idioma).
                        Linea(string.IsNullOrWhiteSpace(row.EventRegion)
                            ? IdiomaInterfaz.T("LibreriaTextos.FichaRegionSinDato")
                            : IdiomaInterfaz.F("LibreriaTextos.FichaRegion", LibraryNames.Region(row.EventRegion), LibraryNames.RegionSource(row.EventRegionSource)));
                    }
                    if (enc.TryGetProperty("levelMin", out var lmin) && enc.TryGetProperty("levelMax", out var lmax))
                        Linea(lmin.GetInt32() == lmax.GetInt32() ? IdiomaInterfaz.F("LibreriaTextos.FichaNivel", lmin.GetInt32()) : IdiomaInterfaz.F("LibreriaTextos.FichaNiveles", lmin.GetInt32(), lmax.GetInt32()));
                }
                if (root.TryGetProperty("provenance", out var prov) && prov.ValueKind == JsonValueKind.Object)
                {
                    var rev = prov.TryGetProperty("revision", out var rv) ? rv.GetString() : null;
                    var sha = prov.TryGetProperty("artifact_sha256", out var sh) ? sh.GetString() : null;
                    Linea(IdiomaInterfaz.F("LibreriaTextos.FichaFuente", LibraryNames.Source(row.SourceLabel)) + (rev is { Length: >= 7 } ? IdiomaInterfaz.F("LibreriaTextos.FichaCommit", rev[..7]) : string.Empty) + (sha is { Length: >= 12 } ? $" · SHA-256 {sha[..12]}…" : string.Empty));
                }
                else
                    Linea(IdiomaInterfaz.F("LibreriaTextos.FichaFuente", LibraryNames.Source(row.SourceLabel)));
                if (root.TryGetProperty("traits", out var tr))
                {
                    if (tr.TryGetProperty("fixed", out var fx)) Linea(IdiomaInterfaz.F("LibreriaTextos.FichaRasgosFijos", LibraryData.TraitList(fx)));
                    if (tr.TryGetProperty("variable", out var vr)) Linea(IdiomaInterfaz.F("LibreriaTextos.FichaRasgosVariables", LibraryData.TraitList(vr)));
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            Linea(IdiomaInterfaz.F("LibreriaTextos.FichaNoLeida", ex.Message));
        }
        return new FichaLibreria(titulo, lineas);
    }

    // ============================================================================================ exportar
    /// <summary>Nombre de fichero sugerido: especie, juego y un trozo de la clave (sin tildes ni caracteres no válidos). El mismo cálculo que la pestaña del escritorio.</summary>
    public static string NombreSugerido(LibraryRow row, string especie)
    {
        static string Limpiar(string s)
        {
            var normalizado = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in normalizado)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && (char.IsLetterOrDigit(ch) || ch is '-' or '_')) sb.Append(ch);
                else if (ch == ' ') sb.Append('-');
            return sb.ToString();
        }
        var clave = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(row.EntryKey)))[..8].ToLowerInvariant();
        return $"ficha-{Limpiar(especie)}-{Limpiar(row.GameName)}-{clave}";
    }

    public FicheroFicha? ExportarFicha(EntradaLibreria entrada, FormatoFicha formato, VersionesMotor? motor)
    {
        var row = Datos.Find(entrada.JuegoId, entrada.Clave);
        if (row is null) return null;
        var versiones = motor is null ? null : new SheetEngineVersions(motor.Motor, motor.Adaptador, motor.Alm, motor.AlmCommit, motor.Core, motor.CoreCommit, motor.CoreSha256);
        var hoja = Datos.Sheet(row, versiones);
        if (hoja is null) return null;
        var nombre = NombreSugerido(row, LibraryNames.SpeciesName(row.SpeciesId, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma));
        return formato == FormatoFicha.Texto
            ? new FicheroFicha(nombre, ".txt", LibrarySheet.ToText(hoja), "text/plain")
            : new FicheroFicha(nombre, ".json", LibrarySheet.ToJson(hoja) + Environment.NewLine, "application/json");
    }

    public void Dispose() => Datos.Dispose();
}
