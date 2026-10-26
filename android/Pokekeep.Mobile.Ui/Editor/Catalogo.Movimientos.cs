// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Editor;

internal static partial class Catalogo
{
    internal static PestanaDef Movimientos()
    {
        var movs = Enumerable.Range(0, 4).Select(i =>
            F.Opcion("PK-ED-059", IdiomaInterfaz.F("Catalogo.Movimiento", i + 1), c => OpcionesMovimiento(c), c => c.Pk.GetMove(i), (c, id) => { Reglas.PonerMovimiento(c.Pk, i, (ushort)id); return null; },
                leer: c => LeerMovimiento(c, i), ayuda: IdiomaInterfaz.T("Catalogo.MovimientoActual")) with { Grupo = "mov", Indice = i });
        var pps = Enumerable.Range(0, 4).Select(i =>
            F.Entero("PK-ED-112", IdiomaInterfaz.F("Catalogo.MovimientoPP", i + 1), c => Reglas.LeerPp(c.Pk, i), (c, v) => Reglas.EscribirPp(c.Pk, i, (int)v), 0, 99, visible: c => c.Pk.GetMove(i) != 0,
                ayuda: IdiomaInterfaz.T("Catalogo.PuntosPoderActuales")) with { Grupo = "pp", Indice = i });
        var ups = Enumerable.Range(0, 4).Select(i =>
            F.Entero("PK-ED-063", IdiomaInterfaz.F("Catalogo.MovimientoPPUps", i + 1), c => Reglas.LeerPpUps(c.Pk, i), (c, v) => { Reglas.EscribirPpUps(c.Pk, i, (int)v); Reglas.CurarPp(c.Pk, i); }, 0, 3,
                visible: c => c.Pk.GetMove(i) != 0, maxDinamico: c => Reglas.MaxPpUps(c.Pk, i),
                motivo: c => Reglas.MaxPpUps(c.Pk, i) == 0 ? IdiomaInterfaz.T("Catalogo.MovimientoAdmitePPUps") : null, ayuda: IdiomaInterfaz.T("CatalogoMovimientos.DeA")) with { Grupo = "ppups", Indice = i });
        var rec = Enumerable.Range(0, 4).Select(i =>
            F.Opcion("PK-ED-064", IdiomaInterfaz.F("Catalogo.MovimientoRecordado", i + 1), c => c.F.Recordados, c => c.Pk.GetRelearnMove(i), (c, id) => { c.Pk.SetRelearnMove(i, (ushort)id); return null; },
                visible: c => c.Formato >= 6, leer: c => c.Pk.GetRelearnMove(i) == 0 ? IdiomaInterfaz.T("Form.Ninguno") : c.F.NombreMovimiento(c.Pk.GetRelearnMove(i))) with { Grupo = "rec", Indice = i });

        return new("movimientos", IdiomaInterfaz.T("Catalogo.Movimientos"),
        [
            new(IdiomaInterfaz.T("Catalogo.MovimientosActuales"), [.. movs, .. pps, .. ups]),
            new(IdiomaInterfaz.T("Catalogo.SugerenciasCore"),
            [
                F.Accion("PK-ED-060", IdiomaInterfaz.T("Catalogo.SugerirMovimientosLegales"), c => SugerirMovimientos(c, false), ayuda: IdiomaInterfaz.T("Catalogo.PideCoreConjuntoMovimientos")) with { AfectaStats = false },
                F.Accion("PK-ED-060", IdiomaInterfaz.T("Catalogo.MovimientosLegalesAzar"), c => SugerirMovimientos(c, true), ayuda: IdiomaInterfaz.T("Catalogo.PideCoreMovimientosLegales")) with { Indice = 1 },
                F.Accion("PK-ED-061", IdiomaInterfaz.T("Catalogo.RestaurarPPMaximo"), c => Reglas.CurarTodosLosPp(c.Pk), ayuda: IdiomaInterfaz.T("Catalogo.PonePPCadaMovimiento")) with { Indice = 2 },
                F.Accion("PK-ED-062", IdiomaInterfaz.T("Catalogo.PPUpsMaximo"), c => Reglas.PonerPpUpsTodos(c.Pk, false), ayuda: IdiomaInterfaz.T("Catalogo.PPUpsCadaMovimiento")) with { Indice = 3 },
                F.Accion("PK-ED-062", IdiomaInterfaz.T("Catalogo.QuitarPPUps"), c => Reglas.PonerPpUpsTodos(c.Pk, true), ayuda: IdiomaInterfaz.T("Catalogo.QuitaPPUpsTodos")) with { Indice = 4 },
            ]),
            new(IdiomaInterfaz.T("Catalogo.MovimientosRecordados"), [.. rec,
                F.Accion("PK-ED-064", IdiomaInterfaz.T("Catalogo.SugerirMovimientosRecordados"), c => SugerirRecordados(c), visible: c => c.Formato >= 6, ayuda: IdiomaInterfaz.T("Catalogo.PideCoreMovimientosRecordados")) with { Indice = 4 }],
                c => c.Formato >= 6),
            new(IdiomaInterfaz.T("Catalogo.TecnicasMaestrias"),
            [
                F.Accion("PK-ED-065", IdiomaInterfaz.T("Catalogo.RegistrosTecnicos"), c => c.Anfitrion?.AbrirSubeditor("tecnicas"), visible: c => c.Pk is ITechRecord, ayuda: IdiomaInterfaz.T("Catalogo.MTDTTRConoce")),
                F.Accion("PK-ED-065", IdiomaInterfaz.T("Catalogo.RegistrosTecnicosLegales"), c => { var t = (ITechRecord)c.Pk; Bloqueado(() => t.SetRecordFlags(c.Pk, TechnicalRecordApplicatorOption.LegalCurrent)); }, visible: c => c.Pk is ITechRecord, ayuda: IdiomaInterfaz.T("Catalogo.MarcaRegistrosTecnicosLegales")) with { Indice = 1 },
                F.Accion("PK-ED-066", IdiomaInterfaz.T("Catalogo.TiendaMovimientosMaestria"), c => c.Anfitrion?.AbrirSubeditor("tienda"), visible: c => c.Pk is IMoveShop8Mastery, ayuda: IdiomaInterfaz.T("Catalogo.MovimientosCompradosDominadosLeyendas")),
                F.Accion("PK-ED-066", IdiomaInterfaz.T("Catalogo.MaestriaLegal"), c => ReiniciarMaestria(c.Pk), visible: c => c.Pk is IMoveShop8Mastery, ayuda: IdiomaInterfaz.T("Catalogo.AplicaMaestriaLegalMovimientos")) with { Indice = 1 },
                F.Accion("PK-ED-067", IdiomaInterfaz.T("Catalogo.RegistroPlus"), c => c.Anfitrion?.AbrirSubeditor("plus"), visible: c => c.Pk is IPlusRecord && c.Pk.PersonalInfo is IPermitPlus, ayuda: IdiomaInterfaz.T("Catalogo.MovimientosPlusLeyendasZ")),
                F.Accion("PK-ED-067", IdiomaInterfaz.T("Catalogo.RegistroPlusLegal"), c => { var m = (IPlusRecord)c.Pk; var p = (IPermitPlus)c.Pk.PersonalInfo; Bloqueado(() => m.SetPlusFlags(c.Pk, p, PlusRecordApplicatorOption.LegalCurrent)); },
                    visible: c => c.Pk is IPlusRecord && c.Pk.PersonalInfo is IPermitPlus, ayuda: IdiomaInterfaz.T("Catalogo.MarcaPlusLegalesAhora")) with { Indice = 1 },
                F.Opcion("PK-ED-068", IdiomaInterfaz.T("Catalogo.MovimientoDominadoAlfa"), c => c.F.Movimientos, c => ((PA8)c.Pk).AlphaMove, (c, id) => { ((PA8)c.Pk).AlphaMove = (ushort)id; return null; }, visible: c => c.Pk is PA8,
                    leer: c => ((PA8)c.Pk).AlphaMove == 0 ? IdiomaInterfaz.T("Form.Ninguno") : c.F.NombreMovimiento(((PA8)c.Pk).AlphaMove)),
            ], c => c.Pk is ITechRecord || c.Pk is IMoveShop8Mastery || c.Pk is IPlusRecord || c.Pk is PA8),
        ]);
    }

