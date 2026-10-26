// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Abrir;

/// <summary>De dónde llega el archivo: el selector de «Abrir archivo…», «Abrir con» (ACTION_VIEW), «Compartir» (ACTION_SEND) o el hexadecimal pegado.</summary>
public enum OrigenArchivo { Selector, AbrirCon, Compartir, Hexadecimal }

/// <summary>Un archivo leído y reconocido. <see cref="Documento"/> es null cuando no viene de un documento (hexadecimal pegado); <see cref="Datos"/> es null si no se llegó a leer (pasaba del tope).</summary>
public sealed record Apertura(DocumentoRef? Documento, byte[]? Datos, ArchivoReconocido Contenido, OrigenArchivo Origen)
{
    /// <summary>Nombre para enseñar: el del documento o, para el hexadecimal pegado (PKHeX lo abre sin nombre), «texto pegado».</summary>
    public string Nombre => string.IsNullOrEmpty(Contenido.Nombre) ? SinNombre : Contenido.Nombre;
    public static string SinNombre => IdiomaInterfaz.T("AbridorArchivos.SinNombre");
}

/// <summary>
/// El «OpenQuick/OpenFromPath» de Pokekeep (PK-IO-004): mira el tamaño ANTES de leer (el que da el proveedor; si no lo da, lee con tope), lee los bytes, los reconoce con <see cref="ReconocedorArchivos"/> y, si
/// es una partida, la abre como copia de trabajo con <see cref="GestorSesiones.AbrirReconocidaAsync"/> (y la anota en recientes). Sin pantalla: la Shell decide qué enseña con el resultado.
/// </summary>
public sealed class AbridorArchivos(IAlmacenDocumentos almacen, GestorSesiones gestor, RecientesStore? recientes = null)
{
    public async Task<Apertura> LeerAsync(DocumentoRef documento, OrigenArchivo origen, SaveFile? referencia = null, IEnumerable<string>? extensionesExtra = null, CancellationToken cancelar = default)
    {
        // F10-ABRIR-007 y PK-IO-027: con el tamaño del proveedor se rechaza ANTES de leer, como OpenFromPath de PKHeX (más de 32 MB, o IsFileTooBig/IsFileTooSmall); sin tamaño (-1), tras leer con tope.
        if (ReconocedorArchivos.MotivoAntesDeLeer(documento.Bytes) is { } motivo)
            return new Apertura(documento, null, new ArchivoNoReconocido(documento.Nombre, motivo, ReconocedorArchivos.FormatosQueLee), origen);
        byte[]? datos;
        try { datos = await almacen.LeerHastaAsync(documento, ReconocedorArchivos.TopeBytes, cancelar).ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            Ajustes.Pkhex.TextosNucleo.Preparar();
            return new Apertura(documento, null, new ArchivoNoReconocido(documento.Nombre, MessageStrings.MsgFileInUse.TrimEnd() + " " + ex.Message, IdiomaInterfaz.T("AbridorArchivos.ArchivoTocado")), origen);
        }
        if (datos is null)
            return new Apertura(documento, null, new ArchivoNoReconocido(documento.Nombre, ReconocedorArchivos.MensajeDemasiadoGrande(-1), ReconocedorArchivos.FormatosQueLee), origen);
        return new Apertura(documento, datos, ReconocedorArchivos.Reconocer(datos, documento.Nombre, referencia, extensionesExtra), origen);
    }

    /// <summary>El hexadecimal pegado (PK-MENU-026, PK-IO-021): mismo reconocimiento, sin documento.</summary>
    public static Apertura DesdeBytes(byte[] datos, string nombre, OrigenArchivo origen, SaveFile? referencia = null, IEnumerable<string>? extensionesExtra = null)
        => new(null, datos, ReconocedorArchivos.Reconocer(datos, nombre, referencia, extensionesExtra), origen);

    /// <summary>
    /// Nombre de archivo de una partida que no viene de un documento (el hexadecimal pegado): «partida» con la extensión que el Core sugiere para su formato (<c>Metadata.GetSuggestedExtension</c>). Es el que
    /// propone «Exportar copia…»; «texto pegado» queda solo para enseñar de dónde vino (PKHeX deja el nombre vacío).
    /// </summary>
    public static string NombreSinDocumento(SaveFile partida) => "partida" + partida.Metadata.GetSuggestedExtension();

    /// <summary>Abre la partida reconocida como sesión: con documento, copia privada y recientes; sin documento (hexadecimal), una sesión que solo se puede exportar.</summary>
    public async Task<Sesion> AbrirPartidaAsync(Apertura apertura, ArchivoPartida partida, CancellationToken cancelar = default)
    {
        if (apertura.Documento is null || apertura.Datos is null) return new Sesion(partida.Partida, NombreSinDocumento(partida.Partida)) { Envoltorio = partida.Envoltorio };
        var s = await gestor.AbrirReconocidaAsync(apertura.Documento, apertura.Datos, partida, cancelar).ConfigureAwait(false);
        recientes?.Anotar(s);
        return s;
    }
}
