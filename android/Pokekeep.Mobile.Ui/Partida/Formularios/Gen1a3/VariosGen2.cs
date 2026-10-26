// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen1a3;

/// <summary>
/// PK-SAV-003: <c>SAV_Misc2</c>, el editor de varios de la 2.ª generación. WinForms solo enseña un boton, «Activar evento GS Ball (Virtual Console)», visible unicamente con la version Cristal (<c>Version is GameVersion.C</c>)
/// y habilitado mientras el evento no este activo (<c>!IsEnabledGSBallMobileEvent</c>); al pulsarlo hace <c>EnableGSBallMobileEvent()</c> y «Guardar» copia los cambios. Aqui es una <see cref="TipoP.Accion"/> pendiente
/// que hace la misma llamada al aplicar y se deshace entera.
/// </summary>
internal static class VariosGen2
{
    public const string Form = "SAV_Misc2";
    public static readonly string[] Censo = ["PK-SAV-003"];

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado (solo Cristal, como <c>B_OpenMiscEditor</c>).</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV2 { Version: GameVersion.C };

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV2 { Version: GameVersion.C }) return null;
        var campos = new List<CampoP>
        {
            P.Accion("gsball", EtiquetasLang.Requerir(Form, "B_VirtualConsoleGSBall"), c => ((SAV2)c.Sav).EnableGSBallMobileEvent(),
                motivo: c => ((SAV2)c.Sav).IsEnabledGSBallMobileEvent ? IdiomaInterfaz.T("Form.Varios2.Activo") : null,
                ayuda: IdiomaInterfaz.T("Form.Varios2.Ayuda"),
                textoBoton: IdiomaInterfaz.T("Form.Activar")),
        };
        return new EditorDefP("misc", IdiomaInterfaz.T("Form.Varios"), [new PestanaP("gsball", EtiquetasLang.RequerirFormulario(Form), [new SeccionP(string.Empty, campos)])], Censo,
            IdiomaInterfaz.F("Form.Varios2.Descripcion", NombresJuego.Corto(GameVersion.C)));
    }
}
