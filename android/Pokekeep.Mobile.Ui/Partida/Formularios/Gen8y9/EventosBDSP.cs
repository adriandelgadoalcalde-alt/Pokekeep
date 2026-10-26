// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen8y9;

/// <summary>
/// PK-SAV-153 a PK-SAV-157 · <c>SAV_FlagWork8b</c>, las marcas de eventos, las marcas del sistema y las variables de trabajo de Diamante Brillante y Perla Reluciente (4.000 marcas, 1.000 marcas de sistema y 500 variables
/// de 32 bits, todas en el bloque <c>FlagWork8b</c>), más la investigación por diferencias. Las cuatro pestañas del original son cuatro pestañas: «Marcas de eventos» (<c>GB_Flags</c>), «Marcas del Sistema» (<c>GB_System</c>),
/// las variables (<c>GB_Work</c>) y «Investigar» (<c>GB_Research</c>). El formulario trabaja sobre una copia del guardado y la copia al original con «Guardar» (<c>CopyChangesFrom</c>): una marca se escribe con
/// <c>SetFlag</c>/<c>SetSystemFlag</c> (0 o 1 en cuatro bytes) y una variable con <c>SetWork</c>. Aquí cada pestaña es una tabla con buscador que enseña TODAS las posiciones (el formulario solo lista las que tienen nombre y
/// deja el resto al editor «por número» de la parte de abajo: «Marca de evento: n» y «Aplicar»); las que tienen nombre en <c>flag_bdsp_es.txt</c>, <c>system_bdsp_en.txt</c> y <c>work_bdsp_es.txt</c> salen con él y el
/// resto como «Marca de evento 0123». Los nombres son los identificadores del juego (<c>FE_PC_SHINOU_ZUKAN_COMP</c>): PKHeX no los traduce. De cada variable con valores predefinidos, el texto secundario los lista.
/// </summary>
internal static class EventosBDSP
{
    public const string Form = "SAV_FlagWork8b";
    public static readonly string[] Censo = ["PK-SAV-153", "PK-SAV-154", "PK-SAV-155", "PK-SAV-156", "PK-SAV-157"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static FlagWork8b Datos(ContextoP c) => ((SAV8BS)c.Sav).FlagWork;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV8BS;

    private static string SinNombre => IdiomaInterfaz.T("Partida.Ranuras.SinNombre");

    /// <summary>El resumen de lo que cambió entre los dos guardados (null si falta alguno): «antes» y «después», como «Cargar ant.» y «Cargar nuevo».</summary>
    private static string? Resumen(ContextoP c)
    {
        var antes = InvestigarGuardados.Leer(c.Valor("antes") as byte[]) as SAV8BS;
        var despues = InvestigarGuardados.Leer(c.Valor("despues") as byte[]) as SAV8BS;
        if (antes is null || despues is null) return IdiomaInterfaz.T("EventosBDSP.EligeGuardadoAntesDespues");
        var d = new EventWorkDiff8b(antes, despues);
        if (d.Message != EventWorkDiffCompatibility.Valid) return IdiomaInterfaz.T("EventosBDSP.DosGuardadosSonVersiones");
        var texto = new List<string>
        {
            IdiomaInterfaz.F("EventosBDSP.MarcasPuestas", d.SetFlags.Count, InvestigarGuardados.Numeros(d.SetFlags)),
            IdiomaInterfaz.F("EventosBDSP.MarcasQuitadas", d.ClearedFlags.Count, InvestigarGuardados.Numeros(d.ClearedFlags)),
            IdiomaInterfaz.F("EventosBDSP.MarcasSistemaPuestas", d.SetSystem.Count, InvestigarGuardados.Numeros(d.SetSystem)),
            IdiomaInterfaz.F("EventosBDSP.MarcasSistemaQuitadas", d.ClearedSystem.Count, InvestigarGuardados.Numeros(d.ClearedSystem)),
            IdiomaInterfaz.F("EventosBDSP.VariablesCambiadas", d.WorkChanged.Count) + (d.WorkChanged.Count == 0 ? "ninguna" : string.Join("; ", d.WorkChanged.Take(InvestigarGuardados.MaximoLineas).Select((w, i) => $"{w:000} ({d.WorkDiff[i]})")) + (d.WorkChanged.Count > InvestigarGuardados.MaximoLineas ? IdiomaInterfaz.F("Form.Mas", d.WorkChanged.Count - InvestigarGuardados.MaximoLineas) : string.Empty)),
        };
        return string.Join("\n", texto);
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV8BS s) return null;
        var flags = FlagWork8b.COUNT_FLAG; var sistema = FlagWork8b.COUNT_SYSTEM; var trabajo = FlagWork8b.COUNT_WORK;
        // Los mismos recortes que el formulario (EventLabelCollectionSystem(game, CountFlag - 1, CountSystem - 1, CountWork - 1)).
        var etiquetas = new EventLabelCollectionSystem("bdsp", flags - 1, sistema - 1, trabajo - 1);
        var nombreFlag = new Dictionary<int, NamedEventValue>(); foreach (var f in etiquetas.Flag) nombreFlag[f.Index] = f;
        var nombreSistema = new Dictionary<int, NamedEventValue>(); foreach (var f in etiquetas.System) nombreSistema[f.Index] = f;
        var nombreTrabajo = new Dictionary<int, NamedEventWork>(); foreach (var f in etiquetas.Work) nombreTrabajo[f.Index] = f;

        var sinNombreMarca = L("CHK_CustomFlag"); var sinNombreSistema = L("CHK_CustomSystem"); var sinNombreVariable = L("L_CustomWork");
        var marcas = new TablaDefP
        {
            Id = "marcas", Marcador = IdiomaInterfaz.T("EventosBDSP.BuscarMarca"), Ayuda = L("L_EventFlagWarn"),
            Filas = _ => [.. Enumerable.Range(0, flags).Select(i =>
            {
                nombreFlag.TryGetValue(i, out var n);
                return new FilaTablaP { Id = i, Nombre = n?.Name ?? $"{sinNombreMarca} {i:0000}", Grupo = n is null ? SinNombre : TextoEventos.Grupo(n.Type), EsMarca = true, Leer = x => Datos(x).GetFlag(i), Escribir = (x, v) => Datos(x).SetFlag(i, (bool)v) };
            })],
        };
        var sistemaTabla = new TablaDefP
        {
            Id = "sistema", Marcador = IdiomaInterfaz.T("EventosBDSP.BuscarMarca"),
            Filas = _ => [.. Enumerable.Range(0, sistema).Select(i =>
            {
                nombreSistema.TryGetValue(i, out var n);
                return new FilaTablaP { Id = i, Nombre = n?.Name ?? $"{sinNombreSistema} {i:0000}", Grupo = n is null ? SinNombre : TextoEventos.Grupo(n.Type), EsMarca = true, Leer = x => Datos(x).GetSystemFlag(i), Escribir = (x, v) => Datos(x).SetSystemFlag(i, (bool)v) };
            })],
        };
        var trabajoTabla = new TablaDefP
        {
            Id = "trabajo", Marcador = IdiomaInterfaz.T("EventosBDSP.BuscarVariable"),
            Filas = _ => [.. Enumerable.Range(0, trabajo).Select(i =>
            {
                nombreTrabajo.TryGetValue(i, out var n);
                var predefinidos = n is null ? null : string.Join(" · ", n.PredefinedValues.Where(p => p.Value != NamedEventConst.CustomMagicValue).Select(p => $"{p.Value} = {p.Name}"));
                return new FilaTablaP
                {
                    Id = i, Nombre = n?.Name ?? $"{sinNombreVariable} {i:000}", Grupo = n is null ? SinNombre : TextoEventos.Grupo(n.Type), Min = int.MinValue, Max = int.MaxValue,
                    Detalle = string.IsNullOrEmpty(predefinidos) ? null : predefinidos,
                    Leer = x => (long)Datos(x).GetWork(i), Escribir = (x, v) => Datos(x).SetWork(i, (int)(long)v),
                };
            })],
        };
        var investigar = new SeccionP(string.Empty,
        [
            InvestigarGuardados.Campo("antes", L("B_LoadOld"), g => g is SAV8BS, IdiomaInterfaz.T("EventosBDSP.ArchivoGuardadoDiamanteBrillante"), IdiomaInterfaz.T("EventosBDSP.GuardadoANTESHacerAlgo")),
            InvestigarGuardados.Campo("despues", L("B_LoadNew"), g => g is SAV8BS, IdiomaInterfaz.T("EventosBDSP.ArchivoGuardadoDiamanteBrillante"), IdiomaInterfaz.T("EventosBDSP.GuardadoDESPUESHacerlo"), Resumen),
        ]);
        return new EditorDefP("eventos8b", IdiomaInterfaz.T("Partida.Especifico.eventos8b.Titulo"),
        [
            new PestanaP("marcas", L("GB_Flags"), [], Tabla: marcas),
            new PestanaP("sistema", L("GB_System"), [], Tabla: sistemaTabla),
            new PestanaP("trabajo", L("GB_Work"), [], Tabla: trabajoTabla),
            new PestanaP("investigar", L("GB_Research"), [investigar]),
        ], Censo, IdiomaInterfaz.T("EventosBDSP.MarcasSistemaVariablesBDSP"));
    }
}
