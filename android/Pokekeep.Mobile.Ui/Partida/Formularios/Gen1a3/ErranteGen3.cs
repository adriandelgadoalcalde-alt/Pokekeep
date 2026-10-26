// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen1a3;

/// <summary>
/// PK-SAV-016: <c>SAV_Roamer3</c>, el Pokémon errante de la 3.ª generación (Latios/Latias en Rubí, Zafiro y Esmeralda; el perro legendario en Rojo Fuego y Verde Hoja). El formulario lee un <see cref="Roamer3"/>
/// del bloque grande y, al guardar, escribe en este orden: PID, especie, IVs (los seis juntos, 5 bits cada uno y tope 31), activo, nivel y PS actuales. Aquí cada campo se escribe solo si cambia y en ese mismo orden (<c>prioridad</c>).
/// «¿Variocolor?» no se edita: es una casilla desactivada que el formulario calcula del PID con el ID del entrenador (<see cref="Roamer3.IsShiny"/>); en el móvil es el texto de debajo del PID.
/// </summary>
internal static class ErranteGen3
{
    public const string Form = "SAV_Roamer3";
    public static readonly string[] Censo = ["PK-SAV-016"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);

    // The original Spanish caption is plural; Hall of Fame already has PKHeX's singular label.
    private static string Especie() => ProgramaPkhex.Idioma == "en" ? L("Label_Species") : EtiquetasLang.Requerir("SAV_HallOfFame1", "Label_Species");
    private static Roamer3 Lector(SaveFile sav) { var s = (SAV3)sav; return new Roamer3(s.LargeBlock.RoamerData, s is not SAV3E); }

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado (todo SAV3 de Rubí/Zafiro/Esmeralda/Rojo Fuego/Verde Hoja).</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV3;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV3) return null;

        CampoP Iv(string id, string control, int prioridad, Func<Roamer3, int> leer, Action<Roamer3, int> escribir)
            => P.Entero(id, AyudasGen3.Con("IV", L(control)), c => leer(Lector(c.Sav)), (c, v) => escribir(Lector(c.Sav), (int)v), 0, 31, prioridad: prioridad);

        var datos = new List<CampoP>
        {
            P.Entero("pid", L("Label_PID"), c => Lector(c.Sav).PID, (c, v) => Lector(c.Sav).PID = (uint)v, 0, uint.MaxValue, hex: true, prioridad: 0,
                detalle: c => $"{L("CHK_Shiny")}: {(Roamer3.IsShiny((uint)c.Entero("pid"), (SAV3)c.Sav) ? IdiomaInterfaz.T("Comun.Si").ToLowerInvariant() : IdiomaInterfaz.T("Comun.No").ToLowerInvariant())}", ayuda: IdiomaInterfaz.T("Form.AyudaHex8")),
            P.Opcion("especie", Especie(), c => Lector(c.Sav).Species, (c, v) => Lector(c.Sav).Species = (ushort)v, AyudasGen3.Especies, titulo: Especie(), prioridad: 1, especie: true),   // lang_es.txt trae «Especies:» (plural) para UNA especie: etiqueta a mano, como en el Hall de la Fama
            Iv("iv_ps", "Label_HP", 2, r => r.IV_HP, (r, v) => r.IV_HP = v),
            Iv("iv_atq", "Label_ATK", 3, r => r.IV_ATK, (r, v) => r.IV_ATK = v),
            Iv("iv_def", "Label_DEF", 4, r => r.IV_DEF, (r, v) => r.IV_DEF = v),
            Iv("iv_vel", "Label_SPE", 5, r => r.IV_SPE, (r, v) => r.IV_SPE = v),
            Iv("iv_atqe", "Label_SPA", 6, r => r.IV_SPA, (r, v) => r.IV_SPA = v),
            Iv("iv_defe", "Label_SPD", 7, r => r.IV_SPD, (r, v) => r.IV_SPD = v),
            P.Marca("activo", L("CHK_Active"), c => Lector(c.Sav).IsActive, (c, v) => Lector(c.Sav).IsActive = v, prioridad: 8, ayuda: IdiomaInterfaz.T("Form.Errante.Activo")),
            P.Entero("nivel", L("L_Level"), c => Lector(c.Sav).CurrentLevel, (c, v) => Lector(c.Sav).CurrentLevel = (byte)v, 0, 100, prioridad: 9),
            P.Entero("ps", L("L_HP"), c => Lector(c.Sav).HP_Current, (c, v) => Lector(c.Sav).HP_Current = (ushort)v, 0, 65535, prioridad: 10),
        };
        return new EditorDefP("roamer", ProgramaPkhex.Idioma == "en" ? EtiquetasLang.RequerirFormulario(Form) : IdiomaInterfaz.T("Form.Errante.Titulo"), [new PestanaP("errante", EtiquetasLang.RequerirFormulario(Form), [new SeccionP(string.Empty, datos)])], Censo,
            IdiomaInterfaz.T("Form.Errante.Descripcion"));
    }
}
