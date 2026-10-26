// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Modelo;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen1a3;

/// <summary>
/// PK-SAV-017: <c>SAV_RTC3</c>, el reloj interno (RTC) de Rubí, Zafiro y Esmeralda: tiempo inicial y tiempo transcurrido (días, horas, minutos y segundos). WinForms trabaja sobre un clon, rellena ocho cuadros numéricos
/// (horas hasta 23, minutos y segundos hasta 59, días hasta 65535) y al guardar escribe los dos relojes enteros. «Reiniciar RTR» pone los ocho a cero y «Arreglo baya» sube los días transcurridos a 734 como mínimo
/// (2 años de 366 días y 2 días) para que las bayas vuelvan a crecer. Aquí los campos se escriben solo si cambian y los dos botones son acciones pendientes que se ejecutan ANTES que los campos (si se marca una acción y se
/// edita además un campo, el campo manda; en WinForms manda lo último que se toca).
/// </summary>
internal static class RelojGen3
{
    public const string Form = "SAV_RTC3";
    public static readonly string[] Censo = ["PK-SAV-017"];

    /// <summary>Días transcurridos mínimos de «Arreglo baya»: <c>(2 * 366) + 2</c> en el formulario.</summary>
    public const int DiasArregloBaya = (2 * 366) + 2;

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static ISaveBlock3SmallHoenn Pequeno(SaveFile sav) => (ISaveBlock3SmallHoenn)((SAV3)sav).SmallBlock;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado (solo Rubí, Zafiro y Esmeralda).</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV3 { SmallBlock: ISaveBlock3SmallHoenn };

    private static IEnumerable<CampoP> Reloj(string prefijo, bool inicial, string[] controles)
    {
        RTC3 Leer(ContextoP c) => inicial ? Pequeno(c.Sav).ClockInitial : Pequeno(c.Sav).ClockElapsed;
        void Escribir(ContextoP c, Action<RTC3> cambia) { var r = Leer(c); cambia(r); if (inicial) Pequeno(c.Sav).ClockInitial = r; else Pequeno(c.Sav).ClockElapsed = r; }
        yield return P.Entero($"{prefijo}_dias", L(controles[0]), c => Leer(c).Day, (c, v) => Escribir(c, r => r.Day = (ushort)v), 0, 65535);
        yield return P.Entero($"{prefijo}_horas", L(controles[1]), c => Leer(c).Hour, (c, v) => Escribir(c, r => r.Hour = (byte)v), 0, 23);
        yield return P.Entero($"{prefijo}_minutos", L(controles[2]), c => Leer(c).Minute, (c, v) => Escribir(c, r => r.Minute = (byte)v), 0, 59);
        yield return P.Entero($"{prefijo}_segundos", L(controles[3]), c => Leer(c).Second, (c, v) => Escribir(c, r => r.Second = (byte)v), 0, 59);
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (!Aplica(sav)) return null;
        var acciones = new List<CampoP>
        {
            P.Accion("reiniciar", L("B_Reset"), c =>
            {
                var s = Pequeno(c.Sav);
                var a = s.ClockInitial; var b = s.ClockElapsed;
                a.Day = a.Hour = a.Minute = a.Second = 0; b.Day = b.Hour = b.Minute = b.Second = 0;
                s.ClockInitial = a; s.ClockElapsed = b;
            }, prioridad: -10, textoBoton: IdiomaInterfaz.T("Form.Rtc.Reiniciar"), confirmacion: IdiomaInterfaz.T("Form.Rtc.ConfirmarReinicio"), ayuda: IdiomaInterfaz.T("Form.Rtc.AyudaReinicio")),
            P.Accion("arreglo_baya", L("B_BerryFix"), c =>
            {
                var s = Pequeno(c.Sav);
                var b = s.ClockElapsed;
                b.Day = (ushort)Math.Max(DiasArregloBaya, b.Day);
                s.ClockElapsed = b;
            }, prioridad: -9, textoBoton: IdiomaInterfaz.T("Form.Rtc.Aplicar"), ayuda: IdiomaInterfaz.F("Form.Rtc.AyudaBaya", DiasArregloBaya)),
        };
        return new EditorDefP("rtc3", IdiomaInterfaz.T("Form.Rtc.Titulo"),
            [new PestanaP("reloj", EtiquetasLang.RequerirFormulario(Form),
            [
                new SeccionP(L("GB_Initial"), [.. Reloj("ini", true, ["L_IDay", "L_IHour", "L_IMinute", "L_ISecond"])]),
                new SeccionP(L("GB_Passed"), [.. Reloj("tra", false, ["L_EDay", "L_EHour", "L_EMinute", "L_ESecond"])]),
                new SeccionP(string.Empty, acciones),
            ])], Censo, IdiomaInterfaz.F("Form.Rtc.Descripcion", NombresJuego.Corto(GameVersion.R) + ", " + NombresJuego.Corto(GameVersion.S) + IdiomaInterfaz.T("Comun.ConjuncionY") + NombresJuego.Corto(GameVersion.E)));
    }
}
