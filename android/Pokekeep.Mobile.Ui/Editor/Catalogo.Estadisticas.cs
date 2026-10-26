// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Editor;

internal static partial class Catalogo
{
    /// <summary>Indices de estadistica en el orden interno del Core: 0 PS, 1 Ataque, 2 Defensa, 3 Velocidad, 4 At. especial, 5 Def. especial.</summary>
    internal static string[] NombresStats => [IdiomaInterfaz.T("Informe.Columna.HP"), IdiomaInterfaz.T("Informe.Columna.ATK"), IdiomaInterfaz.T("Informe.Columna.DEF"), IdiomaInterfaz.T("Informe.Columna.SPE"), IdiomaInterfaz.T("Catalogo.AtEspecial"), IdiomaInterfaz.T("Catalogo.DefEspecial")];

    /// <summary>Orden de pantalla. Desde la 2.ª generacion: PS, Ataque, Defensa, At. especial, Def. especial, Velocidad. En la 1.ª, Velocidad va antes de Especial y Def. especial no existe (StatEditor.SetStatOrder).</summary>
    internal static IReadOnlyList<int> OrdenStats(PKM pk) => pk.Format == 1 ? [0, 1, 2, 3, 4] : [0, 1, 2, 4, 5, 3];

    internal static string NombreStat(PKM pk, int i) => pk.Format == 1 && i == 4 ? IdiomaInterfaz.T("Catalogo.Especial") : NombresStats[i];

    private static bool TieneAv(ContextoEditor c) => c.Pk is IAwakened;
    private static bool TieneGv(ContextoEditor c) => c.Pk is IGanbaru;
    private static bool MuestraEv(ContextoEditor c) => c.Pk is not IAwakened || c.Avanzado;
    private static bool StatVisible(ContextoEditor c, int i) => !(c.Formato == 1 && i == 5);

