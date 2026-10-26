// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida;

/// <summary>Resultado de leer un archivo de tarjeta: el regalo, o el motivo (en castellano) por el que no se puede usar. Nunca escribe nada.</summary>
public sealed record TarjetaLeida(DataMysteryGift? Regalo, string? Motivo)
{
    public bool Valida => Regalo is not null;
}

/// <summary>Lee un archivo de tarjeta de regalo (<c>.pgt</c>, <c>.pcd</c>/<c>.wc4</c>, <c>.pgf</c>, <c>.wc6</c>, <c>.wc7</c>, <c>.wr7</c>, <c>.wb7</c> y sus versiones «full») con el formato de cada generación y la comprueba contra la partida.</summary>
public static class ImportadorRegalos
{
    public static readonly string[] Extensiones = [".pgt", ".pcd", ".wc4", ".pgf", ".wc6", ".wc7", ".wr7", ".wb7", ".wb7full", ".wc5full", ".wc6full", ".wc7full"];

    public static TarjetaLeida Leer(byte[] datos, string nombre, SaveFile sav)
    {
        var ext = Path.GetExtension(nombre ?? string.Empty).ToLowerInvariant();
        if (datos.Length == 0) return new(null, IdiomaInterfaz.T("ImportadorRegalos.ElArchivoEstaVacio"));
        if (ext.Length == 0) return new(null, IdiomaInterfaz.T("ImportadorRegalos.ElArchivoNoTiene"));
        DataMysteryGift? g;
        try { g = MysteryGift.GetMysteryGift(datos.ToArray(), ext); }
        catch (Exception) { return new(null, IdiomaInterfaz.T("ImportadorRegalos.ElArchivoEstaDanado")); }
        if (g is null)
        {
            var conocido = Array.IndexOf(Extensiones, ext) >= 0 || ext is ".wc8" or ".wc8full" or ".wb8" or ".wa8" or ".wc9" or ".wa9";
            return new(null, conocido
                ? IdiomaInterfaz.F("ImportadorRegalos.ElArchivoNoTiene2", ext, datos.Length)
                : IdiomaInterfaz.F("ImportadorRegalos.LaExtensionNoEs", ext));
        }
        if (g.Generation != sav.Generation && g.Generation > 0 && !g.IsCardCompatible(sav, out _))
            return new(null, IdiomaInterfaz.F("ImportadorRegalos.EsUnaTarjetaDe", g.Generation, g.Type, sav.Generation));
        if (!g.IsCardCompatible(sav, out var msg))
            return new(null, string.IsNullOrWhiteSpace(msg) ? IdiomaInterfaz.T("ImportadorRegalos.EstaTarjetaNoEs") : IdiomaInterfaz.F("ImportadorRegalos.EstaTarjetaNoEs2", msg));
        if (g.IsEntity && g.Species > sav.MaxSpeciesID)
            return new(null, IdiomaInterfaz.T("ImportadorRegalos.LaTarjetaEstaDanada"));
        if (g.IsItem && g.ItemID > sav.MaxItemID)
            return new(null, IdiomaInterfaz.T("ImportadorRegalos.LaTarjetaEstaDanada2"));
        return new(g, null);
    }
}
