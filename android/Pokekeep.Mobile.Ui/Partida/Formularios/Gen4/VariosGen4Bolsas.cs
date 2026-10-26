// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>
/// Las pestañas de <see cref="VariosGen4"/> que el formulario de PKHeX resuelve con un <c>PropertyGrid</c>: los Pokochos de Sinnoh (<c>PoffinCase4Editor</c>, PK-SAV-019 y 042) y el Pokégear de HG/SS (<c>PokeGear4Editor</c>, PK-SAV-026 y 043).
/// Los dos editan objetos en memoria que se vuelcan al guardar (<c>Case.Save()</c> escribe los 100 Pokochos; <c>SetPokeGearRoloDex</c> los contactos), y sus botones («Obtener todos», «Quitar todos», «Registrar todos»...) actúan sobre el guardado
/// al momento y refrescan la rejilla, así que en el móvil son acciones inmediatas.
/// </summary>
internal static partial class VariosGen4
{
    // ---------------------------------------------------------------- Pokochos (Tab_Poffins / PoffinCase4Editor)
    private static string[] NombresDePokocho => Util.GetStringList("poffin4", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);

    private static string NombreDelPokocho(PoffinFlavor4 sabor) => NombresDePokocho[(uint)sabor >= NombresDePokocho.Length ? 0 : (uint)sabor];

    // Las descripciones del PropertyGrid son «Cool/Beauty/Cute/Smart/Tough/Sheen Stat Boost»: sabores y atributos de concurso en castellano (los mismos que los Pokécubos de la 3.ª generación).
    private static (string Id, string Etiqueta, Func<Poffin4, byte> Leer, Action<Poffin4, byte> Escribir)[] AtributosPokocho =>
    [
        ("picante", IdiomaInterfaz.T("Form.Varios3.SaborPicante"), p => p.BoostSpicy, (p, v) => p.BoostSpicy = v), ("seco", IdiomaInterfaz.T("Form.Varios3.SaborSeco"), p => p.BoostDry, (p, v) => p.BoostDry = v), ("dulce", IdiomaInterfaz.T("Form.Varios3.SaborDulce"), p => p.BoostSweet, (p, v) => p.BoostSweet = v),
        ("amargo", IdiomaInterfaz.T("Form.Varios3.SaborAmargo"), p => p.BoostBitter, (p, v) => p.BoostBitter = v), ("acido", IdiomaInterfaz.T("Form.Varios3.SaborAcido"), p => p.BoostSour, (p, v) => p.BoostSour = v), ("suavidad", IdiomaInterfaz.T("VariosGen4Bolsas.SuavidadBrillo"), p => p.Smoothness, (p, v) => p.Smoothness = v),
    ];

