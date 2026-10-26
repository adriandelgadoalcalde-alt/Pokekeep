// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using PKHeX.Core.Bulk;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.Integridad;

/// <summary>Resultado de «Comprobar sumas» (<c>SAVEditor.ClickVerifyCHK</c>): tres casos, como PKHeX.</summary>
public enum EstadoSumas { PartidaEditada, Validas, Invalidas }

/// <param name="Mensaje">El texto de PKHeX en castellano (<c>MsgSaveChecksum*</c> de <c>MessageStrings_es.txt</c>).</param>
/// <param name="Detalle">Solo con sumas inválidas: <c>SaveFile.ChecksumInfo</c> (qué bloques fallan), lo que PKHeX copia al portapapeles.</param>
public sealed record ResultadoSumas(EstadoSumas Estado, string Mensaje, string? Detalle);

/// <summary>Un hallazgo de «Verificar todos los Pokémon»: gravedad, lo que dice el Core (humanizado) y dónde está.</summary>
public sealed record HallazgoBulk(string Gravedad, string Texto, string Donde, int Indice1, int Indice2, bool Valido)
{
    public string Resumen => string.IsNullOrEmpty(Donde) ? Texto : $"{Texto} {Donde}";
}

/// <summary>Resultado de «Verificar todos los Pokémon» (<c>SAVEditor.ClickVerifyStoredEntities</c>).</summary>
/// <param name="Limpio">True si <c>BulkAnalysis.Valid</c>: PKHeX lo dice con «¡Limpio!».</param>
/// <param name="Analizados">Cuántos Pokémon entran en el análisis (los que no están vacíos ni tienen la suma rota).</param>
/// <param name="Informe">El texto de <c>BulkAnalysis.Report</c> localizado (lo que PKHeX copia al portapapeles); vacío si <paramref name="Limpio"/>.</param>
public sealed record ResultadoVerificacion(bool Limpio, int Analizados, IReadOnlyList<HallazgoBulk> Hallazgos, string Informe);

/// <summary>
/// PILOTO DE LA F10 (F10-0): las dos herramientas del botón «Verificar» de PKHeX, tal cual. Sin pantalla y sin cambiar la partida: SOLO LEE (regla 13, el original intacto). Se prueba contra el
/// resultado de PKHeX de escritorio sobre las mismas copias (<c>IntegridadContraWinFormsTests</c>) y consume <c>Legality.Bulk</c> de los ajustes de PKHeX (el ajuste <c>CheckActiveHandler</c> cambia el análisis).
/// </summary>
public static class ModeloIntegridad
{
    /// <summary>
    /// Misma lógica que <c>ClickVerifyCHK</c>: si la partida se editó no se puede verificar (el Core recalcula las sumas al EXPORTAR, así que con cambios sin guardar las del archivo en memoria no valen); si las sumas valen,
    /// lo dice; si no, da el detalle. En PKHeX la edición marca <c>State.Edited</c> desde la ventana; en el móvil las ediciones pasan por el historial de la sesión, así que <paramref name="conCambiosSinGuardar"/>
    /// (<c>Sesion.CambiosPendientes &gt; 0</c>) cuenta igual que esa marca.
    /// </summary>
    public static ResultadoSumas ComprobarSumas(SaveFile sav, bool conCambiosSinGuardar = false)
    {
        Ajustes.Pkhex.TextosNucleo.Preparar();
        if (sav.State.Edited || conCambiosSinGuardar) return new(EstadoSumas.PartidaEditada, MessageStrings.MsgSaveChecksumFailEdited, null);
        if (sav.ChecksumsValid) return new(EstadoSumas.Validas, Respaldo(MessageStrings.MsgSaveChecksumValid), null);
        return new(EstadoSumas.Invalidas, IdiomaInterfaz.T("ModeloIntegridad.SumasVerificacionCoincidenPuedes"), sav.ChecksumInfo);
    }

    /// <summary>
    /// Respaldo con motivo (como <c>RespaldoAjustesPkhex</c> con <c>lang_es</c>): <c>MessageStrings_es.txt</c> de PKHeX dice «Sumas de verificación son válidas.» sin artículo, que en castellano de España suena a telegrama.
    /// Solo se corrige ese texto; el resto de mensajes del Core se enseñan tal cual.
    /// </summary>
    private static string Respaldo(string mensaje) => mensaje == IdiomaInterfaz.T("ModeloIntegridad.SumasDeVerificacionSon") ? IdiomaInterfaz.T("ModeloIntegridad.SumasVerificacionSonValidas") : mensaje;

    /// <summary>
    /// <c>new BulkAnalysis(SAV, Settings.Legality.Bulk)</c> y su informe, igual que <c>ClickVerifyStoredEntities</c>. Es CARO (analiza todas las casillas): llámalo en un hilo aparte; toma el cerrojo de legalidad
    /// de la app (<c>LegalityGate</c>) para no pisar el análisis de las casillas ni las acciones del motor.
    /// </summary>
    public static ResultadoVerificacion VerificarTodos(SaveFile sav, BulkAnalysisSettings ajustes, string? idioma = null, CancellationToken cancelar = default)
    {
        Ajustes.Pkhex.TextosNucleo.Preparar();
        cancelar.ThrowIfCancellationRequested();
        BulkAnalysis bulk;
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo) bulk = new BulkAnalysis(sav, ajustes);
        cancelar.ThrowIfCancellationRequested();
        if (bulk.Valid) return new(true, bulk.AllData.Count, [], string.Empty);
        var loc = LegalityLocalizationSet.GetLocalization(idioma ?? Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);
        string informe;
        var hallazgos = new List<HallazgoBulk>();
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo)
        {
            informe = bulk.Report(loc);
            var contexto = LegalityLocalizationContext.Create(bulk.AllAnalysis[0], loc);
            foreach (var (chk, comentario, i1, i2) in bulk.Parse)
                hallazgos.Add(new HallazgoBulk(loc.Description(chk.Judgement), chk.Result is LegalityCheckResultCode.External ? loc.Description(chk.Judgement) : contexto.Humanize(chk), comentario.Replace(Environment.NewLine, " ").Trim(), i1, i2, chk.Valid));
        }
        return new(false, bulk.AllData.Count, hallazgos, informe);
    }
}
