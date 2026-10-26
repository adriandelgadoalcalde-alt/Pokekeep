// SPDX-License-Identifier: GPL-3.0-or-later
using Pokekeep.Mobile.Ui.Partida.Formularios;
namespace Pokekeep.Mobile.Ui.Partida;

public static class NombreReloj
{
    // El Core trae «Poké-reloj S.A.» como lugar, no el nombre independiente del aparato.
    // El rótulo español del formulario es la fuente para toda la interfaz.
    public static string Oficial => EtiquetasLang.Requerir("SAV_Misc4", "GB_Poketch");
    public static bool Coincide(string consulta) => ViewModels.SelectorHojaViewModel.Normalizar(Oficial + " poketch reloj").Contains(ViewModels.SelectorHojaViewModel.Normalizar(consulta));
}
