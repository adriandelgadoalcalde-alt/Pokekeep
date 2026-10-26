// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen5;

/// <summary>
/// PK-SAV-092 a PK-SAV-094 · <c>SAV_Medals5</c>, las Insignias de Negro 2/Blanco 2 (<c>MedalList5</c>) y la lista de hábitats de la Caza (<c>HabitatList5</c>). Las dos pestañas del original (<c>Tab_Medals</c> y <c>Tab_Habitat</c>)
/// son cuatro en el móvil porque una pestaña es o una colección o unas secciones: «Ajustes» (insignia fijada, rango, tutorial y los botones de lote), «Insignias» (255 elementos: estado, sin leer y fecha de la rejilla),
/// «Hábitats» (90 elementos con completo, hierba, surf y pesca) y «Ajustes de hábitats» (tutoriales, los dos valores desconocidos, último tipo de encuentro y los lotes). Reglas calcadas: cambiar el estado de una insignia
/// que puede tener fecha y no la tiene (<c>CanHaveDate</c> sin <c>HasDate</c>) le pone la de hoy (<c>DGV_Medals_CellValueChanged</c>); la fecha es de 2000 a 2099 y solo se edita si la insignia puede tener fecha;
/// «Dar todas» (<c>GiveAll</c>) obtiene las que faltan y deja el rango en el máximo; «Marcar como completo» sin filas elegidas completa los 90 hábitats y «Limpiar» solo actúa sobre las filas elegidas (aquí: por hábitat y, para
/// todos a la vez, «Limpiar todos», que es elegir todas las filas). Las ediciones de la rejilla del original actúan al instante sobre la copia del guardado; aquí se aplican con «Aplicar» y solo lo que difiere de la partida
/// (un estado cambiado y devuelto a su valor antes de guardar no deja la fecha de hoy que el formulario escribió por el camino: ADR 0062).
/// </summary>
internal static class Insignias5
{
    public const string Form = "SAV_Medals5";
    public static readonly string[] Censo = ["PK-SAV-092", "PK-SAV-093", "PK-SAV-094"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static MedalList5 Lista(ContextoP c) => ((SAV5B2W2)c.Sav).Medals;
    private static HabitatList5 Habitats(ContextoP c) => Lista(c).HabitatList;

    private static string[] Nombres => Util.GetStringList("medals", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);
    private static string[] Tipos => Util.GetStringList("medal_types", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);
    private static readonly DateTime FechaMin = new(2000, 1, 1), FechaMax = new(2099, 12, 31);

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV5B2W2;

    private static string Estado(MedalState5 e) => TextosPkhex.Enum(e);

    private static IReadOnlyList<OpcionP> Fijables(ContextoP c)
    {
        // CB_PinnedMedal: «ninguna» (255, con el texto de la especie 0) y despues las 255 insignias por su nombre.
        var ninguna = c.Textos.specieslist.Length > 0 && c.Textos.specieslist[0].Trim().Length > 0 ? c.Textos.specieslist[0] : IdiomaInterfaz.T("Catalogo.Ninguna");
        return [new OpcionP(MedalList5.PinnedMedalNone, ninguna), .. Nombres.Select((n, i) => new OpcionP(i, n))];
    }

    private static IEnumerable<SeccionP> Ajustes()
    {
        yield return new SeccionP(string.Empty,
        [
            P.Opcion("fijada", L("L_PinnedMedal"), c => Lista(c).PinnedMedal, (c, v) => Lista(c).PinnedMedal = (byte)v, Fijables, titulo: L("L_PinnedMedal"), prioridad: 10),
            P.Opcion("rango", L("L_Rank"), c => (long)Lista(c).Rank, (c, v) => Lista(c).Rank = (MedalRank5)(int)v, _ => TextosPkhex.EnumLista<MedalRank5>(), titulo: L("L_Rank"), prioridad: 10),
            P.Marca("tutorial", L("CHK_TutorialComplete"), c => Lista(c).IsTutorialComplete, (c, v) => Lista(c).IsTutorialComplete = v, prioridad: 10),
        ]);
        yield return new SeccionP(L("Tab_Medals"),
        [
            P.Accion("dar-todas", L("B_GiveAll"), c => Lista(c).GiveAll(EncounterDate.GetDateNDS(), unread: true), textoBoton: L("B_GiveAll"), prioridad: -5,
                confirmacion: IdiomaInterfaz.T("Insignias5.ObtienenInsigniasFaltanFecha"), ayuda: IdiomaInterfaz.T("Insignias5.ObtieneInsigniasEstabanObtenidas")),
            P.Accion("calcular-rango", L("B_CalculateRank"), c => Lista(c).Rank = Lista(c).CalculateRank(), textoBoton: L("B_CalculateRank"), prioridad: 5,
                ayuda: IdiomaInterfaz.T("Insignias5.PoneRangoCorrespondeNumero")),
            P.Archivo("importar-todo", IdiomaInterfaz.T("Insignias5.ListaInsigniasMl5"),
                (c, b) => b.AsSpan().CopyTo(Lista(c).AllMedals),
                validar: (_, b) => b.Length == MedalList5.LengthAllMedals ? null : string.Format(AyudasGen5.MensajeCore("MsgFileSizeIncorrect"), b.Length, MedalList5.LengthAllMedals),
                exporta: c => (AyudasGen5.NombreLimpio($"{c.Sav.OT} {c.Sav.Version}.ml5"), Lista(c).AllMedals.ToArray()),
                extension: "ml5", textoBoton: L("B_ImportAll"), prioridad: -5, confirmacion: IdiomaInterfaz.T("Insignias5.SustituyenInsigniasArchivo"),
                ayuda: IdiomaInterfaz.T("Insignias5.ArchivoMl5BytesExportar")),
        ]);
    }

    private static IEnumerable<CampoP> CamposDeLaInsignia(ColeccionDefP col, int i)
    {
        string Id(string campo) => col.IdCampo(i, campo);
        var idEstado = Id("estado");
        // CanHaveDate segun el estado del BORRADOR: pista o obtenida; «lista para obtener» solo si ya tiene fecha.
        bool PuedeTenerFecha(ContextoP c) => c.Entero(idEstado) switch { (long)MedalState5.HintObtained or (long)MedalState5.Obtained => true, (long)MedalState5.ObtainReady => Lista(c)[i].HasDate, _ => false };
        yield return P.Info(Id("tipo"), L("DGV_MedalTypeColumn"), _ => Tipos[(int)MedalList5.GetMedalType(i)]);
        yield return P.Opcion(idEstado, L("DGV_MedalStateColumn"), c => (long)Lista(c)[i].State,
            (c, v) =>
            {
                var m = Lista(c)[i];
                m.State = (MedalState5)(int)v;
                if (m is { CanHaveDate: true, HasDate: false }) m.Date = EncounterDate.GetDateNDS();   // DGV_Medals_CellValueChanged
            },
            _ => TextosPkhex.EnumLista<MedalState5>(), titulo: L("DGV_MedalStateColumn"));
        yield return P.Marca(Id("sin-leer"), L("DGV_MedalUnreadColumn"), c => Lista(c)[i].IsUnread, (c, v) => { var m = Lista(c)[i]; m.IsUnread = v; });
        yield return new CampoP
        {
            Id = Id("fecha"), Etiqueta = L("DGV_MedalDateColumn"), Tipo = TipoP.FechaHora, SoloFecha = true, FechaMin = FechaMin, FechaMax = FechaMax, Prioridad = 1,
            Visible = PuedeTenerFecha,
            // La fecha cruda se lee aunque el estado actual no pueda tener fecha (el campo solo se ve cuando puede): el dato no cambia al cambiar de estado.
            Leer = c => Lista(c)[i] is { HasDate: true } m ? m.Date.ToDateTime(TimeOnly.MinValue) : AyudasGen5.HoyNds(),
            Escribir = (c, v) => { var m = Lista(c)[i]; m.Date = DateOnly.FromDateTime((DateTime)v!); },
        };
    }

    private static ColeccionDefP Insignias()
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "insignias", Elemento = "Insignia", Cantidad = _ => Nombres.Length, Titulo = (_, i) => Nombres[i],
            Resumen = (c, i) => $"{Estado(Lista(c)[i].State)} · {Tipos[(int)MedalList5.GetMedalType(i)]}",
            Secciones = (_, i) => [new SeccionP(string.Empty, [.. CamposDeLaInsignia(col!, i)])],
        };
        return col;
    }

    private static string Resumen(HabitatStatus5 h) => h.IsComplete ? TextosPkhex.Enum(HabitatCompletion5.Complete)
        : $"{L("DGV_HabitatGrassColumn")}: {TextosPkhex.Enum(h.Grass)} · {L("DGV_HabitatSurfColumn")}: {TextosPkhex.Enum(h.Surf)} · {L("DGV_HabitatFishColumn")}: {TextosPkhex.Enum(h.Fish)}";

    private static ColeccionDefP HabitatsDef()
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "habitats", Elemento = IdiomaInterfaz.T("Insignias5.Habitat"), Cantidad = _ => HabitatList5.HabitatCount, Titulo = (_, i) => IdiomaInterfaz.F("Insignias5.Habitat2", i + 1),
            Resumen = (c, i) => Resumen(Habitats(c).GetHabitat(i)),
            Secciones = (_, i) =>
            [
                new SeccionP(string.Empty,
                [
                    P.Marca(col!.IdCampo(i, "completo"), L("DGV_HabitatCompleteColumn"), c => Habitats(c).GetHabitat(i).IsComplete, (c, v) => { var h = Habitats(c).GetHabitat(i); h.IsComplete = v; }),
                    P.Opcion(col.IdCampo(i, "hierba"), L("DGV_HabitatGrassColumn"), c => (long)Habitats(c).GetHabitat(i).Grass, (c, v) => { var h = Habitats(c).GetHabitat(i); h.Grass = (HabitatCompletion5)(int)v; }, _ => TextosPkhex.EnumLista<HabitatCompletion5>(), titulo: L("DGV_HabitatGrassColumn")),
                    P.Opcion(col.IdCampo(i, "surf"), L("DGV_HabitatSurfColumn"), c => (long)Habitats(c).GetHabitat(i).Surf, (c, v) => { var h = Habitats(c).GetHabitat(i); h.Surf = (HabitatCompletion5)(int)v; }, _ => TextosPkhex.EnumLista<HabitatCompletion5>(), titulo: L("DGV_HabitatSurfColumn")),
                    P.Opcion(col.IdCampo(i, "pesca"), L("DGV_HabitatFishColumn"), c => (long)Habitats(c).GetHabitat(i).Fish, (c, v) => { var h = Habitats(c).GetHabitat(i); h.Fish = (HabitatCompletion5)(int)v; }, _ => TextosPkhex.EnumLista<HabitatCompletion5>(), titulo: L("DGV_HabitatFishColumn")),
                ]),
                new SeccionP(string.Empty,
                [
                    P.Accion(col.IdCampo(i, "completar"), L("B_HabitatSetComplete"), c => Habitats(c).GetHabitat(i).SetComplete(), prioridad: -3, textoBoton: L("B_HabitatSetComplete"), ayuda: IdiomaInterfaz.T("Insignias5.CompletoHierbaSurfPesca")),
                    P.Accion(col.IdCampo(i, "limpiar"), L("B_HabitatClear"), c => Habitats(c).GetHabitat(i).Clear(), prioridad: -3, textoBoton: L("B_HabitatClear"), ayuda: IdiomaInterfaz.T("Insignias5.DejaTodoCero")),
                ]),
            ],
        };
        return col;
    }

    private static IEnumerable<SeccionP> AjustesDeHabitats()
    {
        yield return new SeccionP(string.Empty,
        [
            P.Marca("tutorial-visto", L("CHK_HabitatTutorialViewed"), c => Habitats(c).IsTutorialViewed, (c, v) => Habitats(c).IsTutorialViewed = v),
            P.Marca("tutorial-captura", L("CHK_HabitatTutorialCompleteCapture"), c => Habitats(c).IsTutorialCompleteCapture, (c, v) => Habitats(c).IsTutorialCompleteCapture = v),
            P.Entero("desconocido90", L("L_Unknown90"), c => Habitats(c).Unknown90, (c, v) => Habitats(c).Unknown90 = (ushort)v, 0, ushort.MaxValue),
            P.Entero("desconocido92", L("L_Unknown92"), c => Habitats(c).Unknown92, (c, v) => Habitats(c).Unknown92 = (byte)v, 0, byte.MaxValue),
            P.Opcion("ultimo-encuentro", L("L_LastEncounterType"), c => (long)Habitats(c).LastEncounterType, (c, v) => Habitats(c).LastEncounterType = (HabitatEncounterType5)(int)v, _ => TextosPkhex.EnumLista<HabitatEncounterType5>(), titulo: L("L_LastEncounterType")),
        ]);
        yield return new SeccionP(L("Tab_Habitat"),
        [
            P.Accion("completar-todos", L("B_HabitatSetCompleteAll"), c => Habitats(c).CompleteAll(), prioridad: -3, textoBoton: L("B_HabitatSetCompleteAll"),
                confirmacion: IdiomaInterfaz.T("Insignias5.CompletanHabitats"), ayuda: IdiomaInterfaz.T("Insignias5.MarcarCompletoPKHeXFilas")),
            P.Accion("limpiar-todos", L("B_HabitatClearAll"), c => { for (var i = 0; i < HabitatList5.HabitatCount; i++) Habitats(c).GetHabitat(i).Clear(); }, prioridad: -3, textoBoton: L("B_HabitatClearAll"),
                confirmacion: IdiomaInterfaz.T("Insignias5.DejanCeroHabitats"), ayuda: IdiomaInterfaz.T("Insignias5.LimpiarPKHeXTodasFilas")),
        ]);
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (!Aplica(sav)) return null;
        var pestanas = new List<PestanaP>
        {
            new("ajustes", L("Tab_Settings"), [.. Ajustes()]),
            new("insignias", L("Tab_Medals"), [], Coleccion: Insignias()),
            new("habitats", L("Tab_Habitat"), [], Coleccion: HabitatsDef()),
            new("habitats-ajustes", L("Tab_HabitatSettings"), [.. AjustesDeHabitats()]),
        };
        return new EditorDefP("medallas5", IdiomaInterfaz.T("Partida.Especifico.medallas5.Titulo"), pestanas, Censo, IdiomaInterfaz.T("Partida.Especifico.medallas5.Descripcion"));
    }
}