    private static void Bloqueado(Action a) { lock (Pokekeep.Sprites.LegalityGate.Cerrojo) a(); }

    private static string LeerMovimiento(ContextoEditor c, int i)
    {
        var m = c.Pk.GetMove(i);
        return m == 0 ? IdiomaInterfaz.T("Form.Ninguno") : c.F.NombreMovimiento(m);
    }

    /// <summary>Movimientos del formato; los que ESTE Pokemon puede aprender (segun el analisis del Core) primero y marcados «Legal», con el tipo del movimiento (MoveChoice + LegalMoveSource de PKHeX).</summary>
    private static IReadOnlyList<OpcionCampo> OpcionesMovimiento(ContextoEditor c)
    {
        var info = new LegalMoveInfo();
        var tengoInfo = false;
        if (!c.Avanzado)
        {
            try { lock (Pokekeep.Sprites.LegalityGate.Cerrojo) { var la = new LegalityAnalysis(c.Pk); if (la.Parsed) { info.ReloadMoves(la); tengoInfo = true; } } }
            catch { /* sin analisis: sin marcas */ }
        }
        string Tipo(int m) { try { var t = MoveInfo.GetType((ushort)m, c.Pk.Context); return t < c.F.Textos.types.Length ? c.F.Textos.types[t] : string.Empty; } catch { return string.Empty; } }
        int TipoIdx(int m) { try { var t = MoveInfo.GetType((ushort)m, c.Pk.Context); return t < c.F.Textos.types.Length ? t : -1; } catch { return -1; } }
        var todos = c.F.Movimientos.Where(o => o.Id != 0).ToList();
        var legales = tengoInfo ? todos.Where(o => info.CanLearn((ushort)o.Id)).ToList() : [];
        var legalesId = new HashSet<int>(legales.Select(o => o.Id));
        var lista = new List<OpcionCampo> { new(0, IdiomaInterfaz.T("Form.Ninguno")) };
        lista.AddRange(legales.Select(o => o with { Detalle = $"{Tipo(o.Id)} · Legal", Tipo = TipoIdx(o.Id), TipoTexto = Tipo(o.Id) }));
        lista.AddRange(todos.Where(o => !legalesId.Contains(o.Id)).Select(o => o with { Detalle = Tipo(o.Id), Tipo = TipoIdx(o.Id), TipoTexto = Tipo(o.Id) }));
        return lista;
    }