    private static PestanaP Pokochos()
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "pokochos", Elemento = IdiomaInterfaz.T("VariosGen4Bolsas.Pokocho"), Cantidad = _ => 100,
            Titulo = (c, i) => $"{i + 1:000} - {NombreDelPokocho(new PoffinCase4((SAV4Sinnoh)c.Sav).Poffins[i].Type)}",
            Secciones = (_, i) =>
            {
                // El formulario trabaja sobre la caja entera (Case.Poffins) y la vuelca con Case.Save(): cada campo hace lo mismo con el Pokocho que toca.
                void Cambiar(ContextoP c, Action<Poffin4> cambia) { var caja = new PoffinCase4((SAV4Sinnoh)c.Sav); cambia(caja.Poffins[i]); caja.Save(); }
                var campos = new List<CampoP>
                {
                    P.Opcion(col!.IdCampo(i, "sabor"), IdiomaInterfaz.T("VariosGen4Bolsas.Sabor"), c => (long)new PoffinCase4((SAV4Sinnoh)c.Sav).Poffins[i].Type, (c, v) => Cambiar(c, p => p.Type = (PoffinFlavor4)v),
                        _ => P.Lista(NombresDePokocho).Where(o => o.Texto.Length > 0).ToList(), titulo: IdiomaInterfaz.T("VariosGen4Bolsas.Sabor")),
                };
                foreach (var (id, etiqueta, leer, escribir) in AtributosPokocho)
                {
                    var e = escribir; var l = leer;
                    campos.Add(P.Entero(col.IdCampo(i, id), etiqueta, c => l(new PoffinCase4((SAV4Sinnoh)c.Sav).Poffins[i]), (c, v) => Cambiar(c, p => e(p, (byte)v)), 0, 255));
                }
                campos.Add(P.Info(col.IdCampo(i, "nivel"), IdiomaInterfaz.T("VariosGen4Bolsas.NivelPokocho"), c => new PoffinCase4((SAV4Sinnoh)c.Sav).Poffins[i].Level.ToString()));
                return [new SeccionP(string.Empty, campos)];
            },
        };
        return new PestanaP("pokochos", L("Tab_Poffins"), [], Coleccion: col);
    }

    private static PestanaP TodosLosPokochos() => new("pokochos_todos", IdiomaInterfaz.F("Gen4.PestanaTodos", L("Tab_Poffins")),
    [
        new SeccionP(string.Empty,
        [
            // B_PoffinAll / B_PoffinDel: FillCase() pone los 100 al maximo (Rich, 255 en todo); DeleteAll() los deja sin nada. Actuan de golpe sobre toda la caja.
            P.Accion("obtener_todos", L("B_PoffinAll"), c => { var caja = new PoffinCase4((SAV4Sinnoh)c.Sav); caja.FillCase(); caja.Save(); }, inmediata: true, textoBoton: IdiomaInterfaz.T("Form.Obtener"),
                confirmacion: IdiomaInterfaz.T("VariosGen4Bolsas.HuecosCajaPasanSer"), ayuda: IdiomaInterfaz.T("VariosGen4Bolsas.LlenaTodaCajaPokochos")),
            P.Accion("quitar_todos", L("B_PoffinDel"), c => { var caja = new PoffinCase4((SAV4Sinnoh)c.Sav); caja.DeleteAll(); caja.Save(); }, inmediata: true, textoBoton: IdiomaInterfaz.T("PasesBr4.Quitar"),
                confirmacion: IdiomaInterfaz.T("VariosGen4Bolsas.VacianHuecosCajaPokochos"), ayuda: IdiomaInterfaz.T("VariosGen4Bolsas.VaciaTodaCajaPokochos")),
        ]),
    ]);

    // ---------------------------------------------------------------- Pokégear (Tab_PokeGear / PokeGear4Editor)
    // Nombres de los contactos: PKHeX enseña el nombre del enum (en inglés). Los de las líderes y líderes de gimnasio salen de los textos del Core en castellano (script/gen4/flags_hgss_es.txt, «Revancha contra ...»);
    // los demás nombres propios se dejan como están (el Core no trae su traducción) y los cargos se pasan al castellano.
    private static Dictionary<PokegearNumber, string> NombresDeContacto => new()
    {
        [PokegearNumber.None] = IdiomaInterfaz.T("Form.Ninguno"), [PokegearNumber.Mother] = "Madre", [PokegearNumber.Professor_Elm] = "Profesor Elm", [PokegearNumber.Professor_Oak] = "Profesor Oak", [PokegearNumber.Daycare_Man] = "Guardería: hombre",
        [PokegearNumber.Daycare_Lady] = "Guardería: mujer", [PokegearNumber.Bike_Shop] = "Tienda de Bicis", [PokegearNumber.Whitney] = "Blanca", [PokegearNumber.Falkner] = "Pegaso", [PokegearNumber.Janine] = "Sachiko",
        [PokegearNumber.Clair] = "Débora", [PokegearNumber.Blue] = IdiomaInterfaz.T("Catalogo.Azul"), [PokegearNumber.Chuck] = "Aníbal", [PokegearNumber.Bugsy] = "Antón", [PokegearNumber.Morty] = "Morti", [PokegearNumber.Jasmine] = "Yasmina",
        [PokegearNumber.Pryce] = "Fredo", [PokegearNumber.Lieutenant_Surge] = "Lt. Surge", [PokegearNumber.Tim_and_Sue] = "Tim y Sue", [PokegearNumber.Kay_and_Tia] = "Kay y Tia",
    };

    internal static string NombreDeContacto(PokegearNumber n) => IdiomaInterfaz.Actual.Codigo != "en" && NombresDeContacto.TryGetValue(n, out var t) ? t : n.ToString().Replace('_', ' ');   // en inglés, los nombres del enum del Core

    private static IReadOnlyList<OpcionP> Contactos => [.. Enum.GetValues<PokegearNumber>().Where(n => n <= PokegearNumber.Ernest).OrderBy(n => (int)n).Select(n => new OpcionP((int)n, NombreDeContacto(n)))];

    private static PestanaP Pokegear()
    {
        var cantidad = (int)PokegearNumber.Ernest + 1;
        var contactos = Enumerable.Range(0, cantidad).Select(i => (CampoP)P.Opcion($"contacto{i}", $"Contacto {i + 1}", c => (long)Hg(c).GetCallerAtIndex(i), (c, v) => Hg(c).SetCallerAtIndex(i, (PokegearNumber)v),
            _ => Contactos, titulo: $"Contacto {i + 1}", prioridad: i)).ToList();
        return new PestanaP("pokegear", L("Tab_PokeGear"), [new SeccionP(string.Empty, contactos)]);
    }

    private static PestanaP TodosLosContactos() => new("pokegear_todos", IdiomaInterfaz.F("Gen4.PestanaTodos", L("Tab_PokeGear")),
    [
        new SeccionP(string.Empty,
        [
            // B_GiveAll / B_GiveAllNoTrainers / B_DeleteAll: actuan sobre el guardado al momento y rehacen la lista (se descartan las ediciones sin guardar de la rejilla).
            P.Accion("registrar_todos", L("B_GiveAll"), c => Hg(c).PokeGearUnlockAllCallers(), inmediata: true, textoBoton: IdiomaInterfaz.T("VariosGen4Bolsas.Registrar"),
                confirmacion: IdiomaInterfaz.T("VariosGen4Bolsas.RegistranTodosContactosMenos"), ayuda: IdiomaInterfaz.T("VariosGen4Bolsas.RegistraTodosContactosPokegear")),
            P.Accion("registrar_importantes", L("B_GiveAllNoTrainers"), c => Hg(c).PokeGearUnlockAllCallersNoTrainers(), inmediata: true, textoBoton: IdiomaInterfaz.T("VariosGen4Bolsas.Registrar"),
                confirmacion: IdiomaInterfaz.T("VariosGen4Bolsas.RegistranSoloContactosSon"), ayuda: IdiomaInterfaz.T("VariosGen4Bolsas.RegistraSoloContactosImportantes")),
            P.Accion("borrar_todos", L("B_DeleteAll"), c => Hg(c).PokeGearClearAllCallers(), inmediata: true, textoBoton: IdiomaInterfaz.T("Seleccion.Borrar"),
                confirmacion: IdiomaInterfaz.T("VariosGen4Bolsas.QuitanTodosContactosPokegear"), ayuda: IdiomaInterfaz.T("VariosGen4Bolsas.QuitaTodosContactos")),
        ]),
    ]);
}
