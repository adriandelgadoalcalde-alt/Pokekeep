// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static class PresentacionPkhex
{
    public static Avalonia.Media.IBrush Pincel(string color, string defecto)
        => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.TryParse(color, out var valor) ? valor : Avalonia.Media.Color.Parse(defecto));
    public static IReadOnlyList<string> SimbolosGenero => ProgramaPkhex.Ajustes.Actual.Display.Unicode ? GameInfo.GenderSymbolUnicode : GameInfo.GenderSymbolASCII;
    public static string Huella(PantallaPkhex d) => $"{d.Unicode}:{d.FlagIllegal}:{d.SlotLegalityAlwaysVisible}";
    public static string HuellaVisual(AjustesPkhex ajustes)
        => System.Text.Json.JsonSerializer.Serialize(ajustes.Sprite, AjustesPkhexJson.Default.SpritePkhex)
         + System.Text.Json.JsonSerializer.Serialize(ajustes.Hover, AjustesPkhexJson.Default.DetallePkhex)
         + System.Text.Json.JsonSerializer.Serialize(ajustes.Draw, AjustesPkhexJson.Default.DibujoPkhex);
}
