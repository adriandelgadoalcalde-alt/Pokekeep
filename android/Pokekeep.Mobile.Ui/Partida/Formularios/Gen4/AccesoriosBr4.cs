// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>
/// PK-SAV-033: <c>SAV_Gear</c>, los accesorios de los personajes de Pokémon Battle Revolution: una rejilla con una fila por accesorio (personaje, categoría, nombre y «Obtenido»), los seis disfraces variocolor
/// (<c>GearShiny*Outfit</c>) y dos botones, «Desbloquear todos los accesorios» (<c>UnlockAll</c>) y «Restablecer accesorios por defecto» (<c>Clear</c>), que SOLO cambian la rejilla. <c>B_Save_Click</c> escribe cada fila con
/// <c>GearUnlock.Set(índice, obtenido)</c> y después los seis disfraces.
/// <para>En el móvil la rejilla es una tabla con buscador y los dos botones son acciones de la tabla sobre el borrador (se aplican con «Aplicar» y se deshacen, como los de WinForms hasta Guardar); los disfraces son otra pestaña. Divergencia
/// declarada en el ADR 0065: <c>UnlockAll</c> y <c>Clear</c> del Core vacían ANTES los 192 bytes de las banderas (también las que ninguna fila enseña, de personajes que no existen); la app solo cambia las filas que enseña.</para>
/// </summary>
internal static class AccesoriosBr4
{
    public const string Form = "SAV_Gear";
    public static readonly string[] Censo = ["PK-SAV-033"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SAV4BR S(ContextoP c) => (SAV4BR)c.Sav;

    /// <summary>Predicado barato de <see cref="Crear"/>: <c>B_OpenGear</c> solo existe en Battle Revolution.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV4BR;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV4BR) return null;
        var nombres = Util.GetStringList("gear", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);
        // InitializeDGV: un recorrido por personaje (Chico..Niña), categoría y número de accesorio; en los broches solo el primero es de un personaje, los demás son «de todos» (las banderas se comparten).
        Cajas.Idioma.Asegurar();
        var todos = MessageStrings.MsgGearAllCharacterStyles;   // «Todo» (MessageStrings_es.txt:183), lo que enseña SAV_Gear para lo que vale para todos los personajes
        var filas = new List<(int Indice, string Nombre, bool Defecto)>();
        for (var modelo = ModelBR.YoungBoy; modelo <= ModelBR.LittleGirl; modelo++)
            for (GearCategory categoria = 0; (int)categoria < GearUnlock.CategoryCount; categoria++)
            {
                var (inicio, cuantos) = GearUnlock.GetOffsetCount(modelo, categoria);
                for (var i = 0; i < cuantos; i++)
                {
                    var compartido = categoria is GearCategory.Badges && i != 0;
                    var personaje = compartido ? todos : EtiquetasLang.Obtener($"ModelBR.{modelo}") ?? modelo.ToString();
                    var cat = EtiquetasLang.Obtener($"GearCategory.{categoria}") ?? categoria.ToString();
                    filas.Add((inicio + i, $"{personaje} · {cat} · {nombres[inicio + i]}", i == GearUnlock.GetDefault(modelo, categoria)));
                }
            }
        var tabla = new TablaDefP
        {
            Id = "accesorios", SinNumero = true, Marcador = IdiomaInterfaz.T("AccesoriosBr4.BuscarAccesorio"), Ayuda = IdiomaInterfaz.T("AccesoriosBr4.AccesoriosCadaPersonajeTiene"),
            Filas = _ => [.. filas.Select(f => new FilaTablaP
            {
                Id = f.Indice, Nombre = f.Nombre, EsMarca = true,
                Leer = x => S(x).GearUnlock.Get(f.Indice), Escribir = (x, v) => S(x).GearUnlock.Set(f.Indice, (bool)v),
            })],
            Acciones =
            [
                new AccionTablaP("todos", L("B_UnlockAll"), (_, rejilla) => [.. rejilla.Select(f => (f, (object)true))], IdiomaInterfaz.T("AccesoriosBr4.MarcaTodosAccesoriosTodos")),
                new AccionTablaP("defecto", L("B_Clear"), (_, rejilla) => [.. rejilla.Select(f => (f, (object)filas.First(x => x.Indice == f.Id).Defecto))], IdiomaInterfaz.T("AccesoriosBr4.DejaSoloAccesorioDefecto")),
            ],
        };
        // Los seis disfraces variocolor van juntos; CHK_Groudon... se escriben tras la rejilla.
        var disfraces = new (string Id, string Control, Func<SAV4BR, bool> Leer, Action<SAV4BR, bool> Escribir)[]
        {
            ("groudon", "CHK_Groudon", s => s.GearShinyGroudonOutfit, (s, v) => s.GearShinyGroudonOutfit = v),
            ("lucario", "CHK_Lucario", s => s.GearShinyLucarioOutfit, (s, v) => s.GearShinyLucarioOutfit = v),
            ("electivire", "CHK_Electivire", s => s.GearShinyElectivireOutfit, (s, v) => s.GearShinyElectivireOutfit = v),
            ("kyogre", "CHK_Kyogre", s => s.GearShinyKyogreOutfit, (s, v) => s.GearShinyKyogreOutfit = v),
            ("roserade", "CHK_Roserade", s => s.GearShinyRoseradeOutfit, (s, v) => s.GearShinyRoseradeOutfit = v),
            ("pachirisu", "CHK_Pachirisu", s => s.GearShinyPachirisuOutfit, (s, v) => s.GearShinyPachirisuOutfit = v),
        };
        var campos = disfraces.Select((d, i) => (CampoP)P.Marca(d.Id, L(d.Control), c => d.Leer(S(c)), (c, v) => d.Escribir(S(c), v), prioridad: i)).ToList();
        return new EditorDefP("equipobr", TituloGen4.De("equipobr"),
        [
            new PestanaP("accesorios", L("DGV_Gear"), [], Tabla: tabla),
            new PestanaP("variocolor", L("GB_ShinyOutfits"), [new SeccionP(string.Empty, campos)]),
        ], Censo, IdiomaInterfaz.T("Partida.Especifico.equipobr.Descripcion"));
    }
}
