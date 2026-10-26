// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Uso real 1, punto 8: la barra de desplazamiento vertical caia DENTRO del borde de las tarjetas porque las paginas llevan 16 dp de margen a cada lado y el desplazador acaba antes del borde de la pantalla.
/// Se desplaza hacia el borde de la ventana, como mucho <see cref="Maximo"/> dp y nunca mas de lo que le separa de ella (en una pagina sin margen no se mueve), asi que no se sale de la pantalla ni tapa contenido.
/// Es un estilo comun a TODOS los <c>ScrollViewer</c> de la app: se registra una vez al arrancar.
/// </summary>
public static class BarraAlBorde
{
    /// <summary>Lo maximo (dp) que se acerca la barra al borde de la ventana.</summary>
    public const double Maximo = 12;
    /// <summary>Lo maximo (dp) que puede separar al desplazador del borde de la ventana para considerarlo de pagina (las paginas llevan 16 dp de margen).</summary>
    public const double MargenDePagina = 20;
    private static bool _registrado;

    public static void Registrar()
    {
        if (_registrado) return;
        _registrado = true;
        Control.LoadedEvent.AddClassHandler<ScrollViewer>((sv, _) => Enganchar(sv));
        ScrollViewer.TemplateAppliedEvent.AddClassHandler<ScrollViewer>((sv, _) => Ajustar(sv));
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ScrollViewer, object> Enganchados = new();

    private static void Enganchar(ScrollViewer sv)
    {
        if (!Enganchados.TryGetValue(sv, out _))
        {
            Enganchados.Add(sv, new object());
            sv.SizeChanged += (_, _) => Ajustar(sv);
            sv.LayoutUpdated += (_, _) => Ajustar(sv);   // la posicion puede cambiar sin que cambie su tamano (hermanos que aparecen); es una resta y solo escribe si cambia
        }
        Ajustar(sv);
    }

    /// <summary>La barra vertical del PROPIO desplazador (sin entrar en su contenido, que puede llevar otros).</summary>
    private static ScrollBar? BarraVertical(ScrollViewer sv)
    {
        var pila = new Stack<Visual>(sv.GetVisualChildren());
        while (pila.Count > 0)
        {
            var v = pila.Pop();
            if (v is ScrollBar { Name: "PART_VerticalScrollBar" } b) return b;
            if (v is Avalonia.Controls.Presenters.ScrollContentPresenter) continue;
            foreach (var h in v.GetVisualChildren()) pila.Push(h);
        }
        return null;
    }

    private static bool TocaLaBarra(ScrollViewer sv, double anchoBarra)
    {
        foreach (var t in sv.GetVisualDescendants().OfType<Border>())
        {
            if (!t.IsEffectivelyVisible || t.Bounds.Width <= 40 || !t.Classes.Any(k => k.StartsWith("tarjeta", StringComparison.Ordinal))) continue;
            if (t.TranslatePoint(new Point(t.Bounds.Width, 0), sv) is { } r && r.X > sv.Bounds.Width - anchoBarra) return true;
        }
        return false;
    }

    private static void Ajustar(ScrollViewer sv)
    {
        if (TopLevel.GetTopLevel(sv) is not { } top || BarraVertical(sv) is not { } barra) return;
        if (sv.TranslatePoint(new Point(sv.Bounds.Width, 0), top) is not { } p) return;
        var hueco = Math.Max(0, top.Bounds.Width - p.X);
        // Solo se mueve si hace falta: cuando el borde de alguna tarjeta del propio desplazador llega hasta la franja que ocupa la barra.
        // Un desplazador interior sin tarjetas (el texto de un aviso en una columna con un boton al lado) no debe sacar su barra de su propia
        // caja: se saldria 12 dp de ella (lo vio AvisoLayoutTests; es lo que rompia la regla global al retirarse en 58af1f3, que arreglaba
        // ese sintoma pero devolvia el pulgar al borde de las tarjetas de toda la app). Tampoco los que cuelgan de otro desplazador.
        var m = 0.0;
        if (hueco >= 1 && !sv.GetVisualAncestors().OfType<ScrollViewer>().Any() && TocaLaBarra(sv, Math.Max(barra.MinWidth, 16)))
            m = -Math.Min(Maximo, Math.Floor(hueco));
        if (Math.Abs(barra.Margin.Right - m) > 0.01) barra.Margin = new Thickness(0, 0, m, 0);
    }
}
