// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Security.Cryptography;
using System.Text.Json;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Abrir;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Almacen;

/// <summary>Por que no se pudo abrir o guardar. El mensaje esta en castellano y dice que hacer; nunca se pierde el trabajo (la copia de sesion se conserva).</summary>
public sealed class AlmacenException(string mensaje, ResultadoGuardado resultado = ResultadoGuardado.Fallo) : Exception(mensaje)
{
    public ResultadoGuardado Resultado { get; } = resultado;
}

public enum ResultadoGuardado
{
    /// <summary>Escrito y releido byte a byte: el documento es exactamente la copia de sesion.</summary>
    Verificado,
    /// <summary>Escrito, pero el proveedor no deja releer: «exportado, sin comprobar» (la copia de trabajo se conserva).</summary>
    SinComprobar,
    /// <summary>Otro programa cambio el original despues de abrirlo: no se pisa (se ofrece Exportar copia).</summary>
    OriginalCambiado,
    /// <summary>Falló la escritura o su verificación; se restauró y releyó el original byte a byte.</summary>
    Restaurado,
    Fallo,
}

public sealed record InformeGuardado(ResultadoGuardado Resultado, string Mensaje, string? CopiaDeSeguridad, string Sha256);

/// <summary>Una sesion recuperable tras morir el proceso (nombre y juego, nunca OT/TID/SID).</summary>
public sealed record SesionRecuperable(string Carpeta, string Nombre, string Juego, DateTimeOffset Fecha, string UriOriginal);

/// <summary>
/// Sesiones y copias de trabajo (plan 5.2 y 5.5). Abrir = leer el documento entero, SHA-256, copia PRIVADA en <c>sesiones/&lt;sha8&gt;/</c> y <c>SaveUtil.GetSaveFile(datos, nombre)</c> (el Core decide algunos formatos
/// por nombre). Guardar = sustitucion segura en 6 pasos: copia de seguridad rotada, exportar con el Core, validar en memoria, comprobar que el original no cambio, escribir en una pasada, releer y comparar (si no
/// coincide, restaurar). Tras cada operacion aplicada se escribe la copia de recuperacion; nada se exporta solo.
/// </summary>
public sealed partial class GestorSesiones(IAlmacenDocumentos almacen, string carpetaPrivada, int copiasMaximas = 30)
{
    private const string Meta = "sesion.json";
    public string CarpetaSesiones => Path.Combine(carpetaPrivada, "sesiones");
    public string CarpetaCopias => Path.Combine(carpetaPrivada, "bak");

    /// <summary>Lo que se recuerda de la sesión (nunca OT/TID/SID). <c>Envoltorio</c> y <c>SoloLectura</c> (F10-2) faltan en las sesiones de antes: valen «directo» y false.</summary>
    private sealed record MetaSesion(string Uri, string Nombre, long UltimaModificacionMs, long Bytes, string Sha256, string Juego, DateTimeOffset Abierta, string? Envoltorio = null, bool SoloLectura = false);

    /// <summary>Lee el documento, guarda la copia privada y devuelve la sesion con el guardado del Core ya cargado (directo, dentro de un .zip o en una tarjeta de GameCube con una sola partida).</summary>
    public async Task<Sesion> AbrirAsync(DocumentoRef documento, CancellationToken cancelar = default)
    {
        if (ReconocedorArchivos.MotivoAntesDeLeer(documento.Bytes) is { } motivo) throw new AlmacenException($"«{documento.Nombre}»: {motivo}");   // F10-ABRIR-007, PK-IO-027: ni se lee
        byte[] datos;
        try { datos = await almacen.LeerAsync(documento, cancelar).ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new AlmacenException(IdiomaInterfaz.T("GestorSesiones.NoSePudoLeer") + ex.Message); }
        if (!SaveUtil.IsSizeValid(datos.Length) && !EsZip(datos)) throw new AlmacenException(IdiomaInterfaz.F("GestorSesiones.NoTieneElTamano", documento.Nombre, datos.Length));
        // El Core puede descifrar o normalizar IN SITU el buffer que recibe (contenedores de GameCube, DeSmuME...): el reconocedor le da una COPIA y la huella es de lo que realmente hay en el documento.
        return ReconocedorArchivos.Reconocer(datos, documento.Nombre) switch
        {
            ArchivoPartida p => await AbrirReconocidaAsync(documento, datos, p, cancelar).ConfigureAwait(false),
            ArchivoTarjetaGC => throw new AlmacenException(IdiomaInterfaz.F("GestorSesiones.EsUnaTarjetaDe", documento.Nombre)),
            _ => throw new AlmacenException(IdiomaInterfaz.F("GestorSesiones.NoSeReconoceEl", documento.Nombre)),
        };
    }

