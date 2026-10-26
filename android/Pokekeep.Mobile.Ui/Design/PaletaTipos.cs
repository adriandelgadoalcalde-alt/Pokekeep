// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia.Media;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Colores de los 18 tipos para las etiquetas de tipo (<see cref="EtiquetaTipo"/>). El índice es el de la lista de tipos del Core (<c>GameStrings.Types</c>, el de <c>MoveType</c>
/// normalizado: 0 Normal, 1 Lucha, 2 Volador, 3 Veneno, 4 Tierra, 5 Roca, 6 Bicho, 7 Fantasma, 8 Acero, 9 Fuego, 10 Agua, 11 Planta, 12 Eléctrico, 13 Psíquico, 14 Hielo, 15 Dragón, 16 Siniestro, 17 Hada),
/// igual en español e inglés: el color depende del tipo, no del idioma ni del tema.
/// Estilo (variante 7, decisión del propietario): fondo oscuro de la app (#1E201C) mezclado con el 28 % del color del tipo, borde de 2 dp del color puro, texto blanco.
/// </summary>
public static class PaletaTipos
{
    /// <summary>Origen de la tabla de colores (se enseña en las pruebas y en el informe de la release).</summary>
    public const string Origen = "Pokémon Escarlata, medidos en capturas reales del resumen de un Pokémon de cada tipo en el emulador Eden (21-oct-2026): docs/android/tipos-colores-escarlata.json";

    /// <summary>Color puro de cada tipo, por índice del Core.</summary>
    private static readonly Color[] Puros =
    [
        Color.Parse("#9FA09F"), Color.Parse("#FF8000"), Color.Parse("#81B8EF"), Color.Parse("#9141CC"), Color.Parse("#915121"), Color.Parse("#B1A981"),
        Color.Parse("#91A018"), Color.Parse("#704170"), Color.Parse("#60A0B8"), Color.Parse("#E72829"), Color.Parse("#2980EF"), Color.Parse("#3FA029"),
        Color.Parse("#FBC000"), Color.Parse("#EF4179"), Color.Parse("#3FD8FF"), Color.Parse("#4F61E0"), Color.Parse("#524142"), Color.Parse("#F16DF1"),
    ];

    /// <summary>Gris neutro para un tipo desconocido (nunca se inventa el color de uno que no existe).</summary>
    private static readonly Color Neutro = Color.Parse("#6B6F66");

    /// <summary>Fondo común de la etiqueta (el de las tarjetas oscuras de la app), igual en claro y en oscuro.</summary>
    public static readonly Color Base = Color.Parse("#1E201C");

    /// <summary>Peso del color del tipo en el fondo de la etiqueta.</summary>
    public const double PesoColor = 0.28;

    public const int Cantidad = 18;

    public static bool Conocido(int indice) => indice >= 0 && indice < Puros.Length;

    /// <summary>El color puro del tipo (borde de la etiqueta).</summary>
    public static Color Borde(int indice) => Conocido(indice) ? Puros[indice] : Neutro;

    /// <summary>El fondo de la etiqueta: #1E201C con el 28 % del color del tipo.</summary>
    public static Color Fondo(int indice)
    {
        var c = Borde(indice);
        byte M(byte b, byte t) => (byte)Math.Round(b + (t - b) * PesoColor);
        return Color.FromRgb(M(Base.R, c.R), M(Base.G, c.G), M(Base.B, c.B));
    }
}