    private static void SugerirMovimientos(ContextoEditor c, bool azar)
    {
        var pk = c.Pk;
        Span<ushort> movs = stackalloc ushort[4];
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo) pk.GetMoveSet(movs, azar);
        if (movs[0] == 0) { c.Anfitrion?.Aviso(IdiomaInterfaz.T("Catalogo.CoreTieneMovimientosSugerir")); return; }
        pk.SetMoves(movs);
        if (pk is ITechRecord tr)
        {
            lock (Pokekeep.Sprites.LegalityGate.Cerrojo)
            {
                tr.ClearRecordFlags();
                var la = new LegalityAnalysis(pk);
                tr.SetRecordFlags(movs, la.Info.EvoChainsAllGens.Get(pk.Context));
            }
        }
        Reglas.CurarTodosLosPp(pk);
    }

    private static void SugerirRecordados(ContextoEditor c)
    {
        var pk = c.Pk;
        if (pk.Format < 6) return;
        Span<ushort> movs = stackalloc ushort[4];
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo)
        {
            var la = new LegalityAnalysis(pk);
            la.GetSuggestedRelearnMoves(movs);
        }
        pk.SetRelearnMoves(movs);
    }

    private static void ReiniciarMaestria(PKM pk)
    {
        var m = (IMoveShop8Mastery)pk;
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo)
        {
            m.ClearMoveShopFlags();
            var la = new LegalityAnalysis(pk);
            if (la.EncounterMatch is IMasteryInitialMoveShop8 shop) shop.SetInitialMastery(pk, la.EncounterMatch);
            m.SetMoveShopFlags(pk);
        }
    }
}
