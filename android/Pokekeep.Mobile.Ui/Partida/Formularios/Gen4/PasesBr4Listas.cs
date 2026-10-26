// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>Las listas de <see cref="PasesBr4"/> (personajes, títulos, accesorios, fotos, diseños e idiomas) y el aspecto del entrenador.</summary>
internal static partial class PasesBr4
{
    private static string Traducido(string tipo, string valor) => EtiquetasLang.Obtener($"{tipo}.{valor}") ?? valor;

    /// <summary><c>CharacterStyles</c>: los nombres de <see cref="ModelBR"/> en castellano, en el orden del enum (0 = «(Ninguno)»).</summary>
    private static readonly string[] Estilos = [.. Enum.GetNames<ModelBR>().Select(n => Traducido("ModelBR", n))];
    private static readonly IReadOnlyList<OpcionP> Personajes = P.Lista(Estilos);
    private static readonly IReadOnlyList<OpcionP> Pieles = P.Lista(Enum.GetNames<SkinColorBR>().Select(n => Traducido("SkinColorBR", n)));
    private static readonly IReadOnlyList<OpcionP> Fotos = P.Lista(Enum.GetNames<PictureTypeBR>().Select(n => Traducido("PictureTypeBR", n)));
    private static IReadOnlyList<OpcionP> Disenos => P.Lista(Util.GetStringList("pass_design", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma));
    private static string[] NombresDeAccesorio => Util.GetStringList("gear", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);
    private static readonly IReadOnlyList<OpcionP> Idiomas = P.Lista(GameInfo.LanguageDataSource(3, EntityContext.Gen3));
    private static readonly IReadOnlyList<OpcionP> SinAccesorios = [new OpcionP(0, string.Empty)];

    // ---------------------------------------------------------------- Título de entrenador (GetTrainerTitles)
    private const ushort Desplazamiento1 = 11057, Desplazamiento2 = 18015;

    /// <summary>
    /// Los títulos de los entrenadores como los arma el formulario: los de cada personaje y los de los entrenadores no jugadores, y los repetidos se distinguen primero por personaje («(Chico)») o por «(PNJ)» y después por número
    /// («(1)», «(2)»...). Cada título vale su posición más 11057 (los de los personajes) o 18015 (los de los PNJ); el primero, «(Ninguno)», vale 0.
    /// </summary>
    private static IReadOnlyList<OpcionP> TitulosDeEntrenador(ContextoP _) => TitulosCache.Value;

    private static readonly PorIdioma<IReadOnlyList<OpcionP>> TitulosCache = new(ConstruirTitulos);

    private static IReadOnlyList<OpcionP> ConstruirTitulos()
    {
        var titulos1 = (string[])Util.GetStringList("trainer_title", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma).Clone();
        var titulos2 = (string[])Util.GetStringList("trainer_title_npc", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma).Clone();
        const string Pnj = "PNJ";

        var cuentas = new Dictionary<string, int>();
        foreach (var s in titulos1.Concat(titulos2)) { cuentas.TryGetValue(s, out var v); cuentas[s] = v + 1; }
        for (var i = 0; i < titulos1.Length; i++)
            if (cuentas[titulos1[i]] > 1) titulos1[i] += $" ({Estilos[(i / (titulos1.Length / 6)) + 1]})";
        for (var i = 0; i < titulos2.Length; i++)
            if (cuentas[titulos2[i]] > 1) titulos2[i] += $" ({Pnj})";

        // Segunda pasada: los que siguen repetidos se numeran de atras adelante (primero los PNJ).
        cuentas.Clear();
        foreach (var s in titulos1.Concat(titulos2)) { cuentas.TryGetValue(s, out var v); cuentas[s] = v + 1; }
        var maximos = new Dictionary<string, int>(cuentas);
        for (var i = titulos2.Length - 1; i >= 0; i--)
        {
            var s = titulos2[i]; var n = cuentas[s]--;
            if (maximos[s] == 1) continue;
            titulos2[i] += $" ({n})";
        }
        for (var i = titulos1.Length - 1; i >= 0; i--)
        {
            var s = titulos1[i]; var n = cuentas[s]--;
            if (maximos[s] == 1) continue;
            titulos1[i] += $" ({n})";
        }

        var lista = new List<OpcionP> { new(0, Estilos[0]) };
        lista.AddRange(titulos1.Select((t, i) => new OpcionP(Desplazamiento1 + i, t)));
        lista.AddRange(titulos2.Select((t, i) => new OpcionP(Desplazamiento2 + i, t)));
        return lista;
    }

    // ---------------------------------------------------------------- Aspecto (GB_Appearance)
    private static bool PersonajeConAccesorios(long modelo) => (ModelBR)modelo is >= ModelBR.YoungBoy and <= ModelBR.LittleGirl;

