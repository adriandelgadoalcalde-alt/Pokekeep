// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Almacen;

namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

/// <summary>Un archivo de primer nivel de una carpeta del usuario: su identificador para leerlo (Android: el id del documento), su nombre, su tamaño y su fecha de modificación (0 si el explorador no la da).</summary>
public sealed record ArchivoCarpeta(string Id, string Nombre, long Bytes, long ModificadoMs = 0);

/// <summary>Leer el contenido de una carpeta del usuario (Android: árbol de SAF con permiso persistente). Solo los archivos de primer nivel: nombre, tamaño y bytes; nunca nada fuera de esa carpeta.</summary>
public interface IExploradorCarpetas
{
    /// <summary>Los archivos (no las subcarpetas) de la carpeta, con UNA consulta. Lanza si el permiso ya no está concedido.</summary>
    IReadOnlyList<ArchivoCarpeta> Listar(string uriCarpeta);
    /// <summary>Los bytes de un archivo que <see cref="Listar"/> devolvió (por su <see cref="ArchivoCarpeta.Id"/>, sin volver a consultar la carpeta).</summary>
    byte[] Leer(string uriCarpeta, ArchivoCarpeta archivo);
    /// <summary>Devuelve el permiso persistente de esa carpeta al sistema (ya no se usa para nada).</summary>
    void Soltar(string uriCarpeta);
}

/// <summary>Una carpeta de trabajo de PKHeX: la ruta del ajuste (<c>LocalResources.MGDatabasePath</c>), su valor de fábrica y el nombre de la carpeta de la app que la refleja.</summary>
public sealed record RecursoCarpeta(string Ruta, string Defecto, string Clave);

/// <summary>Resultado de elegir una carpeta del usuario para un ajuste.</summary>
public sealed record CarpetaElegida(CarpetaRef Carpeta, int Archivos);

/// <summary>
/// CARPETAS DE TRABAJO DE PKHeX EN EL MÓVIL (<c>LocalResourceSettings</c> y <c>Advanced.PathBlockKeyList</c>; bloque 10-1, PK-IO-060). En PKHeX son carpetas junto al ejecutable (<c>pkmdb</c>, <c>mgdb</c>, <c>bak</c>...)
/// o la ruta que el usuario escriba. En Android una app no puede leer carpetas del usuario por ruta: el usuario ELIGE una carpeta con el selector del sistema (SAF, permiso persistente) y la app guarda una COPIA de lectura
/// de sus archivos en su carpeta privada (<c>carpetas/&lt;clave&gt;</c>). El ajuste de PKHeX apunta a esa copia, así que el Core y cada consumidor siguen leyendo una carpeta normal con las mismas llamadas que PKHeX
/// (<c>GetMGDatabasePath()</c>, <c>Directory.Exists</c>...). Sin carpeta elegida vale la de la app (el valor de fábrica relativo a la carpeta privada), igual que en PKHeX.
/// La copia se rellena al elegir la carpeta y se pone al día tras cada arranque, SIN esperar por ella (<see cref="SincronizarTodasAsync"/>): solo se copia lo nuevo o lo que cambió de tamaño o fecha y se borra lo que ESA sincronización copió y ya no está (manifiesto): lo que la app escribe en la copia, como las copias de seguridad, nunca se borra.
/// Cuando un ajuste deja de apuntar a su copia (restablecer, «Quitar mi carpeta»), la copia, el enlace y el permiso del sistema se liberan (<see cref="Podar"/>) y, si ya no hay carpeta de regalos, el Core los olvida. Un bloque que ESCRIBA en una de estas carpetas (copias de seguridad, base de datos) devuelve lo escrito a la carpeta
/// del usuario con <see cref="IAlmacenCarpetas.CrearEnCarpetaAsync"/> (<see cref="Enlace"/> le da la carpeta). Nada sale del dispositivo (regla 15).
/// </summary>
public sealed class CarpetasTrabajoPkhex
{
    public static readonly IReadOnlyList<RecursoCarpeta> Recursos =
    [
        new("LocalResources.DatabasePath", "pkmdb", "pkmdb"),
        new("LocalResources.MGDatabasePath", "mgdb", "mgdb"),
        new("LocalResources.BackupPath", "bak", "bak"),
        new("LocalResources.SoundPath", "sounds", "sounds"),
        new("LocalResources.TemplatePath", "template", "template"),
        new("LocalResources.TrainerPath", "trainers", "trainers"),
        new("Advanced.PathBlockKeyList", "", "bloques"),
    ];

