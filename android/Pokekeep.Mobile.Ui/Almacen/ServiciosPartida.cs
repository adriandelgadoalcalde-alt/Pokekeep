// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Almacen;

/// <summary>Un guardado abierto hace poco: URI (SAF), nombre y juego. NUNCA nombre de entrenador, TID ni SID.</summary>
public sealed record RecienteRef(string Uri, string Nombre, string Juego, DateTimeOffset Fecha)
{
    /// <summary>Versión exacta para el logo; los recientes anteriores conservan Any y el icono de partida.</summary>
    public PKHeX.Core.GameVersion Version { get; init; }
    /// <summary>Idioma de la partida para su rótulo ("es"/"en"); los recientes anteriores no lo tienen y usan el icono.</summary>
    public string? Idioma { get; init; }
}

/// <summary>
/// Lista de recientes en <c>FilesDir/recientes.json</c> (maximo 8, sin repetidos). Con los ajustes de PKHeX (bloque 10-4) la lista que manda es la de PKHeX, <c>Startup.RecentlyLoaded</c> (las URI, en orden,
/// con el tope <c>Startup.RecentlyLoadedMaxCount</c> y la regla de <c>StartupSettings.LoadSaveFile</c>), y <c>recientes.json</c> solo guarda el nombre, el juego y la fecha que se enseñan.
/// <see cref="Anotar"/> es el final de <c>Main.OpenSAV</c>: la copia automática al cargar (<c>TryBackupExportCheck</c>, <see cref="Guardados.CopiasPkhex.AlCargar"/>) y después el reciente.
/// </summary>
public sealed class RecientesStore(string carpetaPrivada, int maximo = 8, Ajustes.Pkhex.IAjustesPkhex? ajustes = null)
{
    private string Ruta => Path.Combine(carpetaPrivada, "recientes.json");
    private byte[]? _indiceAntes;
    private bool _indiceAnotado;

    /// <summary>A dónde va un aviso que no debe tirar la operación (Android: el registro). Nunca lleva datos de la partida ni del entrenador.</summary>
    public Action<string, Exception>? Avisar { get; set; }

    /// <summary>Lo que hizo la copia automática del último <see cref="Anotar"/> (pruebas y avisos).</summary>
    public Guardados.CopiaAlCargar? UltimaCopia { get; private set; }

    private List<RecienteRef> LeerFichero()
    {
        try { return File.Exists(Ruta) ? JsonSerializer.Deserialize<List<RecienteRef>>(File.ReadAllText(Ruta)) ?? [] : []; }
        catch (Exception ex) when (ex is IOException or JsonException) { return []; }
    }

    public IReadOnlyList<RecienteRef> Leer()
    {
        var fichero = LeerFichero();
        if (ajustes is null) return fichero;
        // El orden y los que cuentan son los de PKHeX; un reciente sin datos guardados (ajustes copiados de otro móvil) sale con el nombre de su URI.
        return [.. ajustes.Actual.Startup.RecentlyLoaded.Select(u => fichero.FirstOrDefault(r => r.Uri == u) ?? new RecienteRef(u, NombreDe(u), string.Empty, DateTimeOffset.MinValue))];
    }

    private static string NombreDe(string uri)
    {
        var u = Uri.UnescapeDataString(uri);
        var corte = u.LastIndexOfAny(['/', '\\', ':']);
        return corte >= 0 && corte < u.Length - 1 ? u[(corte + 1)..] : u;
    }

