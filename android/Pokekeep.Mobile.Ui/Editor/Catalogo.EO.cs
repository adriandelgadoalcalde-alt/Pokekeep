// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Editor;

internal static partial class Catalogo
{
    private static bool SinRegion(ContextoEditor c) => c.Pk is IRegionOrigin && !(c.Sav is SAV7b && !c.Avanzado);

    internal static PestanaDef EO() => new("eo", IdiomaInterfaz.T("Catalogo.EOVarios"),
    [
        new(IdiomaInterfaz.T("Catalogo.EntrenadorOriginal"),
        [
            F.Entero("PK-ED-083", "TID", c => c.Pk.DisplayTID, (c, v) => PonerIdentificador(c.Pk, (uint)v, null), 0, 999999, maxDinamico: c => c.Pk.TrainerIDDisplayFormat == TrainerIDFormat.SixDigit ? 999999 : 65535,
                ayuda: IdiomaInterfaz.T("Catalogo.NumeroEntrenador")),
            F.Entero("PK-ED-083", "SID", c => c.Pk.DisplaySID, (c, v) => PonerIdentificador(c.Pk, null, (uint)v), 0, 65535, visible: c => c.Formato >= 3, maxDinamico: c => c.Pk.TrainerIDDisplayFormat == TrainerIDFormat.SixDigit ? 4294 : 65535,
                ayuda: IdiomaInterfaz.T("Catalogo.NumeroSecretoEntrenador")) with { Indice = 1 },
            F.Info("PK-ED-083", IdiomaInterfaz.T("Informe.Columna.TSV"), c => TextoTsv(c.Pk), visible: c => c.Formato >= 3) with { Indice = 2 },
            F.Texto("PK-ED-084", IdiomaInterfaz.T("Catalogo.NombreEO"), c => c.Pk.OriginalTrainerName, (c, t) => { c.Pk.OriginalTrainerName = t; return null; }, c => c.Pk.MaxStringLengthTrainer, ayuda: IdiomaInterfaz.T("Catalogo.NombreEntrenadorOriginal")),
            F.Info("PK-ED-086", IdiomaInterfaz.T("Catalogo.AvisoTipografia"), c => IdiomaInterfaz.T("Catalogo.NombreEOTieneCaracteres"), visible: c => AvisoFuente(c, c.Pk.OriginalTrainerName)) with { Indice = 1 },
            F.Opcion("PK-ED-085", IdiomaInterfaz.T("Informe.Columna.OriginalTrainerGender"), c => [new(0, IdiomaInterfaz.T("Catalogo.ChicoSexo")), new(1, IdiomaInterfaz.T("Catalogo.Chica"))], c => c.Pk.OriginalTrainerGender & 1, (c, id) => { c.Pk.OriginalTrainerGender = (byte)id; return null; }, visible: c => c.Formato >= 2),
            F.Entero("PK-ED-087", IdiomaInterfaz.T("Catalogo.AmistadEO"), c => c.Pk.OriginalTrainerFriendship, (c, v) => c.Pk.OriginalTrainerFriendship = (byte)v, 0, 255, visible: c => c.Formato >= 2 && !c.Pk.IsEgg, ayuda: IdiomaInterfaz.T("Catalogo.RangoDe0a255")),
            F.Entero("PK-ED-087", IdiomaInterfaz.T("Catalogo.CiclosEclosion"), c => c.Pk.OriginalTrainerFriendship, (c, v) => c.Pk.OriginalTrainerFriendship = (byte)v, 0, 255, visible: c => c.Formato >= 2 && c.Pk.IsEgg, ayuda: IdiomaInterfaz.T("Catalogo.PasosHuevoRestantes")) with { Indice = 1 },
            F.Accion("PK-ED-087", IdiomaInterfaz.T("Catalogo.AmistadMaximaBase"), c => { var p = c.Pk; p.OriginalTrainerFriendship = (byte)(p.IsEgg ? EggStateLegality.GetMaximumEggHatchCycles(p) : p.OriginalTrainerFriendship == 255 ? p.PersonalInfo.BaseFriendship : 255); }, visible: c => c.Formato >= 2,
                ayuda: IdiomaInterfaz.T("Catalogo.AlternaEntreAmistadBase")) with { Indice = 2 },
            F.Accion("PK-ED-087", IdiomaInterfaz.T("Catalogo.AmistadCero"), c => { var p = c.Pk; p.OriginalTrainerFriendship = (byte)(p.IsEgg ? EggStateLegality.GetMinimumEggHatchCycles(p) : 0); }, visible: c => c.Formato >= 2) with { Indice = 3 },
        ]),
        new(IdiomaInterfaz.T("Catalogo.RegionOrigen"),
        [
            F.Opcion("PK-ED-088", IdiomaInterfaz.T("Form.Pais"), c => c.F.Paises, c => ((IRegionOrigin)c.Pk).Country, (c, id) => { var r = (IRegionOrigin)c.Pk; r.Country = (byte)id; r.Region = 0; return null; }, visible: SinRegion),
            F.Opcion("PK-ED-088", IdiomaInterfaz.T("Catalogo.Subregion"), c => c.F.Subregiones(((IRegionOrigin)c.Pk).Country), c => ((IRegionOrigin)c.Pk).Region, (c, id) => { ((IRegionOrigin)c.Pk).Region = (byte)id; return null; }, visible: SinRegion,
                motivo: c => ((IRegionOrigin)c.Pk).Country == 0 ? IdiomaInterfaz.T("Catalogo.EligePrimeroPais") : null) with { Indice = 1 },
            F.Opcion("PK-ED-088", IdiomaInterfaz.T("Catalogo.Region3DS"), c => c.F.RegionesConsola, c => ((IRegionOrigin)c.Pk).ConsoleRegion, (c, id) => { ((IRegionOrigin)c.Pk).ConsoleRegion = (byte)id; return null; }, visible: SinRegion) with { Indice = 2 },
        ], SinRegion),
        new(IdiomaInterfaz.T("Catalogo.EntrenadorActualEO"),
        [
            F.Texto("PK-ED-089", IdiomaInterfaz.T("Catalogo.NombreEntrenadorActual"), c => c.Pk.HandlingTrainerName, (c, t) => { PonerEntrenadorActual(c.Pk, t); return null; }, c => c.Pk.MaxStringLengthTrainer, visible: c => c.Formato >= 6,
                ayuda: IdiomaInterfaz.T("Catalogo.NombreQuienTieneAhora")),
            F.Opcion("PK-ED-089", IdiomaInterfaz.T("Catalogo.SexoEntrenadorActual"), c => [new(0, IdiomaInterfaz.T("Catalogo.ChicoSexo")), new(1, IdiomaInterfaz.T("Catalogo.Chica"))], c => c.Pk.HandlingTrainerGender & 1, (c, id) => { c.Pk.HandlingTrainerGender = (byte)id; return null; },
                visible: c => c.Formato >= 6 && c.Pk.HandlingTrainerName.Length != 0) with { Indice = 1 },
            F.Opcion("PK-ED-090", IdiomaInterfaz.T("Catalogo.IdiomaEntrenadorActual"), c => [c.F.Vacio with { Texto = IdiomaInterfaz.T("Form.Ninguno") }, .. c.F.Idiomas], c => ((IHandlerLanguage)c.Pk).HandlingTrainerLanguage, (c, id) => { ((IHandlerLanguage)c.Pk).HandlingTrainerLanguage = (byte)id; return null; },
                visible: c => c.Formato >= 8 && c.Pk is IHandlerLanguage, leer: c => ((IHandlerLanguage)c.Pk).HandlingTrainerLanguage == 0 ? IdiomaInterfaz.T("Form.Ninguno") : FuentesEditor.Nombre(c.F.Idiomas, ((IHandlerLanguage)c.Pk).HandlingTrainerLanguage)),
            F.Entero("PK-ED-091", IdiomaInterfaz.T("Catalogo.AmistadEntrenadorActual"), c => c.Pk.HandlingTrainerFriendship, (c, v) => c.Pk.HandlingTrainerFriendship = (byte)v, 0, 255, visible: c => c.Formato >= 6, ayuda: IdiomaInterfaz.T("Catalogo.RangoDe0a255")),
            F.Opcion("PK-ED-092", IdiomaInterfaz.T("Catalogo.ManejadoAhora"), c => [new(0, IdiomaInterfaz.T("Catalogo.EntrenadorOriginal2")), new(1, IdiomaInterfaz.T("Catalogo.EntrenadorActual"))], c => c.Pk.CurrentHandler & 1, (c, id) => { c.Pk.CurrentHandler = (byte)id; return null; },
                visible: c => c.Formato >= 6, motivo: c => c.Pk.HandlingTrainerName.Length == 0 ? IdiomaInterfaz.T("Catalogo.EscribeEntrenadorActualPoder") : null),
        ], c => c.Formato >= 6),
        new(IdiomaInterfaz.T("Catalogo.CodigosInternos"),
        [
            F.Hex("PK-ED-095", IdiomaInterfaz.T("Catalogo.ConstanteCifradoEC"), 8, c => c.Pk.EncryptionConstant, (c, v) => c.Pk.EncryptionConstant = (uint)v, visible: c => c.Formato >= 6 && !OcultarSecretos, ayuda: IdiomaInterfaz.T("Catalogo.ConstanteCifradoHexadecimal")),
            F.Accion("PK-ED-095", IdiomaInterfaz.T("Catalogo.RegenerarEc"), c => Reglas.RegenerarEc(c.Pk), visible: c => c.Formato >= 6 && !OcultarSecretos, ayuda: IdiomaInterfaz.T("Catalogo.GeneraConstanteCifradoValida")) with { Indice = 1 },
            F.Hex("PK-ED-094", IdiomaInterfaz.T("Catalogo.RastreadorHome"), 16, c => ((IHomeTrack)c.Pk).Tracker, (c, v) => ((IHomeTrack)c.Pk).Tracker = v, visible: c => c.Formato >= 8 && c.Pk is IHomeTrack && !OcultarSecretos,
                ayuda: IdiomaInterfaz.T("Catalogo.IdentificadorHOMESoloEdita")),
            F.FechaHora("PK-ED-096", IdiomaInterfaz.T("Catalogo.RecibidoFechaHora"), c => FechaRecibido(c.Pk), (c, t) => PonerFechaRecibido(c.Pk, t), visible: c => c.Pk is PB7, ayuda: IdiomaInterfaz.T("Catalogo.DiaMesAnoHora")),
            F.Opcion("PK-ED-093", IdiomaInterfaz.T("Catalogo.ByteExtraPosicion"), c => [.. ((ReadOnlySpan<ushort>)c.Pk.ExtraBytes).ToArray().Select((o, i) => new OpcionCampo(i, $"0x{o:X2}"))], c => c.IndiceByteExtra, (c, id) => { c.IndiceByteExtra = id; return null; },
                visible: c => c.Formato >= 3 && c.Pk.ExtraBytes.Length != 0, avanzado: true, leer: c => $"0x{c.Pk.ExtraBytes[Math.Clamp(c.IndiceByteExtra, 0, c.Pk.ExtraBytes.Length - 1)]:X2}"),
            F.Entero("PK-ED-093", IdiomaInterfaz.T("Catalogo.ByteExtraValor"), c => c.Pk.Data[c.Pk.ExtraBytes[Math.Clamp(c.IndiceByteExtra, 0, c.Pk.ExtraBytes.Length - 1)]], (c, v) => c.Pk.Data[c.Pk.ExtraBytes[Math.Clamp(c.IndiceByteExtra, 0, c.Pk.ExtraBytes.Length - 1)]] = (byte)v, 0, 255,
                visible: c => c.Formato >= 3 && c.Pk.ExtraBytes.Length != 0, avanzado: true) with { Indice = 1 },
            F.Accion("PK-ED-124", IdiomaInterfaz.T("Catalogo.BytesTextoAvanzado"), c => c.Anfitrion?.AbrirSubeditor("basura"), visible: c => c.Avanzado, avanzado: true, ayuda: IdiomaInterfaz.T("Catalogo.MuestraLimpiaBytesSobrantes")) with { Indice = 2 },
        ]),
    ]);

