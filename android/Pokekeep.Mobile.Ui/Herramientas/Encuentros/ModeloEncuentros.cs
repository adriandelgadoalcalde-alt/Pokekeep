// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using PKHeX.Core.Searching;
using Pokekeep.Mobile.Ui.Herramientas.BaseDatos;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.Encuentros;

/// <summary>
/// Los filtros de las pestañas «General» y «Avanzado» de <c>SAV_Encounters</c> (PK-SUB-011, PK-SUB-012): especie (0 = cualquiera), hasta cuatro movimientos, versión del EO (0 = cualquiera), huevo y variocolor
/// (null = indiferente, la casilla «a medias» de WinForms), las instrucciones de filtro y los tipos de encuentro marcados.
/// </summary>
public sealed record FiltrosEncuentros(ushort Especie, IReadOnlyList<ushort> Movimientos, GameVersion Version, bool? Huevo, bool? Variocolor, string Instrucciones, IReadOnlyList<EncounterTypeGroup> Tipos)
{
    public static FiltrosEncuentros Vacios => new(0, [], 0, null, null, string.Empty, ModeloEncuentros.TodosLosTipos);
}

/// <summary>El resultado de generar un encuentro: el Pokémon ya convertido al formato de la partida, o por qué no se pudo (texto del Core: <c>EntityConverter</c>).</summary>
public sealed record EncuentroGenerado(PKM? Pokemon, string? Error, EncounterCriteria Criterios)
{
    public bool Correcto => Pokemon is not null;
}

/// <summary>
/// «Generador de encuentros» (<c>SAV_Encounters</c>, PK-SUB-010) sin pantalla: la búsqueda (<c>SearchDatabase</c>), si se puede buscar (<c>IsSearchAllowed</c>), los criterios desde un Pokémon (<c>BuildCriteriaFromTabs</c>),
/// los criterios que se aplican a un encuentro (<c>GetCriteria</c>) y la generación (<c>ClickView</c>), con las MISMAS llamadas del Core y en el mismo orden. Los ajustes <c>EncounterDb</c> se reciben en cada
/// llamada (el que llama pasa los VIGENTES). Solo lee la partida: lo generado se coloca después por <c>Sesion.Casillas</c> (historial único) o se guarda como archivo por orden del usuario (regla 13).
/// <para>Diferencia con el escritorio, sin efecto en el resultado: PKHeX enseña los resultados como sprites en una cuadrícula y abre el elegido en las pestañas del editor; aquí son tarjetas de la tabla de
/// resultados y el «Pokémon de referencia» (el que en PKHeX está en el editor) se elige de la partida.</para>
/// </summary>
public static class ModeloEncuentros
{
    /// <summary>Los tipos de encuentro que el formulario ofrece como casillas (todos los de <c>EncounterTypeGroup</c> salvo <c>None</c>), en su orden.</summary>
    public static IReadOnlyList<EncounterTypeGroup> TodosLosTipos { get; } = [.. Enum.GetValues<EncounterTypeGroup>().Where(z => z != 0)];

    /// <summary>El nombre en castellano de cada tipo (PKHeX enseña el nombre del enum en inglés).</summary>
    public static string NombreTipo(EncounterTypeGroup t) => t switch
    {
        EncounterTypeGroup.Egg => IdiomaInterfaz.T("Informe.Columna.IsEgg"),
        EncounterTypeGroup.Mystery => IdiomaInterfaz.T("TextosEncuentro.RegaloMisterioso"),
        EncounterTypeGroup.Static => IdiomaInterfaz.T("ModeloEncuentros.EncuentroFijoRegalo"),
        EncounterTypeGroup.Trade => IdiomaInterfaz.T("ModeloEncuentros.Intercambio"),
        EncounterTypeGroup.Slot => IdiomaInterfaz.T("Libreria.Origen.Wild"),
        _ => t.ToString(),
    };

