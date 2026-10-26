// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen6y7;

/// <summary>
/// PK-SAV-111 · <c>SAV_OPower</c>, los Poderes O de X/Y y de Rubí Omega/Zafiro Alfa: 65 poderes (desbloqueado o no), dos niveles por cada uno de los 10 poderes de campo y de los 7 de batalla (0 a 255) y los puntos.
/// «Dar todo» (<c>UnlockAll</c>) y «Limpiar todo» (<c>ClearAll</c>) cambian los controles del formulario y se guardan con el resto: aqui son acciones de la tabla que cambian el BORRADOR de todas las filas, igual que
/// <c>LoadCurrent</c>, y se aplican/deshacen como cualquier cambio. La tabla es una sola porque las dos acciones tocan los tres bloques a la vez (poderes, niveles y puntos) y una accion de tabla solo ve su tabla.
/// El formulario guarda TODO al pulsar Guardar (sus controles ya reflejan la partida); aqui solo se escribe lo que cambia.
/// </summary>
internal static class PoderO
{
    public const string Form = "SAV_OPower";
    public static readonly string[] Censo = ["PK-SAV-111"];

    private const int FilaCampo = 100, FilaBatalla = 200, FilaPuntos = 300;
    private static OPower6 Bloque(ContextoP c) => ((ISaveBlock6Main)c.Sav).OPower;
    private static string L(string control) => EtiquetasLang.Requerir(Form, control);

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is ISaveBlock6Main;

    private static IReadOnlyList<FilaTablaP> Filas()
    {
        var l = new List<FilaTablaP>();
        for (var i = 0; i < (int)OPower6Index.Count; i++)
        {
            var n = i;
            l.Add(new FilaTablaP
            {
                Id = n, Nombre = TextosPkhex.Enum((OPower6Index)n), EsMarca = true, Grupo = IdiomaInterfaz.T("PoderO.Poderes"),
                Leer = x => Bloque(x).GetState((OPower6Index)n) == OPowerFlagState.Unlocked,
                Escribir = (x, v) => Bloque(x).SetState((OPower6Index)n, (bool)v ? OPowerFlagState.Unlocked : OPowerFlagState.Locked),
            });
        }
        for (var t = 0; t < (int)OPower6FieldType.Count; t++)
            for (var nivel = 1; nivel <= 2; nivel++)
            {
                var tipo = (OPower6FieldType)t; var n = nivel;
                l.Add(new FilaTablaP
                {
                    Id = FilaCampo + (t * 2) + (nivel - 1), Nombre = $"{L("GB_Field")} · {TextosPkhex.Enum(tipo)} · {(nivel == 1 ? "A" : "B")}", Min = 0, Max = byte.MaxValue, Grupo = L("GB_Field"),
                    Leer = x => (long)(n == 1 ? Bloque(x).GetLevel1(tipo) : Bloque(x).GetLevel2(tipo)),
                    Escribir = (x, v) => { if (n == 1) Bloque(x).SetLevel1(tipo, (byte)(long)v); else Bloque(x).SetLevel2(tipo, (byte)(long)v); },
                });
            }
        for (var t = 0; t < (int)OPower6BattleType.Count; t++)
            for (var nivel = 1; nivel <= 2; nivel++)
            {
                var tipo = (OPower6BattleType)t; var n = nivel;
                l.Add(new FilaTablaP
                {
                    Id = FilaBatalla + (t * 2) + (nivel - 1), Nombre = $"{L("GB_Battle")} · {TextosPkhex.Enum(tipo)} · {(nivel == 1 ? "A" : "B")}", Min = 0, Max = byte.MaxValue, Grupo = L("GB_Battle"),
                    Leer = x => (long)(n == 1 ? Bloque(x).GetLevel1(tipo) : Bloque(x).GetLevel2(tipo)),
                    Escribir = (x, v) => { if (n == 1) Bloque(x).SetLevel1(tipo, (byte)(long)v); else Bloque(x).SetLevel2(tipo, (byte)(long)v); },
                });
            }
        l.Add(new FilaTablaP { Id = FilaPuntos, Nombre = L("L_Points"), Min = 0, Max = byte.MaxValue, Leer = x => (long)Bloque(x).Points, Escribir = (x, v) => Bloque(x).Points = (byte)(long)v });
        return l;
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not ISaveBlock6Main) return null;
        var tabla = new TablaDefP
        {
            Id = "poderes", Marcador = IdiomaInterfaz.T("PoderO.BuscarPoder"), SinNumero = true,
            Ayuda = IdiomaInterfaz.T("PoderO.NivelesBSonDos"),
            Filas = _ => Filas(),
            Acciones =
            [
                // UnlockAll: todos los poderes (incluido «Habilitar»), todos los niveles a 3; los puntos no se tocan.
                new AccionTablaP("dar-todo", L("B_GiveAll"), (_, filas) => [.. filas.Where(f => f.Id != FilaPuntos).Select(f => (f, f.EsMarca ? (object)true : 3L))], IdiomaInterfaz.T("PoderO.DesbloqueaTodosPoderesPone")),
                // ClearAll: todo menos el primer poder («Habilitar»), incluidos los puntos.
                new AccionTablaP("limpiar-todo", L("B_ClearAll"), (_, filas) => [.. filas.Where(f => f.Id != 0).Select(f => (f, f.EsMarca ? false : (object)0L))], IdiomaInterfaz.T("PoderO.QuitaTodosPoderesMenos")),
            ],
        };
        return new EditorDefP("poderes6", IdiomaInterfaz.T("Partida.Especifico.poderes6.Titulo"), [new PestanaP("poderes", EtiquetasLang.RequerirFormulario(Form), [], Tabla: tabla)], Censo, IdiomaInterfaz.T("Partida.Especifico.poderes6.Descripcion"));
    }
}
