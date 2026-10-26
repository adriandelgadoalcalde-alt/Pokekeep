// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Editor;

internal static partial class Catalogo
{
    private static string NombreMarca(PKM pk, int i) => i switch
    {
        0 => IdiomaInterfaz.T("Catalogo.Circulo"),
        1 => pk.Format == 3 ? IdiomaInterfaz.T("Catalogo.Cuadrado2") : IdiomaInterfaz.T("Catalogo.Triangulo"),   // la 3.ª generacion tiene el cuadrado en segundo lugar
        2 => pk.Format == 3 ? IdiomaInterfaz.T("Catalogo.Triangulo") : IdiomaInterfaz.T("Catalogo.Cuadrado2"),
        3 => IdiomaInterfaz.T("Catalogo.Corazon"),
        4 => IdiomaInterfaz.T("Catalogo.Estrella2"),
        _ => IdiomaInterfaz.T("Catalogo.MarcaRombo"),
    };

    private static OpcionCampo[] ColoresMarca => [new(0, IdiomaInterfaz.T("Catalogo.Marca")), new(1, IdiomaInterfaz.T("Catalogo.Azul")), new(2, IdiomaInterfaz.T("Catalogo.Rosa"))];

    internal static PestanaDef Cosmetico()
    {
        var marcasBool = Enumerable.Range(0, 6).Select(i =>
            F.Casilla("PK-ED-069", IdiomaInterfaz.F("Catalogo.Marca2", i + 1), c => ((IAppliedMarkings<bool>)c.Pk).GetMarking(i), (c, v) => { ((IAppliedMarkings<bool>)c.Pk).SetMarking(i, v); return null; },
                visible: c => c.Pk is IAppliedMarkings<bool> m && i < m.MarkingCount) with { EtiquetaFn = c => NombreMarca(c.Pk, i), Grupo = "marca", Indice = i });
        var marcasColor = Enumerable.Range(0, 6).Select(i =>
            F.Opcion("PK-ED-069", IdiomaInterfaz.F("Catalogo.Marca2", i + 1), c => ColoresMarca, c => (int)((IAppliedMarkings<MarkingColor>)c.Pk).GetMarking(i), (c, id) => { ((IAppliedMarkings<MarkingColor>)c.Pk).SetMarking(i, (MarkingColor)id); return null; },
                visible: c => c.Pk is IAppliedMarkings<MarkingColor> m && i < m.MarkingCount) with { EtiquetaFn = c => NombreMarca(c.Pk, i), Grupo = "marca", Indice = 10 + i });

        return new("cosmetico", IdiomaInterfaz.T("CatalogoCosmetico.Cosmetico"),
        [
            new(IdiomaInterfaz.T("Catalogo.Marcas"),
            [
                .. marcasBool, .. marcasColor,
                F.Accion("PK-ED-069", IdiomaInterfaz.T("Catalogo.MarcarSegunIV"), c => c.Pk.SetMarkings(), visible: c => c.Pk is IAppliedMarkings { MarkingCount: 6 }, ayuda: IdiomaInterfaz.T("Catalogo.MarcaIVPerfectosHace")) with { Indice = 20 },
                F.Info("PK-ED-070", IdiomaInterfaz.T("Catalogo.MarcaVariocolor"), c => c.Pk.IsShiny ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No")) with { Indice = 21 },
                F.Casilla("PK-ED-070", IdiomaInterfaz.T("Catalogo.MarcaPokerusCurado"), c => c.Pk.IsPokerusCured, (c, v) => { Reglas.PonerCurado(c.Pk, v); return null; }, visible: c => c.Formato >= 3) with { Indice = 22 },
                F.Casilla("PK-ED-071", IdiomaInterfaz.T("Mochila.Favorito"), c => ((IFavorite)c.Pk).IsFavorite, (c, v) => { ((IFavorite)c.Pk).IsFavorite = v; return null; }, visible: c => c.Pk is IFavorite),
                F.Info("PK-ED-072", IdiomaInterfaz.T("Catalogo.MarcaOrigen"), c => NombreMarcaOrigen(OriginMarkUtil.GetOriginMark(c.Pk)), visible: c => c.Formato >= 6) with { Indice = 23 },
                F.Accion("PK-ED-072", IdiomaInterfaz.T("Catalogo.CambiarJuegoOrigen"), c => c.Anfitrion?.IrAPestana("encuentro"), visible: c => c.Formato >= 6, ayuda: IdiomaInterfaz.T("Catalogo.LlevaPestanaEncuentroElegir")) with { Indice = 24 },
            ], c => c.Formato >= 3),
            new(IdiomaInterfaz.T("Catalogo.CintasMedallasRecuerdos"),
            [
                F.Info("PK-ED-073", IdiomaInterfaz.T("Catalogo.CintaMarcaFijada"), c => TextoCintaFijada(c), visible: c => c.Pk is IRibbonSetAffixed),
                F.Accion("PK-ED-080", IdiomaInterfaz.T("Catalogo.Cintas"), c => c.Anfitrion?.AbrirSubeditor("cintas"), visible: c => c.Formato >= 3, ayuda: IdiomaInterfaz.T("Catalogo.CintasMarcasPokemon")),
                F.Accion("PK-ED-081", IdiomaInterfaz.T("Catalogo.MedallasSuperentrenamiento"), c => c.Anfitrion?.AbrirSubeditor("superentrenamiento"), visible: c => c.Formato is 6 or 7 && c.Pk is ISuperTrainRegimen, ayuda: IdiomaInterfaz.T("Catalogo.RegimenesSuperentrenamiento")) with { Indice = 1 },
                F.Accion("PK-ED-082", IdiomaInterfaz.T("Catalogo.HistorialRecuerdos"), c => c.Anfitrion?.AbrirSubeditor("memorias"), visible: c => c.Formato >= 6 && c.Pk is not PB7, ayuda: IdiomaInterfaz.T("Catalogo.RecuerdosAfectoResidencia")) with { Indice = 2 },
            ]),
            new(IdiomaInterfaz.T("Catalogo.Concurso"),
            [
                F.Entero("PK-ED-079", IdiomaInterfaz.T("Informe.Columna.Beauty"), c => ((IContestStats)c.Pk).ContestBeauty, (c, v) => ((IContestStats)c.Pk).ContestBeauty = (byte)v, 0, 255, visible: c => c.Pk is IContestStats, ayuda: IdiomaInterfaz.T("Catalogo.RangoDe0a255")) with { Grupo = "concurso", Indice = 0 },
                F.Entero("PK-ED-079", IdiomaInterfaz.T("Catalogo.ConcursoElegancia"), c => ((IContestStats)c.Pk).ContestCool, (c, v) => ((IContestStats)c.Pk).ContestCool = (byte)v, 0, 255, visible: c => c.Pk is IContestStats, ayuda: IdiomaInterfaz.T("Catalogo.RangoDe0a255")) with { Grupo = "concurso", Indice = 1 },
                F.Entero("PK-ED-079", IdiomaInterfaz.T("Catalogo.Simpatia"), c => ((IContestStats)c.Pk).ContestCute, (c, v) => ((IContestStats)c.Pk).ContestCute = (byte)v, 0, 255, visible: c => c.Pk is IContestStats, ayuda: IdiomaInterfaz.T("Catalogo.RangoDe0a255")) with { Grupo = "concurso", Indice = 2 },
                F.Entero("PK-ED-079", IdiomaInterfaz.T("Catalogo.ConcursoInteligencia"), c => ((IContestStats)c.Pk).ContestSmart, (c, v) => ((IContestStats)c.Pk).ContestSmart = (byte)v, 0, 255, visible: c => c.Pk is IContestStats && c.Pk.Context.IsEraPre3DS, ayuda: IdiomaInterfaz.T("Catalogo.RangoDe0a255")) with { Grupo = "concurso", Indice = 3 },
                F.Entero("PK-ED-079", IdiomaInterfaz.T("Catalogo.ConcursoAstucia"), c => ((IContestStats)c.Pk).ContestSmart, (c, v) => ((IContestStats)c.Pk).ContestSmart = (byte)v, 0, 255, visible: c => c.Pk is IContestStats && !c.Pk.Context.IsEraPre3DS, ayuda: IdiomaInterfaz.T("Catalogo.RangoDe0a255")) with { Grupo = "concurso", Indice = 4 },
                F.Entero("PK-ED-079", IdiomaInterfaz.T("Informe.Columna.Tough"), c => ((IContestStats)c.Pk).ContestTough, (c, v) => ((IContestStats)c.Pk).ContestTough = (byte)v, 0, 255, visible: c => c.Pk is IContestStats, ayuda: IdiomaInterfaz.T("Catalogo.RangoDe0a255")) with { Grupo = "concurso", Indice = 5 },
                F.Entero("PK-ED-079", IdiomaInterfaz.T("Informe.Columna.Sheen"), c => ((IContestStats)c.Pk).ContestSheen, (c, v) => ((IContestStats)c.Pk).ContestSheen = (byte)v, 0, 255, visible: c => c.Pk is IContestStats, ayuda: IdiomaInterfaz.T("Catalogo.RangoDe0a255")) with { Grupo = "concurso", Indice = 6 },
                F.Accion("PK-ED-079", IdiomaInterfaz.T("Catalogo.ConcursoMaximo"), c => { var s = (IContestStats)c.Pk; s.ContestBeauty = s.ContestCool = s.ContestCute = s.ContestSmart = s.ContestTough = s.ContestSheen = 255; }, visible: c => c.Pk is IContestStats) with { Indice = 7 },
                F.Accion("PK-ED-079", IdiomaInterfaz.T("Catalogo.ConcursoCero"), c => { var s = (IContestStats)c.Pk; s.ContestBeauty = s.ContestCool = s.ContestCute = s.ContestSmart = s.ContestTough = s.ContestSheen = 0; }, visible: c => c.Pk is IContestStats) with { Indice = 8 },
            ], c => c.Pk is IContestStats),
            new(IdiomaInterfaz.T("Catalogo.TamanoPuntosCombate"),
            [
                F.Entero("PK-ED-108", IdiomaInterfaz.T("Catalogo.AlturaEscalar"), c => ((IScaledSize)c.Pk).HeightScalar, (c, v) => { var s = (IScaledSize)c.Pk; s.HeightScalar = (byte)v; if (c.Pk is PA8 p) p.Scale = s.HeightScalar; }, 0, 255, visible: c => c.Pk is IScaledSize, ayuda: IdiomaInterfaz.T("Catalogo.RangoDe0a255")),
                F.Info("PK-ED-108", IdiomaInterfaz.T("Catalogo.AlturaClaseTamano"), c => TamanoTexto(((IScaledSize)c.Pk).HeightScalar, false), visible: c => c.Pk is IScaledSize) with { Indice = 1 },
                F.Decimal("PK-ED-108", IdiomaInterfaz.T("Catalogo.AlturaAbsolutaM"), c => ((IScaledSizeAbsolute)c.Pk).HeightAbsolute, (c, v) => ((IScaledSizeAbsolute)c.Pk).HeightAbsolute = v, visible: c => c.Pk is IScaledSizeValue) with { Indice = 2 },
                F.Entero("PK-ED-109", IdiomaInterfaz.T("Catalogo.PesoEscalar"), c => ((IScaledSize)c.Pk).WeightScalar, (c, v) => ((IScaledSize)c.Pk).WeightScalar = (byte)v, 0, 255, visible: c => c.Pk is IScaledSize, ayuda: IdiomaInterfaz.T("Catalogo.RangoDe0a255")),
                F.Info("PK-ED-109", IdiomaInterfaz.T("Catalogo.PesoClaseTamano"), c => TamanoTexto(((IScaledSize)c.Pk).WeightScalar, false), visible: c => c.Pk is IScaledSize) with { Indice = 1 },
                F.Decimal("PK-ED-109", IdiomaInterfaz.T("Catalogo.PesoAbsolutoKg"), c => ((IScaledSizeAbsolute)c.Pk).WeightAbsolute, (c, v) => ((IScaledSizeAbsolute)c.Pk).WeightAbsolute = v, visible: c => c.Pk is IScaledSizeValue) with { Indice = 2 },
                F.Entero("PK-ED-110", IdiomaInterfaz.T("Catalogo.Escala"), c => ((IScaledSize3)c.Pk).Scale, (c, v) => { ((IScaledSize3)c.Pk).Scale = (byte)v; if (c.Pk is PA8 p) p.HeightScalar = (byte)v; }, 0, 255, visible: c => c.Pk is IScaledSize3, ayuda: IdiomaInterfaz.T("Catalogo.RangoDe0a255")),
                F.Info("PK-ED-110", IdiomaInterfaz.T("Catalogo.EscalaClaseTamano"), c => TamanoTexto(((IScaledSize3)c.Pk).Scale, c.Pk is PK9), visible: c => c.Pk is IScaledSize3) with { Indice = 1 },
                F.Entero("PK-ED-111", IdiomaInterfaz.T("Catalogo.PuntosCombatePC"), c => ((ICombatPower)c.Pk).Stat_CP, (c, v) => ((ICombatPower)c.Pk).Stat_CP = (int)v, 0, 65535, visible: c => c.Pk is ICombatPower, ayuda: IdiomaInterfaz.T("Catalogo.RangoDe0a65535")),
                F.Accion("PK-ED-111", IdiomaInterfaz.T("Catalogo.RecalcularAlturaPesoPC"), c => { if (c.Pk is IScaledSizeValue v) { v.ResetHeight(); v.ResetWeight(); } if (c.Pk is ICombatPower p) p.ResetCP(); },
                    visible: c => c.Pk is IScaledSizeValue || c.Pk is ICombatPower, ayuda: IdiomaInterfaz.T("Catalogo.CalculaAlturaPesoPC")) with { Indice = 1 },
            ], c => c.Formato >= 8 || c.Pk is PB7),
            new(IdiomaInterfaz.T("Catalogo.EstadoAnimo"),
            [
                F.Entero("PK-ED-074", IdiomaInterfaz.T("Catalogo.Espiritu"), c => ((PB7)c.Pk).Spirit, (c, v) => ((PB7)c.Pk).Spirit = (byte)v, 0, 255, visible: c => c.Pk is PB7),
                F.Entero("PK-ED-074", IdiomaInterfaz.T("CatalogoCosmetico.Humor"), c => ((PB7)c.Pk).Mood, (c, v) => ((PB7)c.Pk).Mood = (byte)v, 0, 255, visible: c => c.Pk is PB7) with { Indice = 1 },
                F.Entero("PK-ED-075", IdiomaInterfaz.T("CatalogoCosmetico.HumorAlCaminar"), c => ((G4PKM)c.Pk).WalkingMood, (c, v) => ((G4PKM)c.Pk).WalkingMood = (sbyte)v, -128, 127, visible: c => c.Formato == 4 && c.Pk is G4PKM),
                F.Entero("PK-ED-076", IdiomaInterfaz.T("Catalogo.FamaPokeStar"), c => ((PK5)c.Pk).PokeStarFame, (c, v) => ((PK5)c.Pk).PokeStarFame = (byte)v, 0, 100, visible: c => c.Pk is PK5),
            ], c => c.Pk is PB7 || c.Formato is 4 or 5),
            new(IdiomaInterfaz.T("Catalogo.HojasBrillantes"),
            [
                .. Enumerable.Range(0, 5).Select(i =>
                    F.Casilla("PK-ED-078", IdiomaInterfaz.F("Catalogo.HojaBrillante", i + 1), c => ((((G4PKM)c.Pk).ShinyLeaf >> i) & 1) == 1, (c, v) => { PonerHoja((G4PKM)c.Pk, i, v); return null; }, visible: c => c.Formato == 4 && c.Pk is G4PKM) with { Indice = i }),
                F.Casilla("PK-ED-078", IdiomaInterfaz.T("Catalogo.Corona"), c => ((((G4PKM)c.Pk).ShinyLeaf >> 5) & 1) == 1, (c, v) => { PonerHoja((G4PKM)c.Pk, 5, v); return null; }, visible: c => c.Formato == 4 && c.Pk is G4PKM,
                    motivo: c => (((G4PKM)c.Pk).ShinyLeaf & 0b11111) == 0b11111 ? null : IdiomaInterfaz.T("Catalogo.CoronaNecesitaCincoHojas")) with { Indice = 5 },
            ], c => c.Formato == 4 && c.Pk is G4PKM),
        ], c => c.Formato > 2);
    }

    // ---- Auxiliares ----
    private static string TamanoTexto(byte valor, bool detallado)
    {
        if (detallado) return PokeSizeDetailedUtil.GetSizeRating(valor) switch
        {
            PokeSizeDetailed.XXXS => IdiomaInterfaz.T("Catalogo.MinusculoXXXS"), PokeSizeDetailed.XXS => IdiomaInterfaz.T("Catalogo.MuyMuyPequenoXXS"), PokeSizeDetailed.XS => IdiomaInterfaz.T("Catalogo.MuyPequenoXS"), PokeSizeDetailed.S => IdiomaInterfaz.T("Catalogo.PequenoS"), PokeSizeDetailed.M => "Mediano (M)",
            PokeSizeDetailed.L => IdiomaInterfaz.T("Catalogo.GrandeL"), PokeSizeDetailed.XL => IdiomaInterfaz.T("Catalogo.MuyGrandeXL"), PokeSizeDetailed.XXL => IdiomaInterfaz.T("Catalogo.MuyMuyGrandeXXL"), _ => "Gigante (XXXL)",
        };
        return PokeSizeUtil.GetSizeRating(valor) switch { PokeSize.XS => IdiomaInterfaz.T("Catalogo.MuyPequenoXS"), PokeSize.S => IdiomaInterfaz.T("Catalogo.PequenoS"), PokeSize.M => "Mediano (M)", PokeSize.L => IdiomaInterfaz.T("Catalogo.GrandeL"), _ => IdiomaInterfaz.T("Catalogo.MuyGrandeXL") };
    }

    private static void PonerHoja(G4PKM pk, int i, bool v)
    {
        var bits = pk.ShinyLeaf;
        if (v) bits |= 1 << i; else bits &= ~(1 << i);
        if (i < 5 && !v) bits &= ~(1 << 5);                          // sin las cinco hojas no hay corona
        if (i == 5 && v && (bits & 0b11111) != 0b11111) bits &= ~(1 << 5);
        pk.ShinyLeaf = bits;
    }

    internal static string NombreMarcaOrigen(OriginMark m) => m switch
    {
        OriginMark.Gen6Pentagon => IdiomaInterfaz.T("Catalogo.PentagonoGeneracion"),
        OriginMark.Gen7Clover => IdiomaInterfaz.T("Catalogo.TrebolGeneracion"),
        OriginMark.Gen8Galar => "Galar",
        OriginMark.Gen8Trio => IdiomaInterfaz.T("Catalogo.DiamanteBrillantePerlaReluciente"),
        OriginMark.Gen8Arc => IdiomaInterfaz.T("Catalogo.LeyendasArceus"),
        OriginMark.Gen9Paldea => "Paldea",
        OriginMark.Gen9ZA => IdiomaInterfaz.T("Catalogo.LeyendasZ"),
        OriginMark.GameBoy => IdiomaInterfaz.T("Catalogo.ConsolaVirtualGameBoy"),
        OriginMark.GameBoyAdvance => IdiomaInterfaz.T("Catalogo.ConsolaVirtualGameBoyAdvance"),   // PKHeX 26.10.18: Rojo Fuego y Verde Hoja de Switch Online pasados a HOME (pk.VC3)
        OriginMark.GO => IdiomaInterfaz.T("Libreria.Origen.GO"),
        OriginMark.LetsGo => "Let's Go",
        _ => IdiomaInterfaz.T("Catalogo.MarcaOrigen2"),
    };

    private static string TextoCintaFijada(ContextoEditor c)
    {
        var a = (IRibbonSetAffixed)c.Pk;
        if (a.AffixedRibbon == AffixedRibbon.None)
            return c.Pk is IRibbonSetMarks { RibbonMarkCount: > 0 } x ? IdiomaInterfaz.F("Catalogo.NingunaDisponiblesFijar", x.RibbonMarkCount) : IdiomaInterfaz.T("Catalogo.Ninguna");
        return NombreCinta(c.F, (RibbonIndex)a.AffixedRibbon);
    }

    internal static string NombreCinta(FuentesEditor f, RibbonIndex i)
        => f.Textos.Ribbons.GetNameSafe($"Ribbon{i}", out var r) ? r : i.ToString();
}
