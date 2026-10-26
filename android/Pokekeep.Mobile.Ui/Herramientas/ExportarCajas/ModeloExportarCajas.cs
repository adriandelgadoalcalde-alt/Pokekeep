// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.ExportarCajas;

/// <summary>Un archivo del volcado: la subcarpeta de su caja (null = en la carpeta elegida), su nombre y sus bytes.</summary>
public sealed record ArchivoVolcado(string? Subcarpeta, string Nombre, byte[] Datos);

/// <summary>Lo que se va a escribir: los archivos en orden y cuántas cajas recorre.</summary>
public sealed record PlanVolcado(IReadOnlyList<ArchivoVolcado> Archivos, int CajaInicio, int CajaFin)
{
    public int Cajas => CajaFin - CajaInicio;
    /// <summary>Nombres distintos (dos Pokémon iguales dan el mismo nombre y PKHeX escribe el segundo encima del primero: el contador cuenta los dos, la carpeta tiene uno).</summary>
    public int ArchivosDistintos => Archivos.Select(a => (a.Subcarpeta, a.Nombre)).Distinct().Count();
}

/// <summary>
/// «Exportar cajas» de PKHeX (<c>BoxExporter</c> → <c>BoxExport.Export(sav, carpeta, namer, ajustes)</c>, PK-SUB-001, PK-IO-036, PK-IO-037) sin rutas de fichero: <see cref="Preparar"/> calcula EXACTAMENTE los archivos que
/// el Core escribiría (alcance, carpeta por caja y su nombre, huecos vacíos o no válidos, prefijo de índice, nombrador y datos de equipo forzados) y <see cref="EscribirAsync"/> los lleva a una carpeta del usuario (SAF).
/// Solo lee la partida: cada Pokémon se lee como copia (<c>GetBoxSlotAtIndex</c>) y lo que el Core cambia al exportar (<c>ForcePartyData</c>) se cambia en esa copia. La prueba <c>VolcadoCajasTests.ExportarEscribeLosMismosArchivosQueBoxExportDelCore</c> compara
/// archivo a archivo con <c>BoxExport.Export</c> del Core escribiendo en una carpeta del disco.
/// </summary>
public static class ModeloExportarCajas
{
    /// <summary>
    /// Los archivos del volcado. <paramref name="cajaActual"/> es la caja que el usuario está viendo: con <see cref="BoxExportScope.Current"/> el Core exporta <c>sav.CurrentBox</c>, que PKHeX iguala antes a la caja
    /// visible (<c>Main.MainMenuBoxDumpSingle</c>: «C_SAV.SAV.CurrentBox = C_SAV.CurrentBox»); aquí se pasa sin tocar la partida.
    /// </summary>
    public static PlanVolcado Preparar(SaveFile sav, int cajaActual, BoxExportSettings ajustes, IFileNamer<PKM> nombrador, CancellationToken cancelar = default, IProgress<AvanceTrabajo>? avance = null)
    {
        if (!sav.HasBox) return new PlanVolcado([], 0, 0);
        int total = sav.SlotCount, porCaja = sav.BoxSlotCount;
        var inicio = ajustes.Scope == BoxExportScope.Current ? Math.Clamp(cajaActual, 0, sav.BoxCount - 1) : 0;
        var fin = ajustes.Scope == BoxExportScope.Current ? inicio + 1 : sav.BoxCount;
        var archivos = new List<ArchivoVolcado>();
        var formatoEquipo = sav.SIZE_BOXSLOT == sav.SIZE_PARTY;
        for (var caja = inicio; caja < fin; caja++)
        {
            cancelar.ThrowIfCancellationRequested();
            avance?.Report(new AvanceTrabajo(IdiomaInterfaz.F("ModeloExportarCajas.PreparandoCaja", caja + 1, fin), fin - inicio <= 1 ? null : 100.0 * (caja - inicio) / (fin - inicio)));
            var sub = ajustes.FolderCreation == BoxExportFolderMode.FolderEachBox ? NombreCarpeta(sav, caja, ajustes.FolderPrefix) : null;
            var huecos = HuecosDeCaja(porCaja, caja, total);
            for (var hueco = 0; hueco < huecos; hueco++)
            {
                var pk = sav.GetBoxSlotAtIndex(caja, hueco);
                if ((pk.Species == 0 || !pk.Valid) && ajustes.EmptySlots == BoxExportEmptySlots.Skip) continue;
                var nombre = NombreArchivo(pk, ajustes.FileIndexPrefix, nombrador, caja, hueco, porCaja);
                if (!formatoEquipo) pk.ForcePartyData();   // como el Core: los datos de equipo calculados en vez de ceros, «to be nice to any external analysis»
                var datos = new byte[sav.SIZE_PARTY];
                pk.WriteDecryptedDataParty(datos);
                archivos.Add(new ArchivoVolcado(sub, nombre, datos));
            }
        }
        return new PlanVolcado(archivos, inicio, fin);
    }