    internal static PestanaDef Estadisticas()
    {
        var iv = Enumerable.Range(0, 6).Select(i =>
            F.Entero("PK-ED-044", $"{NombresStats[i]}, IV", c => c.Pk.GetIV(i), (c, v) => Reglas.PonerIv(c.Pk, i, (int)v), 0, 31, visible: c => StatVisible(c, i),
                motivo: c => c.Formato <= 2 && i == 0 ? IdiomaInterfaz.T("Catalogo.DVPSSaleOtros") : c.Formato <= 2 && i == 5 ? IdiomaInterfaz.T("Catalogo.GeneracionDefEspecialUsa") : null, maxDinamico: c => c.Pk.MaxIV, ayuda: IdiomaInterfaz.T("Catalogo.ValorIndividual")) with { Grupo = "iv", Indice = i, AfectaStats = true });
        var ev = Enumerable.Range(0, 6).Select(i =>
            F.Entero("PK-ED-045", $"{NombresStats[i]}, EV", c => c.Pk.GetEV(i), (c, v) => Reglas.PonerEv(c.Pk, i, (int)v), 0, 255, visible: c => StatVisible(c, i) && MuestraEv(c),
                maxDinamico: c => c.Pk.MaxEV, motivo: c => c.Formato <= 2 && i == 5 ? IdiomaInterfaz.T("Catalogo.GeneracionDefEspecialUsa2") : null, ayuda: IdiomaInterfaz.T("Catalogo.PuntosEsfuerzo")) with { Grupo = "ev", Indice = i, AfectaStats = true });
        var av = Enumerable.Range(0, 6).Select(i =>
            F.Entero("PK-ED-046", $"{NombresStats[i]}, AV", c => ((IAwakened)c.Pk).GetAV(i), (c, v) => ((IAwakened)c.Pk).SetAV(i, (byte)v), 0, AwakeningUtil.AwakeningMax, visible: c => TieneAv(c),
                ayuda: IdiomaInterfaz.T("Catalogo.ValorDespertar")) with { Grupo = "av", Indice = i, AfectaStats = true });
        var gv = Enumerable.Range(0, 6).Select(i =>
            F.Entero("PK-ED-047", $"{NombresStats[i]}, GV", c => ((IGanbaru)c.Pk).GetGV(i), (c, v) => ((IGanbaru)c.Pk).SetGV(i, (byte)v), 0, GanbaruExtensions.TrueMax, visible: c => TieneGv(c),
                ayuda: IdiomaInterfaz.T("Catalogo.ValorGanbaru")) with { Grupo = "gv", Indice = i, AfectaStats = true });
        var ht = Enumerable.Range(0, 6).Select(i =>
            F.Casilla("PK-ED-119", $"{NombresStats[i]}, hiperentrenado", c => ((IHyperTrain)c.Pk).IsHyperTrained(i), (c, v) => { var h = (IHyperTrain)c.Pk; if (h.IsHyperTrained(i) != v) h.HyperTrainInvert(i); return null; },
                visible: c => c.Pk is IHyperTrain && StatVisible(c, i)) with { Grupo = "ht", Indice = i, AfectaStats = true });
        var final = Enumerable.Range(0, 6).Select(i =>
            F.Entero("PK-ED-048", IdiomaInterfaz.F("Catalogo.ValorFinal", NombresStats[i]), c => StatFinal(c, i), (c, v) => EscribirStatFinal(c.Pk, i, (int)v), 0, ushort.MaxValue, visible: c => StatVisible(c, i),
                motivo: c => c.StatsManuales ? null : IdiomaInterfaz.T("Catalogo.CalculadaCoreNivelIV"), ayuda: IdiomaInterfaz.T("Catalogo.EstadisticaCombate")) with { Grupo = "final", Indice = i });
        var baseStat = Enumerable.Range(0, 6).Select(i =>
            F.Info("PK-ED-050", $"{NombresStats[i]}, base", c => c.Pk.PersonalInfo.GetBaseStatValue(i).ToString(), visible: c => StatVisible(c, i)) with { Grupo = "base", Indice = i });

        return new("estadisticas", IdiomaInterfaz.T("Form.Estadisticas"),
        [
            new(IdiomaInterfaz.T("Form.Estadisticas"), [.. iv, .. ev, .. av, .. gv, .. ht, .. final, .. baseStat]),
            new(IdiomaInterfaz.T("Catalogo.TotalesDatos"),
            [
                F.Info("PK-ED-050", IdiomaInterfaz.T("Catalogo.TotalEstadisticasBase"), c => c.Pk.PersonalInfo.GetBaseStatTotal().ToString()) with { Indice = 6 },
                F.Info("PK-ED-051", IdiomaInterfaz.T("Catalogo.IVTotales"), c => c.Pk.IVTotal.ToString(), visible: c => c.Formato >= 3),
                F.Info("PK-ED-051", IdiomaInterfaz.T("Catalogo.EVTotales"), c => c.Pk.EVTotal.ToString(), visible: c => c.Formato >= 3 && MuestraEv(c)) with { Indice = 1 },
                F.Info("PK-ED-051", IdiomaInterfaz.T("Catalogo.AVTotales"), c => ((IAwakened)c.Pk).AwakeningSum().ToString(), visible: c => c.Pk is IAwakened) with { Indice = 2 },
                F.Info("PK-ED-052", IdiomaInterfaz.T("Catalogo.PotencialIV"), c => c.Pk.GetPotentialString(true)),
                F.Info("PK-ED-054", IdiomaInterfaz.T("Catalogo.Caracteristica"), c => c.F.Textos.characteristics[c.Pk.Characteristic], visible: c => c.Formato >= 3 && c.Pk.Characteristic > -1),
                F.Opcion("PK-ED-053", IdiomaInterfaz.T("Informe.Columna.HP_Type"), c => c.F.Tipos, c => c.Pk.HPType, (c, id) => { PonerTipoPoderOculto(c.Pk, id); return null; },
                    visible: c => c.Formato <= 7 || c.Pk is PB8, ayuda: IdiomaInterfaz.T("Catalogo.CambiarTipoAjustaIV")) with { AfectaStats = true },
                F.Info("PK-ED-053", IdiomaInterfaz.T("Catalogo.PotenciaPoderOculto"), c => c.Pk.HPPower.ToString(), visible: c => c.Formato <= 5) with { Indice = 1 },
            ]),
            new(IdiomaInterfaz.T("Catalogo.AleatorizarAjustar"),
            [
                F.Accion("PK-ED-055", IdiomaInterfaz.T("Catalogo.IVAleatorios"), c => IvsAleatorios(c.Pk), ayuda: IdiomaInterfaz.T("Catalogo.SorteaIVRespetandoEncuentro")) with { AfectaStats = true },
                F.Accion("PK-ED-055", IdiomaInterfaz.T("Catalogo.IVMaximo"), c => { c.Pk.SetIVs(Enumerable.Repeat(c.Pk.MaxIV, 6).ToArray()); if (c.Pk is IGanbaru g) g.SetSuggestedGanbaruValues(c.Pk); }, ayuda: IdiomaInterfaz.T("Catalogo.PoneTodosIVMaximo")) with { Indice = 1, AfectaStats = true },
                F.Accion("PK-ED-055", IdiomaInterfaz.T("Catalogo.IVCero"), c => { c.Pk.SetIVs(new int[6]); if (c.Pk is IGanbaru g) g.ClearGanbaruValues(); }, ayuda: IdiomaInterfaz.T("Catalogo.PoneTodosIVCero")) with { Indice = 2, AfectaStats = true },
                F.Accion("PK-ED-055", IdiomaInterfaz.T("Catalogo.EVAleatorios"), c => EvsAleatorios(c.Pk), visible: MuestraEv, ayuda: IdiomaInterfaz.T("Catalogo.ReparteEVAzarDentro")) with { Indice = 3, AfectaStats = true },
                F.Accion("PK-ED-055", IdiomaInterfaz.T("Catalogo.EVMaximo"), c => EvsMaximos(c.Pk), visible: MuestraEv, ayuda: IdiomaInterfaz.T("Catalogo.ReparteMaximoEV")) with { Indice = 4, AfectaStats = true },
                F.Accion("PK-ED-055", IdiomaInterfaz.T("Catalogo.EVCero"), c => c.Pk.SetEVs(new int[6]), visible: MuestraEv, ayuda: IdiomaInterfaz.T("Catalogo.PoneTodosEVCero")) with { Indice = 5, AfectaStats = true },
                F.Accion("PK-ED-046", IdiomaInterfaz.T("Catalogo.AVAleatorios"), c => ((IAwakened)c.Pk).AwakeningSetRandom(), visible: TieneAv, ayuda: IdiomaInterfaz.T("Catalogo.ValoresDespertarAzar")) with { Indice = 6, AfectaStats = true },
                F.Accion("PK-ED-046", IdiomaInterfaz.T("Catalogo.AVSugeridos"), c => ((IAwakened)c.Pk).SetSuggestedAwakenedValues(c.Pk), visible: TieneAv, ayuda: IdiomaInterfaz.T("Catalogo.ValoresDespertarSugiereCore")) with { Indice = 7, AfectaStats = true },
                F.Accion("PK-ED-046", IdiomaInterfaz.T("Catalogo.AVMinimo"), c => ((IAwakened)c.Pk).AwakeningMinimum(), visible: TieneAv, ayuda: IdiomaInterfaz.T("Catalogo.ValoresDespertarMinimos")) with { Indice = 8, AfectaStats = true },
                F.Accion("PK-ED-047", IdiomaInterfaz.T("Catalogo.GVSugeridos"), c => ((IGanbaru)c.Pk).SetSuggestedGanbaruValues(c.Pk), visible: TieneGv, ayuda: IdiomaInterfaz.T("Catalogo.ValoresGanbaruSugiereCore")) with { Indice = 9, AfectaStats = true },
                F.Accion("PK-ED-047", IdiomaInterfaz.T("Catalogo.GVCero"), c => ((IGanbaru)c.Pk).ClearGanbaruValues(), visible: TieneGv) with { Indice = 10, AfectaStats = true },
                F.Accion("PK-ED-044", IdiomaInterfaz.T("Catalogo.HiperentrenarTodos"), c => { if (c.Pk is IHyperTrain h) { h.HyperTrainFlags = 0x3F; } }, visible: c => c.Pk is IHyperTrain, ayuda: IdiomaInterfaz.T("Catalogo.MarcaSeisIVHiperentrenados")) with { Indice = 11, AfectaStats = true },
                F.Accion("PK-ED-044", IdiomaInterfaz.T("Catalogo.QuitarHiperentrenamiento"), c => { if (c.Pk is IHyperTrain h) h.HyperTrainClear(); }, visible: c => c.Pk is IHyperTrain) with { Indice = 12, AfectaStats = true },
            ]),
            new(IdiomaInterfaz.T("Catalogo.NaturalezaEstadistica"),
            [
                F.Opcion("PK-ED-118", IdiomaInterfaz.T("Catalogo.SubirEstadistica"), c => OpcionesSubirBajar(c), c => -1, (c, id) => CambiarAmplificacion(c, id, NatureAmpRequest.Increase), visible: c => c.Formato >= 3, leer: c => IdiomaInterfaz.T("Catalogo.EligeEstadisticaSube"), ayuda: IdiomaInterfaz.T("Catalogo.CambiaNaturalezaSubaEstadistica")) with { AfectaStats = true, SoloAccion = true },
                F.Opcion("PK-ED-118", IdiomaInterfaz.T("Catalogo.BajarEstadistica"), c => OpcionesSubirBajar(c), c => -1, (c, id) => CambiarAmplificacion(c, id, NatureAmpRequest.Decrease), visible: c => c.Formato >= 3, leer: c => IdiomaInterfaz.T("Catalogo.EligeEstadisticaBaja"), ayuda: IdiomaInterfaz.T("Catalogo.CambiaNaturalezaBajeEstadistica")) with { Indice = 1, AfectaStats = true, SoloAccion = true },
                F.Opcion("PK-ED-118", IdiomaInterfaz.T("Catalogo.DejarNeutraEstadistica"), c => OpcionesSubirBajar(c), c => -1, (c, id) => CambiarAmplificacion(c, id, NatureAmpRequest.Neutral), visible: c => c.Formato >= 3, leer: c => IdiomaInterfaz.T("Catalogo.EligeEstadisticaCambia"), ayuda: IdiomaInterfaz.T("Catalogo.NaturalezaNeutraEstadistica")) with { Indice = 2, AfectaStats = true, SoloAccion = true },
            ]),
            new(IdiomaInterfaz.T("Catalogo.FormatoDynamaxLeyendas"),
            [
                F.Entero("PK-ED-056", IdiomaInterfaz.T("Catalogo.NivelDynamax"), c => ((IDynamaxLevel)c.Pk).DynamaxLevel, (c, v) => ((IDynamaxLevel)c.Pk).DynamaxLevel = (byte)v, 0, 10, visible: c => c.Formato == 8 && c.Pk is IDynamaxLevel, ayuda: IdiomaInterfaz.T("CatalogoEstadisticas.DeA")),
                F.Casilla("PK-ED-056", IdiomaInterfaz.T("Libreria.Vista.11"), c => ((IGigantamax)c.Pk).CanGigantamax, (c, v) => { ((IGigantamax)c.Pk).CanGigantamax = v; return null; }, visible: c => c.Formato == 8 && c.Pk is IGigantamax) with { Indice = 1 },
                F.Casilla("PK-ED-057", IdiomaInterfaz.T("Catalogo.Alfa"), c => ((IAlpha)c.Pk).IsAlpha, (c, v) => { ((IAlpha)c.Pk).IsAlpha = v; return null; }, visible: c => c.Pk is IAlpha),
                F.Casilla("PK-ED-057", "Noble", c => ((INoble)c.Pk).IsNoble, (c, v) => { ((INoble)c.Pk).IsNoble = v; return null; }, visible: c => c.Pk is PA8) with { Indice = 1 },
            ]),
            new(IdiomaInterfaz.T("Catalogo.Teratipo"),
            [
                F.Opcion("PK-ED-058", IdiomaInterfaz.T("Catalogo.TeratipoOriginal"), c => c.F.TiposTera, c => (int)((ITeraType)c.Pk).TeraTypeOriginal, (c, id) => { ((ITeraType)c.Pk).TeraTypeOriginal = (MoveType)id; return null; }, visible: c => c.Pk is ITeraType),
                F.Opcion("PK-ED-058", IdiomaInterfaz.T("Catalogo.TeratipoForzado"), c => c.F.TiposTera, c => (int)((ITeraType)c.Pk).TeraTypeOverride, (c, id) => { ((ITeraType)c.Pk).TeraTypeOverride = (MoveType)id; return null; }, visible: c => c.Pk is ITeraType) with { Indice = 1 },
                F.Info("PK-ED-117", IdiomaInterfaz.T("Catalogo.TeratipoEfectivo"), c => c.F.Textos.types[TeraEfectivo(c)], visible: c => c.Pk is ITeraType) with { Indice = 2 },
                F.Accion("PK-ED-117", IdiomaInterfaz.T("Catalogo.TeratipoOriginalTipo"), c => PonerTeraDeEspecie(c, 1), visible: c => c.Pk is ITeraType, leer: c => c.F.Textos.types[c.Pk.PersonalInfo.Type1], ayuda: IdiomaInterfaz.T("Catalogo.UsaPrimerTipoEspecie")) with { Indice = 3 },
                F.Accion("PK-ED-117", IdiomaInterfaz.T("Catalogo.TeratipoOriginalTipo2"), c => PonerTeraDeEspecie(c, 2), visible: c => c.Pk is ITeraType && c.Pk.PersonalInfo.Type2 != c.Pk.PersonalInfo.Type1, leer: c => c.F.Textos.types[c.Pk.PersonalInfo.Type2], ayuda: IdiomaInterfaz.T("Catalogo.UsaSegundoTipoEspecie")) with { Indice = 4 },
                F.Accion("PK-ED-117", IdiomaInterfaz.T("Catalogo.QuitarTeratipoForzado"), c => ((ITeraType)c.Pk).TeraTypeOverride = (MoveType)TeraTypeUtil.OverrideNone, visible: c => c.Pk is ITeraType) with { Indice = 5 },
            ]),
            new(IdiomaInterfaz.T("Catalogo.EstadisticasEditadasMano"),
            [
                F.Casilla("PK-ED-049", IdiomaInterfaz.T("Catalogo.EditarEstadisticasFinalesMano"), c => c.StatsManuales, (c, v) => { c.StatsManuales = v; if (!v) Reglas.RecalcularStats(c.Pk); return null; }, avanzado: true,
                    ayuda: IdiomaInterfaz.T("Catalogo.ModoAvanzadoPermiteEscribir")),
            ], c => c.Avanzado),
        ]);
    }