    /// <summary>Tope por archivo de la copia: ningún recurso de PKHeX (plantillas, regalos, nombres de bloques, gritos) se acerca; evita copiar por error una carpeta de vídeos.</summary>
    public const long MaximoPorArchivo = 16L * 1024 * 1024;
    public const int MaximoArchivos = 5000;
    public const string NombreFichero = "carpetas-trabajo.json";

    private readonly IAjustesPkhex _ajustes;
    private readonly string _privada;
    private readonly Func<IAlmacenCarpetas?> _carpetas;
    private readonly IExploradorCarpetas? _explorador;

    public CarpetasTrabajoPkhex(IAjustesPkhex ajustes, string carpetaPrivada, Func<IAlmacenCarpetas?> carpetas, IExploradorCarpetas? explorador)
    {
        _ajustes = ajustes; _privada = carpetaPrivada; _carpetas = carpetas; _explorador = explorador;
        _ajustes.Cambiado += Podar;
    }

    public static RecursoCarpeta? Recurso(string ruta) => Recursos.FirstOrDefault(r => r.Ruta == ruta);
    public static bool EsCarpeta(string ruta) => Recurso(ruta) is not null;

    /// <summary>Hay selector de carpetas (en la app sí; en una prueba o captura sin servicios, no).</summary>
    public bool PuedeElegir => _carpetas() is not null && _explorador is not null;

    /// <summary>La carpeta de la app que refleja la carpeta del usuario de ese ajuste.</summary>
    public string Espejo(RecursoCarpeta r) => Path.Combine(_privada, "carpetas", r.Clave);

    private static NodoAjustePkhex Nodo(RecursoCarpeta r) => RegistroAjustesPkhex.Buscar(r.Ruta) ?? throw new InvalidOperationException($"«{r.Ruta}» no es un ajuste de PKHeX.");
    private string Valor(RecursoCarpeta r) => (string?)Nodo(r).Leer(_ajustes.Actual) ?? string.Empty;

    /// <summary>
    /// La carpeta (ruta del sistema de archivos) que un consumidor lee AHORA para ese ajuste, como PKHeX: <c>LocalResources.Get…Path()</c> (relativa a la carpeta privada si no es absoluta) o, para
    /// <c>PathBlockKeyList</c>, el valor tal cual.
    /// </summary>
    public string RutaLocal(string ruta)
    {
        var l = _ajustes.Actual.LocalResources;
        return ruta switch
        {
            "LocalResources.DatabasePath" => l.GetDatabasePath(),
            "LocalResources.MGDatabasePath" => l.GetMGDatabasePath(),
            "LocalResources.BackupPath" => l.GetBackupPath(),
            "LocalResources.SoundPath" => l.GetCryPath(),
            "LocalResources.TemplatePath" => l.GetTemplatePath(),
            "LocalResources.TrainerPath" => l.GetTrainerPath(),
            "Advanced.PathBlockKeyList" => _ajustes.Actual.Advanced.PathBlockKeyList,
            _ => throw new ArgumentException($"«{ruta}» no es una carpeta de trabajo.", nameof(ruta)),
        };
    }

    // ------------------------------------------------------------------------------------------------ enlaces (URI de SAF) por ajuste
    private string RutaEnlaces => Path.Combine(_privada, NombreFichero);

    private Dictionary<string, CarpetaRef> LeerEnlaces()
    {
        try { return File.Exists(RutaEnlaces) ? JsonSerializer.Deserialize<Dictionary<string, CarpetaRef>>(File.ReadAllText(RutaEnlaces)) ?? [] : []; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return []; }
    }

