// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using System.ComponentModel;
using System.Reflection;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen1a3;

/// <summary>Las pestañas de <see cref="VariosGen3"/> que son listas de elementos o llevan lógica propia: Frente Batalla, Pokécubos, Adornos y Pinturas.</summary>
internal static partial class VariosGen3
{
    // ---------------------------------------------------------------- Frente Batalla (TAB_BF): pase y simbolos
    // Nombres de las instalaciones: PKHeX los enseña en ingles (el enum). Son los oficiales de Esmeralda en castellano: los siete salen de los textos del propio Core (script/gen3/flags_e_es.txt, los simbolos del Frente: «Torre Batalla»,
    // «Cúpula Batalla», «Palacio Batalla», «Dojo Batalla» (Arena), «Fábrica Batalla», «Sierpe Batalla» (Pike) y «Pirámide Batalla»).
    private static readonly string[] Modos = [.. Enumerable.Range(0, 4).Select(i => ServicioIdioma.Recursos("es-ES")["Form.Varios3.Modo" + i])];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<OpcionP>> SimbolosPorIdioma = new[] { "es-ES", "en" }.ToDictionary(id => id, id => P.Lista(ServicioIdioma.Recursos(id)["Form.Varios3.SinSimbolo"], ServicioIdioma.Recursos(id)["Form.Varios3.Plata"], ServicioIdioma.Recursos(id)["Form.Varios3.Oro"]));
    private static IReadOnlyList<OpcionP> EstadosSimbolo => SimbolosPorIdioma[IdiomaInterfaz.Actual.Codigo];

    private static string Instalacion(int i) => IdiomaInterfaz.T("Form.Varios3.Instalacion" + i);
    private static string Modo(int i) => ProgramaPkhex.Idioma == "en" ? ((BattleFrontierBattleMode3)i).ToString() : Modos[i];

    private static PestanaP PaseFrente()
    {
        var simbolos = new List<CampoP>();
        for (var i = 0; i < 7; i++)
        {
            var f = (BattleFrontierFacility3)i;
            // Sin plata no hay oro: el boton recorre transparente -> plata -> oro, y al guardar escribe plata = no transparente, oro = dorado.
            simbolos.Add(P.Opcion($"simbolo{i}", IdiomaInterfaz.F("Form.Varios3.Simbolo", Instalacion(i)),
                c => S(c).GetEventFlag(BattleFrontier3.GetSymbolSilverFlagIndex(f)) ? S(c).GetEventFlag(BattleFrontier3.GetSymbolGoldFlagIndex(f)) ? 2 : 1 : 0,
                (c, v) => { S(c).SetEventFlag(BattleFrontier3.GetSymbolSilverFlagIndex(f), v != 0); S(c).SetEventFlag(BattleFrontier3.GetSymbolGoldFlagIndex(f), v == 2); },
                _ => EstadosSimbolo, titulo: IdiomaInterfaz.F("Form.Varios3.Simbolo", Instalacion(i)), prioridad: i, grupo: "frente"));
        }
        return new PestanaP("frente", L("TAB_BF"),
        [
            new SeccionP(L("GB_FrontierPass"), [P.Marca("pase", L("CHK_ActivatePass"), c => S(c).GetEventFlag(BattleFrontier3.FrontierPassFlagIndex), (c, v) => S(c).SetEventFlag(BattleFrontier3.FrontierPassFlagIndex, v), prioridad: 7, grupo: "frente")]),
            new SeccionP(L("GB_Icons"), simbolos),
        ]);
    }

    // ---------------------------------------------------------------- Frente Batalla: estadisticas por instalacion (GB_Stats)
    private static string EtiquetaEstadistica(BattleFrontierFacility3 f, BattleFrontierStatType3 s) => EtiquetasLang.Requerir(Form, (f, s) switch
    {
        (BattleFrontierFacility3.Factory, BattleFrontierStatType3.CurrentSwapped) => "L_CurrentSwapped",
        (BattleFrontierFacility3.Factory, BattleFrontierStatType3.RecordSwapped) => "L_RecordSwapped",
        (BattleFrontierFacility3.Dome, BattleFrontierStatType3.Championships) => "L_Championships",
        (BattleFrontierFacility3.Pike, BattleFrontierStatType3.RecordCleared) => "L_RecordCleared",
        (_, BattleFrontierStatType3.CurrentStreak) => "L_CurrentStreak",
        _ => "L_RecordStreak",
    });

