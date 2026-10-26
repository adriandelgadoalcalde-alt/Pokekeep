// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida;

/// <summary>Texto en castellano de un regalo misterioso (el <c>GetDescription</c> de PKHeX sale en el idioma por defecto del Core): titulo, contenido y datos de la tarjeta con las listas de la partida.</summary>
public static class DescripcionRegalo
{
    public static string Contenido(DataMysteryGift g, GameStrings t, SaveFile sav)
    {
        if (g.IsEmpty) return IdiomaInterfaz.T("DescripcionRegalo.RanuraVacia");
        if (g.IsItem)
        {
            var objetos = t.GetItemStrings(g.Context, sav.Version);
            var n = (uint)g.ItemID < objetos.Length ? objetos[g.ItemID] : IdiomaInterfaz.F("DescripcionRegalo.ObjetoN", g.ItemID);
            return g.Quantity > 1 ? $"{n} × {g.Quantity}" : n;
        }
        if (g is PGT { IsManaphyEgg: true }) return IdiomaInterfaz.F("DescripcionRegalo.Huevo", Especie(t, (ushort)Species.Manaphy));
        if (g.Species == 0) return g is PGT ? IdiomaInterfaz.T("DescripcionRegalo.RegaloEspecialPokemonNi") : g.Type;
        var esp = Especie(t, g.Species);
        return g.IsEgg ? IdiomaInterfaz.F("DescripcionRegalo.Huevo", esp) : $"{esp} Nv. {g.Level}";
    }

    public static string Titulo(DataMysteryGift g)
    {
        if (g.IsEmpty) return string.Empty;
        if (g is PGT { IsEntity: false, IsItem: false }) return IdiomaInterfaz.T("DescripcionRegalo.TarjetaPGT");
        var tit = g.CardTitle?.Replace('　', ' ').Trim() ?? string.Empty;
        return g.CardID > 0 ? $"N.º {g.CardID:0000}{(tit.Length > 0 ? " · " + tit : string.Empty)}" : tit;
    }

    private static string Especie(GameStrings t, ushort sp) => sp < t.Species.Count ? t.Species[sp] : IdiomaInterfaz.F("DescripcionRegalo.Especie", sp);

    /// <summary>Lineas de detalle de la tarjeta.</summary>
    public static IReadOnlyList<string> Detalle(DataMysteryGift g, GameStrings t, SaveFile sav)
    {
        var l = new List<string> { IdiomaInterfaz.F("DescripcionRegalo.Tipo", g.Type) };
        if (g.IsEmpty) { l.Add(IdiomaInterfaz.T("DescripcionRegalo.RanuraVacia2")); return l; }
        if (g.CardID > 0) l.Add(IdiomaInterfaz.F("DescripcionRegalo.NTarjeta", g.CardID));
        if (!string.IsNullOrWhiteSpace(g.CardTitle)) l.Add(IdiomaInterfaz.F("DescripcionRegalo.Titulo", g.CardTitle.Replace('　', ' ').Trim()));
        l.Add(IdiomaInterfaz.F("DescripcionRegalo.Contenido", Contenido(g, t, sav)));
        if (g.IsEntity && g.Species != 0)
        {
            if (g.Form != 0) l.Add(IdiomaInterfaz.F("DescripcionRegalo.Forma", g.Form));
            if (g.IsShiny) l.Add(IdiomaInterfaz.T("DescripcionRegalo.Brillante"));
            var obj = t.GetItemStrings(g.Context, sav.Version);
            if (g.HeldItem > 0 && g.HeldItem < obj.Length) l.Add(IdiomaInterfaz.F("DescripcionRegalo.ObjetoLleva", obj[g.HeldItem]));
            var mov = new[] { g.Moves.Move1, g.Moves.Move2, g.Moves.Move3, g.Moves.Move4 }.Where(m => m != 0 && m < t.Move.Count).Select(m => t.Move[m]).ToList();
            if (mov.Count > 0) l.Add(IdiomaInterfaz.F("DescripcionRegalo.Movimientos", string.Join(", ", mov)));
            if (!string.IsNullOrWhiteSpace(g.OriginalTrainerName)) l.Add(IdiomaInterfaz.F("DescripcionRegalo.EO", g.OriginalTrainerName));
            if (g.TID16 != 0 || g.SID16 != 0) l.Add($"TID/SID: {g.TID16:00000}/{g.SID16:00000}");
        }
        if (g is not WR7) l.Add(IdiomaInterfaz.F("DescripcionRegalo.Estado", g.GiftUsed ? IdiomaInterfaz.T("DescripcionRegalo.Usada") : IdiomaInterfaz.T("DescripcionRegalo.Usar")));
        return l;
    }
}
