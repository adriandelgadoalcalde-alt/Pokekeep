// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Abrir;

/// <summary>Lo que hay dentro de un archivo que llega a Pokekeep (selector, «Abrir con», «Compartir» o hexadecimal pegado), clasificado como lo hace PKHeX en <c>Main.OpenFile</c> / <c>LoadFile</c>.</summary>
public abstract record ArchivoReconocido(string Nombre);

/// <summary>Un guardado. <see cref="Envoltorio"/> dice si venía dentro de un .zip o de una tarjeta de memoria de GameCube (al guardar se respeta ese contenedor).</summary>
public sealed record ArchivoPartida(string Nombre, SaveFile Partida, EnvoltorioPartida Envoltorio) : ArchivoReconocido(Nombre);

/// <summary>Una tarjeta de memoria de GameCube con VARIAS partidas de Pokémon (Colosseum, XD, Box R/S): hay que elegir cuál se abre (<c>SelectMemoryCardSaveGame</c>).</summary>
public sealed record ArchivoTarjetaGC(string Nombre, byte[] Datos, IReadOnlyList<SaveFileType> Juegos) : ArchivoReconocido(Nombre);

/// <summary>Un Pokémon (<c>.pk*</c>, <c>.ek*</c>, <c>.pb7</c>, <c>.pkh</c>…), tal y como venía, sin convertir: se convierte al formato de la partida abierta al usarlo.</summary>
public sealed record ArchivoPokemon(string Nombre, PKM Pokemon) : ArchivoReconocido(Nombre);

/// <summary>Una tarjeta de regalo misterioso (<c>.wc*</c>, <c>.pgf</c>, <c>.pcd</c>, <c>.pgt</c>, <c>.wa8</c>…).</summary>
public sealed record ArchivoRegalo(string Nombre, MysteryGift Regalo) : ArchivoReconocido(Nombre);

/// <summary>Un encuentro que se convierte en Pokémon (<c>IEncounterConvertible</c>: el GP1 del Complejo GO de Let's Go).</summary>
public sealed record ArchivoEncuentro(string Nombre, IEncounterConvertible Encuentro) : ArchivoReconocido(Nombre);

/// <summary>Un volcado de caja o de todo el PC (<c>ConcatenatedEntitySet</c>) del formato de la partida abierta.</summary>
public sealed record ArchivoCajas(string Nombre, byte[] Datos) : ArchivoReconocido(Nombre);

/// <summary>Un grupo de Pokémon (vídeo de batalla, equipo de alquiler de Gen. 8-9).</summary>
public sealed record ArchivoGrupo(string Nombre, IPokeGroup Grupo) : ArchivoReconocido(Nombre);

/// <summary>Nada que Pokekeep sepa abrir, o un archivo que no se debe leer. <see cref="Mensaje"/> es el texto de PKHeX (castellano); <see cref="Detalle"/>, el tamaño y qué formatos lee Pokekeep.</summary>
public sealed record ArchivoNoReconocido(string Nombre, string Mensaje, string Detalle) : ArchivoReconocido(Nombre);

/// <summary>
/// El reconocimiento GLOBAL de archivos de PKHeX (filas PK-IO-004/005/007/010/012/013/014/015/022/023/027/028, F10-ABRIR-006/007), sin pantalla y sin plataforma: límites de tamaño
/// (<c>FileUtil.IsFileTooBig</c>/<c>IsFileTooSmall</c> y el tope de 32 MB de Pokekeep), <c>FileUtil.GetSupportedFile</c> en el orden del Core (guardado, incluido .zip; tarjeta de GameCube; Pokémon;
/// volcado de cajas; vídeo de batalla; regalo; GP1; equipos de alquiler; PKH de HOME), el estado de la tarjeta de memoria (<c>CheckGCMemoryCard</c>), <c>IsVersionValid</c> y la pista de PKHeX cuando no
/// se reconoce (todo ceros, todo 0xFF, formato no soportado). Nunca lanza: lo que no entiende es un <see cref="ArchivoNoReconocido"/>.
/// </summary>
public static class ReconocedorArchivos
{
    /// <summary>Tope de Pokekeep (F10-ABRIR-007): por encima de 32 MB no se lee el archivo (el mayor formato que lee PKHeX, la tarjeta de memoria de 16 MB, cabe de sobra).</summary>
    public const long TopeBytes = 32L * 1024 * 1024;

