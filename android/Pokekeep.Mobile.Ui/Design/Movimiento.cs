// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Preferencias del sistema que cambian la interfaz sin que el usuario tenga que tocar nada de Pokekeep: «reducir movimiento» (sin animaciones) y la escala de la fuente del usuario sobre los tokens de tamano de texto.
/// La plataforma las lee del sistema y llama a <see cref="Aplicar"/>; las pruebas headless las fijan a mano para recorrer la matriz de escalas.
/// </summary>
public static class Movimiento
{
    private static readonly (string Clave, double Base)[] Tamanos =
    [("TamTitulo", 24), ("TamTitulo2", 18), ("TamCuerpo", 15), ("TamPequeno", 13), ("TamEtiqueta", 12), ("TamMini", 11), ("TamDato", 14), ("TamCampo", 16)];

    /// <summary>Las etiquetas de la barra de destinos no pasan de x1,3 (como en Material): cinco etiquetas en una fila de 360-430 dp no caben a x2.</summary>
    public const double EscalaMaximaDeLaBarraDeDestinos = 1.3;

    /// <summary>Escala de texto minima y maxima aceptadas (el sistema permite hasta 2 con el control de accesibilidad; mas alla la maqueta no esta pensada).</summary>
    public const double EscalaMinima = 0.85, EscalaMaxima = 2.0;

    public static bool Reducido { get; private set; }
    public static double EscalaTexto { get; private set; } = 1.0;
    /// <summary>Solo pruebas: sin temporizadores que retiren avisos solos.</summary>
    public static bool Pruebas { get; set; }
    public static event Action? Cambiado;

    // Suscripciones DEBILES: un modelo de vista que escucha este evento estatico con «+=» queda retenido para siempre (con su partida, sus Pokemon y sus bitmaps de Skia): en las pruebas cada pantalla
    // montada dejaba hasta decenas de MB y el gate visual llegaba a 17 GB. Quien se suscribe aqui guarda su Action en un campo (la referencia fuerte) y esta lista solo la nombra de forma debil.
    private static readonly List<WeakReference<Action>> Debiles = [];

    /// <summary>Suscribe sin retener al dueno: <paramref name="accion"/> debe vivir en un campo del suscriptor; cuando el suscriptor se libera, la suscripcion desaparece sola.</summary>
    public static void SuscribirDebil(Action accion) { lock (Debiles) Debiles.Add(new WeakReference<Action>(accion)); }

    /// <summary>Clase CSS de la raiz: las animaciones solo se declaran bajo <c>.movimiento</c>, asi que sin ella no hay ninguna.</summary>
    public static string ClaseRaiz => Reducido ? "sin-movimiento" : "movimiento";

    public static void Aplicar(bool reducirMovimiento, double escalaTexto)
    {
        Reducido = reducirMovimiento;
        EscalaTexto = Math.Clamp(double.IsNaN(escalaTexto) || escalaTexto <= 0 ? 1.0 : escalaTexto, EscalaMinima, EscalaMaxima);
        if (Application.Current is { } app)
            {
            foreach (var (clave, valor) in Tamanos) app.Resources[clave] = Math.Round(valor * EscalaTexto, 1);
            app.Resources["TamNav"] = Math.Round(12 * Math.Min(EscalaTexto, EscalaMaximaDeLaBarraDeDestinos), 1);
            // La leyenda del fondo crece con el texto; la miniatura conserva su tamaño.
            app.Resources["AnchoOpcionFondo"] = Math.Clamp(Math.Round(88 * EscalaTexto, 1), 88, 160);
        }
        Cambiado?.Invoke();
        List<Action> vivas = [];
        lock (Debiles) { Debiles.RemoveAll(w => !w.TryGetTarget(out _)); foreach (var w in Debiles) if (w.TryGetTarget(out var a)) vivas.Add(a); }
        foreach (var a in vivas) a();
    }
}