    /// <summary>
    /// El tipo de un encuentro en castellano por su clase del Core (el <c>LongName</c> del Core está en inglés: «Wild Encounter», «Static Encounter»…). Las clases siguen un patrón fijo
    /// (<c>EncounterSlot*</c>, <c>EncounterStatic*</c>, <c>EncounterTrade*</c>, <c>EncounterEgg*</c>, las tarjetas son <c>MysteryGift</c>); lo que no encaja se dice con el nombre del Core.
    /// </summary>
    public static string Tipo(IEncounterInfo e)
    {
        if (e is MysteryGift) return NombreTipo(EncounterTypeGroup.Mystery);
        var n = e.GetType().Name;
        if (n.StartsWith("EncounterSlot", StringComparison.Ordinal)) return NombreTipo(EncounterTypeGroup.Slot);
        if (n.StartsWith("EncounterTrade", StringComparison.Ordinal)) return NombreTipo(EncounterTypeGroup.Trade);
        if (n.StartsWith("EncounterEgg", StringComparison.Ordinal) || e.IsEgg) return NombreTipo(EncounterTypeGroup.Egg);
        if (n.StartsWith("EncounterStatic", StringComparison.Ordinal) || n.StartsWith("EncounterGift", StringComparison.Ordinal)) return NombreTipo(EncounterTypeGroup.Static);
        return e is IEncounterable ie ? ie.LongName : n;
    }

    /// <summary><c>GetSearchSettings</c>: los filtros como <c>SearchSettings</c> del Core (lo que usan <c>IsSearchAllowed</c> y <c>GetVersions</c>).</summary>
    public static SearchSettings Busqueda(SaveFile sav, FiltrosEncuentros f)
    {
        var s = new SearchSettings { Context = sav.Context, Generation = sav.Generation, Species = f.Especie, BatchInstructions = f.Instrucciones, Version = f.Version };
        foreach (var m in f.Movimientos) s.AddMove(m);
        if (f.Huevo is { } h) s.SearchEgg = h;
        if (f.Variocolor is { } v) s.SearchShiny = v;
        return s;
    }

    /// <summary>
    /// <c>IsSearchAllowed</c>: sin ningún tipo marcado no se busca; sin especie ni movimientos tampoco si el ajuste <c>EncounterDb.ReturnNoneIfEmptySearch</c> está activo (lo que PKHeX hace al desactivar «¡Buscar!»).
    /// Devuelve el motivo en castellano, o null si se puede buscar.
    /// </summary>
    public static string? MotivoNoBuscar(SaveFile sav, FiltrosEncuentros f, EncounterDatabaseSettings ajustes)
    {
        if (f.Tipos.Count == 0) return IdiomaInterfaz.T("ModeloEncuentros.MarcaMenosTipoEncuentro");
        var s = Busqueda(sav, f);
        if (s is { Species: 0, Moves.Count: 0 } && ajustes.ReturnNoneIfEmptySearch) return IdiomaInterfaz.T("ModeloEncuentros.EligeEspecieMovimientoBuscar");
        return null;
    }

