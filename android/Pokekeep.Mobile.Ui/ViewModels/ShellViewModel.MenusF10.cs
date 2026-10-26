// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Herramientas;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

public sealed partial class ShellViewModel
{
    [RelayCommand] private void AbrirGuiaGuardados() => Pantalla = new GuiaGuardadosViewModel(CerrarPantalla);
    [RelayCommand] private void AbrirGuiaGestos() => Pantalla = new GuiaGuardadosViewModel(CerrarPantalla, gestos: true);
    [RelayCommand]
    private void AbrirMenuArchivo()
    {
        var filas = MenuPartida.Select(e => new EntradaHoja(e.Titulo, e.Descripcion, e.Glifo,
            e.Comando is null ? null : () => e.Comando.Execute(null), Motivo: e.Comando is null ? IdiomaInterfaz.T("Shell.AbrePrimeroPartidaConfigura") : null)).ToList();
        filas.Insert(1, new EntradaHoja(IdiomaInterfaz.T("Shell.ExportarPokemonCasilla"), IdiomaInterfaz.T("Shell.ArchivoPkSetQR"), "FileDown",
            Cajas is null ? null : Cajas.VerExportarElegida, Motivo: Cajas?.Seleccionada?.Ocupada != true ? IdiomaInterfaz.T("Shell.SeleccionaPrimeroPokemonCajas") : null));
        AbrirHoja(new MenuHojaViewModel(IdiomaInterfaz.T("Shell.Archivo2"), [new SeccionHoja(null, [.. filas])]));
    }

    [RelayCommand]
    private void AbrirMenuDatos()
    {
        var filas = new List<EntradaHoja>
        {
            new(IdiomaInterfaz.T("Cajas.CargarCajas"), IdiomaInterfaz.T("Shell.ImportarPokemonCarpeta"), "Upload", Cajas is null ? null : Cajas.CargarCajasDeCarpeta,
                Motivo: Cajas is null ? IdiomaInterfaz.T("Shell.AbrePrimeroPartida") : !Cajas.TieneCajas ? IdiomaInterfaz.T("Shell.GuardadoTieneCajas") : Carpetas is not Almacen.IAlmacenCarpetasCajas || Documentos is null ? IdiomaInterfaz.T("Shell.AlmacenamientoCarpetasDisponible") : null),
            new(IdiomaInterfaz.T("Cajas.ExportarCaja2"), IdiomaInterfaz.T("Shell.PokemonCajaActualArchivos"), "Download", Cajas is null ? null : () => Cajas.AbrirExportarCajas(false),
                Motivo: Cajas is null ? IdiomaInterfaz.T("Shell.AbrePrimeroPartida") : !Cajas.TieneCajas ? IdiomaInterfaz.T("Shell.GuardadoTieneCajas") : ((IAnfitrionCajas)this).MotivoExportadorCajas),
            new(IdiomaInterfaz.T("Cajas.ExportarCajas"), IdiomaInterfaz.T("Shell.PokemonTodasCajasArchivos"), "Download", Cajas is null ? null : () => Cajas.AbrirExportarCajas(true),
                Motivo: Cajas is null ? IdiomaInterfaz.T("Shell.AbrePrimeroPartida") : !Cajas.TieneCajas ? IdiomaInterfaz.T("Shell.GuardadoTieneCajas") : ((IAnfitrionCajas)this).MotivoExportadorCajas),
        };
        foreach (var id in new[] { "informe-cajas", "base-datos-pkm", "regalos-db", "encuentros", "editor-lotes", "showdown" })
        {
            var d = CatalogoHerramientas.Buscar(id);
            if (d is null) continue;
            filas.Add(new(d.Titulo, d.Descripcion, d.Glifo, () => NavegarF10("herramienta=" + id, out _), Motivo: d.Motivo(Sesion)));
        }
        AbrirHoja(new MenuHojaViewModel(IdiomaInterfaz.T("Shell.Datos"), [new SeccionHoja(null, filas)]));
    }
}