    private void EscribirEnlaces(Dictionary<string, CarpetaRef> e)
    {
        Directory.CreateDirectory(_privada);
        var tmp = RutaEnlaces + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(e));
        File.Move(tmp, RutaEnlaces, overwrite: true);
    }

    /// <summary>La carpeta del usuario enlazada con ese ajuste, o null. Un enlace solo vale mientras el ajuste apunte a su copia: si el usuario restablece el grupo o escribe otra ruta, el enlace deja de contar.</summary>
    public CarpetaRef? Enlace(string ruta)
        => Recurso(ruta) is { } r && LeerEnlaces().TryGetValue(ruta, out var c) && string.Equals(Valor(r), Espejo(r), StringComparison.Ordinal) ? c : null;

    /// <summary>Texto de la fila: qué carpeta se usa ahora.</summary>
    public string Describir(string ruta)
    {
        var r = Recurso(ruta) ?? throw new ArgumentException(ruta, nameof(ruta));
        if (Enlace(ruta) is { } c)
        {
            var n = Directory.Exists(Espejo(r)) ? Directory.EnumerateFiles(Espejo(r)).Count() : 0;
            return IdiomaInterfaz.F("Ajustes.Carpetas.Tuya", c.Nombre, Archivos(n));
        }
        var v = Valor(r);
        if (v.Length == 0) return IdiomaInterfaz.T("Ajustes.Carpetas.Ninguna");
        if (v == r.Defecto) return Directory.Exists(RutaLocal(ruta)) ? IdiomaInterfaz.F("Ajustes.Carpetas.App", v) : SinCarpetaExtra(ruta);
        return v;
    }

    /// <summary>
    /// Sin carpeta propia elegida. No es que falte nada: los regalos conocidos van dentro del Core de PKHeX (MGDB_G4…G9A) y el catálogo de
    /// Pokekeep en el APK; la carpeta solo añade regalos extra. Los gritos también vienen dentro de la app (X11, PokeAPI/cries): la carpeta del usuario suena antes y solo añade o sustituye.
    /// </summary>
    private static string SinCarpetaExtra(string ruta) => ruta switch
    {
        "LocalResources.MGDatabasePath" => IdiomaInterfaz.T("Ajustes.Carpetas.RegalosIncluidos"),
        "LocalResources.SoundPath" => IdiomaInterfaz.T("Ajustes.Carpetas.GritosIncluidos"),
        _ => IdiomaInterfaz.T("Ajustes.Carpetas.Ninguna"),
    };

    public static string Archivos(int n) => IdiomaInterfaz.P("Ajustes.Carpetas.Archivos", n);

    /// <summary>
    /// «Elegir carpeta…»: el selector del sistema, permiso persistente, el ajuste pasa a apuntar a la copia de la app y la copia se rellena con los archivos de esa carpeta. Null si el usuario cancela o no hay selector.
    /// </summary>
    public async Task<CarpetaElegida?> ElegirAsync(string ruta, CancellationToken cancelar = default)
    {
        var r = Recurso(ruta) ?? throw new ArgumentException(ruta, nameof(ruta));
        if (_carpetas() is not { } selector || _explorador is null) return null;
        // Sin ConfigureAwait(false): «Guardar» avisa a quien pinta (tema, idioma, filas) y tiene que volver al hilo de quien eligió (la interfaz).
        var c = await selector.ElegirCarpetaAsync(cancelar);
        if (c is null) return null;
        var enlaces = LeerEnlaces();
        var anterior = enlaces.TryGetValue(ruta, out var previa) ? previa : null;
        enlaces[ruta] = c;
        EscribirEnlaces(enlaces);
        // Reemplazar el enlace no puede dejar el permiso persistente del anterior colgado (Android limita los permisos por app): se suelta si nadie más lo usa.
        if (anterior is not null && anterior.Uri != c.Uri && anterior.Uri != new CarpetaStore(_privada).Leer()?.Uri && enlaces.Values.All(e => e.Uri != anterior.Uri))
            try { _explorador.Soltar(anterior.Uri); } catch (Exception ex) when (ex is not OutOfMemoryException) { }
        Nodo(r).Escribir(_ajustes.Actual, Espejo(r));
        var (n, _) = await SincronizarAsync(ruta, cancelar);
        _ajustes.Guardar();                    // aplica al Core con la copia ya llena (p. ej. la base de datos de regalos se relee)
        return new CarpetaElegida(c, n);
    }

    /// <summary>«Quitar mi carpeta»: el ajuste vuelve a su valor de fábrica; al guardarlo, <see cref="Podar"/> borra la copia, olvida el enlace y suelta el permiso (la carpeta del usuario no se toca).</summary>
    public void UsarLaDeLaApp(string ruta)
    {
        var r = Recurso(ruta) ?? throw new ArgumentException(ruta, nameof(ruta));
        Nodo(r).Escribir(_ajustes.Actual, r.Defecto);
        _ajustes.Guardar();
    }

    /// <summary>
    /// Tras cada cambio de ajustes (también «Restablecer este grupo» y «Restablecer los ajustes de PKHeX»): cada carpeta cuyo ajuste ya no apunta a su copia pierde la copia, el enlace y, si nadie más usa esa carpeta
    /// (otra carpeta de trabajo o la carpeta de copias de la Partida), el permiso persistente del sistema. Y como <c>StartupUtil.ReloadSettings</c> no toca los regalos cuando la carpeta no existe, si ya no hay carpeta
    /// de regalos se vacían los del Core (<c>EncounterEvent.RefreshMGDB()</c> sin rutas): un regalo de una carpeta que el usuario quitó no puede seguir dando por legal un Pokémon.
    /// </summary>
    public void Podar()
    {
        var enlaces = LeerEnlaces();
        var soltar = new List<string>();
        foreach (var r in Recursos)
        {
            if (string.Equals(Valor(r), Espejo(r), StringComparison.Ordinal)) continue;
            if (enlaces.Remove(r.Ruta, out var c)) soltar.Add(c.Uri);
            try { if (File.Exists(RutaManifiesto(r))) File.Delete(RutaManifiesto(r)); if (Directory.Exists(Espejo(r))) Directory.Delete(Espejo(r), true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        if (soltar.Count > 0)
        {
            EscribirEnlaces(enlaces);
            var copias = new CarpetaStore(_privada).Leer()?.Uri;
            foreach (var uri in soltar.Distinct().Where(u => u != copias && enlaces.Values.All(e => e.Uri != u)))
                try { _explorador?.Soltar(uri); } catch (Exception ex) when (ex is not OutOfMemoryException) { }
        }
        if (!Directory.Exists(_ajustes.Actual.LocalResources.GetMGDatabasePath()) && HayRegalosExternos())
            _ = AplicadorPkhex.EnCola(() => { lock (Pokekeep.Sprites.LegalityGate.Cerrojo) EncounterEvent.RefreshMGDB(); });   // detrás de la aplicación de ajustes pendiente y fuera del hilo de interfaz
    }

    private static bool HayRegalosExternos()
        => EncounterEvent.EGDB_G4.Length + EncounterEvent.EGDB_G5.Length + EncounterEvent.EGDB_G6.Length + EncounterEvent.EGDB_G7.Length + EncounterEvent.EGDB_G7GG.Length + EncounterEvent.EGDB_G8.Length
         + EncounterEvent.EGDB_G8A.Length + EncounterEvent.EGDB_G8B.Length + EncounterEvent.EGDB_G9.Length + EncounterEvent.EGDB_G9A.Length > 0;

    /// <summary>
    /// Pone al día la copia de una carpeta enlazada (archivos de primer nivel, con tope de tamaño): UNA consulta a la carpeta, solo se leen los archivos nuevos o con otro tamaño y se borran los que ya no están.
    /// Devuelve cuántos archivos hay en la copia y si algo cambió; (0, false) si no hay enlace. Sin permiso se queda la copia anterior.
    /// </summary>
    public Task<(int Archivos, bool Cambio)> SincronizarAsync(string ruta, CancellationToken cancelar = default) => Task.Run(() =>
    {
        if (Recurso(ruta) is not { } r || Enlace(ruta) is not { } c || _explorador is null) return (0, false);
        var destino = Espejo(r);
        static int Contar(string d) => Directory.Exists(d) ? Directory.EnumerateFiles(d).Count() : 0;
        List<ArchivoCarpeta> lista;
        try { lista = [.. _explorador.Listar(c.Uri)]; }
        catch (Exception ex) when (ex is not OperationCanceledException) { return (Contar(destino), false); }   // sin permiso: se queda la copia anterior
        Directory.CreateDirectory(destino);
        var cambio = false;
        // Manifiesto de lo que ESTA sincronización copió desde la carpeta del usuario: solo eso se puede borrar del espejo. Lo que la app escribe en el espejo
        // (p. ej. las copias de seguridad .bak, bloque 10-4) no está en el manifiesto y nunca se toca.
        var copiados = LeerManifiesto(r);
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var nuevos = new Dictionary<string, EntradaCopiada>(StringComparer.Ordinal);
        foreach (var a in lista.Take(MaximoArchivos))
        {
            cancelar.ThrowIfCancellationRequested();
            var limpio = Path.GetFileName(a.Nombre);                      // nunca fuera de la copia
            if (limpio.Length == 0 || limpio is "." or ".." || !vistos.Add(limpio)) continue;
            if (a.Bytes > MaximoPorArchivo) continue;
            var fichero = Path.Combine(destino, limpio);
            // Ya copiado: mismo nombre, mismo tamaño y misma fecha (si el explorador la da). Con tamaño desconocido (0) se decide solo por la fecha; sin fecha, se copia una vez y se recuerda.
            if (copiados.TryGetValue(limpio, out var previa) && File.Exists(fichero)
                && previa.Bytes == a.Bytes && previa.ModificadoMs == a.ModificadoMs
                && (a.Bytes == 0 ? a.ModificadoMs != 0 : new FileInfo(fichero).Length == a.Bytes))
            { nuevos[limpio] = previa; continue; }
            try
            {
                var bytes = _explorador.Leer(c.Uri, a);
                if (bytes.LongLength > MaximoPorArchivo) { vistos.Remove(limpio); continue; }   // tope también cuando el tamaño era desconocido
                nuevos[limpio] = new EntradaCopiada(a.Bytes, a.ModificadoMs);
                // Sin tamaño ni fecha no se puede saber si cambió sin leerlo: se lee, pero si el contenido es el mismo no se reescribe ni se avisa de un cambio.
                if (File.Exists(fichero) && File.ReadAllBytes(fichero).AsSpan().SequenceEqual(bytes)) continue;
                var tmp = fichero + ".copia";
                File.WriteAllBytes(tmp, bytes);
                File.Move(tmp, fichero, overwrite: true);
                cambio = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                vistos.Remove(limpio);
                if (copiados.TryGetValue(limpio, out var anterior) && File.Exists(fichero)) nuevos[limpio] = anterior;   // se queda la copia anterior
            }
        }
        // Lo que se copió antes y ya no está en la carpeta del usuario (o ahora supera el tope) se borra; nada más.
        foreach (var nombre in copiados.Keys.Where(n => !nuevos.ContainsKey(n)))
        {
            var f = Path.Combine(destino, nombre);
            try { if (File.Exists(f)) { File.Delete(f); cambio = true; } } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        EscribirManifiesto(r, nuevos);
        return (Contar(destino), cambio);
    }, cancelar);

    private sealed record EntradaCopiada(long Bytes, long ModificadoMs);

    /// <summary>El manifiesto vive junto al espejo (no dentro: el espejo es lo que lee el Core).</summary>
    private string RutaManifiesto(RecursoCarpeta r) => Path.Combine(_privada, "carpetas", r.Clave + ".copiados.json");

    private Dictionary<string, EntradaCopiada> LeerManifiesto(RecursoCarpeta r)
    {
        try { return File.Exists(RutaManifiesto(r)) ? JsonSerializer.Deserialize<Dictionary<string, EntradaCopiada>>(File.ReadAllText(RutaManifiesto(r))) ?? [] : []; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return []; }
    }

    private void EscribirManifiesto(RecursoCarpeta r, Dictionary<string, EntradaCopiada> m)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaManifiesto(r))!);
            var tmp = RutaManifiesto(r) + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(m));
            File.Move(tmp, RutaManifiesto(r), overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Tras el arranque, en segundo plano: pone al día la copia de todas las carpetas enlazadas. Devuelve true si alguna cambió; entonces quien la llamó vuelve a aplicar los ajustes al Core (<see cref="IAjustesPkhex.Guardar"/>)
    /// en el hilo de la interfaz, para que el Core relea los regalos y quien pinta se entere.
    /// </summary>
    public async Task<bool> SincronizarTodasAsync(CancellationToken cancelar = default)
    {
        var alguna = false;
        foreach (var r in Recursos.Where(r => Enlace(r.Ruta) is not null))
            alguna |= (await SincronizarAsync(r.Ruta, cancelar).ConfigureAwait(false)).Cambio;
        return alguna;
    }
}
