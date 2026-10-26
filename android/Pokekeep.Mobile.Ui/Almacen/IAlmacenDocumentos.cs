// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Almacen;

/// <summary>
/// Un documento del almacenamiento del usuario (Android: URI de SAF). Solo nombre, tamano y fecha: nunca nada del entrenador. <see cref="SoloLectura"/> (F10-2): llego por «Abrir con» o «Compartir» sin permiso
/// de escritura; «Guardar» permite volver a elegir el original y autorizarlo, o exportar una copia.
/// </summary>
public sealed record DocumentoRef(string Uri, string Nombre, long UltimaModificacionMs, long Bytes, bool SoloLectura = false);

/// <summary>
/// Acceso al almacenamiento del usuario por el Storage Access Framework (plan 5.5). En Android lo implementa <c>AlmacenDocumentosAndroid</c> (ContentResolver + selectores del sistema); en el PC, las pruebas usan
/// ficheros. Todo lo que escribe lo hace por orden EXPLICITA del usuario (regla 13): el original nunca se toca por detras.
/// </summary>
public interface IAlmacenDocumentos
{
    /// <summary>Selector del sistema para abrir un guardado (<c>ACTION_OPEN_DOCUMENT</c>, con permiso persistente). Null si el usuario cancela.</summary>
    Task<DocumentoRef?> ElegirParaAbrirAsync(CancellationToken cancelar);

    /// <summary>Pide elegir el original con escritura, sugiriendo su URI. Todavía NO persiste el permiso.</summary>
    Task<DocumentoRef?> ElegirOriginalAsync(DocumentoRef original, CancellationToken cancelar) => Task.FromResult<DocumentoRef?>(null);

    /// <summary>Solo tras comprobar la identidad del documento elegido: persiste lectura y escritura.</summary>
    Task<bool> PersistirEscrituraAsync(DocumentoRef elegido, CancellationToken cancelar) => Task.FromResult(false);

    /// <summary>Selector del sistema para crear un destino (<c>ACTION_CREATE_DOCUMENT</c>) con el nombre sugerido. Null si cancela.</summary>
    Task<DocumentoRef?> ElegirDestinoAsync(string nombreSugerido, CancellationToken cancelar);

    /// <summary>Estado ACTUAL del documento (existe, fecha y tamano), o null si ya no esta o se perdio el permiso.</summary>
    Task<DocumentoRef?> ObtenerAsync(string uri, CancellationToken cancelar);

    Task<byte[]> LeerAsync(DocumentoRef documento, CancellationToken cancelar);

    /// <summary>Comprueba el permiso de lectura sin cargar la partida completa.</summary>
    async Task<bool> PuedeLeerAsync(DocumentoRef documento, CancellationToken cancelar)
    {
        try { await LeerHastaAsync(documento, 1, cancelar).ConfigureAwait(false); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException) { return false; }
    }

    /// <summary>
    /// Lee como mucho <paramref name="tope"/> bytes: null si el documento es MAYOR (F10-ABRIR-007: un archivo de 4 GB compartido por error no se carga en memoria). Android lo implementa leyendo por
    /// trozos; esta version por defecto (anfitriones de prueba) lee y mide.
    /// </summary>
    async Task<byte[]?> LeerHastaAsync(DocumentoRef documento, long tope, CancellationToken cancelar)
    {
        var datos = await LeerAsync(documento, cancelar).ConfigureAwait(false);
        return datos.LongLength > tope ? null : datos;
    }

    /// <summary>Sustituye el contenido en UNA pasada (Android: <c>openFileDescriptor(uri, "rwt")</c>); nunca borra y crea.</summary>
    Task EscribirAsync(DocumentoRef documento, ReadOnlyMemory<byte> datos, CancellationToken cancelar);
}
