// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Cajas;

/// <summary>Un grupo de casillas con titulo (equipo, guarderia, Caja de combate, GTS, fusion...), con las mismas casillas que PKHeX (<c>SAVEditor</c>) muestra en cada sitio.</summary>
/// <param name="Id">Identificador estable (pruebas, capturas y nombres accesibles).</param>
/// <param name="Nota">Texto de ayuda de la zona (por ejemplo «Solo lectura»).</param>
/// <param name="OcultaLegalidad">Las casillas de esta zona no se analizan (no llevan datos de entrenador: Subsuelo, cache de variocolor de Z-A).</param>
public sealed record GrupoCasillas(string Id, string Titulo, string? Nota, IReadOnlyList<ISlotInfo> Casillas, StorageSlotType Tipo, bool OcultaLegalidad = false)
{
    public int Columnas => Casillas.Count switch { <= 3 => Math.Max(1, Casillas.Count), 4 => 4, <= 6 => 3, _ => 5 };
}

/// <summary>Datos de una guarderia de la partida: experiencia ganada, huevo disponible y semilla (como el grupo «Guarderia» de PKHeX).</summary>
public sealed class GuarderiaInfo(IDaycareStorage almacen, int numero, int total)
{
    public IDaycareStorage Almacen { get; } = almacen;
    public int Numero { get; } = numero;
    public int Total { get; } = total;
    public int Casillas => Almacen.DaycareSlotCount;
    public bool Ocupada(int i) => Almacen.IsDaycareOccupied(i);

    public bool TieneExperiencia => Almacen is IDaycareExperience;
    public uint Experiencia(int i) => Almacen is IDaycareExperience e ? e.GetDaycareEXP(i) : 0;
    public void FijarExperiencia(int i, uint valor) { if (Almacen is IDaycareExperience e) e.SetDaycareEXP(i, valor); }

    public bool TieneHuevo => Almacen is IDaycareEggState;
    public bool HuevoDisponible { get => Almacen is IDaycareEggState h && h.IsEggAvailable; set { if (Almacen is IDaycareEggState h) h.IsEggAvailable = value; } }

    /// <summary>Digitos hexadecimales de la semilla (4, 8, 16 o 32), o 0 si la guarderia no la tiene.</summary>
    public int DigitosSemilla => Almacen switch
    {
        IDaycareRandomState<ushort> => 4,
        IDaycareRandomState<uint> => 8,
        IDaycareRandomState<ulong> => 16,
        IDaycareRandomState<UInt128> => 32,
        _ => 0,
    };

    public string Semilla => Almacen switch
    {
        IDaycareRandomState<ushort> s => $"{s.Seed:X4}",
        IDaycareRandomState<uint> s => $"{s.Seed:X8}",
        IDaycareRandomState<ulong> s => $"{s.Seed:X16}",
        IDaycareRandomState<UInt128> s => $"{s.Seed:X32}",
        _ => string.Empty,
    };

    /// <summary>Escribe la semilla (hexadecimal, exactamente los digitos del formato). Falso si el texto no es valido: el guardado no se toca.</summary>
    public bool FijarSemilla(string hex)
    {
        hex = hex.Trim();
        var d = DigitosSemilla;
        if (d == 0 || hex.Length != d || !hex.All(Uri.IsHexDigit)) return false;
        var h = System.Globalization.NumberStyles.HexNumber;
        switch (Almacen)
        {
            case IDaycareRandomState<ushort> s when ushort.TryParse(hex, h, null, out var v): s.Seed = v; return true;
            case IDaycareRandomState<uint> s when uint.TryParse(hex, h, null, out var v): s.Seed = v; return true;
            case IDaycareRandomState<ulong> s when ulong.TryParse(hex, h, null, out var v): s.Seed = v; return true;
            case IDaycareRandomState<UInt128> s when UInt128.TryParse(hex, h, null, out var v): s.Seed = v; return true;
            default: return false;
        }
    }

    public ISlotInfo Casilla(int i) => new SlotInfoMisc(Almacen.GetDaycareSlot(i), i) { Type = StorageSlotType.Daycare };
}

/// <summary>
/// Que zonas de almacenamiento tiene un guardado, calculadas igual que <c>SAVEditor.ToggleViewBox/Party/Daycare</c> y <c>GetExtraSlots</c> de PKHeX (lineas 1270-1330 del censo):
/// cajas (<c>HasBox</c>), equipo (<c>HasParty</c> y exportable), guarderias, Caja de combate, GTS, fusiones, acompanante, Pokewalker, Subsuelo, Poke Resort... y los equipos registrados de Stadium.
/// </summary>
public sealed class ZonasGuardado
{
    public bool TieneCajas { get; init; }
    public bool TieneEquipo { get; init; }
    public int CasillasEquipo { get; init; }
    public IReadOnlyList<GuarderiaInfo> Guarderias { get; init; } = [];
    public IReadOnlyList<GrupoCasillas> Otros { get; init; } = [];
    public IReadOnlyList<SlotGroup> EquiposStadium { get; init; } = [];
    public bool TieneOtros => Guarderias.Count > 0 || Otros.Count > 0 || EquiposStadium.Count > 0;

    /// <summary>Todos los grupos de «Otros» en el orden de pantalla (guarderias primero, como en la pestana «Otros» de PKHeX).</summary>
    public IReadOnlyList<GrupoCasillas> Grupos { get; init; } = [];

