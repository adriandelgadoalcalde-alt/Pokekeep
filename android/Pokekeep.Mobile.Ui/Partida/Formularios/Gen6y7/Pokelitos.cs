// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen6y7;

/// <summary>
/// PK-SAV-115 · <c>SAV_Pokepuff</c>, los Pokélitos de X/Y y de Rubí Omega/Zafiro Alfa: 100 ranuras, cada una con un tipo de Pokélito. «Todos» (<c>MaxCheat</c>), «Ninguno» (<c>Reset</c>) y «Ordenar» (<c>Sort</c>) actúan
/// DIRECTAMENTE sobre la partida en el formulario original (no tiene copia de trabajo: «Salir» no los deshace), por eso son acciones inmediatas, con su Deshacer. «Guardar» escribe las 100 ranuras y pone el contador
/// a 100 (<c>PuffCount = puffs.Length</c>); aqui, al cambiar una ranura se escribe esa ranura y el contador queda en 100 igual. Con Ctrl, «Todos» da solo los mejores y «Ordenar» ordena al reves: dos acciones mas.
/// </summary>
internal static class Pokelitos
{
    public const string Form = "SAV_Pokepuff";
    public static readonly string[] Censo = ["PK-SAV-115"];
    private const int Ranuras = 100, PorPagina = 10;

    private static Puff6 Puff(SaveFile sav) => ((ISaveBlock6Main)sav).Puff;
    private static string L(string control) => EtiquetasLang.Requerir(Form, control);

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is ISaveBlock6Main;

    /// <summary>Los tipos de Pokélito: todos los textos del juego, con el valor igual a su posicion (el 0, sin texto en el juego, se enseña como «---» igual que los sacos del Superentrenamiento).</summary>
    private static IReadOnlyList<OpcionP> Tipos(ContextoP c) => [.. P.Lista(c.Textos.puffs).Select(o => o.Id == 0 ? new OpcionP(0, "---") : o)];

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not ISaveBlock6Main) return null;
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "ranuras", Elemento = IdiomaInterfaz.T("Pokelitos.GrupoRanuras"), Cantidad = _ => Ranuras / PorPagina, MostrarPosicion = false,
            Titulo = (_, i) => $"{(i * PorPagina) + 1} a {(i + 1) * PorPagina}",
            Resumen = (c, i) =>
            {
                var ocupadas = Enumerable.Range(i * PorPagina, PorPagina).Count(n => Puff(c.Sav).GetPuffs()[n] != 0);
                return ocupadas == 0 ? IdiomaInterfaz.T("Pokelitos.Vacias") : IdiomaInterfaz.F("Pokelitos.Ocupadas", ocupadas, PorPagina);
            },
            Secciones = (_, i) =>
            [
                new SeccionP(string.Empty, [.. Enumerable.Range(i * PorPagina, PorPagina).Select(n => P.Opcion(col!.IdCampo(i, $"r{n}"), $"{n + 1}",
                    c => Puff(c.Sav).GetPuffs()[n],
                    (c, v) => { Puff(c.Sav).GetPuffs()[n] = (byte)v; Puff(c.Sav).PuffCount = Ranuras; },
                    Tipos, titulo: IdiomaInterfaz.F("ModeloRegalos.Ranura", n + 1)))]),
            ],
        };
        var acciones = new SeccionP(string.Empty,
        [
            P.Accion("todos", L("B_All"), c => Puff(c.Sav).MaxCheat(false), inmediata: true, textoBoton: IdiomaInterfaz.T("Comun.Dar"), confirmacion: IdiomaInterfaz.T("Pokelitos.SustituyenRanurasPokelitosTodos"), ayuda: IdiomaInterfaz.T("Pokelitos.RellenaRanurasPokelitosVariados")),
            P.Accion("todos-mejores", IdiomaInterfaz.F("Pokelitos.PuntoMejores", L("B_All")), c => Puff(c.Sav).MaxCheat(true), inmediata: true, textoBoton: IdiomaInterfaz.T("Comun.Dar"), confirmacion: IdiomaInterfaz.T("Pokelitos.SustituyenRanurasPokelitosSupremos"), ayuda: IdiomaInterfaz.T("Pokelitos.RellenaRanurasSoloMejores")),
            P.Accion("ninguno", L("B_None"), c => Puff(c.Sav).Reset(), inmediata: true, textoBoton: IdiomaInterfaz.T("GoPark.Vaciar"), confirmacion: IdiomaInterfaz.T("Pokelitos.VacianRanurasQuedanCinco"), ayuda: IdiomaInterfaz.T("Pokelitos.DejaSoloCincoPokelitos")),
            P.Accion("ordenar", L("B_Sort"), c => Puff(c.Sav).Sort(false), inmediata: true, textoBoton: IdiomaInterfaz.T("Mochila.Ordenar"), ayuda: IdiomaInterfaz.T("Pokelitos.OrdenaRanurasTipo")),
            P.Accion("ordenar-reves", IdiomaInterfaz.F("Pokelitos.PuntoAlReves", L("B_Sort")), c => Puff(c.Sav).Sort(true), inmediata: true, textoBoton: IdiomaInterfaz.T("Mochila.Ordenar"), ayuda: IdiomaInterfaz.T("Pokelitos.OrdenaRanurasRevesCtrl")),
        ]);
        return new EditorDefP("pokeflautas", IdiomaInterfaz.T("Partida.Especifico.pokeflautas.Titulo"),
            [new PestanaP("acciones", EtiquetasLang.RequerirFormulario(Form), [acciones]), new PestanaP("ranuras", IdiomaInterfaz.T("Pokelitos.Ranuras"), [], Coleccion: col)],
            Censo, IdiomaInterfaz.T("Partida.Especifico.pokeflautas.Descripcion"));
    }
}
