// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Herramientas.EstadisticasBase;

/// <summary>
/// Una fila de KChart: una especie en una forma, con lo que dice la tabla de datos de especies del juego de la partida (<c>SaveFile.Personal</c>). El número es el de PKHeX: tres cifras (cuatro desde la 9.ª
/// generación) y, si no es la forma base, <c>-FF</c>. <see cref="Nativo"/> = sale en la Pokédex propia del juego (<c>GetIsNative</c>). <see cref="Alfa"/> solo existe en Leyendas Pokémon: Z-A (movimiento alfa).
/// </summary>
public sealed record FilaEstadistica(ushort Especie, byte Forma, string Numero, string Nombre, bool Nativo, int Bst, int Captura, byte Tipo1, byte Tipo2, string NombreTipo1, string NombreTipo2,
    int Ps, int Ataque, int Defensa, int AtaqueEspecial, int DefensaEspecial, int Velocidad, string Habilidad1, string Habilidad2, string HabilidadOculta, string? Alfa)
{
    /// <summary>Orden natural de la tabla (especie y luego forma), el de <c>DGV.Sort(DGV.Columns[0])</c>: el número lleva ceros delante, así que el texto y esto ordenan igual.</summary>
    public int Clave => Especie * 1000 + Forma;
    public bool UnSoloTipo => Tipo1 == Tipo2;
}

/// <summary>
/// KChart (<c>PKHeX.WinForms.KChart</c>, PK-SUB-005, PK-MENU-019) sin pantalla: recorre las especies de la tabla de la partida (<c>SAV.Personal</c>, de la 1 a <c>MaxSpeciesID</c>) y todas sus formas que están en el juego
/// (<c>IsPresentInGame</c>) y saca, de <c>IPersonalInfo</c>, las estadísticas base, el total (BST), el ratio de captura, los tipos y las habilidades. Es el MISMO recorrido y las mismas lecturas que el formulario;
/// los nombres salen de las listas del Core en castellano que ya usa la partida.
/// </summary>
public static class ModeloEstadisticasBase
{
    /// <summary><c>SpeciesNumberFormat</c>: «0000» desde la 9.ª generación, «000» antes.</summary>
    public static string FormatoNumero(SaveFile sav) => sav.MaxSpeciesID >= 1000 ? "0000" : "000";   // KChart de PKHeX 26.10.18 (antes: sav.Generation >= 9)

    /// <summary><c>KChart</c> (constructor + <c>PopEntry</c>).</summary>
    public static List<FilaEstadistica> Filas(SaveFile sav, GameStrings textos, IReadOnlyList<string> simbolosDeGenero)
    {
        var pt = sav.Personal;
        var especies = textos.specieslist;
        var habilidades = textos.abilitylist;
        var movimientos = textos.movelist;
        var formato = FormatoNumero(sav);
        var lista = new List<FilaEstadistica>();
        for (ushort s = 1; s <= pt.MaxSpeciesID; s++)
        {
            var fc = pt[s, 0].FormCount;
            var nombresDeForma = fc <= 1
                ? []
                : FormConverter.GetFormList(s, textos.Types, textos.forms, simbolosDeGenero, sav.Context);

            for (byte f = 0; f < fc; f++)
            {
                var nombre = f == 0 ? especies[s] : $"{especies[s]}-{(f < nombresDeForma.Length ? nombresDeForma[f] : f.ToString(System.Globalization.CultureInfo.InvariantCulture))}";
                if (!pt.IsPresentInGame(s, f))
                    continue;
                var p = pt.GetFormEntry(s, f);
                var bst = p.BST;
                var habs = p.AbilityCount;
                lista.Add(new FilaEstadistica(s, f,
                    s.ToString(formato, System.Globalization.CultureInfo.InvariantCulture) + (f > 0 ? $"-{f:00}" : string.Empty), nombre, EsNativo(p, s), bst, p.CatchRate,
                    p.Type1, p.Type2, textos.Types[p.Type1], textos.Types[p.Type2],
                    p.HP, p.ATK, p.DEF, p.SPA, p.SPD, p.SPE,
                    habilidades[habs > 0 ? p.GetAbilityAtIndex(0) : 0], habilidades[habs > 1 ? p.GetAbilityAtIndex(1) : 0], habilidades[habs > 2 ? p.GetAbilityAtIndex(2) : 0],
                    p is PersonalInfo9ZA za ? movimientos[za.AlphaMove] : null));
            }
        }
        return lista;
    }

    /// <summary><c>GetIsNative</c>.</summary>
    public static bool EsNativo(IPersonalInfo personalInfo, ushort s) => personalInfo switch
    {
        PersonalInfo7 => PersonalInfo7.IsPastGenNative(s),
        PersonalInfo8SWSH ss => ss.IsInDex,
        PersonalInfo8BDSP bs => bs.IsInDex,
        PersonalInfo8LA bs => bs.IsPresentInGame,
        PersonalInfo9SV sv => sv.IsInDex,
        PersonalInfo9ZA za => za is { IsLumioseNative: true },
        _ => true,
    };
}

/// <summary>
/// Los colores de las celdas de KChart (<c>PKHeX.Drawing.ColorUtil.ColorBaseStat</c> y <c>ColorBaseStatTotal</c>): un degradado pastel de rojo a amarillo y a verde según el valor, sin <c>System.Drawing</c>. Los números
/// son los del original (tope 180 por estadística; el total se desplaza 175 y se divide entre 3 para caer en el rango de una estadística; mezcla del 40 % con blanco).
/// </summary>
public static class ColoresEstadisticas
{
    private const byte EstadisticaMaxima = 180;
    private const byte DesplazamientoBst = 175;
    private const float DivisorBst = 3;

    /// <summary><c>ColorBaseStat</c>: RGB de una estadística base.</summary>
    public static (byte R, byte G, byte B) DeEstadistica(int valor)
    {
        var x = (uint)valor >= EstadisticaMaxima ? 1f : (float)valor / EstadisticaMaxima;
        return PastelRojoAmarilloVerde(x);
    }

    /// <summary><c>ColorBaseStatTotal</c>: RGB del total de estadísticas base.</summary>
    public static (byte R, byte G, byte B) DeTotal(int bst)
    {
        var aUna = Math.Max(0, bst - DesplazamientoBst) / DivisorBst;
        return DeEstadistica((int)aUna);
    }

    /// <summary><c>GetPastelRYG</c>.</summary>
    public static (byte R, byte G, byte B) PastelRojoAmarilloVerde(float x)
    {
        var r = x > .5f ? 510 * (1 - x) : 255;
        var g = x > .5f ? 255 : 510 * x;
        const float blanco = 0.4f;
        const byte b = (byte)(0xFF * (1 - blanco));
        return ((byte)((r * blanco) + b), (byte)((g * blanco) + b), b);
    }

    /// <summary>«#RRGGBB» para pintar.</summary>
    public static string Hex((byte R, byte G, byte B) c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
