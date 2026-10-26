// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.Concurrent;
using PKHeX.Core;
using PKHeX.Core.Searching;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Herramientas.BaseDatos;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.BaseDatosPkm;

/// <summary>Lo que dice una acción sobre la base de datos (añadir, borrar, exportar...): si salió bien y el texto para el usuario (el del Core donde PKHeX lo da).</summary>
public sealed record ResultadoBd(bool Correcto, string Mensaje);

/// <summary>
/// «Base de datos de PKM» (<c>SAV_Database</c>, PK-SUB-007 a PK-SUB-009, PK-MENU-018) sin pantalla, con las MISMAS llamadas del Core y en el mismo orden que el formulario: la carga
/// (<c>LoadDatabase</c> + <c>LoadEntitiesFromFolder</c>), la búsqueda (<c>SearchDatabase</c> + <c>GetSearchSettings</c> + <c>EntitySearchControl.CreateSearchSettings</c>), «Eliminar» (<c>ClickDelete</c>), «Añadir»
/// (<c>ClickSet</c>), «Exportar resultados a carpeta» (<c>Menu_Export_Click</c>), «Importar resultados a partida» (<c>Menu_Import_Click</c>) y «Eliminar clones» (<c>Menu_DeleteClones_Click</c>). Los ajustes
/// <c>EntityDb</c> llegan en cada llamada (quien llama pasa los VIGENTES). Solo lee la partida salvo lo que escribe por <c>Sesion.Casillas</c> (historial único, Deshacer exacto) y siempre por orden del usuario (regla 13);
/// lo único que borra para siempre son archivos de la carpeta de la base de datos de la propia app.
/// <para>La carpeta de la base de datos (<c>LocalResources.DatabasePath</c>, «pkmdb») vive en la carpeta privada de la app, como la de copias de seguridad; el resto de orígenes (las copias de seguridad de la app
/// y las «Otras rutas de copias») los lee <see cref="FuentesExternas"/> del almacenamiento del usuario.</para>
/// </summary>
public static class ModeloBaseDatosPkm
{
    /// <summary>La carpeta de la base de datos VIGENTE (<c>Main.DatabasePath</c> = <c>LocalResources.GetDatabasePath()</c>, leída ahora).</summary>
    public static string Carpeta(AjustesPkhex a) => a.LocalResources.GetDatabasePath();

    // ---------------------------------------------------------------------------------------------------------------- carga (LoadEntitiesFromFolder)
    /// <summary>
    /// <c>LoadEntitiesFromFolder</c>: los archivos de la carpeta de la base de datos (con subcarpetas, extensiones de <c>EntityFileExtension.GetExtensionsAll</c>), los Pokémon de cada partida externa
    /// (<paramref name="externas"/>: copias y otras partidas, ya filtradas por <c>SearchBackups</c> / <c>SearchExtraSaves</c>) y los de la partida abierta; fuera los que no son válidos (<c>IsDataValid</c>); con
    /// <c>FilterUnavailableSpecies</c> fuera las especies que la partida no puede importar (<c>EntityPresenceFilters.GetFilterEntity</c>); ordenados según <c>InitialSortMode</c>. Después, como <c>LoadDatabase</c>,
    /// <c>ForcePartyData</c> a cada uno para que tenga estadísticas. Una carpeta de la base de datos que no existe no es un error (PKHeX ni siquiera abre la herramienta): solo no aporta nada.
    /// </summary>
    public static List<SlotCache> Cargar(SaveFile sav, string carpetaBd, IEnumerable<SaveFile> externas, EntityDatabaseSettings ajustes, CancellationToken cancelar = default)
    {
        TextosNucleo.Preparar();
        var dbTemp = new ConcurrentBag<SlotCache>();
        if (Directory.Exists(carpetaBd))
        {
            var extensiones = new HashSet<string>(EntityFileExtension.GetExtensionsAll().Select(z => $".{z}"));
            var archivos = Directory.EnumerateFiles(carpetaBd, "*", SearchOption.AllDirectories);
            Parallel.ForEach(archivos, new ParallelOptions { CancellationToken = cancelar }, file => SlotInfoLoader.AddFromLocalFile(file, dbTemp, sav, extensiones));
        }
        foreach (var otra in externas)
        {
            cancelar.ThrowIfCancellationRequested();
            SlotInfoLoader.AddFromSaveFile(otra, dbTemp);
        }

        // Fetch from save file
        SlotInfoLoader.AddFromSaveFile(sav, dbTemp);
        var result = new List<SlotCache>(dbTemp);
        result.RemoveAll(z => !z.IsDataValid());

        if (ajustes.FilterUnavailableSpecies)
        {
            var filtro = EntityPresenceFilters.GetFilterEntity(sav.Context);
            if (filtro is not null)
                result.RemoveAll(z => !filtro(z.Entity));
        }

        var orden = ajustes.InitialSortMode;
        if (orden is DatabaseSortMode.SlotIdentity)
            result.Sort();
        else if (orden is DatabaseSortMode.SpeciesForm)
            result.Sort((primero, segundo) => primero.CompareToSpeciesForm(segundo));

        // Load stats for pk who do not have any
        foreach (var entrada in result)
            entrada.Entity.ForcePartyData();
        return result;
    }