    public void Anotar(Sesion s)
    {
        _indiceAnotado = false;
        _indiceAntes = null;
        if (ajustes is not null) UltimaCopia = Guardados.CopiasPkhex.AlCargar(s, ajustes.Actual);   // también sin documento (copia de la app, carga forzada): PKHeX hace la copia de todo lo que abre con ruta
        if (s.Documento is null) return;
        var nuevo = new RecienteRef(s.Documento.Uri, s.Documento.Nombre, s.Juego, DateTimeOffset.UtcNow)
            { Version = Design.LogosDeJuego.VersionDe(s.Guardado), Idioma = Design.LogosDeJuego.IdiomaDe(s.Guardado) };
        List<RecienteRef> lista;
        if (ajustes is null) lista = [.. LeerFichero().Where(r => r.Uri != s.Documento.Uri).Prepend(nuevo).Take(maximo)];
        else
        {
            Guardados.RecientesPkhex.Anotar(ajustes.Actual.Startup, s.Documento.Uri);
            ajustes.Persistir();   // solo se escribe, como PKHeX en LoadSaveFile: los recientes no los lee el Core, así que no se recarga nada por abrir una partida (auditoría de Fable I2)
            var vigentes = ajustes.Actual.Startup.RecentlyLoaded.ToHashSet(StringComparer.Ordinal);
            lista = [.. LeerFichero().Where(r => r.Uri != s.Documento.Uri && vigentes.Contains(r.Uri)).Prepend(nuevo)];
        }
        // La partida ya está abierta: un fallo de disco al anotar el reciente no puede tirar la apertura ni dejar el estado a medias (los ajustes ya dicen cuál es el reciente; el fichero solo guarda nombres).
        try
        {
            Directory.CreateDirectory(carpetaPrivada);
            File.WriteAllText(Ruta, JsonSerializer.Serialize(lista));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Avisar?.Invoke(IdiomaInterfaz.T("ServiciosPartida.PudoAnotarPartidaLista"), ex);
            return;
        }
        // Solo tras guardar los recientes: un fallo de la carpeta se comunica una vez.
        try
        {
            var indice = new IndicePartidas(carpetaPrivada);
            var anterior = indice.Capturar();
            indice.Anotar(s.Documento, s.Juego);
            _indiceAntes = anterior;
            _indiceAnotado = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Avisar?.Invoke("gestor-partidas", ex); }
    }

    public void Quitar(string uri)
    {
        if (ajustes is not null && ajustes.Actual.Startup.RecentlyLoaded.Remove(uri)) ajustes.Persistir();
        Directory.CreateDirectory(carpetaPrivada);
        File.WriteAllText(Ruta, JsonSerializer.Serialize(LeerFichero().Where(r => r.Uri != uri).ToList()));
    }
    /// <summary>Deja la lista tal como estaba (una apertura que el usuario descartó no deja rastro).</summary>
    public void Restaurar(IReadOnlyList<RecienteRef> antes)
    {
        // El índice no tiene el límite de ocho: restaurar sus bytes, no reconstruirlo desde Recientes.
        if (_indiceAnotado)
        {
            new IndicePartidas(carpetaPrivada).Restaurar(_indiceAntes);
            _indiceAnotado = false;
            _indiceAntes = null;
        }
        // El fichero y la lista de PKHeX deben retroceder juntos tras descartar la apertura (integración 10-3 + 10-4).
        if (ajustes is not null)
        {
            ajustes.Actual.Startup.RecentlyLoaded.Clear();
            ajustes.Actual.Startup.RecentlyLoaded.AddRange(antes.Select(r => r.Uri));
            ajustes.Persistir();
        }
        if (antes.Count == 0) { if (File.Exists(Ruta)) File.Delete(Ruta); return; }   // antes no había lista: no se deja ni el archivo
        Directory.CreateDirectory(carpetaPrivada); File.WriteAllText(Ruta, JsonSerializer.Serialize(antes.ToList()));
    }

}

/// <summary>Lo que la Shell necesita para abrir, guardar, exportar y recuperar partidas (plan 5.5), sin conocer Android.</summary>
public interface IServiciosPartida
{
    Task<Sesion?> AbrirAsync(CancellationToken cancelar);
    Task<Sesion?> AbrirRecienteAsync(RecienteRef reciente, CancellationToken cancelar);
    Task<InformeGuardado> GuardarAsync(Sesion sesion, CancellationToken cancelar);
    Task<InformeGuardado?> ExportarCopiaAsync(Sesion sesion, CancellationToken cancelar);
    /// <summary>Copia de recuperacion tras una operacion aplicada (nunca exporta).</summary>
    void Recuperacion(Sesion sesion);
    IReadOnlyList<RecienteRef> Recientes();
    IReadOnlyList<SesionRecuperable> Recuperables();
    Task<Sesion> RecuperarAsync(SesionRecuperable r, CancellationToken cancelar);
    void Descartar(SesionRecuperable r);

    /// <summary>Carpeta de copias vigente (permiso persistente comprobado), o null si no hay o se perdio.</summary>
    Task<CarpetaRef?> CarpetaCopiasAsync(CancellationToken cancelar);
    Task<CarpetaRef?> ElegirCarpetaCopiasAsync(CancellationToken cancelar);
    /// <summary>Exporta una copia con marca de fecha a la carpeta de copias, sin selector; null si no hay carpeta.</summary>
    Task<InformeGuardado?> GuardarEnCarpetaAsync(Sesion sesion, CancellationToken cancelar);