    /// <summary>
    /// Abre como sesión una partida YA reconocida (<see cref="ReconocedorArchivos"/>) a partir de los bytes leídos del documento: copia privada <c>original.bin</c> (el contenedor tal cual: .zip o tarjeta),
    /// huella SHA-256 de esos bytes, el envoltorio para guardar igual y si el documento se abrió sin permiso de escritura.
    /// </summary>
    public async Task<Sesion> AbrirReconocidaAsync(DocumentoRef documento, byte[] datos, ArchivoPartida partida, CancellationToken cancelar = default)
    {
        documento = documento with { Bytes = datos.LongLength };
        var sha = Convert.ToHexString(SHA256.HashData(datos));
        var carpeta = Path.Combine(CarpetaSesiones, sha[..8]);
        Directory.CreateDirectory(carpeta);
        await File.WriteAllBytesAsync(Path.Combine(carpeta, "original.bin"), datos, cancelar).ConfigureAwait(false);
        var sesion = new Sesion(partida.Partida, documento.Nombre) { Documento = documento, Sha256Original = sha, CarpetaPrivada = carpeta, Envoltorio = partida.Envoltorio };
        var meta = new MetaSesion(documento.Uri, documento.Nombre, documento.UltimaModificacionMs, documento.Bytes, sha, sesion.Juego, DateTimeOffset.UtcNow, partida.Envoltorio.Clave, documento.SoloLectura);
        await File.WriteAllTextAsync(Path.Combine(carpeta, Meta), JsonSerializer.Serialize(meta), cancelar).ConfigureAwait(false);
        return sesion;
    }

