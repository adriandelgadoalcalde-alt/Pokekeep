// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>
/// PK-SAV-212: <c>SAV_Chatter</c>, la grabación de Chatot de la 4.ª y 5.ª generación (1000 bytes de sonido PCM de 4 bits): «Inicializado», el porcentaje de confusión (solo se muestra: sale de la grabación), importar y exportar el
/// <c>.pcm</c>, exportar como <c>.wav</c> y reproducir. El formulario trabaja sobre un clon y <c>B_Save_Click</c> lo copia al guardado; cada acción actúa sobre el clon al momento: marcar «Inicializado» pone el indicador, y «Importar .pcm»
/// copia los 1000 bytes a la grabación Y marca «Inicializado» (con un error si el archivo no mide 1000 bytes).
/// <para>En el móvil el .pcm es un campo de archivo (importar queda pendiente hasta aplicar y deriva «Inicializado» a marcado en el borrador; exportar escribe lo que hay en la partida) y el .wav es un campo solo de exportar. El .pcm y la
/// casilla son un grupo de escritura (en el orden del formulario: primero la importación, luego la casilla) para que «importar y después desmarcar» acabe desmarcado.</para>
/// <para><b>No aplica «Reproducir» (<c>B_PlayRecording</c>)</b>: la app no tiene salida de audio (ni servicio de plataforma para ello) y no se añade una dependencia de sonido solo para este botón; exportar el .wav deja oír la grabación en cualquier
/// reproductor del teléfono. Declarado en el ADR 0065.</para>
/// </summary>
internal static class ChacharaGen4
{
    public const string Form = "SAV_Chatter";
    public static readonly string[] Censo = ["PK-SAV-212"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static IChatter Ch(ContextoP c) => c.Sav is SAV5 s5 ? s5.Chatter : ((SAV4)c.Sav).Chatter;

    /// <summary>Predicado barato de <see cref="Crear"/>: <c>B_OpenChatterEditor.Visible = sav is SAV4 or SAV5</c>.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV4 or SAV5;

    /// <summary>El porcentaje de confusión que dará la grabación (las fórmulas de <c>Chatter4</c> y <c>Chatter5</c> del Core) con un indicador y unos bytes dados; sirve para enseñar el efecto de lo que está en el borrador.</summary>
    internal static int Confusion(bool gen5, bool inicializado, ReadOnlySpan<byte> grabacion)
    {
        if (!inicializado) return gen5 ? 0 : 1;
        if (gen5) return (grabacion[99] ^ grabacion[499] ^ grabacion[699]) is < 100 or >= 150 ? 10 : 0;
        return (sbyte)grabacion[15] switch { < -30 => 11, >= 30 => 31, _ => 1 };
    }

    // Cabecera del .wav de PKHeX (RIFF/WAVE, mono, 2000 Hz, 8 bits, 2000 bytes de datos): la misma que SAV_Chatter.WAVHeader.
    private static readonly byte[] CabeceraWav =
    [
        0x52, 0x49, 0x46, 0x46, 0xF4, 0x07, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45,
        0x66, 0x6D, 0x74, 0x20, 0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0xD0, 0x07, 0x00, 0x00, 0xD0, 0x07, 0x00, 0x00, 0x01, 0x00, 0x08, 0x00,
        0x64, 0x61, 0x74, 0x61, 0xD0, 0x07, 0x00, 0x00,
    ];

    /// <summary>De PCM de 4 bits sin signo a PCM de 8 bits con la cabecera del .wav (<c>ConvertPCMToWAV</c>): cada byte da dos muestras, primero el nibble bajo.</summary>
    internal static byte[] AWav(ReadOnlySpan<byte> pcm)
    {
        var datos = new byte[CabeceraWav.Length + (IChatter.SIZE_PCM * 2)];
        CabeceraWav.CopyTo(datos, 0);
        var i = CabeceraWav.Length;
        foreach (var b in pcm) { datos[i++] = (byte)((b & 0x0F) << 4); datos[i++] = (byte)(b & 0xF0); }
        return datos;
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not (SAV4 or SAV5)) return null;
        const string idIni = "inicializado", idPcm = "pcm", idWav = "wav", g = "chatter";
        var gen5 = sav is SAV5;
        // La casilla lleva el porcentaje de confusion del borrador (MT_Confusion): con el indicador y la grabacion que habra al aplicar.
        var casilla = new CampoP
        {
            Id = idIni, Etiqueta = L("CHK_Initialized"), Tipo = TipoP.Marca, Prioridad = 1, GrupoEscritura = g, AfectaA = _ => [idPcm], Ayuda = IdiomaInterfaz.T("ChacharaGen4.ChatotTieneGrabacionGuardada"),
            Leer = c => Ch(c).Initialized, Escribir = (c, v) => Ch(c).Initialized = v is true,
            Detalle = c => $"{L("L_Confusion")} {Confusion(gen5, c.Marca(idIni), c.Valor(idPcm) as byte[] ?? Ch(c).Recording)}",
        };
        // B_ImportPCM_Click: 1000 bytes a la grabacion y «Inicializado» marcado (y la casilla, en el borrador). B_ExportPCM_Click escribe Chatter.Recording.
        var pcm = new CampoP
        {
            Id = idPcm, Etiqueta = IdiomaInterfaz.T("ChacharaGen4.GrabacionPcm"), Tipo = TipoP.Archivo, Leer = _ => null, Prioridad = 0, GrupoEscritura = g, Extension = "pcm", TextoBoton = L("B_ImportPCM"),
            Ayuda = IdiomaInterfaz.T("ChacharaGen4.SonidoPCMCabeceraBytes"),
            Detalle = _ => IdiomaInterfaz.T("ChacharaGen4.ExportarGuardaGrabacionPartida"),
            Escribir = (c, v) => { if (v is byte[] datos) { datos.CopyTo(Ch(c).Recording); Ch(c).Initialized = true; } },
            ValidarArchivo = (_, datos) => datos.Length == IChatter.SIZE_PCM ? null : IdiomaInterfaz.F("ChacharaGen4.TamanoIncorrectoArchivoTiene", datos.Length, IChatter.SIZE_PCM),
            Exporta = c => ("Recording.pcm", Ch(c).Recording.ToArray()),
            Deriva = c => new Dictionary<string, object?> { [idIni] = c.Valor(idPcm) is byte[] || Ch(c).Initialized },   // al quitar el archivo, la casilla vuelve a lo que hay en la partida
            AfectaA = _ => [idIni],
        };
        // B_ExportWAV_Click: el .wav de la grabacion de la partida. Solo se exporta.
        var wav = new CampoP
        {
            Id = idWav, Etiqueta = IdiomaInterfaz.T("ChacharaGen4.GrabacionWav"), Tipo = TipoP.Archivo, Leer = _ => null, Extension = "wav", SoloExporta = true,
            Ayuda = IdiomaInterfaz.T("ChacharaGen4.SoloExportaWavPuede"),
            Detalle = _ => IdiomaInterfaz.T("ChacharaGen4.GuardaGrabacionPartidaCambios"),
            Exporta = c => ("Recording.wav", AWav(Ch(c).Recording)),
        };
        return new EditorDefP("chatter", TituloGen4.De("chatter"), [new PestanaP("grabacion", TituloGen4.De("chatter"), [new SeccionP(string.Empty, [casilla, pcm, wav])])], Censo, IdiomaInterfaz.T("Partida.Especifico.chatter.Descripcion"));
    }
}
