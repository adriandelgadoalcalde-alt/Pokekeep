// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen5;

/// <summary>
/// PK-SAV-095 a PK-SAV-101 · <c>SAV_Misc5</c>, «Editor de Datos del Entrenador» de Negro/Blanco y Negro 2/Blanco 2. Las seis pestañas del original (<c>TAB_Main</c>, <c>TAB_Entralink</c>, <c>TAB_Forest</c>, <c>TAB_Subway</c>, <c>TAB_BWCityForest</c>, <c>TAB_Muscial</c>)
/// son diez en el móvil porque una pestaña es o una tabla, o una colección, o unas secciones: «Inicio» (Ticket Libertad y los errantes, solo Negro/Blanco), «Vuelo y llaves» (los destinos de vuelo y, en Negro 2/Blanco 2, el sistema de llaves; una tabla con
/// «Marcar todos» y «Marcar todas»), «Récords» (los 68 de 32 bits y los 100 de 16 bits), «Zona Nexo» (Nexárbol, Poderes Regalo y Festival), «Misiones del Festival» (las 45, solo Negro 2/Blanco 2), «Bosque» (zonas desbloqueadas y aleatorizar),
/// «Ranuras del Bosque» (los 530 Pokémon), «Metro» (marcas, partida actual y los ocho modos), «Bosque Blanco/Ciudad Negra» (importar y exportar, solo Negro/Blanco) y «Musical» (complementos).
/// Reglas calcadas de <c>Save()</c>: el formulario lee y escribe todo; la app solo lo que se cambia, salvo lo que el formulario deriva de otro dato y por eso se escribe en grupo (los «Está activa la partida» del Metro salen de «Pasado» /7 + 1; el récord y la
/// partida actual de un modo del Metro se guardan juntos; el nivel y la experiencia de la Zona Nexo; los cuatro datos de una misión del Festival forman un solo valor de 32 bits). Divergencias, a su vez, en el ADR 0062: lo que el formulario recorta al abrir
/// (<c>SetValueClamped</c>) o reescribe al guardar aunque no se toque; el récord de 32 bits se limita a 999.999.999 (el control llega a 4.294.967.295 pero el Core recorta al guardar); «Flag3» del Metro no tiene casilla conectada en PKHeX y se
/// escribe con el valor de «Flag7».
/// </summary>
internal static partial class Varios5
{
    public const string Form = "SAV_Misc5";
    public static readonly string[] Censo = ["PK-SAV-095", "PK-SAV-096", "PK-SAV-097", "PK-SAV-098", "PK-SAV-099", "PK-SAV-100", "PK-SAV-101"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SAV5 S(ContextoP c) => (SAV5)c.Sav;
    private static SAV5BW BW(ContextoP c) => (SAV5BW)c.Sav;
    private static SAV5B2W2 B2(ContextoP c) => (SAV5B2W2)c.Sav;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV5;

    /// <summary>Nombre de un lugar del juego en castellano (la lista de lugares del Core, <c>bw2_00000</c>).</summary>
    internal static string Lugar(ContextoP c, int indice) => indice >= 0 && indice < c.Textos.Gen5.Met0.Length ? c.Textos.Gen5.Met0[indice] : IdiomaInterfaz.F("Catalogo.Lugar", indice);

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (!Aplica(sav)) return null;
        var bw = sav is SAV5BW;
        var pestanas = new List<PestanaP>();
        if (bw) pestanas.Add(new("inicio", L("TAB_Main"), [.. Inicio()]));
        pestanas.Add(new("destinos", L("TAB_FlyKeys"), [], Tabla: DestinosYLlaves(sav)));
        pestanas.Add(new("records", L("TAB_Records"), [], Tabla: Records()));
        pestanas.Add(new("entralink", L("TAB_Entralink"), [.. Entralink(sav)]));
        if (!bw) pestanas.Add(new("misiones", L("GB_FunfestMissions"), [], Coleccion: Misiones()));
        pestanas.Add(new("bosque", L("TAB_Forest"), [.. AjustesDelBosque()]));
        pestanas.Add(new("ranuras", L("TAB_ForestSlots"), [], Coleccion: Ranuras()));
        pestanas.Add(new("metro", L("TAB_Subway"), [.. Metro()]));
        if (bw) pestanas.Add(new("ciudadbosque", L("TAB_BWCityForest"), [new SeccionP(string.Empty, [.. CiudadBosque()])]));
        pestanas.Add(new("musical", L("TAB_Muscial"), [], Tabla: Complementos()));
        return new EditorDefP("misc", IdiomaInterfaz.T("ModeloEventos.Varios"), pestanas, Censo, IdiomaInterfaz.T("Partida.Especifico.misc.Descripcion"));
    }
}
