// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Partida;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

public sealed partial class ShellViewModel
{
    private async Task<bool> CambiarRanuraBr(int ranura)
    {
        if (Sesion?.Guardado is not SAV4BR br || ranura == br.CurrentSlot) return false;
        if (BorradoresSinAplicar() is { Length: > 0 } borradores)
        {
            Informar(IdiomaInterfaz.F("Shell.AplicaDescartaPrimeroCambios", borradores), "aviso", "CircleAlert");
            return false;
        }
        if (Sesion.CambiosPendientes > 0)
        {
            AbrirHoja(new ConfirmarHojaViewModel(IdiomaInterfaz.T("Shell.CambiarPerfil"), [IdiomaInterfaz.T("Shell.CambiosTodosPerfilesConservan")],
                IdiomaInterfaz.T("Shell.CambiarPerfil"), () => _ = Cambiar()));
            return false;
        }
        return await Cambiar();

        Task<bool> Cambiar() => AbrirSinDocumentoAsync(() => Task.FromResult<Modelo.Sesion?>(HerramientasGuardado.CambiarRanura(Sesion!, ranura)));
    }
}