    // ---- Auxiliares ----
    private static string? PonerIdentificador(PKM pk, uint? tid, uint? sid)
    {
        if (pk.TrainerIDDisplayFormat == TrainerIDFormat.SixDigit)
        {
            var t = tid ?? pk.GetTrainerTID7();
            var s = sid ?? pk.GetTrainerSID7();
            if (!pk.IsValidTrainerID7(s, t)) return IdiomaInterfaz.T("Catalogo.CombinacionTIDSIDCabe");
            pk.SetTrainerID7(s, t);
            return null;
        }
        if (tid is { } a) pk.DisplayTID = a;
        if (sid is { } b) pk.DisplaySID = b;
        return null;
    }

    private static string TextoTsv(PKM pk)
    {
        var tsv = pk.GetTSV(pk.Format);
        return tsv > ushort.MaxValue ? "—" : $"{tsv:D4} · {pk.GetTextRepresentation()}";
    }

    private static void PonerEntrenadorActual(PKM pk, string t)
    {
        pk.HandlingTrainerName = t;
        if (string.IsNullOrWhiteSpace(t))
        {
            pk.CurrentHandler = 0;      // sin entrenador actual vuelve a manejarlo el EO (UpdateNotOT)
            pk.HandlingTrainerGender = 0;
        }
    }

    private static string FechaRecibido(PKM pk)
    {
        if (pk is not PB7 p) return string.Empty;
        try
        {
            if (p is { ReceivedDate: { } d, ReceivedTime: { } t }) return new DateTime(d, t).ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        }
        catch { /* datos corruptos: se muestra vacio */ }
        return string.Empty;
    }

    private static string? PonerFechaRecibido(PKM pk, string texto)
    {
        if (pk is not PB7 p) return null;
        if (!DateTime.TryParseExact(texto.Trim(), ["dd/MM/yyyy HH:mm:ss", "d/M/yyyy H:mm:ss", "dd/MM/yyyy HH:mm", "d/M/yyyy H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return IdiomaInterfaz.T("Catalogo.EscribeFechaDiaMes");
        if (d.Year is < 2000 or > 2099) return IdiomaInterfaz.T("Catalogo.AnoTieneEstarEntre");
        p.ReceivedYear = (byte)(d.Year - 2000); p.ReceivedMonth = (byte)d.Month; p.ReceivedDay = (byte)d.Day;
        p.ReceivedHour = (byte)d.Hour; p.ReceivedMinute = (byte)d.Minute; p.ReceivedSecond = (byte)d.Second;
        return null;
    }
}
