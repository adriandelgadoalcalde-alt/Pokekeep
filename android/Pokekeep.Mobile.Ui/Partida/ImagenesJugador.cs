// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;
using static PKHeX.Core.SaveBlockAccessor9SV;
using ZA = PKHeX.Core.SaveBlockAccessor9ZA;

namespace Pokekeep.Mobile.Ui.Partida;

/// <summary>Una imagen del jugador guardada en la partida: titulo, tamano y los pixeles ya descomprimidos (4 bytes por pixel, en el orden de <c>Format32bppArgb</c>, es decir B, G, R, A).</summary>
public sealed record ImagenJugador(string Titulo, int Ancho, int Alto, byte[] Pixeles);

/// <summary>
/// Imágenes del jugador de Escarlata/Púrpura y Leyendas Z-A (<c>Tab_Images</c> de <c>SAV_Trainer9</c> y <c>SAV_Trainer9a</c>): las fotos y los iconos que guarda la partida, comprimidos en DXT1 (<c>DXT1.Decompress</c> del Core). Solo se ven y se exportan:
/// el formulario de PKHeX tampoco las edita (un clic las guarda como PNG).
/// </summary>
public static class ImagenesJugador
{
    public static string? NoAplica(SaveFile sav) => sav is SAV9SV or SAV9ZA ? null : IdiomaInterfaz.F("Partida.NoAplica.Imagenes", NombresJuego.Corto(GameVersion.SL), NombresJuego.Corto(GameVersion.VL), NombresJuego.Corto(GameVersion.ZA));

    public static IReadOnlyList<ImagenJugador> Leer(SaveFile sav)
    {
        var l = new List<ImagenJugador>();
        switch (sav)
        {
            case SAV9SV sv:
                Añadir(l, IdiomaInterfaz.T("ImagenesJugador.FotoPerfilActual"), sv.Blocks, KPictureProfileCurrent, KPictureProfileCurrentWidth, KPictureProfileCurrentHeight, ignoraVacia: false);
                Añadir(l, IdiomaInterfaz.T("ImagenesJugador.IconoActual"), sv.Blocks, KPictureIconCurrent, KPictureIconCurrentWidth, KPictureIconCurrentHeight, ignoraVacia: false);
                Añadir(l, IdiomaInterfaz.T("ImagenesJugador.IconoInicial"), sv.Blocks, KPictureIconInitial, KPictureIconInitialWidth, KPictureIconInitialHeight, ignoraVacia: false);
                break;
            case SAV9ZA za:
                Añadir(l, IdiomaInterfaz.T("ImagenesJugador.FotoActual"), za.Blocks, ZA.KPictureCurrentData, ZA.KPictureCurrentWidth, ZA.KPictureCurrentHeight, ignoraVacia: true);
                Añadir(l, IdiomaInterfaz.T("ImagenesJugador.FotoSociedadCombateSBC"), za.Blocks, ZA.KPictureSBCData, ZA.KPictureSBCWidth, ZA.KPictureSBCHeight, ignoraVacia: true);
                Añadir(l, IdiomaInterfaz.T("ImagenesJugador.FotoInicialHotelZ"), za.Blocks, ZA.KPictureInitialData, ZA.KPictureInitialWidth, ZA.KPictureInitialHeight, ignoraVacia: true);
                break;
        }
        return l;
    }

    private static void Añadir(List<ImagenJugador> l, string titulo, SCBlockAccessor bloques, uint datos, uint ancho, uint alto, bool ignoraVacia)
    {
        try
        {
            var w = bloques.GetBlockValue<uint>(ancho); var h = bloques.GetBlockValue<uint>(alto);
            if (w == 0 && h == 0) return;   // sin imagen (la foto aun no se ha hecho)
            if (w == 0 || h == 0 || w > 4096 || h > 4096 || (w & 3) != 0 || (h & 3) != 0) return;   // datos imposibles: no se pinta nada
            var data = bloques.GetBlock(datos).Data;
            if (data.Length < (int)(w * h / 2)) return;
            l.Add(new ImagenJugador(titulo, (int)w, (int)h, DXT1.Decompress(data, (int)w, (int)h)));
        }
        catch (Exception ex) when (ex is KeyNotFoundException or ArgumentOutOfRangeException or IndexOutOfRangeException or InvalidOperationException) { }
    }
}