    // ---------------------------------------------------------------------------------------------------------------- búsqueda
    /// <summary>
    /// <c>EntitySearchControl.CreateSearchSettings</c> + <c>GetSearchSettings</c>. La legalidad (<c>SearchLegal</c>) NO se entrega al Core: se hace con un filtro extra (<see cref="EsLegal"/>) que toma el cerrojo de
    /// legalidad por Pokémon en vez de durante toda la búsqueda, para que el análisis de las casillas de la pantalla no espere a que termine una búsqueda larga. El resultado es el mismo (todos los filtros se suman con Y).
    /// </summary>
    public static SearchSettings CrearBusqueda(FiltrosPkm f, OpcionesBusqueda o)
    {
        var s = new SearchSettings
        {
            Context = f.Formato,
            SearchContext = f.ComparadorFormato,
            Generation = f.Generacion,

            Version = f.Version,
            HiddenPowerType = f.TipoPoderOculto,

            Species = f.Especie,
            Ability = f.Habilidad,
            Nature = f.Naturaleza,
            Item = f.Objeto,
            Nickname = f.Apodo.Trim(),

            BatchInstructions = f.Instrucciones,

            Level = f.Nivel,
            SearchLevel = f.ComparadorNivel,
            EVType = f.EntrenamientoEv,
            IVType = f.PotencialIv,
        };
        foreach (var m in f.Movimientos) s.AddMove(m);
        if (f.Variocolor is { } brillo) s.SearchShiny = brillo;
        if (f.Huevo is { } huevo)
        {
            s.SearchEgg = huevo;
            if (f.Esv is { } esv) s.ESV = esv;
        }
        if (o.Legales != o.Ilegales)
        {
            var legal = o.Legales;
            var palabras = o.VerificarFiltroDePalabras;
            s.ExtraFilters.Add(pk => EsLegal(pk, palabras) == legal);
        }
        if (o.SoloClones) s.SearchClones = o.MetodoClones;
        return s;
    }

