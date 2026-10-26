// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Editor;

internal static partial class Catalogo
{
    /// <summary>Pestana Encuentro (<c>Tab_Met</c>). No existe en el formato 1 (PKMEditor.FinalizeInterface).</summary>
    internal static PestanaDef Encuentro() => new("encuentro", IdiomaInterfaz.T("Informe.Columna.EncounterType"),
    [
        new(IdiomaInterfaz.T("Libreria.Origen"),
        [
            F.Opcion("PK-ED-032", IdiomaInterfaz.T("Informe.Columna.Version"), c => c.F.Juegos, c => (int)c.Pk.Version, (c, id) => { Reglas.CambiarVersion(c.Pk, (GameVersion)id, c.Sav); return null; },
                visible: c => c.Formato >= 3, leer: c => LeerJuego(c), ayuda: IdiomaInterfaz.T("Catalogo.JuegoCapturoRecibio")),
            F.Opcion("PK-ED-033", IdiomaInterfaz.T("Catalogo.VersionCombate"), c => c.F.Juegos, c => (int)((IBattleVersion)c.Pk).BattleVersion, (c, id) => { ((IBattleVersion)c.Pk).BattleVersion = (GameVersion)id; return null; },
                visible: c => c.Pk is IBattleVersion, leer: c => ((IBattleVersion)c.Pk).BattleVersion == 0 ? IdiomaInterfaz.T("Catalogo.Ninguna") : FuentesEditor.Nombre(c.F.Juegos, (int)((IBattleVersion)c.Pk).BattleVersion)),
            F.Opcion("PK-ED-035", IdiomaInterfaz.T("Informe.Columna.Ball"), OpcionesBola, c => c.Pk.Ball, (c, id) => { c.Pk.Ball = (byte)id; return null; },
                visible: c => c.Formato >= 3, ayuda: IdiomaInterfaz.T("Catalogo.PokeBallLegalesPokemon")),
            F.Accion("PK-ED-035", IdiomaInterfaz.T("Catalogo.PokeBallLegalColor"), c => c.Pk.Ball = BallComoSugerida(c.Pk), visible: c => c.Formato >= 3, ayuda: IdiomaInterfaz.T("Catalogo.PoneBolaLegalSegun")) with { Indice = 1 },
            F.Accion("PK-ED-035", IdiomaInterfaz.T("CatalogoEncuentro.PokeBallNormal"), c => c.Pk.Ball = (byte)Ball.Poke, visible: c => c.Formato >= 3, ayuda: IdiomaInterfaz.T("Catalogo.PonePokeBallNormal")) with { Indice = 2 },
            F.Casilla("PK-ED-038", IdiomaInterfaz.T("Catalogo.EncuentroPredestinado"), c => c.Pk.FatefulEncounter, (c, v) => { c.Pk.FatefulEncounter = v; return null; }, visible: c => c.Formato >= 3),
        ]),
        new(IdiomaInterfaz.T("Catalogo.LugarNivel"),
        [
            F.Opcion("PK-ED-034", IdiomaInterfaz.T("Informe.Columna.MetLoc"), c => c.F.Lugares(c.F.VersionDeLugares(c.Pk), c.Pk.Context, false), c => c.Pk.MetLocation, (c, id) => { c.Pk.MetLocation = (ushort)id; return null; },
                visible: c => c.Formato >= 2, leer: LeerLugar, ayuda: IdiomaInterfaz.T("Catalogo.LugarDondeEncontro")),
            F.Accion("PK-ED-034", IdiomaInterfaz.T("Catalogo.SugerirLugarNivelLegales"), c => { var r = Reglas.AplicarLugarSugerido(c.Pk); if (r is null) c.Anfitrion?.Aviso(IdiomaInterfaz.T("Catalogo.CoreTieneNadaSugerir")); else c.Anfitrion?.Aviso(r); },
                visible: c => c.Formato >= 2, avanzado: false, ayuda: IdiomaInterfaz.T("Catalogo.PideCoreLugarNivel")) with { Indice = 1 },
            F.Entero("PK-ED-037", IdiomaInterfaz.T("Informe.Columna.MetLevel"), c => c.Pk.MetLevel, (c, v) => c.Pk.MetLevel = (byte)v, 0, 100, visible: c => c.Formato >= 2, ayuda: IdiomaInterfaz.T("Catalogo.NivelEncontro")),
            F.Fecha("PK-ED-036", IdiomaInterfaz.T("MenuCaja.FechaEncuentro"), c => c.Pk.MetDate, (c, d) => PonerFechaEncuentro(c.Pk, d, esHuevo: false), visible: c => c.Formato >= 4),
            F.Opcion("PK-ED-041", IdiomaInterfaz.T("Catalogo.MomentoDia"), c => [new(0, IdiomaInterfaz.T("Catalogo.Dato")), new(1, IdiomaInterfaz.T("Catalogo.Manana")), new(2, IdiomaInterfaz.T("Catalogo.Dia")), new(3, IdiomaInterfaz.T("CatalogoEncuentro.Noche"))], c => ((ICaughtData2)c.Pk).MetTimeOfDay, (c, id) => { ((ICaughtData2)c.Pk).MetTimeOfDay = id; return null; },
                visible: c => c.Formato == 2 && c.Pk is ICaughtData2),
            F.Opcion("PK-ED-040", IdiomaInterfaz.T("Catalogo.TipoTerreno"), c => c.F.Terrenos, c => (int)((IGroundTile)c.Pk).GroundTile, (c, id) => { ((IGroundTile)c.Pk).GroundTile = (GroundTileType)id; return null; },
                visible: c => c.Pk is IGroundTile && c.Formato is 4 or 5 or 6 && c.Pk.Gen4),
            F.Entero("PK-ED-039", IdiomaInterfaz.T("Catalogo.NivelObediencia"), c => ((IObedienceLevel)c.Pk).ObedienceLevel, (c, v) => ((IObedienceLevel)c.Pk).ObedienceLevel = (byte)v, 0, 100, visible: c => c.Pk is IObedienceLevel, ayuda: IdiomaInterfaz.T("Catalogo.NivelHastaObedece")),
            F.Accion("PK-ED-039", IdiomaInterfaz.T("Catalogo.SugerirNivelObediencia"), c => ((IObedienceLevel)c.Pk).ObedienceLevel = ((IObedienceLevel)c.Pk).GetSuggestedObedienceLevel(c.Pk, c.Pk.MetLevel), visible: c => c.Pk is IObedienceLevel) with { Indice = 1 },
        ]),
        new(IdiomaInterfaz.T("Informe.Columna.IsEgg"),
        [
            F.Casilla("PK-ED-042", IdiomaInterfaz.T("Catalogo.EncontradoHuevo"), c => EncounterStateUtil.IsMetAsEgg(c.Pk), (c, v) => { Reglas.PonerComoHuevo(c.Pk, v, c.Sav); return null; }, visible: c => c.Formato >= 4),
            F.Opcion("PK-ED-043", IdiomaInterfaz.T("Informe.Columna.EggLoc"), c => c.F.Lugares(c.F.VersionDeLugares(c.Pk), c.Pk.Context, true), c => c.Pk.EggLocation, (c, id) => { c.Pk.EggLocation = (ushort)id; return null; },
                visible: c => c.Formato >= 4, motivo: c => EncounterStateUtil.IsMetAsEgg(c.Pk) ? null : IdiomaInterfaz.T("Catalogo.MarcaEncontradoHuevoEditar"),
                leer: c => { var l = c.F.Lugares(c.F.VersionDeLugares(c.Pk), c.Pk.Context, true); return FuentesEditor.Nombre(l, c.Pk.EggLocation) is var n && n != "—" ? n : (c.Pk.EggLocation == 0 ? IdiomaInterfaz.T("Form.Ninguno") : IdiomaInterfaz.F("Catalogo.Lugar", c.Pk.EggLocation)); }),
            F.Fecha("PK-ED-043", IdiomaInterfaz.T("Catalogo.FechaHuevo"), c => c.Pk.EggMetDate, (c, d) => PonerFechaEncuentro(c.Pk, d, esHuevo: true), visible: c => c.Formato >= 4,
                motivo: c => EncounterStateUtil.IsMetAsEgg(c.Pk) ? null : IdiomaInterfaz.T("Catalogo.MarcaEncontradoHuevoEditar")) with { Indice = 1 },
        ]),
    ], c => c.Formato != 1);

