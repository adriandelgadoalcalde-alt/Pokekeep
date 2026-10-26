// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.BaseDatos;

/// <summary>Un Pokémon de la partida que se puede elegir como referencia (lo que en PKHeX está cargado en las pestañas del editor).</summary>
public sealed record PokemonDeReferencia(int Id, string Texto, PKM Pokemon);

/// <summary>
/// Lo que se hace con un resultado del Generador de encuentros o de la base de datos de regalos. En PKHeX el resultado se abre en las pestañas del editor (Ctrl+clic) y el usuario lo coloca; en el móvil hay dos
/// acciones explícitas: <b>colocar</b> en la primera casilla libre de las cajas por <c>Sesion.Casillas</c> (una entrada del historial, Deshacer exacto) y <b>guardar como archivo</b> (<c>SavePKMDialog</c>: el
/// <c>.pk*</c> descifrado con la parte de equipo, como <see cref="Intercambio.Bytes"/>), siempre por orden del usuario (regla 13).
/// </summary>
public static class AccionesResultado
{
    /// <summary>La primera casilla de caja vacía y escribible, recorriendo las cajas desde la primera (nunca pisa nada).</summary>
    public static ISlotInfo? PrimeraLibre(SaveFile sav)
    {
        if (!sav.HasBox) return null;
        for (var b = 0; b < sav.BoxCount; b++)
            for (var s = 0; s < sav.BoxSlotCount; s++)
            {
                var info = new SlotInfoBox(b, s, sav);
                if (info.CanWriteTo(sav) && !sav.IsBoxSlotOverwriteProtected(b, s) && info.Read(sav).Species == 0) return info;
            }
        return null;
    }

    /// <summary>Coloca <paramref name="pk"/> en la primera casilla libre (un Deshacer). El informe dice dónde.</summary>
    public static InformeOperacion Colocar(Sesion sesion, PKM pk)
    {
        var sav = sesion.Guardado;
        if (!sav.HasBox) return InformeOperacion.Falla(TipoResultado.NoAdmitido, MessageStrings.MsgSaveBoxFailNone);
        if (PrimeraLibre(sav) is not { } destino) return InformeOperacion.Falla(TipoResultado.NoAdmitido, IdiomaInterfaz.T("Shell.NingunaCasillaLibreCajas"));
        var r = sesion.Casillas.Colocar(destino, pk);
        return r.Correcto ? InformeOperacion.Ok(IdiomaInterfaz.F("AccionesResultado.ColocadoPuedesDeshacerlo", EditorCasillas.Describir(destino))) : r;
    }

    /// <summary>Guarda el Pokémon como archivo donde elija el usuario. Devuelve el texto del resultado (vacío si el usuario cancela).</summary>
    public static async Task<string> GuardarArchivoAsync(IAlmacenDocumentos? documentos, PKM pk, CancellationToken cancelar = default)
    {
        if (documentos is null) return IdiomaInterfaz.T("AccionesResultado.PuedenGuardarArchivosVentana");
        var destino = await documentos.ElegirDestinoAsync(Intercambio.NombreArchivo(pk), cancelar).ConfigureAwait(true);
        if (destino is null) return string.Empty;
        await documentos.EscribirAsync(destino, Intercambio.Bytes(pk), cancelar).ConfigureAwait(true);
        return IdiomaInterfaz.F("AccionesResultado.Guardado", destino.Nombre);
    }

    /// <summary>Los Pokémon de la partida (equipo y cajas) para elegir uno como referencia; el primero es «Ninguno» (un Pokémon en blanco, como el editor de PKHeX sin nada cargado).</summary>
    public static IReadOnlyList<PokemonDeReferencia> Referencias(Sesion sesion)
    {
        var sav = sesion.Guardado;
        var l = new List<PokemonDeReferencia> { new(0, IdiomaInterfaz.T("AccionesResultado.NingunoBlanco"), sav.BlankPKM) };
        string Nombre(PKM p) => (uint)p.Species < sesion.Textos.Species.Count ? sesion.Textos.Species[p.Species] : p.Species.ToString(System.Globalization.CultureInfo.InvariantCulture);
        for (var i = 0; i < sav.PartyCount; i++) { var p = sav.GetPartySlotAtIndex(i); if (p.Species != 0) l.Add(new(l.Count, IdiomaInterfaz.F("AccionesResultado.Equipo", i + 1, Nombre(p)), p)); }
        if (sav.HasBox)
            for (var b = 0; b < sav.BoxCount; b++)
                for (var s = 0; s < sav.BoxSlotCount; s++) { var p = sav.GetBoxSlotAtIndex(b, s); if (p.Species != 0) l.Add(new(l.Count, IdiomaInterfaz.F("AccionesResultado.Casilla", sesion.NombreCaja(b), s + 1, Nombre(p)), p)); }
        return l;
    }

    /// <summary>
    /// «Cualquiera»: PKHeX pone <c>MsgAny</c> en el lugar de «(Ninguno)», pero <c>MessageStrings_es.txt</c> lo traduce «Ninguno», que en un filtro dice lo contrario de lo que hace (no filtrar). Se corrige solo aquí.
    /// </summary>
    public static string Cualquiera => IdiomaInterfaz.T("ModeloLotes.Cualquiera");

    /// <summary>Una lista del Core con «Cualquiera» delante (lo que PKHeX pone en el lugar de «(Ninguno)»).</summary>
    public static IReadOnlyList<OpcionSelector> ConCualquiera(IEnumerable<ComboItem> lista, int valorCualquiera, bool especies = false)
        => [new(valorCualquiera, Cualquiera), .. lista.Where(c => c.Value != valorCualquiera).Select(c => new OpcionSelector(c.Value, c.Text, null, especies ? c.Value : 0))];
}