    private static PestanaP EstadisticasFrente()
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "instalaciones", Elemento = L("L_Facility"), Cantidad = _ => 7, Titulo = (_, i) => Instalacion(i),
            Secciones = (_, i) =>
            {
                var f = (BattleFrontierFacility3)i;
                var secciones = new List<SeccionP>();
                for (var modo = 0; modo < BattleFrontier3.GetModeCount(f); modo++)
                for (var registro = 0; registro < 2; registro++)
                {
                    var mo = (BattleFrontierBattleMode3)modo; var re = (BattleFrontierRecordType3)registro;
                    var titulo = BattleFrontier3.GetModeCount(f) == 1 ? L(registro == 0 ? "RB_Stats3_01" : "RB_Stats3_02") : $"{Modo(modo)} · {L(registro == 0 ? "RB_Stats3_01" : "RB_Stats3_02")}";
                    var campos = new List<CampoP>();
                    foreach (var estadistica in BattleFrontier3.GetValidStats(f))
                    {
                        var st = estadistica;
                        campos.Add(P.Entero(col!.IdCampo(i, $"{modo}.{registro}.{st}"), EtiquetaEstadistica(f, st),
                            c => Math.Min((ushort)9999, ((SAV3E)c.Sav).SmallBlock.BattleFrontier.GetStat(f, mo, re, st)),
                            (c, v) => { var bf = ((SAV3E)c.Sav).SmallBlock.BattleFrontier; bf.SetStat(f, mo, re, st, (ushort)v); }, 0, 9999));
                    }
                    campos.Add(P.Marca(col!.IdCampo(i, $"{modo}.{registro}.continuar"), L("L_Continue"), c => ((SAV3E)c.Sav).SmallBlock.BattleFrontier.GetContinueFlag(f, mo, re),
                        (c, v) => { var bf = ((SAV3E)c.Sav).SmallBlock.BattleFrontier; bf.SetContinueFlag(f, mo, re, v); }));
                    secciones.Add(new SeccionP(titulo, campos));
                }
                return secciones;
            },
        };
        return new PestanaP("estadisticas", L("GB_Stats"), [], Coleccion: col);
    }

    // ---------------------------------------------------------------- Pokecubos (Tab_Pokeblocks / PokeBlock3CaseEditor)
    private static readonly IReadOnlyDictionary<string, string[]> CubosPorIdioma = new Dictionary<string, string[]> { ["es"] = Util.GetStringList("pokeblock3", "es"), ["en"] = Util.GetStringList("pokeblock3", "en") };
    private static string[] NombresPokecubo => CubosPorIdioma[ProgramaPkhex.Idioma];

    private static string NombreDelCubo(PokeBlock3Color color) => NombresPokecubo[(uint)color >= NombresPokecubo.Length ? 0 : (uint)color];

    // PropertyGrid enseña Spicy, Dry, Sweet, Bitter, Sour y Feel con la descripcion «Cool/Beauty/Cute/Smart/Tough/Sheen Stat Boost»: sabores y atributos de concurso en castellano.
    private static readonly (string Id, Func<string> Etiqueta, Func<PokeBlock3, byte> Leer, Action<PokeBlock3, byte> Escribir)[] AtributosCubo =
    [
        ("picante", () => AtributoCubo("Form.Varios3.SaborPicante", nameof(PokeBlock3.Spicy)), b => b.Spicy, (b, v) => b.Spicy = v), ("seco", () => AtributoCubo("Form.Varios3.SaborSeco", nameof(PokeBlock3.Dry)), b => b.Dry, (b, v) => b.Dry = v), ("dulce", () => AtributoCubo("Form.Varios3.SaborDulce", nameof(PokeBlock3.Sweet)), b => b.Sweet, (b, v) => b.Sweet = v),
        ("amargo", () => AtributoCubo("Form.Varios3.SaborAmargo", nameof(PokeBlock3.Bitter)), b => b.Bitter, (b, v) => b.Bitter = v), ("acido", () => AtributoCubo("Form.Varios3.SaborAcido", nameof(PokeBlock3.Sour)), b => b.Sour, (b, v) => b.Sour = v), ("sensacion", () => AtributoCubo("Form.Varios3.Sensacion", nameof(PokeBlock3.Feel)), b => b.Feel, (b, v) => b.Feel = v),
    ];

    // PropertyGrid's original English property names and descriptions belong to Core.
    private static readonly IReadOnlyDictionary<string, string> AtributosCuboEn = typeof(PokeBlock3).GetProperties()
        .Where(p => p.GetCustomAttribute<DescriptionAttribute>() is not null)
        .ToDictionary(p => p.Name, p => $"{p.Name} ({p.GetCustomAttribute<DescriptionAttribute>()!.Description.Replace(" Stat Boost", string.Empty)})");
    private static string AtributoCubo(string clave, string propiedad) => ProgramaPkhex.Idioma == "en" ? AtributosCuboEn[propiedad] : IdiomaInterfaz.T(clave);

    private static PestanaP Pokecubos()
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "bloques", Elemento = IdiomaInterfaz.T("Form.Varios3.Pokecubo"), Cantidad = _ => 40,
            Titulo = (c, i) => $"{i + 1:00} - {NombreDelCubo(Hoenn(c).PokeBlocks.Blocks[i].Color)}",
            Secciones = (_, i) =>
            {
                // El formulario trabaja sobre una copia de la cajita y, al guardar, la vuelca entera (PokeBlocks = Case): cada campo hace lo mismo con el cubo que toca.
                void Cambiar(ContextoP c, Action<PokeBlock3> cambia) { var h = Hoenn(c); var caja = h.PokeBlocks; cambia(caja.Blocks[i]); h.PokeBlocks = caja; }
                var campos = new List<CampoP>
                {
                    P.Opcion(col!.IdCampo(i, "color"), IdiomaInterfaz.T("Form.Varios3.Color"), c => (long)Hoenn(c).PokeBlocks.Blocks[i].Color, (c, v) => Cambiar(c, b => b.Color = (PokeBlock3Color)v), _ => P.Lista(NombresPokecubo), titulo: IdiomaInterfaz.T("Form.Varios3.Color")),
                };
                foreach (var (id, etiqueta, leer, escribir) in AtributosCubo)
                {
                    var e = escribir; var l = leer;
                    campos.Add(P.Entero(col!.IdCampo(i, id), etiqueta(), c => l(Hoenn(c).PokeBlocks.Blocks[i]), (c, v) => Cambiar(c, b => e(b, (byte)v)), 0, 255));
                }
                campos.Add(P.Info(col!.IdCampo(i, "nivel"), IdiomaInterfaz.T("Form.Varios3.NivelCubo"), c => Hoenn(c).PokeBlocks.Blocks[i].Level.ToString()));
                return [new SeccionP(string.Empty, campos)];
            },
        };
        return new PestanaP("pokecubos", L("Tab_Pokeblocks"), [], Coleccion: col);
    }

    private static PestanaP TodosLosPokecubos() => new("pokecubos_todos", IdiomaInterfaz.T("Form.Varios3.TodosCubos"),
    [
        new SeccionP(string.Empty,
        [
            // B_PokeblockAll / B_PokeblockDel: MaximizeAll(true) rellena los 40 con el cubo dorado al maximo; DeleteAll los pone a cero. Actuan de golpe sobre toda la cajita.
            P.Accion("obtener_todos", L("B_PokeblockAll"), c => { var h = Hoenn(c); var caja = h.PokeBlocks; caja.MaximizeAll(true); h.PokeBlocks = caja; }, inmediata: true, textoBoton: IdiomaInterfaz.T("Form.Obtener"),
                confirmacion: IdiomaInterfaz.T("Form.Varios3.CubosTodosConfirmar"), ayuda: IdiomaInterfaz.T("Form.Varios3.CubosTodosAyuda")),
            P.Accion("quitar_todos", L("B_PokeblockDel"), c => { var h = Hoenn(c); var caja = h.PokeBlocks; caja.DeleteAll(); h.PokeBlocks = caja; }, inmediata: true, textoBoton: IdiomaInterfaz.T("Form.Quitar"),
                confirmacion: IdiomaInterfaz.T("Form.Varios3.CubosQuitarConfirmar"), ayuda: IdiomaInterfaz.T("Form.Varios3.CubosQuitarAyuda")),
        ]),
    ]);

    // ---------------------------------------------------------------- Adornos (Tab_Decorations)
    private static readonly IReadOnlyDictionary<string, string[]> AdornosPorIdioma = new Dictionary<string, string[]> { ["es"] = Util.GetStringList("decoration3", "es"), ["en"] = Util.GetStringList("decoration3", "en") };
    private static readonly string[] ControlesCategoria = ["TB_Desk", "TB_Chair", "TB_Plant", "TB_Ornament", "TB_Mat", "TB_Poster", "TB_Doll", "TB_Cushion"];
    private static readonly int[] HuecosCategoria = [10, 10, 10, 30, 30, 10, 40, 10];

    /// <summary>Opciones de cada categoría: «ninguno» (todas pueden tener huecos vacíos) y los adornos que son de ella, como las columnas de combo del formulario.</summary>
    private static IReadOnlyList<OpcionP>[] CrearCategorias(string[] nombres) => [.. Enumerable.Range(0, 8).Select(k =>
        (IReadOnlyList<OpcionP>)[.. Enumerable.Range(0, nombres.Length).Where(id => id == (int)Decoration3.NONE || (id <= (int)Decoration3.REGISTEEL_DOLL && ((Decoration3)id).GetCategory() == (DecorationCategory3)k)).Select(id => new OpcionP(id, nombres[id]))])];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<OpcionP>[]> CategoriasPorIdioma = new Dictionary<string, IReadOnlyList<OpcionP>[]>
    { ["es"] = CrearCategorias(AdornosPorIdioma["es"]), ["en"] = CrearCategorias(AdornosPorIdioma["en"]) };
    private static IReadOnlyList<OpcionP>[] OpcionesCategoria => CategoriasPorIdioma[ProgramaPkhex.Idioma];

    private static Span<Decoration3> Categoria(DecorationInventory3 d, int k) => k switch
    {
        0 => d.Desk, 1 => d.Chair, 2 => d.Plant, 3 => d.Ornament, 4 => d.Mat, 5 => d.Poster, 6 => d.Doll, _ => d.Cushion,
    };

    private static PestanaP Adornos()
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "categorias", Elemento = IdiomaInterfaz.T("Form.Categoria"), Cantidad = _ => 8,
            Titulo = (_, k) => L(ControlesCategoria[k]),
            Resumen = (c, k) => { var d = Hoenn(c).Decorations; var datos = Categoria(d, k); var n = 0; foreach (var x in datos) if (x != Decoration3.NONE) n++; return IdiomaInterfaz.F("Form.Fama.Miembros", n, datos.Length); },
            Secciones = (_, k) =>
            {
                var n = HuecosCategoria[k];
                var ids = Enumerable.Range(0, n).Select(r => col!.IdCampo(k, $"hueco{r}")).ToArray();
                var campos = new List<CampoP>();
                for (var r = 0; r < n; r++)
                {
                    var fila = r;
                    campos.Add(P.Opcion(ids[r], $"{L("DGV_Item_Desk")} {r + 1}", c => (long)Categoria(Hoenn(c).Decorations, k)[fila],
                        (c, v) => { if (fila == 0) GuardarCategoria(c, k, ids); },   // un solo volcado de la categoria entera: los huecos vacios se compactan al final (SaveDecorationCategory)
                        _ => OpcionesCategoria[k], titulo: $"{L("DGV_Item_Desk")} {r + 1}", prioridad: r, grupo: $"adornos:{k}", lecturaAsimetrica: true, afectaA: _ => ids));
                }
                return [new SeccionP(L(ControlesCategoria[k]), campos)];
            },
        };
        return new PestanaP("adornos", L("Tab_Decorations"), [], Coleccion: col);
    }

    /// <summary>SaveDecorationCategory: los adornos elegidos van seguidos desde el principio y los huecos vacíos al final.</summary>
    private static void GuardarCategoria(ContextoP c, int k, string[] ids)
    {
        var datos = Categoria(Hoenn(c).Decorations, k);
        var cuenta = 0;
        for (var i = 0; i < datos.Length; i++)
        {
            var deco = (Decoration3)(int)c.Entero(ids[i]);
            if (deco == Decoration3.NONE) continue;
            datos[cuenta++] = deco;
        }
        for (var i = cuenta; i < datos.Length; i++) datos[i] = Decoration3.NONE;
    }

    // ---------------------------------------------------------------- Pinturas (Tab_Paintings)
    private static Paintings3 Pintura(ContextoP c, int i) => Hoenn(c).GetPainting(i, S(c).Japanese);
    private static bool PinturaActiva(ContextoP c, int i) => S(c).GetEventFlag(Paintings3.GetFlagIndexContestStat(i));

    private static PestanaP Pinturas()
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "pinturas", Elemento = IdiomaInterfaz.T("Form.Varios3.Pintura"), Cantidad = _ => 5, Titulo = (_, i) => IdiomaInterfaz.F("Form.Varios3.PinturaNumero", i + 1),
            Resumen = (c, i) => PinturaActiva(c, i) ? $"{c.Textos.specieslist[Pintura(c, i).Species]} · {Pintura(c, i).OT}" : IdiomaInterfaz.T("Form.Varios3.SinActivar"),
            Secciones = (_, i) =>
            {
                var g = $"pintura:{i}";
                var idAct = col!.IdCampo(i, "activa"); var idTid = col.IdCampo(i, "tid"); var idSid = col.IdCampo(i, "sid"); var idPid = col.IdCampo(i, "pid");
                bool Activa(ContextoP c) => c.Marca(idAct);
                // SavePainting: la bandera de la pintura primero; si esta desactivada se vacia entera y no se escribe nada mas; si no, especie, leyenda, ID, IDS, PID, mote y EO.
                void Cambiar(ContextoP c, Action<Paintings3> cambia)
                {
                    if (!Activa(c)) return;
                    var h = Hoenn(c); var p = h.GetPainting(i, S(c).Japanese); cambia(p); h.SetPainting(i, p);
                }
                var datos = new List<CampoP>
                {
                    P.Opcion(col.IdCampo(i, "especie"), L("L_Species"), c => Pintura(c, i).Species, (c, v) => Cambiar(c, p => p.Species = (ushort)v), AyudasGen3.Especies, visible: Activa, titulo: L("L_Species"), prioridad: 1, grupo: g, especie: true),
                    P.Entero(col.IdCampo(i, "leyenda"), L("L_Caption"), c => Pintura(c, i).GetCaptionRelative(i), (c, v) => Cambiar(c, p => p.SetCaptionRelative(i, (byte)v)), 0, Paintings3.CountCaption - 1, visible: Activa, prioridad: 2, grupo: g),
                    P.Entero(idTid, L("L_TID"), c => Pintura(c, i).TID, (c, v) => Cambiar(c, p => p.TID = (ushort)v), 0, 65535, visible: Activa, prioridad: 3, grupo: g),
                    P.Entero(idSid, L("L_SID"), c => Pintura(c, i).SID, (c, v) => Cambiar(c, p => p.SID = (ushort)v), 0, 65535, visible: Activa, prioridad: 4, grupo: g),
                    P.Entero(idPid, L("L_PID"), c => Pintura(c, i).PID, (c, v) => Cambiar(c, p => p.PID = (uint)v), 0, uint.MaxValue, hex: true, visible: Activa, prioridad: 5, grupo: g,
                        detalle: c => $"{L("CHK_Shiny")}: {(ShinyUtil.GetIsShiny3(((uint)c.Entero(idSid) << 16) | (uint)c.Entero(idTid), (uint)c.Entero(idPid)) ? IdiomaInterfaz.T("Comun.Si").ToLowerInvariant() : IdiomaInterfaz.T("Comun.No").ToLowerInvariant())}", ayuda: IdiomaInterfaz.T("Form.AyudaHex8")),
                    P.Texto(col.IdCampo(i, "mote"), L("L_Nickname"), c => Pintura(c, i).Nickname, (c, v) => Cambiar(c, p => p.Nickname = v), maxLongitud: 10, visible: Activa, prioridad: 6, grupo: g),
                    P.Texto(col.IdCampo(i, "eo"), L("L_OT"), c => Pintura(c, i).OT, (c, v) => Cambiar(c, p => p.OT = v), maxLongitud: 7, visible: Activa, prioridad: 7, grupo: g),
                };
                var activa = P.Marca(idAct, L("CHK_EnablePaint"), c => PinturaActiva(c, i), (c, v) =>
                {
                    S(c).SetEventFlag(Paintings3.GetFlagIndexContestStat(i), v);
                    if (v) return;
                    // Lo que hace Paintings3.Clear() (ceros y 0xFF al principio del mote y del EO) pero SOLO con ESTA pintura: el Core da a cada pintura la ventana de las cinco y su Clear() borra tambien las siguientes (ADR 0063).
                    var h = Hoenn(c); var p = h.GetPainting(i, S(c).Japanese);
                    p.Data[..Paintings3.SIZE].Clear(); p.Data[0x0B] = 0xFF; p.Data[0x16] = 0xFF;
                    h.SetPainting(i, p);
                }, prioridad: 0, grupo: g, ayuda: IdiomaInterfaz.T("Form.Varios3.PinturaAyuda"));
                return [new SeccionP(string.Empty, [activa]), new SeccionP(L("GB_Painting"), datos, Visible: c => c.Marca(idAct))];
            },
        };
        return new PestanaP("pinturas", L("Tab_Paintings"), [], Coleccion: col);
    }
}
