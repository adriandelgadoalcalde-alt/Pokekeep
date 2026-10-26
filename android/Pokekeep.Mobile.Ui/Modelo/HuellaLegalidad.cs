// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Modelo;

/// <summary>
/// Huella de los ajustes de legalidad que el usuario tiene vigentes en el Core (<c>ParseSettings.Settings</c>): todo lo que puede cambiar lo que enseña un análisis. La memoria de legalidad de las casillas
/// (<see cref="AnalizadorLegalidad"/>) la incluye en su clave, de modo que cambiar un ajuste en «Más &gt; Ajustes &gt; Legalidad» invalida las insignias ya calculadas sin reiniciar nada.
/// Solo afecta a lo que ENSEÑA la interfaz: la segunda opinión de Pokekeep (<c>LegalityCheck</c>) usa siempre los valores por defecto del Core (ADR 0053) y no mira esta huella.
/// <see cref="De"/> recorre los ajustes uno a uno, sin reflexión (apto para el recorte de Android); <c>AjustesLegalidadTests</c> comprueba que cambiar CADA hoja de «Legality.*» del registro cambia la huella,
/// de modo que un ajuste nuevo que se olvide aquí no pasa desapercibido.
/// </summary>
public static class HuellaLegalidad
{
    /// <summary>La huella de los ajustes que el Core tiene ahora (la fija <see cref="Ajustes.Pkhex.AplicadorPkhex"/> al aplicar, bajo el cerrojo de legalidad).</summary>
    public static string Vigente { get; private set; } = De(new LegalitySettings());
    /// <summary>Avisa cuando el Core ya tiene los nuevos ajustes; puede llegar desde la cola de segundo plano.</summary>
    public static event Action? Cambiado;

    /// <summary>Publica los ajustes recién aplicados; si cambian, vacía la memoria de insignias.</summary>
    internal static void Publicar(LegalitySettings ajustes)
    {
        var h = De(ajustes);
        if (h == Vigente) return;
        Vigente = h;
        AnalizadorLegalidad.VaciarCache();
        Cambiado?.Invoke();
    }

    public static string De(LegalitySettings s)
    {
        var sb = new StringBuilder(160);
        void Sev(Severity v) => sb.Append((int)v).Append(',');
        void Bool(bool v) => sb.Append(v ? '1' : '0').Append(',');
        Bool(s.Bulk.CheckActiveHandler);
        Sev(s.FramePattern.RNGFrameNotFound3); Sev(s.FramePattern.RNGFrameNotFound4); Bool(s.FramePattern.EggRandomAnyType3); Bool(s.FramePattern.EggRandomAnyType4);
        Bool(s.Game.Gen3.AllowBattleTowerTeamSwap); Sev(s.Game.Gen7.Gen7TransferStarPID); Sev(s.Game.Gen8.Gen8MemoryMissingHT);
        Sev(s.Handler.CurrentHandlerMismatch); Bool(s.Handler.CheckActiveHandler);
        var r = s.Handler.Restrictions;
        Bool(r.AllowHandleOTGen6); Bool(r.AllowHandleOTGen7); Bool(r.AllowHandleOTGen7b); Bool(r.AllowHandleOTGen8); Bool(r.AllowHandleOTGen8a); Bool(r.AllowHandleOTGen8b); Bool(r.AllowHandleOTGen9); Bool(r.AllowHandleOTGen9a);
        Sev(s.HOMETransfer.HOMETransferTrackerNotPresent); Sev(s.HOMETransfer.ZeroHeightWeight);
        var n = s.Nickname;
        Sev(n.NicknamedAnotherSpecies);
        foreach (var x in new[] { n.Nickname12, n.Nickname3, n.Nickname4, n.Nickname5, n.Nickname6, n.Nickname7, n.Nickname7b, n.Nickname8, n.Nickname8a, n.Nickname8b, n.Nickname9, n.Nickname9a }) { Sev(x.NicknamedTrade); Sev(x.NicknamedMysteryGift); }
        Bool(s.Tradeback.AllowGen1Tradeback);
        Bool(s.WordFilter.CheckWordFilter); Bool(s.WordFilter.DisableWordFilterPastGen);
        return sb.ToString();
    }
}