    /// <summary>Lo que se dice cuando un archivo pasa del tope, ANTES de leerlo (el tamaño lo da el proveedor de documentos).</summary>
    public static string MensajeDemasiadoGrande(long bytes)
    {
        TextosNucleo.Preparar();
        var tamano = bytes > 0 ? $" ({bytes:N0} bytes)" : string.Empty;
        return IdiomaInterfaz.F("ReconocedorArchivos.PokekeepLeeArchivosMB", MessageStrings.MsgFileSizeLarge, tamano);
    }

    /// <summary>
    /// El rechazo por tamaño ANTES de leer, con el tamaño que da el proveedor (como <c>OpenFromPath</c> de PKHeX, que mira el tamaño antes de <c>ReadAllBytes</c>): el tope de 32 MB y luego
    /// <c>IsFileTooBig</c>/<c>IsFileTooSmall</c>. Un tamaño desconocido (-1) o 0 (hay proveedores que no lo dan) se comprueba después, sobre lo leído.
    /// </summary>
    public static string? MotivoAntesDeLeer(long bytes) => bytes <= 0 ? null : MotivoPorTamano(bytes);

    /// <summary>¿Se puede leer un archivo de este tamaño? Null = sí; si no, el motivo (como <c>OpenFromPath</c> de PKHeX, más el tope de 32 MB). Un tamaño desconocido (&lt; 0) se comprueba tras leer.</summary>
    public static string? MotivoPorTamano(long bytes)
    {
        if (bytes < 0) return null;
        TextosNucleo.Preparar();
        if (bytes > TopeBytes) return MensajeDemasiadoGrande(bytes);
        if (FileUtil.IsFileTooBig(bytes)) return MessageStrings.MsgFileSizeLarge + " " + string.Format(MessageStrings.MsgFileSize, bytes.ToString("N0"));
        if (FileUtil.IsFileTooSmall(bytes)) return MessageStrings.MsgFileSizeSmall + " " + string.Format(MessageStrings.MsgFileSize, bytes.ToString("N0"));
        return null;
    }

    /// <summary>
    /// Clasifica <paramref name="datos"/> (el archivo entero) como PKHeX. <paramref name="referencia"/> es la partida abierta (la usa el Core para reconocer volcados de caja de su formato) y
    /// <paramref name="extensionesExtra"/> las «Otras extensiones de partidas» del ajuste <c>Backup.OtherSaveFileExtensions</c> (solo cambian la pista cuando no se reconoce, como en PKHeX).
    /// Al Core se le da SIEMPRE una copia: descifra o normaliza algunos formatos en el propio búfer.
    /// </summary>
    public static ArchivoReconocido Reconocer(byte[] datos, string nombre, SaveFile? referencia = null, IEnumerable<string>? extensionesExtra = null)
    {
        TextosNucleo.Preparar();
        if (MotivoPorTamano(datos.Length) is { } motivo) return new ArchivoNoReconocido(nombre, motivo, Detalle(datos.Length));
        object? contenido;
        try { contenido = FileUtil.GetSupportedFile(datos.ToArray(), Path.GetExtension(nombre.AsSpan()), referencia); }
        catch (Exception ex) { return new ArchivoNoReconocido(nombre, MessageStrings.MsgFileLoadFail + " " + ex.GetType().Name, Detalle(datos.Length)); }
        try
        {
            switch (contenido)
            {
                case SaveFile sav:
                    if (!sav.IsVersionValid()) return new ArchivoNoReconocido(nombre, MessageStrings.MsgFileLoadSaveLoadFail, Detalle(datos.Length));
                    return new ArchivoPartida(nombre, EnvoltorioPartida.ConNombre(sav, nombre), EsZip(datos) ? EnvoltorioPartida.Zip : EnvoltorioPartida.Directo);
                case SAV3GCMemoryCard tarjeta:
                    return DeTarjeta(datos, nombre, tarjeta, extensionesExtra);
                case PKM pk: return new ArchivoPokemon(nombre, pk);
                case MysteryGift mg: return new ArchivoRegalo(nombre, mg);
                case ConcatenatedEntitySet cj: return new ArchivoCajas(nombre, cj.Data.ToArray());
                case IPokeGroup g: return new ArchivoGrupo(nombre, g);
                case IEncounterConvertible enc: return new ArchivoEncuentro(nombre, enc);
            }
        }
        catch (Exception ex) { return new ArchivoNoReconocido(nombre, MessageStrings.MsgFileLoadFail + " " + ex.GetType().Name, Detalle(datos.Length)); }
        return new ArchivoNoReconocido(nombre, Pista(datos, nombre, extensionesExtra), Detalle(datos.Length));
    }