    // ---- Auxiliares ----
    private static int StatFinal(ContextoEditor c, int i)
    {
        var pk = c.Pk;
        if (c.StatsManuales)
            return i switch { 0 => pk.Stat_HPMax, 1 => pk.Stat_ATK, 2 => pk.Stat_DEF, 3 => pk.Stat_SPE, 4 => pk.Stat_SPA, _ => pk.Stat_SPD };
        return pk.GetStats(pk.PersonalInfo)[i];
    }

    private static void EscribirStatFinal(PKM pk, int i, int v)
    {
        switch (i)
        {
            case 0: pk.Stat_HPMax = v; pk.Stat_HPCurrent = v; break;
            case 1: pk.Stat_ATK = v; break;
            case 2: pk.Stat_DEF = v; break;
            case 3: pk.Stat_SPE = v; break;
            case 4: pk.Stat_SPA = v; break;
            default: pk.Stat_SPD = v; break;
        }
    }

    private static void PonerTipoPoderOculto(PKM pk, int tipo)
    {
        Span<int> ivs = stackalloc int[6];
        pk.GetIVs(ivs);
        if (AjustesEditor.HiddenPowerOnChangeMaxPower) ivs.Fill(pk.MaxIV);   // StatEditor.UpdateHPType: con el ajuste, IV al máximo; sin él, los más cercanos a los actuales
        HiddenPower.SetIVs(tipo, ivs, pk.Context);
        pk.SetIVs(ivs);
    }

