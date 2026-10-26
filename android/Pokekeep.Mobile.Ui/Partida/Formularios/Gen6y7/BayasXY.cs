// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Buffers.Binary;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen6y7;

/// <summary>
/// PK-SAV-104 · <c>SAV_BerryFieldXY</c>, el Huerto de Bayas de Kalos (solo X/Y). El formulario de PKHeX es SOLO LECTURA («Sin terminar: necesita mas investigacion»): una lista de parcelas y, de la elegida, ocho cajas de texto
/// de solo lectura (la baya y siete datos de 16 bits sin identificar). Aqui cada parcela es un elemento de una coleccion con esos ocho datos como informacion; nada se escribe. WinForms enseña 36 parcelas pero el Core
/// (<see cref="BerryField6XY.Count"/>) solo tiene 32: elegir la 33 a la 36 lanza una excepcion en el original, asi que aqui se enseñan las 32 reales (divergencia declarada en el ADR 0064).
/// </summary>
internal static class BayasXY
{
    public const string Form = "SAV_BerryFieldXY";
    public static readonly string[] Censo = ["PK-SAV-104"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static BerryField6XY Campo(SaveFile sav) => ((SAV6XY)sav).BerryField;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV6XY;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV6XY) return null;
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "parcelas", Elemento = L("L_Field"), Cantidad = _ => BerryField6XY.Count,
            Titulo = (_, i) => $"{i + 1:00}",
            Secciones = (_, i) =>
            {
                var campos = new List<CampoP>
                {
                    P.Info(col!.IdCampo(i, "estado"), IdiomaInterfaz.T("CartasP.Estado"), _ => L("L_Unfinished")),
                    P.Info(col.IdCampo(i, "baya"), L("L_Berry"), c => ReadU16(c.Sav, i, 0)),
                };
                // «1» a «7»: los siete datos de 16 bits que el formulario enseña tal cual (sin interpretar).
                for (var k = 1; k <= 7; k++)
                {
                    var n = k;
                    campos.Add(P.Info(col.IdCampo(i, $"u{n}"), L($"L_u{n}"), c => ReadU16(c.Sav, i, n)));
                }
                return [new SeccionP(string.Empty, campos)];
            },
        };
        return new EditorDefP("bayas6", IdiomaInterfaz.T("Partida.Especifico.bayas6.Titulo"),
            [new PestanaP("huerto", L("L_Field"), [], Coleccion: col)],
            Censo, IdiomaInterfaz.T("Partida.Especifico.bayas6.Descripcion"));
    }

    private static string ReadU16(SaveFile sav, int parcela, int palabra)
        => BinaryPrimitives.ReadUInt16LittleEndian(Campo(sav).GetPlot(parcela)[(2 * palabra)..]).ToString();
}