    /// <summary>
    /// <c>B_Search_Click</c> + <c>SearchDatabase</c>: fija <c>EncounterMovesetGenerator.PriorityList</c> con los tipos marcados, recorre especies y formas, filtra por huevo, variocolor, especies que la partida
    /// no admite (ajuste <c>EncounterDb.FilterUnavailableSpecies</c>) e instrucciones, quita los repetidos POR REFERENCIA y deja <c>PriorityList</c> como estaba. La lista de tipos del Core es global: la búsqueda
    /// se hace bajo el cerrojo de legalidad de la app (<c>LegalityGate</c>) para no pisar a otro análisis. Cancelable entre especies.
    /// </summary>
    public static List<IEncounterInfo> Buscar(SaveFile sav, FiltrosEncuentros f, EncounterDatabaseSettings ajustes, CancellationToken cancelar = default, IProgress<AvanceTrabajo>? avance = null)
    {
        Pokekeep.Mobile.Ui.Ajustes.Pkhex.TextosNucleo.Preparar();
        if (MotivoNoBuscar(sav, f, ajustes) is not null) return [];
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo)
        {
            EncounterMovesetGenerator.PriorityList = [.. f.Tipos];
            try { return BuscarSinCerrojo(sav, f, ajustes, cancelar, avance); }
            finally { EncounterMovesetGenerator.ResetFilters(); }
        }
    }

    private static List<IEncounterInfo> BuscarSinCerrojo(SaveFile sav, FiltrosEncuentros f, EncounterDatabaseSettings ajustes, CancellationToken cancelar, IProgress<AvanceTrabajo>? avance)
    {
        var settings = Busqueda(sav, f);
        var pk = sav.BlankPKM;
        var moves = settings.Moves.ToArray();
        // PKHeX 26.10.18: SearchSettings.GetVersions devuelve todos los guardables entre MinGameID y MaxGameID, que ahora incluye WI y WA (Winds y Waves, sin contexto de entidad), y
        // EncounterMovesetGenerator lanza ArgumentOutOfRangeException para ellos (el formulario real de PKHeX 26.10.18 se cierra con 'Cualquiera' en las partidas de la era HOME). Aqui se quitan.
        var versions = (ReadOnlyMemory<GameVersion>)settings.GetVersions(sav).ToArray().Where(static v => v.Context.IsValid).ToArray();
        IReadOnlyList<ushort> especies = settings.Species == 0 ? [.. Enumerable.Range(1, sav.MaxSpeciesID).Select(i => (ushort)i)] : [settings.Species];
        IEnumerable<IEncounterInfo> results = Recorrer(especies, sav, versions, moves, pk, ajustes, cancelar, avance);
        if (settings.SearchEgg is not null) results = results.Where(z => z.IsEgg == settings.SearchEgg);
        if (settings.SearchShiny is not null) results = results.Where(z => z.IsShiny == settings.SearchShiny);
        results = SinRepetidosPorReferencia(results);
        if (ajustes.FilterUnavailableSpecies && EntityPresenceFilters.GetFilterGeneric<IEncounterInfo>(sav.Context) is { } filtro) results = results.Where(filtro);
        if (InstruccionesFiltro.Crear<IEncounterInfo>(f.Instrucciones) is { } instr) results = results.Where(instr);
        var lista = results.ToList();
        cancelar.ThrowIfCancellationRequested();
        return lista;
    }

    /// <summary><c>GetAllSpeciesFormEncounters</c>: las formas de cada especie (las de combate no), con la tabla de USUM si la especie no está en el juego y no se ocultan las no disponibles.</summary>
    private static IEnumerable<IEncounterInfo> Recorrer(IReadOnlyList<ushort> especies, SaveFile sav, ReadOnlyMemory<GameVersion> versions, ReadOnlyMemory<ushort> moves, PKM pk, EncounterDatabaseSettings ajustes,
        CancellationToken cancelar, IProgress<AvanceTrabajo>? avance)
    {
        var pt = sav.Personal;
        for (var i = 0; i < especies.Count; i++)
        {
            cancelar.ThrowIfCancellationRequested();
            var s = especies[i];
            if (especies.Count > 1 && i % 16 == 0) avance?.Report(new AvanceTrabajo(IdiomaInterfaz.F("ModeloEncuentros.BuscandoEncuentrosEspecies", i, especies.Count), (double)i / especies.Count * 100));
            var pi = pt.GetFormEntry(s, 0);
            var fc = pi.FormCount;
            if (fc == 0 && !ajustes.FilterUnavailableSpecies) { pi = PersonalTable.USUM.GetFormEntry(s, 0); fc = pi.FormCount; }
            for (byte form = 0; form < fc; form++)
            {
                if (FormInfo.IsBattleOnlyForm(s, form, pk.Format)) continue;
                pk.Species = s;
                pk.Form = form;
                pk.SetGender(pk.GetSaneGender());
                EncounterMovesetGenerator.OptimizeCriteria(pk, sav);
                foreach (var enc in EncounterMovesetGenerator.GenerateEncounters(pk, moves, versions)) yield return enc;
            }
        }
    }

    /// <summary>Quita los repetidos POR REFERENCIA (lo que hace <c>SearchDatabase</c> de PKHeX). X20: el comparador anterior decía «iguales» cuando coincidía <c>RuntimeHelpers.GetHashCode</c>, que es un número de 26 bits y NO identifica al objeto: dos encuentros distintos con el mismo número se fundían en uno y la búsqueda perdía un resultado de forma intermitente (1.411 en lugar de 1.412 en Pikachu de Escarlata, según los objetos que el Core crea en cada búsqueda, como los huevos).</summary>
    public static IEnumerable<T> SinRepetidosPorReferencia<T>(IEnumerable<T> origen) where T : class => origen.Distinct((IEqualityComparer<T>)(object)ReferenceEqualityComparer.Instance);

    /// <summary><c>BuildCriteriaFromTabs</c>: los criterios que salen de un Pokémon (en PKHeX, el del editor; aquí, el de referencia o uno en blanco del formato de la partida).</summary>
    public static EncounterCriteria CriteriosDe(PKM referencia)
    {
        var set = new ShowdownSet(referencia);
        var mutations = EncounterMutationUtil.GetSuggested(referencia.Context, set.Level);
        var criteria = EncounterCriteria.GetCriteria(set, referencia.PersonalInfo, mutations);
        if (referencia.Context.IsHyperTrainingAvailable(100)) criteria = criteria.ReviseIVsHyperTrainAvailable();
        return criteria;
    }

    /// <summary>
    /// <c>GetCriteria</c>: sin el ajuste <c>EncounterDb.UseTabsAsCriteria</c>, sin restricciones; con él, los criterios elegidos, salvo que el encuentro no sea de la línea evolutiva del Pokémon de referencia y el ajuste
    /// <c>UseTabsAsCriteriaAnySpecies</c> esté apagado. Fuera de la línea o con especie de un solo sexo el sexo pasa a aleatorio, y una habilidad imposible para el encuentro pasa a «cualquiera».
    /// </summary>
    public static EncounterCriteria CriteriosPara(IEncounterTemplate enc, EncounterDatabaseSettings ajustes, PKM referencia, EncounterCriteria elegidos)
    {
        if (!ajustes.UseTabsAsCriteria) return EncounterCriteria.Unrestricted;
        var tree = EvolutionTree.GetEvolutionTree(referencia.Context);
        var enLaLinea = tree.IsSpeciesDerivedFrom(referencia.Species, referencia.Form, enc.Species, enc.Form);
        if (!ajustes.UseTabsAsCriteriaAnySpecies && !enLaLinea) return EncounterCriteria.Unrestricted;
        var criteria = elegidos;
        if (!enLaLinea || EntityGender.IsSingleGender(enc.Species)) criteria = criteria with { Gender = Gender.Random };
        if (!criteria.Mutations.CanGetAbility(enc.Ability, criteria.Ability)) criteria = criteria with { Ability = AbilityPermission.Any12H };
        return criteria;
    }

    /// <summary>
    /// <c>ClickView</c>: genera el Pokémon del encuentro con el entrenador de la base de entrenadores para su versión (o el de la partida), lo convierte al formato de la partida, lo adapta a ella y recalcula la suma.
    /// La generación sortea (PID, IV…): dos llamadas dan Pokémon distintos, como en PKHeX. Bajo el cerrojo de legalidad, porque el Core analiza al generar.
    /// </summary>
    public static EncuentroGenerado Generar(IEncounterInfo enc, SaveFile sav, TrainerDatabase entrenadores, EncounterDatabaseSettings ajustes, PKM referencia, EncounterCriteria elegidos)
    {
        Pokekeep.Mobile.Ui.Ajustes.Pkhex.TextosNucleo.Preparar();
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo)
        {
            var criteria = CriteriosPara(enc, ajustes, referencia, elegidos);
            var trainer = entrenadores.GetTrainer(enc.Version, enc.Generation <= 2 ? (LanguageID)sav.Language : null) ?? sav;
            var temp = enc.ConvertToPKM(trainer, criteria);
            var pk = EntityConverter.ConvertToType(temp, sav.PKMType, out var c);
            if (pk is null) return new(null, c.GetDisplayString(temp, sav.PKMType), criteria);
            sav.AdaptToSaveFile(pk);
            pk.RefreshChecksum();
            return new(pk, null, criteria);
        }
    }

    /// <summary>
    /// <c>Menu_EncDatabase_Click</c>: la base de entrenadores con los Pokémon de la carpeta de entrenadores (<c>LocalResources.GetTrainerPath()</c>, con subcarpetas), uno por versión e idioma. Sin carpeta, vacía:
    /// cada encuentro se genera con el entrenador de la partida, como en PKHeX sin esa carpeta.
    /// </summary>
    public static TrainerDatabase CargarEntrenadores(string? carpeta, EntityContext contexto, CancellationToken cancelar = default)
    {
        var db = new TrainerDatabase();
        if (string.IsNullOrEmpty(carpeta) || !Directory.Exists(carpeta)) return db;
        var files = Directory.EnumerateFiles(carpeta, "*.*", SearchOption.AllDirectories);
        foreach (var pk in BoxUtil.GetPKMsFromPaths(files, contexto))
        {
            cancelar.ThrowIfCancellationRequested();
            db.RegisterCopy(pk);
        }
        return db;
    }

    /// <summary>Las líneas de texto de un encuentro como las enseña PKHeX al pasar por encima (<c>GetTextLines</c>), en castellano.</summary>
    public static IReadOnlyList<string> Lineas(IEncounterInfo enc)
    {
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo) return enc.GetTextLines(false, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);
    }
}