    /// <summary><c>SetupGearCategoryComboBoxes</c>: los accesorios del personaje elegido (los broches son siempre los del primer personaje); sin personaje de pase, una lista vacía y el campo bloqueado.</summary>
    private static IReadOnlyList<OpcionP> Accesorios(long modelo, GearCategory categoria)
    {
        if (!PersonajeConAccesorios(modelo)) return SinAccesorios;
        var m = categoria == GearCategory.Badges ? ModelBR.YoungBoy : (ModelBR)modelo;
        var (inicio, cuantos) = GearUnlock.GetOffsetCount(m, categoria);
        return P.Lista(NombresDeAccesorio.Skip(inicio).Take(cuantos));
    }

    private static int PorDefecto(long modelo, GearCategory categoria)
        => PersonajeConAccesorios(modelo) ? GearUnlock.GetDefault(categoria == GearCategory.Badges ? ModelBR.YoungBoy : (ModelBR)modelo, categoria) : 0;

    private static IReadOnlyList<CampoP> Aspecto(ColeccionDefP col, int i)
    {
        string Id(string campo) => col.IdCampo(i, campo);
        var idModelo = Id("modelo");
        // (id, etiqueta, categoria, leer, escribir): el orden de SaveCurrent.
        (string Clave, string Control, GearCategory Categoria, Func<BattlePass, int> Leer, Action<BattlePass, int> Escribir)[] piezas =
        [
            ("gorra", "L_Head", GearCategory.Head, p => p.Head, (p, v) => p.Head = v),
            ("cabello", "L_Hair", GearCategory.Hair, p => p.Hair, (p, v) => p.Hair = v),
            ("rostro", "L_Face", GearCategory.Face, p => p.Face, (p, v) => p.Face = v),
            ("gafas", "L_Glasses", GearCategory.Glasses, p => p.Glasses, (p, v) => p.Glasses = v),
            ("prenda", "L_Top", GearCategory.Top, p => p.Top, (p, v) => p.Top = v),
            ("manos", "L_Hands", GearCategory.Hands, p => p.Hands, (p, v) => p.Hands = v),
            ("pantalon", "L_Bottom", GearCategory.Bottom, p => p.Bottom, (p, v) => p.Bottom = v),
            ("calzado", "L_Shoes", GearCategory.Shoes, p => p.Shoes, (p, v) => p.Shoes = v),
            ("broche", "L_Badge", GearCategory.Badges, p => p.Badge, (p, v) => p.Badge = v),
            ("bolsa", "L_Bag", GearCategory.Bags, p => p.Bag, (p, v) => p.Bag = v),
        ];
        var ids = piezas.Select(p => Id(p.Clave)).ToArray();
        var campos = new List<CampoP>
        {
            // CB_Model_SelectedIndexChanged: los accesorios pasan a los del personaje nuevo (SetupComboBoxesAppearance) y, ya cargado el formulario, UpdatePresetIndexes: SaveCurrent y ResetPresetIndexes. El personaje se escribe el ultimo.
            P.Opcion(idModelo, Ln("L_Model"), c => Pase(c, i).Model, (c, v) => { var p = Pase(c, i); p.Model = (int)v; p.ResetPresetIndexes(); }, _ => Personajes, titulo: Ln("L_Model"), prioridad: 400,
                deriva: c => piezas.Select((pieza, k) => (ids[k], pieza.Categoria)).ToDictionary(t => t.Item1, t => (object?)(long)PorDefecto(c.Entero(idModelo), t.Categoria)),
                afectaA: _ => ids.Concat([Id("saludo.indice"), Id("salida.indice"), Id("cambio1.indice"), Id("cambio2.indice"), Id("victoria.indice"), Id("derrota.indice")])),
            P.Opcion(Id("piel"), Ln("L_SkinColor"), c => Pase(c, i).Skin, (c, v) => Pase(c, i).Skin = (int)v, _ => Pieles, titulo: Ln("L_SkinColor"), prioridad: 5),
        };
        for (var k = 0; k < piezas.Length; k++)
        {
            var pieza = piezas[k];
            campos.Add(P.Opcion(ids[k], Ln(pieza.Control), c => pieza.Leer(Pase(c, i)), (c, v) => pieza.Escribir(Pase(c, i), (int)v), c => Accesorios(c.Entero(idModelo), pieza.Categoria), titulo: Ln(pieza.Control), prioridad: 6 + k,
                motivo: c => PersonajeConAccesorios(c.Entero(idModelo)) ? null : IdiomaInterfaz.T("PasesBr4Listas.PersonajeTieneAccesorios")));
        }
        return campos;
    }
}