    /// <summary>Selectores del sistema para importar y exportar Pokemon y cajas (F4); null en anfitriones que no los tienen (la interfaz lo dice, nunca un boton muerto).</summary>
    IAlmacenDocumentos? Documentos => null;
    Task<IReadOnlyList<RecienteRef>> RevisarRecientesAsync(CancellationToken cancelar) => Task.FromResult(Recientes());
    Task<bool> AutorizarOriginalAsync(Sesion sesion, CancellationToken cancelar) => Task.FromResult(false);
    IAlmacenCarpetas? Carpetas => null;
    /// <summary>X37: la carpeta privada de la app (donde viven las plantillas de equipo). Null en anfitriones sin carpeta propia.</summary>
    string? CarpetaPrivada => null;

    /// <summary>
    /// Bloque 10-4: abre un guardado ya reconocido que NO viene de un documento del usuario (una copia de la app desde «Lista de carpetas», una carga forzada). Sin documento: solo «Exportar copia…».
    /// Por defecto (anfitriones sin almacén), una sesión en memoria.
    /// </summary>
    Task<Sesion> AbrirGuardadoAsync(PKHeX.Core.SaveFile sav, byte[] datos, string nombre, CancellationToken cancelar) => Task.FromResult(new Sesion(sav, nombre));
    /// <summary>Abrir CUALQUIER archivo (F10-2: selector, «Abrir con», «Compartir»): leer con tope, reconocer como PKHeX y abrir partidas. Null en anfitriones sin documentos.</summary>
    Abrir.AbridorArchivos? Abridor => null;
    /// <summary>El documento de un reciente, para abrirlo por el despachador (bloque 10-2); si ya no está, lo quita de Recientes y lo dice. Null = estos servicios no lo saben dar (se usa <see cref="AbrirRecienteAsync"/>).</summary>
    Task<DocumentoRef?> DocumentoRecienteAsync(RecienteRef reciente, CancellationToken cancelar) => Task.FromResult<DocumentoRef?>(null);

    /// <summary>
    /// Bloque 10-3 (<c>SlotWrite.ModifyUnset</c>): una partida abierta con <see cref="AbrirAsync"/>/<see cref="AbrirRecienteAsync"/> que el usuario descarta al preguntarle por los cambios sin guardar no deja rastro: sin su copia de trabajo
    /// y con Recientes como estaba (<paramref name="recientesAntes"/>). Como <c>Main.OpenSAV</c>, que no anota nada hasta el «Sí». Por defecto no hace nada (servicios que no escriben al abrir).
    /// </summary>
    void DeshacerApertura(Sesion descartada, IReadOnlyList<RecienteRef> recientesAntes) { }
}

public sealed class ServiciosPartida(IAlmacenDocumentos almacen, GestorSesiones gestor, RecientesStore recientes, IAlmacenCarpetas? carpetas = null, CarpetaStore? carpetaStore = null) : IServiciosPartida
{
    public async Task<IReadOnlyList<RecienteRef>> RevisarRecientesAsync(CancellationToken cancelar)
    {
        var vigentes = new List<RecienteRef>();
        foreach (var r in recientes.Leer())
        {
            bool disponible;
            try { disponible = await almacen.ObtenerAsync(r.Uri, cancelar).ConfigureAwait(false) is { } doc && await almacen.PuedeLeerAsync(doc, cancelar).ConfigureAwait(false); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException) { disponible = false; }
            if (disponible) vigentes.Add(r); else recientes.Quitar(r.Uri);
        }
        return vigentes;
    }
    public IAlmacenDocumentos? Documentos => almacen;
    public IAlmacenCarpetas? Carpetas => carpetas;
    public string? CarpetaPrivada => Path.GetDirectoryName(gestor.CarpetaSesiones);
    public Abrir.AbridorArchivos? Abridor { get; } = new(almacen, gestor, recientes);

    private sealed class DestinoFijo(IAlmacenDocumentos real, DocumentoRef destino) : IAlmacenDocumentos
    {
        public Task<DocumentoRef?> ElegirParaAbrirAsync(CancellationToken c) => real.ElegirParaAbrirAsync(c);
        public Task<DocumentoRef?> ElegirDestinoAsync(string n, CancellationToken c) => Task.FromResult<DocumentoRef?>(destino);
        public Task<DocumentoRef?> ObtenerAsync(string u, CancellationToken c) => real.ObtenerAsync(u, c);
        public Task<byte[]> LeerAsync(DocumentoRef d, CancellationToken c) => real.LeerAsync(d, c);
        public Task EscribirAsync(DocumentoRef d, ReadOnlyMemory<byte> b, CancellationToken c) => real.EscribirAsync(d, b, c);
    }