    public static ZonasGuardado De(SaveFile sav)
    {
        var exportable = sav.State.Exportable;
        var guarderias = new List<GuarderiaInfo>();
        var grupos = new List<GrupoCasillas>();
        if (exportable)
        {
            if (sav is IDaycareMulti m)
            {
                for (var i = 0; i < m.DaycareCount; i++) guarderias.Add(new GuarderiaInfo(m[i], i + 1, m.DaycareCount));
            }
            else if (sav is IDaycareStorage s) guarderias.Add(new GuarderiaInfo(s, 1, 1));

            foreach (var g in guarderias)
            {
                var casillas = new List<ISlotInfo>();
                for (var i = 0; i < g.Casillas; i++) casillas.Add(g.Casilla(i));
                grupos.Add(new GrupoCasillas($"guarderia{g.Numero}", g.Total > 1 ? IdiomaInterfaz.F("Zonas.Guarderia", g.Numero) : IdiomaInterfaz.T("Zonas.Guarderia2"), IdiomaInterfaz.T("Zonas.SoloLecturaCopiaPokemon"), casillas, StorageSlotType.Daycare));
            }

            // Los grupos de «ranuras extra» se agrupan por tipo, en el orden en que PKHeX los pone.
            var extras = sav.GetExtraSlots(true);
            foreach (var porTipo in extras.GroupBy(e => Agrupar(e.Type)))
            {
                var lista = porTipo.Cast<ISlotInfo>().ToList();
                var tipo = porTipo.First().Type;
                var editable = porTipo.Any(x => x.CanWriteTo(sav));
                var oculta = porTipo.Any(x => x.HideLegality);
                grupos.Add(new GrupoCasillas($"extra-{porTipo.Key}", Titulo(porTipo.Key), editable ? null : IdiomaInterfaz.T("Zonas.SoloLecturaCopiaPokemon"), lista, tipo, oculta));
            }
        }

        SlotGroup[] stadium = [];
        if (exportable && sav is SAV_STADIUM st)
        {
            try { stadium = st.GetRegisteredTeams(); } catch { stadium = []; }
        }

        return new ZonasGuardado
        {
            TieneCajas = sav.HasBox,
            TieneEquipo = sav.HasParty && exportable,
            CasillasEquipo = 6,
            Guarderias = guarderias,
            Otros = grupos.Where(g => !g.Id.StartsWith("guarderia", StringComparison.Ordinal)).ToList(),
            Grupos = grupos,
            EquiposStadium = stadium,
        };
    }

    /// <summary>Las cuatro fusiones (Kyurem, Necrozma sol y luna, Calyrex) forman un solo grupo «Fusión», como en la pestana de PKHeX.</summary>
    private static StorageSlotType Agrupar(StorageSlotType t) => t is StorageSlotType.FusedKyurem or StorageSlotType.FusedNecrozmaS or StorageSlotType.FusedNecrozmaM or StorageSlotType.FusedCalyrex ? StorageSlotType.Fused : t;

    public static string Titulo(StorageSlotType t) => t switch
    {
        StorageSlotType.Box => IdiomaInterfaz.T("Zonas.Cajas"),
        StorageSlotType.Party => IdiomaInterfaz.T("Form.Equipo"),
        StorageSlotType.BattleBox => IdiomaInterfaz.T("Zonas.CajaCombate"),
        StorageSlotType.Daycare => IdiomaInterfaz.T("Zonas.Guarderia2"),
        StorageSlotType.Scripted => IdiomaInterfaz.T("Zonas.RegalosEventosJuego"),
        StorageSlotType.GTS => "GTS",
        StorageSlotType.PGL => IdiomaInterfaz.T("Partida.Especifico.globallink.Titulo"),
        StorageSlotType.SurpriseTrade => IdiomaInterfaz.T("Zonas.IntercambioSorpresa"),
        StorageSlotType.Shiny => IdiomaInterfaz.T("Zonas.VariocoloresAvistados"),
        StorageSlotType.Underground => IdiomaInterfaz.T("Partida.Especifico.subsuelo.Titulo"),
        StorageSlotType.Fused => IdiomaInterfaz.T("Zonas.Fusion"),
        StorageSlotType.Resort => "Poké Resort",
        StorageSlotType.Ride => IdiomaInterfaz.T("Zonas.PokemonAcompanante"),
        StorageSlotType.BattleAgency => IdiomaInterfaz.T("Zonas.AgenciaCombate"),
        StorageSlotType.Pokéwalker => "Pokéwalker",
        _ => IdiomaInterfaz.T("Zonas.Otros"),
    };

    public static string TituloCasilla(StorageSlotType t, int indice) => t switch
    {
        StorageSlotType.FusedKyurem => IdiomaInterfaz.T("Zonas.FusionadoKyurem"),
        StorageSlotType.FusedNecrozmaS => IdiomaInterfaz.T("Zonas.FusionadoNecrozmaSolgaleo"),
        StorageSlotType.FusedNecrozmaM => IdiomaInterfaz.T("Zonas.FusionadoNecrozmaLunala"),
        StorageSlotType.FusedCalyrex => IdiomaInterfaz.T("Zonas.FusionadoCalyrex"),
        _ => $"{Titulo(t)} {indice + 1}",
    };
}