    /// <summary>
    /// <c>CheckGCMemoryCard</c> de PKHeX: sin partidas de Pokémon o con duplicadas, error; con UNA, se abre; con varias, hay que elegir (<see cref="ArchivoTarjetaGC"/>).
    /// </summary>
    private static ArchivoReconocido DeTarjeta(byte[] datos, string nombre, SAV3GCMemoryCard tarjeta, IEnumerable<string>? extensionesExtra)
    {
        switch (tarjeta.GetMemoryCardState())
        {
            case MemoryCardSaveStatus.NoPkmSaveGame: return new ArchivoNoReconocido(nombre, MessageStrings.MsgFileGameCubeNoGames, Detalle(datos.Length));
            case MemoryCardSaveStatus.DuplicateCOLO or MemoryCardSaveStatus.DuplicateXD or MemoryCardSaveStatus.DuplicateRSBOX:
                return new ArchivoNoReconocido(nombre, MessageStrings.MsgFileGameCubeDuplicate, Detalle(datos.Length));
            case MemoryCardSaveStatus.MultipleSaveGame:
                List<SaveFileType> juegos = [];
                if (tarjeta.HasCOLO) juegos.Add(SaveFileType.Colosseum);
                if (tarjeta.HasXD) juegos.Add(SaveFileType.XD);
                if (tarjeta.HasRSBOX) juegos.Add(SaveFileType.RSBox);
                return new ArchivoTarjetaGC(nombre, datos, juegos);
            case MemoryCardSaveStatus.SaveGameCOLO: return AbrirDeTarjeta(datos, nombre, SaveFileType.Colosseum);
            case MemoryCardSaveStatus.SaveGameXD: return AbrirDeTarjeta(datos, nombre, SaveFileType.XD);
            case MemoryCardSaveStatus.SaveGameRSBOX: return AbrirDeTarjeta(datos, nombre, SaveFileType.RSBox);
            default:
                return new ArchivoNoReconocido(nombre, !SAV3GCMemoryCard.IsMemoryCardSize(datos.Length) ? MessageStrings.MsgFileGameCubeBad : Pista(datos, nombre, extensionesExtra), Detalle(datos.Length));
        }
    }

    /// <summary>Abre la partida <paramref name="juego"/> de una tarjeta de memoria (la elegida por el usuario si había varias). Null si la tarjeta no la tiene.</summary>
    public static ArchivoReconocido AbrirDeTarjeta(byte[] datos, string nombre, SaveFileType juego)
    {
        TextosNucleo.Preparar();
        var envoltorio = EnvoltorioPartida.TarjetaGC(juego);
        var sav = envoltorio.Desempaquetar(datos, nombre);
        if (sav is null) return new ArchivoNoReconocido(nombre, MessageStrings.MsgFileGameCubeBad, Detalle(datos.Length));
        if (!sav.IsVersionValid()) return new ArchivoNoReconocido(nombre, MessageStrings.MsgFileLoadSaveLoadFail, Detalle(datos.Length));
        return new ArchivoPartida(nombre, sav, envoltorio);
    }