    // ---- Auxiliares ----
    private static string LeerJuego(ContextoEditor c)
    {
        var v = (int)c.Pk.Version;
        return v == 0 ? IdiomaInterfaz.T("Catalogo.JuegoOrigen") : FuentesEditor.Nombre(c.F.Juegos, v) is var n && n != "—" ? n : GameInfo.GetVersionName(c.Pk.Version);
    }

    private static string LeerLugar(ContextoEditor c)
    {
        var l = c.F.Lugares(c.F.VersionDeLugares(c.Pk), c.Pk.Context, false);
        var n = FuentesEditor.Nombre(l, c.Pk.MetLocation);
        if (n != "—") return n;
        try { return c.F.Textos.GetLocationName(false, c.Pk.MetLocation, c.Pk.Format, c.Pk.Generation, c.Pk.Version); } catch { return IdiomaInterfaz.F("Catalogo.Lugar", c.Pk.MetLocation); }
    }

    private static string? PonerFechaEncuentro(PKM pk, DateOnly? fecha, bool esHuevo)
    {
        if (fecha is null)
        {
            if (esHuevo) pk.EggMetDate = null; else pk.MetDate = null;
            return null;
        }
        if (fecha.Value.Year is < 2000 or > 2099) return IdiomaInterfaz.T("Catalogo.AnoTieneEstarEntre");
        if (esHuevo) pk.EggMetDate = fecha; else pk.MetDate = fecha;
        return null;
    }

