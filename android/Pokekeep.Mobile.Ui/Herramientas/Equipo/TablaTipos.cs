// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Herramientas.Equipo;

/// <summary>
/// La tabla de eficacias de tipos por generación (X37). PKHeX.Core no trae ninguna (solo conoce los tipos de cada especie y de cada movimiento), así que la tabla viene de Pokémon Showdown
/// (<c>DatosCobertura.g.cs</c>, generado por <c>android/herramientas/generar-tabla-tipos.cjs</c>) y se comprueba contra casos conocidos de cada generación en <c>TablaTiposTests</c>.
/// Los tipos se numeran como <c>MoveType</c> del Core (Normal 0, Lucha 1, Volador 2... Hada 17), también en las generaciones 1 y 2 (donde el Core numera distinto, <c>GetMoveTypeGeneration</c> lo normaliza).
/// Tres tablas: generación 1 (sin Siniestro, Acero ni Hada), generaciones 2 a 5 (sin Hada) y 6 en adelante.
/// </summary>
public static class TablaTipos
{
    /// <summary>Cantidad de tipos que numera el Core (Normal a Hada).</summary>
    public const int Cantidad = 18;

    private static (int Desde, int Hasta, bool[] Existe, byte[,] Eficacia) De(int generacion)
    {
        var g = Math.Clamp(generacion, 1, 9);
        foreach (var t in DatosCobertura.Tablas) if (g >= t.Desde && g <= t.Hasta) return t;
        return DatosCobertura.Tablas[^1];
    }

    /// <summary>¿Existe ese tipo en esa generación? (Siniestro y Acero desde la 2; Hada desde la 6.)</summary>
    public static bool Existe(int generacion, int tipo) => (uint)tipo < Cantidad && De(generacion).Existe[tipo];

    /// <summary>Los tipos que existen en esa generación, en el orden del Core.</summary>
    public static IReadOnlyList<int> Tipos(int generacion) => [.. Enumerable.Range(0, Cantidad).Where(t => Existe(generacion, t))];

    /// <summary>Multiplicador de un ataque de tipo <paramref name="atacante"/> contra UN tipo defensor: 0, 0,5, 1 o 2. Un tipo que no existe en la generación se trata como neutro.</summary>
    public static double Contra(int generacion, int atacante, int defensor)
    {
        if ((uint)atacante >= Cantidad || (uint)defensor >= Cantidad) return 1;
        return De(generacion).Eficacia[atacante, defensor] / 2.0;
    }

    /// <summary>Multiplicador contra un Pokémon de uno o dos tipos (<paramref name="tipo2"/> igual al primero o negativo = un solo tipo): 0, ¼, ½, 1, 2 o 4.</summary>
    public static double Contra(int generacion, int atacante, int tipo1, int tipo2)
    {
        var m = Contra(generacion, atacante, tipo1);
        return tipo2 < 0 || tipo2 == tipo1 ? m : m * Contra(generacion, atacante, tipo2);
    }

    public static bool EsSupereficaz(int generacion, int atacante, int defensor) => Contra(generacion, atacante, defensor) > 1;
}
