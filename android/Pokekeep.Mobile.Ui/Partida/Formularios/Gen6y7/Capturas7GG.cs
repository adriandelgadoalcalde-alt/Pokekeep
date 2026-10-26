// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen6y7;

/// <summary>
/// PK-SAV-128 · <c>SAV_Capture7GG</c>, los récords de captura de Let's Go, Pikachu! y Let's Go, Eevee! (<c>SAV7b</c>): por cada una de las 153 especies (las 151 de Kanto, Meltan y Melmetal) cuántas se han capturado
/// (0 a 9.999) y cuántas se han transferido al Profesor Oak (0 a 999.999.999) y los dos totales (0 a 999.999.999). «Marcar todos» (<c>B_Modify</c>) pone en TODAS las especies ya capturadas (según la Pokédex) las cantidades que enseñan
/// los cuadros de la especie elegida, y «Σ» (<c>B_SumTotal</c>) calcula los totales como la suma de las especies. En el formulario las dos acciones cambian los controles (se guardan con «Guardar»): aqui es una sola tabla
/// (totales, las dos cantidades de «Marcar todos» y las 306 cifras de las especies, con buscador, que es el «ir a:») y las dos acciones cambian el BORRADOR de sus filas. «Guardar» escribe tambien los totales desde los controles
/// (los que habia antes de «Marcar todos», aunque el Core haya bajado el total): el total no cambia con «Marcar todos» a menos que se edite.
/// </summary>
internal static class Capturas7GG
{
    public const string Form = "SAV_Capture7GG";
    public static readonly string[] Censo = ["PK-SAV-128"];
    private const int FilaTotalCapturados = 1000, FilaTotalTransferidos = 1001, FilaTodosCapturados = 1002, FilaTodosTransferidos = 1003;
    private const long MaxCapturadosPorEspecie = 9_999, MaxTransferidos = 999_999_999;

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static CaptureRecords Registro(ContextoP c) => ((SAV7b)c.Sav).Blocks.Captured;
    private static bool EspecieLegal(int especie) => especie is >= 1 and (<= 151 or 808 or 809);

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV7b;

    private static string Clave(int fila) => $"capturas:{fila}";

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV7b) return null;
        var tabla = new TablaDefP
        {
            Id = "capturas", Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), SinNumero = true,
            Ayuda = IdiomaInterfaz.T("Capturas7GG.MarcarTodosUsaDos"),
            Filas = c =>
            {
                var l = new List<FilaTablaP>
                {
                    new() { Id = FilaTotalCapturados, Nombre = $"{L("GB_Total")} · {L("L_TotalCaptured")}", Min = 0, Max = 999_999_999, Leer = x => (long)Registro(x).TotalCaptured, Escribir = (x, v) => Registro(x).TotalCaptured = (uint)(long)v },
                    new() { Id = FilaTotalTransferidos, Nombre = $"{L("GB_Total")} · {L("L_TotalTransferred")}", Min = 0, Max = 999_999_999, Leer = x => (long)Registro(x).TotalTransferred, Escribir = (x, v) => Registro(x).TotalTransferred = (uint)(long)v },
                    // Las cantidades que «Marcar todos» pone a las especies capturadas: no se guardan en la partida.
                    new() { Id = FilaTodosCapturados, Nombre = $"{L("B_Modify")} · {L("L_SpeciesCaptured")}", Min = 0, Max = MaxCapturadosPorEspecie, Leer = _ => MaxCapturadosPorEspecie, Escribir = (_, _) => { }, SinEscritura = true, Detalle = IdiomaInterfaz.T("Capturas7GG.GuardaCantidadPoneAccion") },
                    new() { Id = FilaTodosTransferidos, Nombre = $"{L("B_Modify")} · {L("L_SpeciesTransferred")}", Min = 0, Max = MaxTransferidos, Leer = _ => MaxTransferidos, Escribir = (_, _) => { }, SinEscritura = true, Detalle = IdiomaInterfaz.T("Capturas7GG.GuardaCantidadPoneAccion") },
                };
                foreach (var e in FuentesGen6y7.Filtro(c).Species.Where(z => EspecieLegal(z.Value)).OrderBy(z => z.Value))
                {
                    var indice = CaptureRecords.GetSpeciesIndex((ushort)e.Value);
                    l.Add(new FilaTablaP { Id = indice * 2, Nombre = $"{e.Value:000}: {e.Text} · {L("L_SpeciesCaptured")}", Min = 0, Max = MaxCapturadosPorEspecie, Leer = x => (long)Registro(x).GetCapturedCountIndex(indice), Escribir = (x, v) => Registro(x).SetCapturedCountIndex(indice, (uint)(long)v) });
                    l.Add(new FilaTablaP { Id = (indice * 2) + 1, Nombre = $"{e.Value:000}: {e.Text} · {L("L_SpeciesTransferred")}", Min = 0, Max = MaxTransferidos, Leer = x => (long)Registro(x).GetTransferredCountIndex(indice), Escribir = (x, v) => Registro(x).SetTransferredCountIndex(indice, (uint)(long)v) });
                }
                return l;
            },
            Acciones =
            [
                new AccionTablaP("marcar-todos", L("B_Modify"), (c, filas) =>
                {
                    // SetAllCaptured / SetAllTransferred: con una cantidad distinta de 0 solo las especies ya capturadas (Pokédex); con 0, todas.
                    var capturados = Math.Min(c.Entero(Clave(FilaTodosCapturados)), MaxCapturadosPorEspecie);
                    var transferidos = Math.Min(c.Entero(Clave(FilaTodosTransferidos)), MaxTransferidos);
                    var dex = ((SAV7b)c.Sav).Blocks.Zukan;
                    var l = new List<(FilaTablaP, object)>();
                    foreach (var f in filas.Where(f => f.Id < FilaTotalCapturados))
                    {
                        var especie = CaptureRecords.GetIndexSpecies((ushort)(f.Id / 2));
                        var cantidad = f.Id % 2 == 0 ? capturados : transferidos;
                        if (cantidad != 0 && !dex.GetCaught(especie)) continue;
                        l.Add((f, cantidad));
                    }
                    return l;
                }, IdiomaInterfaz.T("Capturas7GG.PoneTodasEspeciesCapturadas")),
                new AccionTablaP("sumar", L("B_SumTotal"), (c, filas) =>
                {
                    // CalculateTotalCaptured / CalculateTotalTransferred: la suma (con tope de 999.999.999) de los borradores de las especies.
                    long capturados = 0, transferidos = 0;
                    foreach (var f in filas.Where(f => f.Id < FilaTotalCapturados)) { var v = c.Entero(Clave(f.Id)); if (f.Id % 2 == 0) capturados += v; else transferidos += v; }
                    return [(filas.First(f => f.Id == FilaTotalCapturados), Math.Min(capturados, 999_999_999)), (filas.First(f => f.Id == FilaTotalTransferidos), Math.Min(transferidos, 999_999_999))];
                }, IdiomaInterfaz.T("Capturas7GG.TotalesPasanSerSuma")),
            ],
        };
        return new EditorDefP("capturas7gg", IdiomaInterfaz.T("Partida.Especifico.capturas7gg.Titulo"), [new PestanaP("capturas", EtiquetasLang.RequerirFormulario(Form), [], Tabla: tabla)], Censo, IdiomaInterfaz.T("Partida.Especifico.capturas7gg.Descripcion"));
    }
}
