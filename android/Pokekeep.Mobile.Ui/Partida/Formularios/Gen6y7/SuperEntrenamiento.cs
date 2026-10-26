// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen6y7;

/// <summary>
/// PK-SAV-120 · <c>SAV_SuperTrain</c>, los récords de Superentrenamiento de X/Y y de Rubí Omega/Zafiro Alfa: 32 regímenes, cada uno con DOS récords (especie, género, forma y tiempo del Pokémon que los tiene) y los
/// 12 sacos de entrenamiento. Los récords se escriben al cambiarlos (<c>holder.Species</c>, <c>holder.Form</c>, <c>holder.Gender</c>, <c>SetTime1/2</c>); «Guardar» solo copia los sacos, y lo hace de una manera
/// peculiar que se calca tal cual: recorre las 12 filas, salta las que estan en «---» y escribe cada saco en el hueco <c>i - vacios</c> (los sacos se compactan hacia arriba) SIN vaciar los huecos de al final.
/// Los sacos son por tanto un grupo de escritura: si cambia uno se escriben los 12 en orden.
/// </summary>
internal static class SuperEntrenamiento
{
    public const string Form = "SAV_SuperTrain";
    public static readonly string[] Censo = ["PK-SAV-120"];
    private const int Regimenes = 32, Sacos = 12;

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SuperTrainBlock Bloque(ContextoP c) => ((ISaveBlock6Main)c.Sav).SuperTrain;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is ISaveBlock6Main;

    /// <summary>Los sacos que el formulario ofrece: todos los textos del juego que no estan vacios, con el valor igual a su posicion y la primera como «---».</summary>
    private static IReadOnlyList<OpcionP> TiposDeSaco(ContextoP c)
        => [.. c.Textos.trainingbags.Select((t, i) => new OpcionP(i, i == 0 ? "---" : t)).Where(o => o.Id == 0 || o.Texto.Length != 0)];

    private static IEnumerable<CampoP> Registro(ColeccionDefP col, int i, int n)
    {
        SuperTrainingSpeciesRecord Titular(ContextoP c) => n == 1 ? Bloque(c).GetHolder1(i) : Bloque(c).GetHolder2(i);
        var sufijo = n.ToString();
        yield return P.Opcion(col.IdCampo(i, $"especie{n}"), $"{L("L_Species")} ({n})", c => Titular(c).Species, (c, v) => Titular(c).Species = (ushort)v,
            c => P.Lista(c.Datos.SpeciesDataSource.Where(x => x.Value <= c.Sav.MaxSpeciesID)), titulo: L("L_Species"), especie: true);
        yield return P.Entero(col.IdCampo(i, $"genero{n}"), L($"MTB_Gender{n}"), c => Titular(c).Gender, (c, v) => Titular(c).Gender = (byte)v, 0, byte.MaxValue, ayuda: IdiomaInterfaz.T("SuperEntrenamiento.MachoHembraGenero"));
        yield return P.Entero(col.IdCampo(i, $"forma{n}"), L($"MTB_Form{n}"), c => Titular(c).Form, (c, v) => Titular(c).Form = (byte)v, 0, byte.MaxValue);
        yield return P.Decimal(col.IdCampo(i, $"tiempo{n}"), $"{L("L_Time0").TrimEnd(':')} ({sufijo})", c => n == 1 ? Bloque(c).GetTime1(i) : Bloque(c).GetTime2(i),
            (c, v) => { if (n == 1) Bloque(c).SetTime1(i, (float)v); else Bloque(c).SetTime2(i, (float)v); }, 0, 1e9, decimales: 3);
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not ISaveBlock6Main) return null;
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "registros", Elemento = IdiomaInterfaz.T("SuperEntrenamiento.Regimen"), Cantidad = _ => Regimenes, MostrarPosicion = false,
            Titulo = (c, i) => $"{i + 1:00} - {c.Textos.trainingstage[i]}",
            Secciones = (_, i) => [new SeccionP(IdiomaInterfaz.T("SuperEntrenamiento.Record"), [.. Registro(col!, i, 1)]), new SeccionP(IdiomaInterfaz.T("SuperEntrenamiento.Record2"), [.. Registro(col!, i, 2)])],
        };
        const string idSaco = "saco";
        var sacos = new List<CampoP>();
        for (var i = 0; i < Sacos; i++)
        {
            var n = i;
            sacos.Add(P.Opcion($"{idSaco}{n}", $"{n + 1}", c => Bloque(c).GetBag(n),
                (c, v) =>
                {
                    // «Guardar»: cada fila que no esta en «---» se escribe en el hueco (fila - filas vacias anteriores).
                    if (v == 0) return;
                    var vacios = Enumerable.Range(0, n).Count(j => c.Entero($"{idSaco}{j}") == 0);
                    Bloque(c).SetBag(n - vacios, (byte)v);
                },
                TiposDeSaco, titulo: IdiomaInterfaz.F("SuperEntrenamiento.Saco", n + 1), grupo: "sacos", ayuda: n == 0 ? IdiomaInterfaz.T("SuperEntrenamiento.PKHeXGuardarSacosColocan") : null));
        }
        return new EditorDefP("supertrain", IdiomaInterfaz.T("Partida.Especifico.supertrain.Titulo"),
            [new PestanaP("records", L("L_Records"), [], Coleccion: col), new PestanaP("sacos", L("L_Bags"), [new SeccionP(string.Empty, sacos)])],
            Censo, IdiomaInterfaz.T("Partida.Especifico.supertrain.Descripcion"));
    }
}
