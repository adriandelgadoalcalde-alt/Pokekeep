// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia.Controls;

namespace Pokekeep.Mobile.Ui.Views;

public partial class LibreriaView : UserControl
{
    public LibreriaView()
    {
        InitializeComponent();
        Raiz.SizeChanged += (_, _) => AjustarAlEspacio();
    }

    /// <summary>
    /// Con poco alto (móvil apaisado, teclado abierto, texto grande) la cabecera (título, buscador y chips) no puede quitarle el sitio a la lista ni empujar la paginación bajo la barra de destinos:
    /// se limita a lo que sobra y se desplaza. La lista conserva siempre un mínimo y la paginación se ve entera (hallazgo de la matriz de la F6: apaisado a x2,0).
    /// </summary>
    private void AjustarAlEspacio()
    {
        var alto = Raiz.Bounds.Height;
        if (alto <= 0) return;
        const double paginacion = 64, minimoLista = 96;
        Cabecera.MaxHeight = Math.Max(72, alto - paginacion - minimoLista);
    }
}