    private static IReadOnlyList<OpcionCampo> OpcionesSubirBajar(ContextoEditor c)
        => [.. Enumerable.Range(0, 5).Select(i => new OpcionCampo(i, c.F.Textos.natures.Length > 0 ? EstadisticaDeNaturaleza(c, i) : i.ToString()))];

    /// <summary>El Core numera las estadisticas de la naturaleza como Ataque 0, Defensa 1, Velocidad 2, At. esp. 3, Def. esp. 4.</summary>
    private static string EstadisticaDeNaturaleza(ContextoEditor c, int i) => i switch { 0 => IdiomaInterfaz.T("Informe.Columna.ATK"), 1 => IdiomaInterfaz.T("Informe.Columna.DEF"), 2 => IdiomaInterfaz.T("Informe.Columna.SPE"), 3 => IdiomaInterfaz.T("Catalogo.AtEspecial"), _ => IdiomaInterfaz.T("Catalogo.DefEspecial") };

    private static string? CambiarAmplificacion(ContextoEditor c, int indiceStat, NatureAmpRequest peticion)
    {
        var actual = c.Pk.Format >= 8 ? c.Pk.StatAlignment : c.Pk.Nature;
        var nueva = peticion.GetNewNature(indiceStat, actual);
        if (nueva == Nature.Random) return IdiomaInterfaz.T("Catalogo.EstadisticaAsiNaturalezaActual");
        if (c.Pk.Format >= 8) { c.Pk.StatAlignment = nueva; return null; }
        return Reglas.PonerNaturaleza(c.Pk, nueva);
    }