    /// <summary>
    /// <c>new LegalityAnalysis(pk).Valid</c> bajo el cerrojo de legalidad de la app. Con <paramref name="verificarFiltroDePalabras"/> falso y el filtro de palabras activo, lo apaga SOLO durante este análisis
    /// (lo que PKHeX hace en <c>B_Search_Click</c> al contestar «No» y restaura después): dentro del cerrojo nadie más ve el cambio, y se restaura aunque el análisis falle.
    /// </summary>
    public static bool EsLegal(PKM pk, bool verificarFiltroDePalabras = true)
    {
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo)
        {
            var palabras = ParseSettings.Settings.WordFilter;
            var antes = palabras.CheckWordFilter;
            try
            {
                if (!verificarFiltroDePalabras) palabras.CheckWordFilter = false;
                return new LegalityAnalysis(pk).Valid;
            }
            finally { palabras.CheckWordFilter = antes; }
        }
    }

    /// <summary><c>IsIndividualFilePKMDB</c>: un archivo suelto DENTRO de la carpeta de la base de datos.</summary>
    public static bool EsDeLaBaseDeDatos(SlotCache pk, string carpetaBd) => pk.Source is SlotInfoFileSingle(var ruta) && ruta.StartsWith(carpetaBd + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    /// <summary><c>IsBackupSaveFile</c>: viene de otra partida (copia u otra ruta), no de la abierta ni de un archivo suelto.</summary>
    public static bool EsDeOtraPartida(SlotCache pk, SaveFile sav) => pk.SAV is not FakeSaveFile && pk.SAV != sav;

    /// <summary><c>SearchDatabase</c>: primero se recorta por origen (cajas, base de datos, copias) y luego se aplican los filtros. Se puede cancelar entre un Pokémon y otro.</summary>
    public static List<SlotCache> Buscar(IReadOnlyList<SlotCache> crudo, FiltrosPkm f, OpcionesBusqueda o, SaveFile sav, string carpetaBd, CancellationToken cancelar = default)
    {
        var ajustes = CrearBusqueda(f, o);
        IEnumerable<SlotCache> res = crudo;

        // pre-filter based on the file path (if specified)
        if (!o.Cajas)
            res = res.Where(z => z.SAV != sav);
        if (!o.BaseDeDatos)
            res = res.Where(z => !EsDeLaBaseDeDatos(z, carpetaBd));
        if (!o.Copias)
            res = res.Where(z => !EsDeOtraPartida(z, sav));
        res = res.Where(z => { cancelar.ThrowIfCancellationRequested(); return true; });
        return [.. ajustes.Search(res)];
    }

    // ---------------------------------------------------------------------------------------------------------------- exportar / importar
    /// <summary>
    /// <c>Menu_Export_Click</c>: un archivo por resultado, <c>PathUtil.CleanFileName(pk.FileName)</c> y <c>WriteDecryptedDataParty</c> (con <c>ForcePartyData</c> antes). Divergencia: PKHeX reutiliza UN búfer del tamaño
    /// de equipo de la PARTIDA ABIERTA para todos (un Pokémon de otro formato sale con el tamaño equivocado y con bytes del anterior); aquí cada archivo mide lo que mide su formato (<c>SIZE_PARTY</c>), igual que
    /// «Añadir» (<c>ClickSet</c>), y por eso se puede volver a leer. Con el mismo formato que la partida los bytes son idénticos a los de PKHeX.
    /// </summary>
    public static IEnumerable<(string Nombre, byte[] Datos)> ArchivosExportables(IEnumerable<SlotCache> resultados)
    {
        foreach (var pk in resultados.Select(z => z.Entity))
        {
            pk.ForcePartyData();
            var datos = new byte[pk.SIZE_PARTY];
            pk.WriteDecryptedDataParty(datos);
            yield return (PathUtil.CleanFileName(pk.FileName), datos);
        }
    }

    /// <summary>
    /// <c>Menu_Import_Click</c> con lo que contesta <c>GetBulkImportSettings</c>: <c>SaveFile.LoadBoxes(resultados, …, caja, vaciar, sobrescribir = false, ajustes)</c>. Una sola entrada del historial sobre todas las
    /// casillas desde esa caja (Deshacer exacto). Se importan COPIAS de los Pokémon (el Core adapta lo que importa a la partida y la base de datos no debe cambiar).
    /// </summary>
    public static InformeOperacion ImportarACajas(Sesion sesion, IEnumerable<SlotCache> resultados, int caja, bool vaciar)
    {
        var sav = sesion.Guardado;
        if (!sav.HasBox) return InformeOperacion.Falla(TipoResultado.NoAdmitido, MessageStrings.MsgSaveBoxFailNone);
        var casillas = new List<ISlotInfo>();
        for (var b = caja; b < sav.BoxCount; b++) for (var s = 0; s < sav.BoxSlotCount; s++) casillas.Add(new SlotInfoBox(b, s, sav));
        var pks = resultados.Select(z => z.Entity.Clone()).ToList();
        var mensaje = string.Empty;
        int tocadas;
        try
        {
            lock (Pokekeep.Sprites.LegalityGate.Cerrojo)
                tocadas = sesion.Casillas.Lote(casillas, () =>
                {
                    var n = sav.LoadBoxes(pks, out mensaje, caja, vaciar, false, RegalosDb.ModeloRegalosDb.AjustesImportacion);
                    // Menu_Import_Click: con ctr <= 0 no hace nada más. El Core vacía las cajas ANTES de contar: si no entró nada se deshace ese vaciado (Lote revierte si la acción lanza).
                    if (n <= 0) throw new NadaImportadoException(mensaje);
                    return n;
                }, descripcion: IdiomaInterfaz.T("ModeloBaseDatosPkm.ImportarResultadosBaseDatos"));
        }
        catch (NadaImportadoException e) { return InformeOperacion.Falla(TipoResultado.SinCambios, e.Message.Length > 0 ? e.Message : MessageStrings.MsgSaveBoxImportNoFiles); }
        return InformeOperacion.Ok(IdiomaInterfaz.F("Herramientas.PuedesDeshacerloConMensaje", mensaje.TrimEnd('.')), tocadas);
    }

    private sealed class NadaImportadoException(string mensaje) : Exception(mensaje);

    /// <summary>
    /// <c>ClickView</c>: el Pokémon convertido al formato de la partida (<c>EntityConverter.ConvertToType</c>; si no se puede, el motivo del Core), adaptado a ella (<c>AdaptToSaveFile</c>) y con la suma
    /// recalculada. Es una COPIA: la entrada de la base de datos no cambia.
    /// </summary>
    public static (PKM? Pokemon, string? Error) PrepararParaPartida(SlotCache entrada, SaveFile sav)
    {
        TextosNucleo.Preparar();
        var temp = entrada.Entity.Clone();
        var pk = EntityConverter.ConvertToType(temp, sav.PKMType, out var c);
        if (pk is null) return (null, c.GetDisplayString(temp, sav.PKMType));
        sav.AdaptToSaveFile(pk);
        pk.RefreshChecksum();
        return (pk, null);
    }

    // ---------------------------------------------------------------------------------------------------------------- añadir y eliminar
    /// <summary>
    /// <c>ClickSet</c>: guarda <paramref name="pk"/> en la carpeta de la base de datos con <c>PathUtil.CleanFileName(pk.FileName)</c> y <c>WriteDecryptedDataParty</c>; si el archivo ya existe,
    /// <c>MsgDBAddFailExistsFile</c> y no se toca. Crea la carpeta si no existe (como PKHeX). Devuelve la entrada nueva.
    /// </summary>
    public static (ResultadoBd Resultado, SlotCache? Entrada) Anadir(string carpetaBd, PKM pk)
    {
        TextosNucleo.Preparar();
        try
        {
            Directory.CreateDirectory(carpetaBd);
            var ruta = Path.Combine(carpetaBd, PathUtil.CleanFileName(pk.FileName));
            if (File.Exists(ruta)) return (new(false, MessageStrings.MsgDBAddFailExistsFile), null);
            var datos = new byte[pk.SIZE_PARTY];
            pk.WriteDecryptedDataParty(datos);
            File.WriteAllBytes(ruta, datos);
            return (new(true, MessageStrings.MsgDBAddFromTabsSuccess), new SlotCache(new SlotInfoFileSingle(ruta), pk));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return (new(false, IdiomaInterfaz.T("ModeloBaseDatosPkm.PodidoGuardarBaseDatos") + ex.Message), null); }
    }

    /// <summary>¿Se puede eliminar este resultado? Un archivo de la base de datos, o un Pokémon de las cajas o del equipo de la partida abierta; no los de una copia (<c>MsgDBDeleteFailBackup</c>).</summary>
    public static bool PuedeEliminar(SlotCache entrada, SaveFile sav) => entrada.Source is SlotInfoFileSingle || (entrada.SAV == sav && entrada.Source is SlotInfoBox or SlotInfoParty);

    /// <summary>
    /// <c>ClickDelete</c>: un archivo de la base de datos se borra del disco; un Pokémon de las cajas de la partida abierta se borra de la casilla (por <c>Sesion.Casillas</c>: se puede deshacer) si la casilla sigue
    /// teniendo lo mismo (<c>EqualsStored</c>; si no, <c>MsgDBDeleteFailModified</c>); en una copia, <c>MsgDBDeleteFailBackup</c>. Divergencia: PKHeX no deja borrar el equipo («copia»); aquí sí, con las
    /// mismas reglas que «Borrar» de las cajas (un equipo no se queda vacío).
    /// </summary>
    public static ResultadoBd Eliminar(SlotCache entrada, Sesion sesion)
    {
        TextosNucleo.Preparar();
        var sav = sesion.Guardado;
        var pk = entrada.Entity;
        if (entrada.Source is SlotInfoFileSingle(var ruta))
        {
            // Data from Database: Delete file from disk
            try { if (File.Exists(ruta)) File.Delete(ruta); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new(false, MessageStrings.MsgFileDeleteFail + " " + ex.Message); }
            return new(true, IdiomaInterfaz.T("ModeloBaseDatosPkm.ArchivoEliminadoBaseDatos"));
        }
        if (entrada.SAV == sav && entrada.Source is SlotInfoBox or SlotInfoParty)
        {
            // Data from Box: Delete from save file
            var casilla = (ISlotInfo)entrada.Source;
            var existe = casilla.Read(sav);
            if (!existe.EqualsStored(pk)) // data modified already?
                return new(false, MessageStrings.MsgDBDeleteFailModified + " " + MessageStrings.MsgDBDeleteFailWarning);
            var r = sesion.Casillas.Borrar(casilla);
            return new(r.Correcto, r.Mensaje);
        }
        return new(false, MessageStrings.MsgDBDeleteFailBackup + " " + MessageStrings.MsgDBDeleteFailWarning);
    }

    /// <summary>
    /// <c>Menu_DeleteClones_Click</c>: de los archivos de la base de datos (más nuevo primero, <c>GetRevisedTime</c>) se borran los que repiten a otro por <c>HashDetails</c> y se quedan los más nuevos
    /// (<c>SearchUtil.GetExtraClones</c>); después se vacían de las cajas de la partida los clones con <c>BoxManipClearDuplicate</c>, como UNA entrada del historial (Deshacer exacto). Lo de la carpeta se borra
    /// del disco y NO se puede recuperar (<c>MsgDBDeleteCloneWarning</c>). Devuelve cuántos archivos y cuántos Pokémon de las cajas se quitaron.
    /// </summary>
    public static (int Archivos, int Cajas, string? Error) EliminarClones(IReadOnlyList<SlotCache> crudo, string carpetaBd, Sesion sesion, CancellationToken cancelar = default)
    {
        var (archivos, error) = EliminarClonesDeArchivos(crudo, carpetaBd, cancelar);
        return (archivos, EliminarClonesDeCajas(sesion), error);
    }

    /// <summary>La parte de <see cref="EliminarClones"/> que toca el disco (se puede lanzar en segundo plano): borra los archivos repetidos de la carpeta de la base de datos y devuelve cuántos y el primer error.</summary>
    public static (int Archivos, string? Error) EliminarClonesDeArchivos(IReadOnlyList<SlotCache> crudo, string carpetaBd, CancellationToken cancelar = default)
    {
        TextosNucleo.Preparar();
        var archivos = 0;
        string? error = null;
        var db = crudo.Where(z => EsDeLaBaseDeDatos(z, carpetaBd)).OrderByDescending(RevisadoEn);

        const CloneDetectionMethod metodo = CloneDetectionMethod.HashDetails;
        var hasher = SearchUtil.GetCloneDetectMethod(metodo);
        var duplicados = SearchUtil.GetExtraClones(db, z => hasher(z.Entity)).ToList();
        foreach (var entrada in duplicados)
        {
            cancelar.ThrowIfCancellationRequested();
            if (entrada.Source is not SlotInfoFileSingle(var ruta) || !File.Exists(ruta))
                continue;
            try { File.Delete(ruta); ++archivos; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { error ??= MessageStrings.MsgDBDeleteCloneFail + " " + ex.Message + " " + ruta; }
        }
        return (archivos, error);
    }

    /// <summary>La parte de <see cref="EliminarClones"/> que toca la partida: <c>BoxManipClearDuplicate</c> sobre todas las cajas, como UNA entrada del historial (Deshacer exacto).</summary>
    public static int EliminarClonesDeCajas(Sesion sesion)
    {
        TextosNucleo.Preparar();
        var sav = sesion.Guardado;
        if (!sav.HasBox) return 0;
        const CloneDetectionMethod metodo = CloneDetectionMethod.HashDetails;
        var limpiar = new BoxManipClearDuplicate<string>(BoxManipType.DeleteClones, pk => SearchUtil.GetCloneDetectMethod(metodo)(pk));
        var param = new BoxManipParam(0, sav.BoxCount - 1);
        var casillas = new List<ISlotInfo>();
        for (var b = 0; b < sav.BoxCount; b++) for (var s = 0; s < sav.BoxSlotCount; s++) casillas.Add(new SlotInfoBox(b, s, sav));
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo)
            return Math.Max(0, sesion.Casillas.Lote(casillas, () => limpiar.Execute(sav, param), descripcion: IdiomaInterfaz.T("ModeloBaseDatosPkm.EliminarClonesCajas")));
    }

    /// <summary>
    /// Un archivo de Pokémon suelto (cualquier formato que el Core reconoce, <c>FileUtil.TryGetPKM</c>) para <see cref="Anadir"/>: la base de datos guarda cada Pokémon en SU formato y se convierte al verlo, como PKHeX.
    /// </summary>
    public static (PKM? Pokemon, string? Error) LeerArchivo(byte[] datos, string nombre, SaveFile sav)
    {
        TextosNucleo.Preparar();
        return FileUtil.TryGetPKM(datos.ToArray(), out var pk, Path.GetExtension(nombre), sav) && pk is not null
            ? (pk, null)
            : (null, MessageStrings.MsgFileLoadFail + " " + Path.GetFileName(nombre));
    }

    /// <summary><c>GetRevisedTime</c>: la fecha de escritura del archivo (UTC); lo que no es un archivo cuenta como «ahora».</summary>
    private static DateTime RevisadoEn(SlotCache entrada)
        => entrada.Source is SlotInfoFileSingle(var ruta) ? File.GetLastWriteTimeUtc(ruta) : DateTime.UtcNow;

    // ---------------------------------------------------------------------------------------------------------------- textos
    /// <summary>Dónde está un resultado, para la tarjeta: caja y casilla, equipo, archivo de la base de datos o la partida de la que sale (<c>SlotCache.Identify</c> sin la ruta completa).</summary>
    public static string Origen(SlotCache e, SaveFile sav, string carpetaBd, Func<int, string>? nombreCaja = null)
    {
        string Caja(int caja) => nombreCaja?.Invoke(caja) ?? IdiomaInterfaz.F("Qr.CajaAccesible", caja + 1);
        var nombrePartida = e.SAV.Metadata.FileName is { Length: > 0 } fn ? Path.GetFileName(fn) : string.Empty;
        return e.Source switch
        {
            SlotInfoFileSingle(var ruta) => EsDeLaBaseDeDatos(e, carpetaBd) ? IdiomaInterfaz.T("ModeloBaseDatosPkm.BaseDatos") + Path.GetFileName(ruta) : IdiomaInterfaz.T("ModeloBaseDatosPkm.Archivo") + Path.GetFileName(ruta),
            SlotInfoBox b when e.SAV == sav => IdiomaInterfaz.F("ModeloBaseDatosPkm.CajasCasilla", Caja(b.Box), b.Slot + 1),
            SlotInfoParty p when e.SAV == sav => IdiomaInterfaz.F("ModeloBaseDatosPkm.EquipoPosicion", p.Slot + 1),
            SlotInfoBox b => IdiomaInterfaz.F("ModeloBaseDatosPkm.CopiaCajaCasilla", nombrePartida, b.Box + 1, b.Slot + 1),
            SlotInfoParty p => IdiomaInterfaz.F("ModeloBaseDatosPkm.CopiaEquipo", nombrePartida, p.Slot + 1),
            SlotInfoMisc m => $"{(e.SAV == sav ? IdiomaInterfaz.T("ModeloBaseDatosPkm.Partida") : IdiomaInterfaz.T("ModeloBaseDatosPkm.Copia") + nombrePartida)} · {m.Type} {m.Slot}",
            _ => string.Empty,
        };
    }
}