    /// <summary>El nombre de PKHeX de cada juego de una tarjeta (<c>MsgGameColosseum</c>, <c>MsgGameXD</c>, <c>MsgGameRSBOX</c>).</summary>
    public static string NombreJuegoTarjeta(SaveFileType juego)
    {
        TextosNucleo.Preparar();
        return juego switch { SaveFileType.Colosseum => MessageStrings.MsgGameColosseum, SaveFileType.XD => MessageStrings.MsgGameXD, SaveFileType.RSBox => MessageStrings.MsgGameRSBOX, _ => juego.ToString() };
    }

    /// <summary><c>GetHintInvalidFile</c> de PKHeX: si la extensión es de guardado, avisa de un archivo todo ceros o todo 0xFF; si no, el mensaje de formato no soportado.</summary>
    public static string Pista(ReadOnlySpan<byte> datos, string nombre, IEnumerable<string>? extensionesExtra = null)
    {
        TextosNucleo.Preparar();
        if (!EsExtensionDePartida(nombre, extensionesExtra)) return MessageStrings.MsgPKMUnsupported;
        if (!datos.ContainsAnyExcept((byte)0x00)) return MessageStrings.MsgFileLoadAllZero;
        if (!datos.ContainsAnyExcept((byte)0xFF)) return MessageStrings.MsgFileLoadAllFFFF;
        return MessageStrings.MsgFileUnsupported;
    }

    /// <summary>
    /// <c>WinFormsUtil.IsFileExtensionSAV</c>: la extensión acaba en una de las de guardado del Core (<c>SaveFileMetadata.CustomSaveExtensions</c>: sav, dat, gci, dsv, srm, fla, SaveRAM) o de las que el usuario
    /// añadió en «Otras extensiones de partidas» (PK-IO-003). No muta la lista del Core: se une con la del ajuste en cada llamada (el ajuste se lee EN EL MOMENTO).
    /// </summary>
    public static bool EsExtensionDePartida(string nombre, IEnumerable<string>? extensionesExtra = null)
    {
        var ext = Path.GetExtension(nombre.AsSpan());
        foreach (var otra in ExtensionesDePartida(extensionesExtra))
            if (otra.Length > 0 && ext.EndsWith(otra)) return true;
        return false;
    }

    public static IReadOnlyList<string> ExtensionesDePartida(IEnumerable<string>? extensionesExtra = null)
        => [.. SaveFileMetadata.CustomSaveExtensions.Concat(extensionesExtra ?? []).Where(e => !string.IsNullOrWhiteSpace(e)).Distinct()];

    /// <summary>Extensiones de Pokémon que lee Pokekeep (<c>EntityFileExtension.GetExtensions</c>: pk1-pk9, sk2, ck3, xk3, bk4, rk4, pb7, pb8, pa8, pa9), más las cifradas ek* y el PKH de HOME.</summary>
    public static IReadOnlyList<string> ExtensionesDePokemon() => [.. EntityFileExtension.GetExtensions(), .. Enumerable.Range(3, 7).Select(i => $"ek{i}"), "pkh"];

    /// <summary>Contexto que el Core supone por la extensión (<c>EntityFileExtension.GetContextFromExtension</c>), p. ej. «.pb8» = Diamante Brillante/Perla Reluciente.</summary>
    public static EntityContext ContextoPorExtension(string nombre) => EntityFileExtension.GetContextFromExtension(Path.GetExtension(nombre.AsSpan()));

    /// <summary>Texto fijo de qué lee Pokekeep (F10-ABRIR-006): nunca una pantalla en blanco.</summary>
    public static string FormatosQueLee => IdiomaInterfaz.T("ReconocedorArchivos.FormatosQueLee");

    private static string Detalle(long bytes)
    {
        TextosNucleo.Preparar();
        return string.Format(MessageStrings.MsgFileSize, bytes.ToString("N0")) + $" (0x{bytes:X4}). " + FormatosQueLee;
    }

    private static bool EsZip(ReadOnlySpan<byte> datos) => datos is [0x50, 0x4B, 0x03, 0x04, ..];
}
