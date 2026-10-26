// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Herramientas.Equipo;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>X37: «Cobertura y plantillas», abierta desde el botón de Pokémon &gt; Equipo. Aparte para no tocar la Shell de los demás bloques.</summary>
public sealed partial class ShellViewModel
{
    private IAlmacenPlantillas? _almacenPlantillas;

    /// <summary>Las plantillas: archivos en la carpeta privada de la app (<c>plantillas</c>). Los anfitriones sin carpeta privada (pruebas) las guardan en memoria mientras dura la ventana.</summary>
    internal IAlmacenPlantillas AlmacenDePlantillas => _almacenPlantillas ??= _servicios?.CarpetaPrivada is { } carpeta ? new AlmacenPlantillasArchivos(Path.Combine(carpeta, "plantillas")) : new AlmacenPlantillasMemoria();

    /// <summary>Cambia el almacén (pruebas y capturas).</summary>
    internal void FijarAlmacenDePlantillas(IAlmacenPlantillas almacen) => _almacenPlantillas = almacen;

    public void AbrirCoberturaYPlantillas()
    {
        Pantalla = new CoberturaYPlantillasViewModel(ContextoDeHerramientas(), CerrarPantalla, AlmacenDePlantillas);
    }
}