    private static int TeraEfectivo(ContextoEditor c)
    {
        var t = (ITeraType)c.Pk;
        var tipo = TeraTypeUtil.GetTeraType((byte)t.TeraTypeOriginal, (byte)t.TeraTypeOverride);
        var i = (int)tipo;
        return i == TeraTypeUtil.Stellar ? TeraTypeUtil.StellarTypeDisplayStringIndex : Math.Clamp(i, 0, c.F.Textos.types.Length - 1);
    }

    private static void PonerTeraDeEspecie(ContextoEditor c, int cual)
    {
        var pi = c.Pk.PersonalInfo;
        var tipo = cual == 1 ? pi.Type1 : pi.Type2;
        var t = (ITeraType)c.Pk;
        t.TeraTypeOriginal = (MoveType)tipo;
        t.TeraTypeOverride = (MoveType)TeraTypeUtil.OverrideNone;
    }

    /// <summary>IV aleatorios respetando lo que el encuentro exige (StatEditor.UpdateRandomIVs): IV perfectos garantizados o fijos del encuentro.</summary>
    internal static void IvsAleatorios(PKM pk)
    {
        Span<int> ivs = stackalloc int[6];
        LegalityAnalysis la;
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo) la = new LegalityAnalysis(pk);
        var enc = la.EncounterMatch;
        if (enc is IFlawlessIVCount { FlawlessIVCount: not 0 } fc) pk.SetRandomIVs(ivs, fc.FlawlessIVCount);
        else if (enc is IFixedIVSet { IVs: { IsSpecified: true } iv }) pk.SetRandomIVs(ivs, iv);
        else if (enc is IFlawlessIVCountConditional cc && cc.GetFlawlessIVCount(pk) is { Max: not 0 } x) pk.SetRandomIVs(ivs, Util.Rand.Next(x.Min, x.Max + 1));
        else pk.SetRandomIVs(ivs);
        if (pk is IGanbaru g) { pk.SetIVs(ivs); g.ClearGanbaruValues(); }
    }

    internal static void EvsAleatorios(PKM pk)
    {
        Span<int> v = stackalloc int[6];
        EffortValues.SetRandom(v, pk.Format);
        pk.SetEVs(v);
    }

    internal static void EvsMaximos(PKM pk)
    {
        Span<int> v = stackalloc int[6];
        EffortValues.SetMax(v, pk);
        pk.SetEVs(v);
    }
}