    public async Task<CarpetaRef?> CarpetaCopiasAsync(CancellationToken cancelar)
    {
        var guardada = carpetaStore?.Leer();
        if (guardada is null || carpetas is null) return null;
        var viva = await carpetas.ObtenerCarpetaAsync(guardada.Uri, cancelar).ConfigureAwait(false);
        if (viva is null) carpetaStore!.Olvidar();
        return viva;
    }

    public async Task<CarpetaRef?> ElegirCarpetaCopiasAsync(CancellationToken cancelar)
    {
        if (carpetas is null || carpetaStore is null) return null;
        var c = await carpetas.ElegirCarpetaAsync(cancelar).ConfigureAwait(false);
        if (c is not null) carpetaStore.Guardar(c);
        return c;
    }

    public async Task<InformeGuardado?> GuardarEnCarpetaAsync(Sesion sesion, CancellationToken cancelar)
    {
        var carpeta = await CarpetaCopiasAsync(cancelar).ConfigureAwait(false);
        if (carpeta is null || carpetas is null) return null;
        var ext = Path.GetExtension(sesion.NombreArchivo);
        var baseNombre = Path.GetFileNameWithoutExtension(sesion.NombreArchivo);
        var nombre = $"{baseNombre}-{DateTime.Now:yyyyMMdd-HHmmss}{ext}";
        var doc = await carpetas.CrearEnCarpetaAsync(carpeta, nombre, cancelar).ConfigureAwait(false);
        return await new GestorSesiones(new DestinoFijo(almacen, doc), sesion.CarpetaPrivada ?? string.Empty).ExportarCopiaAsync(sesion, cancelar).ConfigureAwait(false);
    }

    public async Task<Sesion?> AbrirAsync(CancellationToken cancelar)
    {
        var doc = await almacen.ElegirParaAbrirAsync(cancelar).ConfigureAwait(false);
        return doc is null ? null : await Abrir(doc, cancelar).ConfigureAwait(false);
    }

    public async Task<Sesion?> AbrirRecienteAsync(RecienteRef reciente, CancellationToken cancelar)
        => await Abrir((await DocumentoRecienteAsync(reciente, cancelar).ConfigureAwait(false))!, cancelar).ConfigureAwait(false);

    public async Task<DocumentoRef?> DocumentoRecienteAsync(RecienteRef reciente, CancellationToken cancelar)
    {
        var doc = await almacen.ObtenerAsync(reciente.Uri, cancelar).ConfigureAwait(false);
        if (doc is null)
        {
            recientes.Quitar(reciente.Uri);
            throw new AlmacenException(IdiomaInterfaz.F("ServiciosPartida.YaNoEstaAccesible", reciente.Nombre));
        }
        return doc;
    }

    private async Task<Sesion> Abrir(DocumentoRef doc, CancellationToken cancelar)
    {
        var s = await gestor.AbrirAsync(doc, cancelar).ConfigureAwait(false);
        recientes.Anotar(s);
        return s;
    }

    public async Task<Sesion> AbrirGuardadoAsync(PKHeX.Core.SaveFile sav, byte[] datos, string nombre, CancellationToken cancelar)
    {
        var s = await gestor.AbrirSinDocumentoAsync(sav, datos, nombre, cancelar).ConfigureAwait(false);
        recientes.Anotar(s);   // la copia automática al cargar; sin documento no hay reciente
        return s;
    }

    public Task<InformeGuardado> GuardarAsync(Sesion sesion, CancellationToken cancelar) => gestor.GuardarAsync(sesion, cancelar);
    public Task<bool> AutorizarOriginalAsync(Sesion sesion, CancellationToken cancelar) => gestor.AutorizarOriginalAsync(sesion, cancelar);
    public Task<InformeGuardado?> ExportarCopiaAsync(Sesion sesion, CancellationToken cancelar) => gestor.ExportarCopiaAsync(sesion, cancelar);
    public void Recuperacion(Sesion sesion) { try { gestor.GuardarRecuperacion(sesion); } catch (IOException) { } }
    public IReadOnlyList<RecienteRef> Recientes() => recientes.Leer();
    public IReadOnlyList<SesionRecuperable> Recuperables() => gestor.Recuperables();
    public Task<Sesion> RecuperarAsync(SesionRecuperable r, CancellationToken cancelar) => gestor.RecuperarAsync(r, cancelar);
    public void DeshacerApertura(Sesion descartada, IReadOnlyList<RecienteRef> recientesAntes)
    {
        recientes.Restaurar(recientesAntes);
        gestor.BorrarCopiaDeTrabajo(descartada);
    }
    public void Descartar(SesionRecuperable r) => gestor.Descartar(r);
}
