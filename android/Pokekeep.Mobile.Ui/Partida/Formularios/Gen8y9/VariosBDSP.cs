// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen8y9;

/// <summary>
/// PK-SAV-158 y PK-SAV-159 · <c>SAV_Misc8b</c>, «Varios» de Diamante Brillante y Perla Reluciente: una sola pestaña («Inicio») con diez botones que actúan sobre una COPIA del guardado (<c>SAV.Clone()</c>) y se copian al
/// original con «Guardar» (<c>CopyChangesFrom</c>): saludar a los 32 PNJ del Subsuelo (Spiritomb), desbloquear los eventos de Shaymin, Darkrai y Arceus, todas las zonas, reiniciar el encuentro de Dialga/Palkia y los errantes
/// (Mesprit y Cresselia), derrotar o volver a combatir a todos los Entrenadores Eyecatch y dar toda la moda. Cada botón sale habilitado según <c>EventUnlocker8b.UnlockReady*</c>/<c>ResetReady*</c> y <c>BattleTrainer.Any*</c>, lo
/// que se lee al abrir; aquí son acciones pendientes (se pueden desmarcar antes de aplicar) que se ejecutan en el orden de los botones, y la razón por la que una no se puede hacer es su motivo. «Derrotar» y «Volver a combatir»
/// se habilitan mutuamente en el original (derrotar habilita volver a combatir y al revés): aquí se excluyen (si marcas una, la otra queda con su motivo), porque el resultado de las dos seguidas depende del orden de los clics.
/// </summary>
internal static class VariosBDSP
{
    public const string Form = "SAV_Misc8b";
    public static readonly string[] Censo = ["PK-SAV-158", "PK-SAV-159"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SAV8BS Sav(ContextoP c) => (SAV8BS)c.Sav;
    private static EventUnlocker8b Desbloqueo(ContextoP c) => new(Sav(c));

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV8BS;

    private static CampoP Boton(string id, string control, Action<ContextoP> ejecutar, Func<ContextoP, string?>? motivo, string ayuda)
        => P.Accion(id, L(control), ejecutar, motivo, ayuda: ayuda, textoBoton: IdiomaInterfaz.T("Form.Hacer"));

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV8BS) return null;
        var botones = new SeccionP(string.Empty,
        [
            Boton("spiritomb", "B_Spiritomb", c => Desbloqueo(c).UnlockSpiritomb(), c => Desbloqueo(c).UnlockReadySpiritomb ? null : IdiomaInterfaz.T("VariosBDSP.HasSaludadoPNJSubsuelo"), IdiomaInterfaz.T("VariosBDSP.MarcaSaludadosPNJSubsuelo")),
            Boton("shaymin", "B_Shaymin", c => Desbloqueo(c).UnlockShaymin(), c => Desbloqueo(c).UnlockReadyShaymin ? null : IdiomaInterfaz.T("VariosBDSP.EventoShayminDesbloqueado"), IdiomaInterfaz.T("VariosBDSP.DaCartaOakPokedex")),
            Boton("darkrai", "B_Darkrai", c => Desbloqueo(c).UnlockDarkrai(), c => Desbloqueo(c).UnlockReadyDarkrai ? null : IdiomaInterfaz.T("VariosBDSP.EventoDarkraiDesbloqueado"), IdiomaInterfaz.T("VariosBDSP.DaTarjetaSocioPokedex")),
            Boton("arceus", "B_Arceus", c => Desbloqueo(c).UnlockArceus(), c => Desbloqueo(c).UnlockReadyArceus ? null : IdiomaInterfaz.T("VariosBDSP.EventoArceusDesbloqueado"), IdiomaInterfaz.T("VariosBDSP.DaFlautaAzurPokedex")),
            Boton("zonas", "B_Zones", c => Desbloqueo(c).UnlockZones(), null, IdiomaInterfaz.T("VariosBDSP.MarcaVisitadasTodasZonas")),
            Boton("dialga-palkia", "B_DialgaPalkia", c => Desbloqueo(c).UnlockBoxLegend(), c => Desbloqueo(c).UnlockReadyBoxLegend ? null : IdiomaInterfaz.T("VariosBDSP.SoloPuedeReiniciarEncuentro"), IdiomaInterfaz.T("VariosBDSP.PermiteVolverEncontrarLegendario")),
            Boton("errantes", "B_Roamer", c => Desbloqueo(c).RespawnRoamer(), c => Desbloqueo(c).ResetReadyRoamerMesprit || Desbloqueo(c).ResetReadyRoamerCresselia ? null : IdiomaInterfaz.T("VariosBDSP.MespritCresseliaEstanAparecer"), IdiomaInterfaz.T("VariosBDSP.HaceReaparecerMespritCresselia")),
            Boton("derrotar-eyecatch", "B_DefeatEyecatch", c => Sav(c).BattleTrainer.DefeatAll(), c => c.Marca("rebattle-eyecatch") ? IdiomaInterfaz.T("VariosBDSP.HasMarcadoVolverCombatir") : Sav(c).BattleTrainer.AnyUndefeated ? null : IdiomaInterfaz.T("VariosBDSP.TodosEntrenadoresEyecatchEstan"), IdiomaInterfaz.T("VariosBDSP.MarcaDerrotadosTodosEntrenadores")),
            Boton("rebattle-eyecatch", "B_RebattleEyecatch", c => Sav(c).BattleTrainer.RebattleAll(), c => c.Marca("derrotar-eyecatch") ? IdiomaInterfaz.T("VariosBDSP.HasMarcadoDerrotarTodos") : Sav(c).BattleTrainer.AnyDefeated ? null : IdiomaInterfaz.T("VariosBDSP.NingunEntrenadorEyecatchDerrotado"), IdiomaInterfaz.T("VariosBDSP.PermiteVolverCombatirTodos")),
            Boton("moda", "B_Fashion", c => Desbloqueo(c).UnlockFashion(), null, IdiomaInterfaz.T("VariosBDSP.DaTodosObjetosModa")),
        ]);
        return new EditorDefP("misc", IdiomaInterfaz.T("ModeloEventos.Varios"), [new PestanaP("inicio", L("TAB_Main"), [botones])], Censo, IdiomaInterfaz.T("VariosBDSP.EventosDesbloqueosBDSP"));
    }
}