    /// <summary>
    /// Escribe el plan en la carpeta elegida: crea (o reutiliza) la subcarpeta de cada caja y sustituye el archivo que ya tenga ese nombre (<c>File.WriteAllBytes</c>). Devuelve cuántos archivos escribió, contando
    /// las repeticiones como el contador de PKHeX.
    /// </summary>
    public static async Task<int> EscribirAsync(PlanVolcado plan, CarpetaRef carpeta, IAlmacenCarpetasCajas carpetas, IAlmacenDocumentos documentos, CancellationToken cancelar = default, IProgress<AvanceTrabajo>? avance = null, Action<int>? escrito = null)
    {
        var subcarpetas = new Dictionary<string, CarpetaRef>(StringComparer.Ordinal);
        var n = 0;
        foreach (var a in plan.Archivos)
        {
            cancelar.ThrowIfCancellationRequested();
            var destino = carpeta;
            if (a.Subcarpeta is { } s)
            {
                if (!subcarpetas.TryGetValue(s, out var sub)) subcarpetas[s] = sub = await carpetas.SubcarpetaAsync(carpeta, s, cancelar).ConfigureAwait(false);
                destino = sub;
            }
            var doc = await carpetas.ArchivoParaEscribirAsync(destino, a.Nombre, cancelar).ConfigureAwait(false);
            await documentos.EscribirAsync(doc, a.Datos, cancelar).ConfigureAwait(false);
            n++;
            escrito?.Invoke(n);
            avance?.Report(new AvanceTrabajo(IdiomaInterfaz.F("ModeloExportarCajas.EscribiendoDe", n, plan.Archivos.Count), 100.0 * n / plan.Archivos.Count));
        }
        return n;
    }

    /// <summary>El aviso final de PKHeX (<c>MsgSaveBoxExportPathCount</c> + la carpeta); null con «Silencioso» (PKHeX solo hace sonar el aviso del sistema).</summary>
    public static string? Aviso(BoxExportNofify notificar, int escritos, string carpeta)
        => notificar == BoxExportNofify.Silent ? null : string.Format(MessageStrings.MsgSaveBoxExportPathCount, escritos) + Environment.NewLine + carpeta;

    /// <summary>Los nombradores que ofrece PKHeX (<c>EntityFileNamer.AvailableNamers</c>; sin complementos, solo «Default»).</summary>
    public static IReadOnlyList<IFileNamer<PKM>> Nombradores => EntityFileNamer.AvailableNamers;

    /// <summary>El índice del nombrador guardado en <c>SlotExport.DefaultBoxExportNamer</c>, o el primero si no existe (como el constructor de <c>BoxExporter</c>).</summary>
    public static int IndiceNombrador(string guardado)
    {
        var l = Nombradores;
        for (var i = 0; i < l.Count; i++) if (l[i].Name == guardado) return i;
        return 0;
    }

    // ---- lo mismo que BoxExport (privado en el Core) ----
    private static int HuecosDeCaja(int porCaja, int caja, int total)
    {
        var inicio = porCaja * caja;
        return inicio + porCaja > total ? total - inicio : porCaja;   // cajas «dentadas» con menos huecos
    }

    /// <summary>
    /// Los caracteres que Windows (y el proveedor de SAF de Android) no admiten en un nombre: <c>" * / : &lt; &gt; ? \ |</c> y los de control. <c>PathUtil.CleanFileName</c> quita los de la plataforma donde corre, que en
    /// Linux/Android son solo «/» y el nulo: un mote con «?» o «:» llegaría tal cual a <c>DocumentsContract.CreateDocument</c>, que lo cambia por otra cosa y la siguiente exportación crearía «x (1).pk8» en vez
    /// de sustituir. Aquí se quitan siempre, como hace PKHeX en Windows.
    /// </summary>
    public static string LimpiarNombre(string nombre)
    {
        var sinPlataforma = PathUtil.CleanFileName(nombre);
        var b = new System.Text.StringBuilder(sinPlataforma.Length);
        foreach (var c in sinPlataforma) if (c >= ' ' && c != '"' && c != '*' && c != '/' && c != ':' && c != '<' && c != '>' && c != '?' && c != '\\' && c != '|') b.Append(c);
        return b.ToString();
    }

    public static string NombreCarpeta(SaveFile sav, int caja, BoxExportFolderNaming modo)
    {
        var nombre = LimpiarNombre(sav is IBoxDetailNameRead r ? r.GetBoxName(caja) : BoxDetailNameExtensions.GetDefaultBoxName(caja));
        return modo switch
        {
            BoxExportFolderNaming.BoxName => nombre,
            BoxExportFolderNaming.Index => $"{caja + 1:00}",
            BoxExportFolderNaming.IndexBoxName => $"{caja + 1:00} {nombre}",
            _ => throw new ArgumentOutOfRangeException(nameof(modo), modo, null),
        };
    }

    public static string NombreArchivo(PKM pk, BoxExportIndexPrefix modo, IFileNamer<PKM> nombrador, int caja, int hueco, int porCaja)
    {
        string interno;
        try { interno = LimpiarNombre(nombrador.GetName(pk)); }
        catch { interno = "Name Error"; }   // el texto del Core, tal cual: es el nombre del archivo que PKHeX escribe
        var prefijo = modo switch
        {
            BoxExportIndexPrefix.None => string.Empty,
            BoxExportIndexPrefix.InAll => $"{(caja * porCaja) + hueco:0000} - ",
            BoxExportIndexPrefix.InBox => $"{hueco:00} - ",
            BoxExportIndexPrefix.InBoxAndSlot => $"{caja + 1:00}-{hueco:00} - ",
            _ => throw new ArgumentOutOfRangeException(nameof(modo), modo, null),
        };
        return $"{prefijo}{LimpiarNombre(interno)}.{pk.Extension}";
    }
}