    /// <summary>
    /// Borra la copia de trabajo de una apertura que el usuario descartó (bloque 10-3). Si en esa carpeta hay una copia de recuperación (la misma partida con cambios sin guardar de otra vez), no se toca nada:
    /// es de la persona, no de esta apertura.
    /// </summary>
    public void BorrarCopiaDeTrabajo(Sesion descartada)
    {
        var carpeta = descartada.CarpetaPrivada;
        if (carpeta is null || !Path.GetFullPath(carpeta).StartsWith(Path.GetFullPath(CarpetaSesiones) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return;
        try { if (Directory.Exists(carpeta) && !File.Exists(Path.Combine(carpeta, "recuperacion.bin"))) Directory.Delete(carpeta, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static bool EsZip(ReadOnlySpan<byte> datos) => datos is [0x50, 0x4B, 0x03, 0x04, ..];

    /// <summary>El contenedor tal y como está en el documento (copia privada <c>original.bin</c>): un .zip se actualiza a partir de él al guardar.</summary>
    private static byte[] Contenedor(Sesion s)
    {
        if (s.Envoltorio.Tipo != TipoEnvoltorio.Zip || s.CarpetaPrivada is null) return [];
        var ruta = Path.Combine(s.CarpetaPrivada, "original.bin");
        return File.Exists(ruta) ? File.ReadAllBytes(ruta) : [];
    }

    /// <summary>Copia de recuperacion: el guardado EXPORTADO en memoria (no el fichero del usuario) tras cada operacion aplicada. Nunca se exporta automaticamente.</summary>
    public void GuardarRecuperacion(Sesion s)
    {
        if (s.CarpetaPrivada is null) return;
        var tmp = Path.Combine(s.CarpetaPrivada, "recuperacion.bin.tmp");
        File.WriteAllBytes(tmp, s.Guardado.Write().ToArray());
        File.Move(tmp, Path.Combine(s.CarpetaPrivada, "recuperacion.bin"), overwrite: true);
        File.WriteAllText(Path.Combine(s.CarpetaPrivada, "cambios.json"), JsonSerializer.Serialize(new { s.CambiosPendientes, fecha = DateTimeOffset.UtcNow }));
    }

    /// <summary>Sesiones con cambios sin guardar (hay copia de recuperacion mas nueva que el original), mas recientes primero.</summary>
    public IReadOnlyList<SesionRecuperable> Recuperables()
    {
        var lista = new List<SesionRecuperable>();
        if (!Directory.Exists(CarpetaSesiones)) return lista;
        foreach (var d in Directory.EnumerateDirectories(CarpetaSesiones))
        {
            try
            {
                var rec = Path.Combine(d, "recuperacion.bin");
                var metaRuta = Path.Combine(d, Meta);
                if (!File.Exists(rec) || !File.Exists(metaRuta)) continue;
                var m = JsonSerializer.Deserialize<MetaSesion>(File.ReadAllText(metaRuta));
                if (m is null) continue;
                lista.Add(new SesionRecuperable(d, m.Nombre, m.Juego, File.GetLastWriteTimeUtc(rec), m.Uri));
            }
            catch (Exception ex) when (ex is IOException or JsonException) { }
        }
        return [.. lista.OrderByDescending(x => x.Fecha)];
    }

    /// <summary>Reabre una sesion desde su copia de recuperacion (con los cambios sin guardar). El original sigue siendo el documento del usuario.</summary>
    public async Task<Sesion> RecuperarAsync(SesionRecuperable r, CancellationToken cancelar = default)
    {
        var meta = JsonSerializer.Deserialize<MetaSesion>(await File.ReadAllTextAsync(Path.Combine(r.Carpeta, Meta), cancelar).ConfigureAwait(false))!;
        var datos = await File.ReadAllBytesAsync(Path.Combine(r.Carpeta, "recuperacion.bin"), cancelar).ConfigureAwait(false);
        // La copia de recuperación es lo que exporta el Core: la partida suelta si venía en un .zip, la tarjeta entera si venía en una tarjeta de GameCube.
        var envoltorio = EnvoltorioPartida.DeClave(meta.Envoltorio);
        var sav = (envoltorio.Tipo == TipoEnvoltorio.Zip ? EnvoltorioPartida.Directo : envoltorio).Desempaquetar(datos, meta.Nombre) ?? throw new AlmacenException(IdiomaInterfaz.T("GestorSesiones.LaCopiaDeRecuperacion"));
        var doc = new DocumentoRef(meta.Uri, meta.Nombre, meta.UltimaModificacionMs, meta.Bytes, meta.SoloLectura);
        var cambios = 1;
        try { using var j = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(r.Carpeta, "cambios.json"), cancelar).ConfigureAwait(false)); cambios = j.RootElement.GetProperty("CambiosPendientes").GetInt32(); } catch { }
        return new Sesion(sav, meta.Nombre) { Documento = doc, Sha256Original = meta.Sha256, CarpetaPrivada = r.Carpeta, CambiosPendientes = cambios, Envoltorio = envoltorio };
    }

    public void Descartar(SesionRecuperable r)
    {
        try { File.Delete(Path.Combine(r.Carpeta, "recuperacion.bin")); File.Delete(Path.Combine(r.Carpeta, "cambios.json")); } catch (IOException) { }
    }

    /// <summary>X34: una nueva concesión de SAF solo cambia el original tras comprobar la identidad de lo abierto.</summary>
    public async Task<bool> AutorizarOriginalAsync(Sesion s, CancellationToken cancelar = default)
    {
        if (s.Documento is not { SoloLectura: true } original) return false;
        // Las sesiones anteriores a X34 podían conservar tamaño desconocido del proveedor.
        if (original.Bytes < 0 && s.CarpetaPrivada is { } privada)
            original = original with { Bytes = new FileInfo(Path.Combine(privada, "original.bin")).Length };
        if (original.Bytes < 0 || original.Bytes > ReconocedorArchivos.TopeBytes)
            throw new AlmacenException(IdiomaInterfaz.T("GestorSesiones.NoSePudoComprobar"));
        var elegido = await almacen.ElegirOriginalAsync(original, cancelar).ConfigureAwait(false);
        if (elegido is null) return false;
        if (!string.Equals(original.Nombre, elegido.Nombre, StringComparison.Ordinal) ||
            (elegido.Bytes >= 0 && elegido.Bytes != original.Bytes))
            throw new AlmacenException(IdiomaInterfaz.T("GestorSesiones.NoEsElMismo"));
        var datos = await almacen.LeerHastaAsync(elegido, original.Bytes, cancelar).ConfigureAwait(false);
        if (datos is null || datos.LongLength != original.Bytes ||
            !string.Equals(Convert.ToHexString(SHA256.HashData(datos)), s.Sha256Original, StringComparison.OrdinalIgnoreCase))
            throw new AlmacenException(IdiomaInterfaz.T("GestorSesiones.ElContenidoNoCoincide"));
        if (elegido.SoloLectura || !await almacen.PersistirEscrituraAsync(elegido, cancelar).ConfigureAwait(false))
            throw new AlmacenException(IdiomaInterfaz.T("GestorSesiones.ElProveedorNoConcedio"));
        var escribible = elegido with { Bytes = datos.LongLength, SoloLectura = false };
        // Persistir también la nueva URI: Samsung puede entregar el mismo documento mediante otro proveedor.
        if (s.CarpetaPrivada is { } carpeta)
        {
            var ruta = Path.Combine(carpeta, Meta);
            var meta = JsonSerializer.Deserialize<MetaSesion>(await File.ReadAllTextAsync(ruta, cancelar).ConfigureAwait(false))!;
            await File.WriteAllTextAsync(ruta + ".tmp", JsonSerializer.Serialize(meta with
            { Uri = escribible.Uri, Nombre = escribible.Nombre, Bytes = escribible.Bytes, UltimaModificacionMs = escribible.UltimaModificacionMs, SoloLectura = false }), cancelar).ConfigureAwait(false);
            File.Move(ruta + ".tmp", ruta, overwrite: true);
        }
        s.Documento = escribible;
        return true;
    }

    /// <summary>«Guardar»: sustitucion segura del documento ORIGINAL en 6 pasos. Solo por orden explicita del usuario.</summary>
    public async Task<InformeGuardado> GuardarAsync(Sesion s, CancellationToken cancelar = default)
    {
        var doc = s.Documento ?? throw new AlmacenException(IdiomaInterfaz.T("GestorSesiones.EstaSesionNoTiene"));
        // F10-ABRIR-003: abierta con «Abrir con» o «Compartir» SIN permiso de escritura, el original no se puede sustituir; se dice y se ofrece Exportar copia.
        if (doc.SoloLectura) throw new AlmacenException(MensajeSoloLectura, ResultadoGuardado.OriginalCambiado);
        // (2) Exportar con el Core, en memoria (dentro del .zip original si venía en un .zip; la tarjeta entera si venía en una tarjeta de GameCube).
        var exportado = s.Envoltorio.Empaquetar(s.Guardado, Contenedor(s), doc.Nombre, mismoContenedor: true);
        var sha = Convert.ToHexString(SHA256.HashData(exportado));
        // (3) Validar en memoria: el Core reconoce el resultado, es del mismo tipo y las cajas son las de la sesion.
        var comprobado = s.Envoltorio.Desempaquetar(exportado, s.NombreArchivo);   // sobre una copia: el Core puede tocar el buffer
        if (comprobado is null || comprobado.GetType() != s.Guardado.GetType() || !CajasIguales(s.Guardado, comprobado))
            throw new AlmacenException(IdiomaInterfaz.T("GestorSesiones.ElGuardadoExportadoNo"));
        // (4) El original no cambio desde que lo abriste.
        DocumentoRef? actual;
        try { actual = await almacen.ObtenerAsync(doc.Uri, cancelar).ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return new InformeGuardado(ResultadoGuardado.Fallo, IdiomaInterfaz.T("GestorSesiones.PudoComprobarOriginalAntes"), null, s.Sha256Original);
        }
        if (actual is null) throw new AlmacenException(IdiomaInterfaz.T("GestorSesiones.ElArchivoOriginalYa"), ResultadoGuardado.OriginalCambiado);
        byte[] original;
        try { original = await almacen.LeerAsync(actual, cancelar).ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return new InformeGuardado(ResultadoGuardado.Fallo, IdiomaInterfaz.T("GestorSesiones.PudoLeerOriginalAntes"), null, s.Sha256Original);
        }
        var shaActual = Convert.ToHexString(SHA256.HashData(original));
        if (!string.Equals(shaActual, s.Sha256Original, StringComparison.OrdinalIgnoreCase))
            return new InformeGuardado(ResultadoGuardado.OriginalCambiado, IdiomaInterfaz.T("GestorSesiones.OtroProgramaCambioArchivo"), null, shaActual);
        // (1) Copia de seguridad rotada de lo que hay AHORA en el original (equivale a bak\ de PKHeX).
        var copia = CopiaDeSeguridad(s.Sha256Original, original);
        // (5) Escribir en una sola pasada. Una excepción NO demuestra que el proveedor no haya truncado o escrito parte del archivo.
        cancelar.ThrowIfCancellationRequested();
        try { await almacen.EscribirAsync(actual, exportado, cancelar).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return await RecuperarOriginalAsync(actual, original, copia, IdiomaInterfaz.F("GestorSesiones.PudoEscribirArchivo", ex.Message)).ConfigureAwait(false);
        }
        // (6) Releer y comparar; si no coincide, restaurar desde la copia de seguridad.
        byte[] releido;
        try
        {
            var tras = await almacen.ObtenerAsync(doc.Uri, cancelar).ConfigureAwait(false) ?? actual;
            releido = await almacen.LeerAsync(tras, cancelar).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            RecordarDocumentoEscrito(s, actual, exportado, sha);
            return new InformeGuardado(ResultadoGuardado.SinComprobar, IdiomaInterfaz.T("GestorSesiones.GuardadoPeroProveedorDeja"), copia, sha);
        }
        if (!releido.AsSpan().SequenceEqual(exportado))
        {
            return await RecuperarOriginalAsync(actual, original, copia, IdiomaInterfaz.T("GestorSesiones.EscritoCoincideEsperado")).ConfigureAwait(false);
        }
        RecordarDocumentoEscrito(s, actual, exportado, sha);
        s.CambiosPendientes = 0;
        try { File.Delete(Path.Combine(s.CarpetaPrivada ?? string.Empty, "recuperacion.bin")); } catch { }
        return new InformeGuardado(ResultadoGuardado.Verificado, IdiomaInterfaz.T("GestorSesiones.GuardadoComprobadoArchivoExactamente"), copia, sha);
    }

    /// <summary>Recuerda los bytes enviados sin dar por limpio el trabajo: la siguiente lectura debe coincidir con esta huella, también tras recuperar la sesión.</summary>
    private static void RecordarDocumentoEscrito(Sesion s, DocumentoRef documento, byte[] datos, string sha)
    {
        s.Documento = documento with { Bytes = datos.Length, SoloLectura = s.Documento?.SoloLectura ?? documento.SoloLectura };
        s.Sha256Original = sha;
        if (s.CarpetaPrivada is null) return;
        var original = Path.Combine(s.CarpetaPrivada, "original.bin");
        File.WriteAllBytes(original + ".tmp", datos);
        File.Move(original + ".tmp", original, overwrite: true);
        var ruta = Path.Combine(s.CarpetaPrivada, Meta);
        if (!File.Exists(ruta)) return;
        var meta = JsonSerializer.Deserialize<MetaSesion>(File.ReadAllText(ruta));
        if (meta is not null)
        {
            File.WriteAllText(ruta + ".tmp", JsonSerializer.Serialize(meta with { Sha256 = sha, Bytes = datos.Length }));
            File.Move(ruta + ".tmp", ruta, overwrite: true);
        }
    }

    /// <summary>
    /// Recuperación tras una escritura que puede haber sido parcial. La cancelación de la operación no cancela la reparación del original.
    /// Solo una relectura EXACTA permite afirmar «intacto» o «restaurado»; ante cualquier duda se conservan copia, huella y cambios pendientes.
    /// </summary>
    private async Task<InformeGuardado> RecuperarOriginalAsync(DocumentoRef documento, byte[] original, string copia, string motivo)
    {
        var shaOriginal = Convert.ToHexString(SHA256.HashData(original));
        var ayuda = IdiomaInterfaz.F("GestorSesiones.CopiaTrabajoSigueApp", Path.GetFileName(copia));
        try
        {
            var actual = await almacen.ObtenerAsync(documento.Uri, CancellationToken.None).ConfigureAwait(false) ?? documento;
            var bytes = await almacen.LeerAsync(actual, CancellationToken.None).ConfigureAwait(false);
            if (bytes.AsSpan().SequenceEqual(original))
                return new InformeGuardado(ResultadoGuardado.Fallo, motivo + IdiomaInterfaz.T("GestorSesiones.ComprobadoOriginalSigueIntacto") + ayuda, copia, shaOriginal);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { /* Aún se puede intentar restaurar; no se afirma que el original siga intacto. */ }

        try
        {
            await almacen.EscribirAsync(documento, original, CancellationToken.None).ConfigureAwait(false);
            var actual = await almacen.ObtenerAsync(documento.Uri, CancellationToken.None).ConfigureAwait(false) ?? documento;
            var bytes = await almacen.LeerAsync(actual, CancellationToken.None).ConfigureAwait(false);
            if (bytes.AsSpan().SequenceEqual(original))
                return new InformeGuardado(ResultadoGuardado.Restaurado, motivo + IdiomaInterfaz.T("GestorSesiones.SeHaRestauradoEl") + ayuda, copia, shaOriginal);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { /* Incluye fallos del proveedor Android: nunca se anuncia una restauración no comprobada. */ }
        return new InformeGuardado(ResultadoGuardado.Fallo, motivo + IdiomaInterfaz.T("GestorSesiones.PodidoComprobarRestauracionArchivo") + ayuda, copia, shaOriginal);
    }

    /// <summary>«Exportar copia…»: un documento NUEVO elegido por el usuario (nombre y extension originales; para <c>main</c>, exactamente <c>main</c>). No sustituye el original.</summary>
    public async Task<InformeGuardado?> ExportarCopiaAsync(Sesion s, CancellationToken cancelar = default)
    {
        var destino = await almacen.ElegirDestinoAsync(s.NombreArchivo, cancelar).ConfigureAwait(false);
        if (destino is null) return null;
        var exportado = s.Envoltorio.Empaquetar(Exportar(s.Guardado, destino.Nombre), Contenedor(s), destino.Nombre);   // banderas de GetSuggestedFlags y después el contenedor (.zip actualizado), como ExportSAV → ExportSAVInternal
        await almacen.EscribirAsync(destino, exportado, cancelar).ConfigureAwait(false);
        string mensaje; var resultado = ResultadoGuardado.Verificado;
        try
        {
            var relee = await almacen.LeerAsync((await almacen.ObtenerAsync(destino.Uri, cancelar).ConfigureAwait(false)) ?? destino, cancelar).ConfigureAwait(false);
            if (relee.AsSpan().SequenceEqual(exportado)) mensaje = IdiomaInterfaz.F("GestorSesiones.CopiaExportadaComprobada", destino.Nombre);
            else { resultado = ResultadoGuardado.Fallo; mensaje = IdiomaInterfaz.T("GestorSesiones.CopiaExportadaCoincideEsperado"); }
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException) { resultado = ResultadoGuardado.SinComprobar; mensaje = IdiomaInterfaz.T("GestorSesiones.CopiaExportadaPeroProveedor"); }
        return new InformeGuardado(resultado, mensaje, null, Convert.ToHexString(SHA256.HashData(exportado)));
    }

    /// <summary>Copias de seguridad disponibles (nombre, fecha, tamano), mas recientes primero.</summary>
    public IReadOnlyList<(string Nombre, DateTime Fecha, long Bytes)> Copias()
        => Directory.Exists(CarpetaCopias) ? [.. CopiasPropias().OrderByDescending(f => f.LastWriteTimeUtc).Select(f => (f.Name, f.LastWriteTimeUtc, f.Length))] : [];

    /// <summary>
    /// Restaura el ORIGINAL desde una copia de seguridad elegida por NOMBRE (nunca por ruta): comprueba nombre simple, que el contenido lo reconoce el Core y su SHA-256 (los 8 primeros del nombre son los del original
    /// que respaldaba), y despues lo escribe con la misma sustitucion segura. Antes de escribir se hace una copia nueva de lo que hay ahora.
    /// </summary>
    public async Task<InformeGuardado> RestaurarCopiaAsync(Sesion s, string nombreCopia, CancellationToken cancelar = default)
    {
        if (!ZipSeguro.NombreSimple(nombreCopia)) throw new AlmacenException(IdiomaInterfaz.T("GestorSesiones.NombreDeCopiaNo"));
        var ruta = Path.GetFullPath(Path.Combine(CarpetaCopias, nombreCopia));
        if (!ruta.StartsWith(Path.GetFullPath(CarpetaCopias) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(ruta)) throw new AlmacenException(IdiomaInterfaz.T("GestorSesiones.LaCopiaNoExiste"));
        var datos = await File.ReadAllBytesAsync(ruta, cancelar).ConfigureAwait(false);
        var sav = s.Envoltorio.Desempaquetar(datos, s.NombreArchivo) ?? throw new AlmacenException(IdiomaInterfaz.T("GestorSesiones.LaCopiaDeSeguridad"));
        var sha = Convert.ToHexString(SHA256.HashData(datos));
        if (!sha.StartsWith(nombreCopia[..8], StringComparison.OrdinalIgnoreCase)) throw new AlmacenException(IdiomaInterfaz.T("GestorSesiones.LaCopiaDeSeguridad2"));
        var doc = s.Documento ?? throw new AlmacenException(IdiomaInterfaz.T("GestorSesiones.EstaSesionNoTiene2"));
        if (doc.SoloLectura) throw new AlmacenException(MensajeSoloLectura, ResultadoGuardado.OriginalCambiado);
        DocumentoRef actual;
        byte[] vigente;
        try
        {
            actual = await almacen.ObtenerAsync(doc.Uri, cancelar).ConfigureAwait(false) ?? throw new AlmacenException(IdiomaInterfaz.T("GestorSesiones.ElArchivoOriginalYa2"));
            vigente = await almacen.LeerAsync(actual, cancelar).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            throw new AlmacenException(IdiomaInterfaz.T("GestorSesiones.NoSePudoLeer2"));
        }
        var copia = CopiaDeSeguridad(Convert.ToHexString(SHA256.HashData(vigente)), vigente);
        cancelar.ThrowIfCancellationRequested();
        // Restaurar cambia el disco, nunca el borrador de trabajo; se conserva también si antes estaba limpio.
        s.CambiosPendientes = Math.Max(1, s.CambiosPendientes);
        GuardarRecuperacion(s);
        try { await almacen.EscribirAsync(actual, datos, cancelar).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return await RecuperarOriginalAsync(actual, vigente, copia, IdiomaInterfaz.T("GestorSesiones.PudoEscribirCopiaRestaurar")).ConfigureAwait(false);
        }
        byte[] relee;
        try { relee = await almacen.LeerAsync((await almacen.ObtenerAsync(doc.Uri, cancelar).ConfigureAwait(false)) ?? actual, cancelar).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            RecordarDocumentoEscrito(s, actual, datos, sha);
            return new InformeGuardado(ResultadoGuardado.SinComprobar, IdiomaInterfaz.T("GestorSesiones.CopiaExportadaOriginalComprobar"), copia, sha);
        }
        if (!relee.AsSpan().SequenceEqual(datos))
            return await RecuperarOriginalAsync(actual, vigente, copia, IdiomaInterfaz.T("GestorSesiones.EscritoRestaurarCoincideCopia")).ConfigureAwait(false);
        RecordarDocumentoEscrito(s, actual, datos, sha);
        return new InformeGuardado(ResultadoGuardado.Verificado, IdiomaInterfaz.T("GestorSesiones.CopiaRestauradaComprobada"), copia, sha);
    }

    /// <summary>Lo que se dice al pulsar «Guardar» en una partida abierta sin permiso de escritura.</summary>
    public static string MensajeSoloLectura => IdiomaInterfaz.T("GestorSesiones.MensajeSoloLectura");

    private string CopiaDeSeguridad(string shaOriginal, byte[] original)
    {
        Directory.CreateDirectory(CarpetaCopias);
        var ruta = Path.Combine(CarpetaCopias, $"{shaOriginal[..8]}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.bak");
        File.WriteAllBytes(ruta, original);
        var viejas = CopiasPropias().OrderByDescending(f => f.LastWriteTimeUtc).Skip(copiasMaximas);
        foreach (var f in viejas) { try { f.Delete(); } catch (IOException) { } }
        return ruta;
    }

    private static bool CajasIguales(SaveFile a, SaveFile b)
    {
        if (a.BoxCount != b.BoxCount) return false;
        for (var i = 0; i < a.BoxCount; i++)
        {
            var x = a.GetBoxData(i); var y = b.GetBoxData(i);
            if (x.Length != y.Length) return false;
            for (var j = 0; j < x.Length; j++)
                if (!x[j].Data.SequenceEqual(y[j].Data)) return false;
        }
        return true;
    }

    // ------------------------------------------------------------------------------------------------ bloque 10-4
    /// <summary>
    /// Las copias de antes de guardar de ESTE gestor (<c>&lt;sha8&gt;-&lt;aaaaMMdd-HHmmss&gt;.bak</c>). La carpeta <c>bak</c> es también la carpeta de copias de PKHeX (<c>LocalResources.GetBackupPath()</c>), donde la copia
    /// automática al cargar deja las suyas con el nombre de PKHeX (<c>«archivo» [EO (versión) - tiempo].bak</c>): esas NO se listan aquí, NO se restauran por huella y NO rotan (PKHeX nunca las borra).
    /// </summary>
    private IEnumerable<FileInfo> CopiasPropias()
        => Directory.Exists(CarpetaCopias) ? new DirectoryInfo(CarpetaCopias).GetFiles("*.bak").Where(f => NombreCopiaPropia().IsMatch(f.Name)) : [];

    [System.Text.RegularExpressions.GeneratedRegex(@"^[0-9A-Fa-f]{8}-\d{8}-\d{6}\.bak$")]
    private static partial System.Text.RegularExpressions.Regex NombreCopiaPropia();

    /// <summary>
    /// Una sesión de un guardado que NO viene de un documento del usuario (bloque 10-4): una copia de la carpeta de copias de la app abierta desde «Lista de carpetas» o un guardado abierto a la fuerza con
    /// «Forzar carga de SAV». Lleva su copia privada (<c>original.bin</c>, para la copia automática y «Exportar copia (BAK)»), pero ni documento (solo «Exportar copia…»: nunca se sobrescribe nada del usuario) ni
    /// <c>sesion.json</c> (no sale en «Recuperar»).
    /// </summary>
    public async Task<Sesion> AbrirSinDocumentoAsync(SaveFile sav, byte[] datos, string nombre, CancellationToken cancelar = default)
    {
        var sha = Convert.ToHexString(SHA256.HashData(datos));
        var carpeta = Path.Combine(CarpetaSesiones, "sin-documento-" + sha[..8]);   // aparte: nunca comparte carpeta (ni recuperación) con la sesión del mismo archivo abierta desde un documento
        Directory.CreateDirectory(carpeta);
        await File.WriteAllBytesAsync(Path.Combine(carpeta, "original.bin"), datos, cancelar).ConfigureAwait(false);
        return new Sesion(sav, nombre) { Sha256Original = sha, CarpetaPrivada = carpeta };
    }

    /// <summary>
    /// <c>WinFormsUtil.ExportSAV</c>: el guardado exportado con las opciones que sugiere el Core para la extensión del DESTINO (<c>Metadata.GetSuggestedFlags</c>): un <c>.dsv</c> exportado con otra extensión
    /// pierde el pie de DeSmuME y un <c>.gci</c> la cabecera de GameCube, como en PKHeX (bloque 10-4, PK-IO-033).
    /// </summary>
    public static byte[] Exportar(SaveFile sav, string nombreDestino) => sav.Write(sav.Metadata.GetSuggestedFlags(Path.GetExtension(nombreDestino.AsSpan()))).ToArray();
}
