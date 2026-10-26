// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>
/// PK-SAV-056 a 060: <c>SAV_Underground</c>, el Subsuelo de Sinnoh (Diamante, Perla y Platino): trece puntuaciones (0-999.999) y cuatro bolsas de 40 huecos (objetos, gemas con su tamaño, trampas y tesoros).
/// <c>B_Save_Click</c> escribe primero las puntuaciones y después, por cada bolsa, VACÍA la lista entera y vuelve a escribir seguidos los huecos que no son «(Ninguno)» (los vacíos quedan al final); en las gemas el hueco necesita
/// además un tamaño numérico, que se guarda como un byte.
/// <para>En el móvil cada bolsa es una pestaña con sus 40 huecos como campos de lista (más el tamaño en las gemas) y un grupo de escritura: cuando cambia uno, el hueco 0 vuelca la bolsa entera desde el borrador, igual que
/// el formulario (los huecos vacíos se compactan al final). Divergencias declaradas en el ADR 0062: el formulario reescribe las cuatro bolsas y las trece puntuaciones aunque no se toquen (compacta, recorta las puntuaciones a
/// 999.999 y borra los identificadores que no existen); la app solo escribe la bolsa que se cambia y las puntuaciones que se cambian. El tamaño de una gema se limita a 0-255 (el formulario deja escribir cualquier número y lo guarda
/// como un byte).</para>
/// </summary>
internal static class SubsueloGen4
{
    public const string Form = "SAV_Underground";
    public static readonly string[] Censo = ["PK-SAV-056", "PK-SAV-057", "PK-SAV-058", "PK-SAV-059", "PK-SAV-060"];
    private const int Huecos = SAV4Sinnoh.UG_POUCH_SIZE;

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SAV4Sinnoh S(ContextoP c) => (SAV4Sinnoh)c.Sav;

    /// <summary>Predicado barato de <see cref="Crear"/>: <c>B_OpenUGSEditor</c> en Sinnoh (D/P/Pt); en BDSP es otro formulario (8b-2).</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV4Sinnoh;

    private enum Bolsa { Bienes, Gemas, Trampas, Tesoros }

    private static Span<byte> Datos(SAV4Sinnoh s, Bolsa b) => b switch
    {
        Bolsa.Bienes => s.GetUGI_Goods(),
        Bolsa.Gemas => s.GetUGI_Spheres(),
        Bolsa.Trampas => s.GetUGI_Traps(),
        _ => s.GetUGI_Treasures(),
    };

    /// <summary>Los nombres sin hueco, ordenados como el formulario (<c>Array.Sort</c>), cada uno con el identificador de su PRIMERA aparición (<c>IndexOf</c>); el «(Ninguno)» entra como cualquier otro.</summary>
    private static IReadOnlyList<OpcionP> Opciones(string[] nombres)
    {
        var ordenados = Array.FindAll(nombres, x => !string.IsNullOrEmpty(x));
        Array.Sort(ordenados);
        return [.. ordenados.Select(n => new OpcionP(Array.IndexOf(nombres, n), n))];
    }

    private static string[] Nombres(ContextoP c, Bolsa b) => b switch { Bolsa.Bienes => c.Textos.uggoods, Bolsa.Gemas => c.Textos.ugspheres, Bolsa.Trampas => c.Textos.ugtraps, _ => c.Textos.ugtreasures };

    // Las cuatro listas se construyen UNA vez por contexto e idioma (auditoría uso10 (A7): con el cambio de idioma en vivo la lista se rehace si cambian los textos del contexto): una lista de opciones se pide cada vez que se pinta un campo.
    private sealed class Listas { public GameStrings? Textos; public readonly IReadOnlyList<OpcionP>?[] L = new IReadOnlyList<OpcionP>?[4]; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ContextoP, Listas> Cache = new();
    private static IReadOnlyList<OpcionP> Lista(ContextoP c, Bolsa b)
    {
        var l = Cache.GetValue(c, _ => new Listas());
        if (!ReferenceEquals(l.Textos, c.Textos)) { l.Textos = c.Textos; Array.Clear(l.L); }
        return l.L[(int)b] ??= Opciones(Nombres(c, b));
    }

    private static string IdHueco(string pestana, int r) => $"{pestana}{r}";
    private static string IdTamano(int r) => $"gemas_tamano{r}";

    private static PestanaP PestanaDeBolsa(Bolsa bolsa, string id, string titulo, string etiquetaHueco)
    {
        var ids = Enumerable.Range(0, Huecos).Select(r => IdHueco(id, r)).ToArray();
        var todos = bolsa == Bolsa.Gemas ? ids.Concat(Enumerable.Range(0, Huecos).Select(IdTamano)).ToArray() : ids;
        string g = $"subsuelo:{id}";
        var campos = new List<CampoP>();
        for (var r = 0; r < Huecos; r++)
        {
            var fila = r;
            // Lo que leen las celdas: un identificador fuera de la lista se ve como el primero («(Ninguno)») y, en las gemas, con tamaño 0.
            long Leer(ContextoP c)
            {
                var n = Nombres(c, bolsa); var v = Datos(S(c), bolsa)[fila];
                return v >= n.Length ? 0 : v;
            }
            campos.Add(P.Opcion(ids[r], $"{etiquetaHueco} {r + 1}", Leer,
                (c, v) => { if (fila == 0) Volcar(c, bolsa, ids); },   // un solo volcado de la bolsa entera (SaveUGData)
                c => Lista(c, bolsa), titulo: $"{etiquetaHueco} {r + 1}", prioridad: r, grupo: g, lecturaAsimetrica: true, afectaA: _ => todos));
            if (bolsa == Bolsa.Gemas)
                campos.Add(P.Entero(IdTamano(r), $"{L("DGV_Size_Spheres")} {r + 1}",
                    c => { var l = S(c).GetUGI_Spheres(); return l[fila] >= c.Textos.ugspheres.Length ? 0 : l[Huecos + fila]; }, (c, v) => { }, 0, 99, prioridad: r, grupo: g, lecturaAsimetrica: true, afectaA: _ => todos));
        }
        var secciones = new List<SeccionP>();
        for (var i = 0; i < Huecos; i += 10)
            secciones.Add(new SeccionP($"{titulo} · {i + 1}-{i + 10}", [.. campos.Where(c => Indice(c.Id) >= i && Indice(c.Id) < i + 10)]));
        return new PestanaP(id, titulo, secciones);

        static int Indice(string id) => int.Parse(new string(id.Where(char.IsDigit).ToArray()));
    }