    private static byte BallComoSugerida(PKM pk)
    {
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo) return BallApplicator.ApplyBallLegalByColor(pk);
    }

    /// <summary>Las bolas del formato con su legalidad para ESTE Pokemon (BallBrowser): con el ajuste <c>EntityEditor.ShowLegalBallsFirst</c> (activo de fábrica) las legales primero; sin él, por número de bola, como PKHeX.</summary>
    private static IReadOnlyList<OpcionCampo> OpcionesBola(ContextoEditor c)
    {
        var legales = new HashSet<int>();
        try
        {
            lock (Pokekeep.Sprites.LegalityGate.Cerrojo)
            {
                Span<Ball> valido = stackalloc Ball[BallApplicator.MaxBallSpanAlloc];
                var n = BallApplicator.GetLegalBalls(valido, c.Pk);
                for (var i = 0; i < n; i++) legales.Add((int)valido[i]);
            }
        }
        catch { /* sin analisis: ninguna se marca */ }
        var todas = c.F.Bolas;
        OpcionCampo Marcar(OpcionCampo o) => legales.Contains(o.Id) ? o with { Detalle = IdiomaInterfaz.T("Informe.Columna.Legal") } : o with { Detalle = legales.Count == 0 ? null : IdiomaInterfaz.T("Catalogo.LegalPokemon") };
        if (!AjustesEditor.ShowLegalBallsFirst) return [.. todas.OrderBy(o => o.Id).Select(Marcar)];   // BallBrowser.LoadBalls: sin el ajuste, en orden de número de bola
        // BallBrowser.LoadBalls recorre ballID=1..max: dentro de cada grupo (legales y resto) el orden es el numérico
        return [.. todas.Where(o => legales.Contains(o.Id)).OrderBy(o => o.Id).Select(Marcar), .. todas.Where(o => !legales.Contains(o.Id)).OrderBy(o => o.Id).Select(Marcar)];
    }
}