    /// <summary>
    /// <c>SaveUGData</c> de una bolsa: vacía la lista y escribe seguidos los huecos con algo (identificador mayor que 0). En las gemas el hueco necesita además un tamaño (siempre lo tiene aquí) que va en la segunda mitad de la lista.
    /// </summary>
    private static void Volcar(ContextoP c, Bolsa bolsa, string[] ids)
    {
        var lista = Datos(S(c), bolsa);
        lista.Clear();
        var n = 0;
        for (var r = 0; r < Huecos; r++)
        {
            var indice = (int)c.Entero(ids[r]);
            if (indice <= 0) continue;
            lista[n] = (byte)indice;
            if (bolsa == Bolsa.Gemas) lista[n + Huecos] = (byte)c.Entero(IdTamano(r));
            n++;
        }
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV4Sinnoh) return null;
        // Puntuaciones: LoadValue recorta a 999.999 al abrir y SetUGScores escribe las trece al guardar, en este orden.
        static CampoP Punt(string id, string control, Func<SAV4Sinnoh, uint> leer, Action<SAV4Sinnoh, uint> escribir, int orden)
            => P.Entero(id, EtiquetasLang.Requerir(Form, control), c => Math.Min(SAV4Sinnoh.UG_MAX, leer(S(c))), (c, v) => escribir(S(c), (uint)v), 0, SAV4Sinnoh.UG_MAX, prioridad: orden);
        var puntuaciones = new SeccionP(L("GB_UScores"),
        [
            Punt("jugadores", "L_PeopleMet", s => s.UG_PeopleMet, (s, v) => s.UG_PeopleMet = v, 0),
            Punt("regalos_dados", "L_Gifts", s => s.UG_GiftsGiven, (s, v) => s.UG_GiftsGiven = v, 1),
            Punt("regalos_recibidos", "L_GiftsReceived", s => s.UG_GiftsReceived, (s, v) => s.UG_GiftsReceived = v, 2),
            Punt("gemas", "L_Spheres", s => s.UG_Spheres, (s, v) => s.UG_Spheres = v, 3),
            Punt("fosiles", "L_Fossils", s => s.UG_Fossils, (s, v) => s.UG_Fossils = v, 4),
            Punt("trampas_otros", "L_TrapOthers", s => s.UG_TrapPlayers, (s, v) => s.UG_TrapPlayers = v, 5),
            Punt("trampas_propias", "L_TrapSelf", s => s.UG_TrapSelf, (s, v) => s.UG_TrapSelf = v, 6),
            Punt("base_movida", "L_MyBaseMoved", s => s.UG_MyBaseMoved, (s, v) => s.UG_MyBaseMoved = v, 7),
            Punt("banderas_obtenidas", "L_FlagsObtained", s => s.UG_FlagsTaken, (s, v) => s.UG_FlagsTaken = v, 8),
            Punt("mi_bandera_tomada", "L_MyFlagTaken", s => s.UG_FlagsFromMe, (s, v) => s.UG_FlagsFromMe = v, 9),
            Punt("banderas_recuperadas", "L_MyFlagRecovered", s => s.UG_FlagsRecovered, (s, v) => s.UG_FlagsRecovered = v, 10),
            Punt("banderas_capturadas", "L_FlagsCaptured", s => s.UG_FlagsCaptured, (s, v) => s.UG_FlagsCaptured = v, 11),
            Punt("ayudado_a_otros", "L_OthersHelped", s => s.UG_HelpedOthers, (s, v) => s.UG_HelpedOthers = v, 12),
        ]);
        return new EditorDefP("subsuelo", TituloGen4.De("subsuelo"),
        [
            new PestanaP("puntuaciones", L("GB_UScores"), [puntuaciones]),
            PestanaDeBolsa(Bolsa.Bienes, "objetos", L("TB_UGGoods"), L("DGV_Item_Goods")),
            PestanaDeBolsa(Bolsa.Gemas, "gemas", L("TB_UGSpheres"), L("DGV_Item_Spheres")),
            PestanaDeBolsa(Bolsa.Trampas, "trampas", L("TB_UGTraps"), L("DGV_Item_Traps")),
            PestanaDeBolsa(Bolsa.Tesoros, "tesoros", L("TB_UGTreasures"), L("DGV_Item_Treasures")),
        ], Censo, IdiomaInterfaz.T("Partida.Especifico.subsuelo.Descripcion"));
    }
}
