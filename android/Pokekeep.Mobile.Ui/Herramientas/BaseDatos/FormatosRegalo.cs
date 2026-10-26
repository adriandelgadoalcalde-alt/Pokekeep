// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.BaseDatos;

/// <summary>Un archivo de regalo leído: la tarjeta, o por qué no se puede usar (en castellano).</summary>
public sealed record RegaloLeido(DataMysteryGift? Regalo, string? Motivo)
{
    public bool Valido => Regalo is not null;
}

/// <summary>
/// Los formatos de archivo de regalo misterioso (PK-IO-025) y el filtro por contexto de <c>WinFormsUtil.GetMysterGiftFilter</c> (PK-IO-026). En Android los selectores del sistema (SAF) no filtran por extensión: el
/// filtro se traduce en la extensión del nombre sugerido (la del contexto de la tarjeta) y en el texto que dice qué archivos valen. Leer acepta TODOS los formatos que el Core lee por extensión
/// (<c>MysteryGift.GetMysteryGift(data, ext)</c>), también los de Gen. 8 y 9 que no tienen álbum en la partida.
/// </summary>
public static class FormatosRegalo
{
    /// <summary>Las extensiones que el Core lee como tarjeta (las de <c>MysteryGift.GetMysteryGift(Memory, ReadOnlySpan)</c>, en su orden).</summary>
    public static IReadOnlyList<string> Todas { get; } =
        [".pgt", ".pcd", ".wc4", ".pgf", ".wc6", ".wc7", ".wr7", ".wb7", ".wb7full", ".wc8", ".wc8full", ".wb8", ".wa8", ".wc9", ".wa9", ".wc5full", ".wc6full", ".wc7full"];

    /// <summary><c>GetMysterGiftFilter</c>: las extensiones del filtro de cada contexto (sin «Todos los archivos»). Vacío si el contexto no tiene tarjetas (Gen. 1-3).</summary>
    public static IReadOnlyList<string> Extensiones(EntityContext contexto) => contexto switch
    {
        EntityContext.Gen4 => [".pgt", ".pcd", ".wc4"],
        EntityContext.Gen5 => [".pgf", ".wc5full"],
        EntityContext.Gen6 => [".wc6", ".wc6full"],
        EntityContext.Gen7 => [".wc7", ".wc7full"],
        EntityContext.Gen8 => [".wc8"],
        EntityContext.Gen9 => [".wc9"],
        EntityContext.Gen7b => [".wr7"],
        EntityContext.Gen8b => [".wb8"],
        EntityContext.Gen8a => [".wa8"],
        EntityContext.Gen9a => [".wa9"],
        _ => [],
    };

    /// <summary>El nombre del filtro de PKHeX en castellano («Gen4 Mystery Gift» → «Regalo misterioso de la 4.ª generación»…). Vacío si el contexto no tiene tarjetas.</summary>
    public static string Nombre(EntityContext contexto) => contexto switch
    {
        EntityContext.Gen4 or EntityContext.Gen5 or EntityContext.Gen6 or EntityContext.Gen7 or EntityContext.Gen8 or EntityContext.Gen9 => IdiomaInterfaz.F("FormatosRegalo.RegaloMisteriosoGeneracion", contexto.Generation),
        EntityContext.Gen7b => IdiomaInterfaz.T("FormatosRegalo.RegistroRegaloLetS"),
        EntityContext.Gen8b => IdiomaInterfaz.T("FormatosRegalo.RegaloDiamanteBrillantePerla"),
        EntityContext.Gen8a => IdiomaInterfaz.T("FormatosRegalo.RegaloLeyendasPokemonArceus"),
        EntityContext.Gen9a => IdiomaInterfaz.T("FormatosRegalo.RegaloLeyendasPokemonZ"),
        _ => string.Empty,
    };

    /// <summary>El filtro como texto («Regalo misterioso de la 6.ª generación (.wc6, .wc6full)»), o vacío.</summary>
    public static string Filtro(EntityContext contexto)
    {
        var ext = Extensiones(contexto);
        return ext.Count == 0 ? string.Empty : $"{Nombre(contexto)} ({string.Join(", ", ext)})";
    }

    /// <summary>
    /// El nombre del archivo al exportar una tarjeta (<c>ExportMGDialog</c>): <c>PathUtil.CleanFileName(gift.FileName)</c>, con la extensión de su tipo, que es una del filtro de su contexto
    /// (<see cref="Extensiones"/>) salvo en las <c>.wb7</c> de Let's Go, que PKHeX también guarda así (por «Todos los archivos»): el formato no se cambia al guardar.
    /// </summary>
    public static string NombreArchivo(DataMysteryGift regalo) => PathUtil.CleanFileName(regalo.FileName);

    /// <summary>
    /// Lee un archivo de tarjeta de cualquier formato con <c>MysteryGift.GetMysteryGift(data, ext)</c> (la extensión decide el formato, como al abrir un archivo en PKHeX). Pasa al Core una COPIA: las tarjetas de
    /// Gen. 4 se descifran in situ.
    /// </summary>
    public static RegaloLeido Leer(ReadOnlySpan<byte> datos, string nombre)
    {
        var ext = Path.GetExtension(nombre ?? string.Empty).ToLowerInvariant();
        if (datos.Length == 0) return new(null, IdiomaInterfaz.T("Intercambio.ArchivoVacio"));
        if (ext.Length == 0) return new(null, IdiomaInterfaz.T("FormatosRegalo.ArchivoTieneExtensionSabe"));
        if (!Todas.Contains(ext)) return new(null, IdiomaInterfaz.F("FormatosRegalo.ExtensionTarjetaRegalo", ext));
        DataMysteryGift? g;
        try { g = MysteryGift.GetMysteryGift(datos.ToArray(), ext); }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or InvalidOperationException) { return new(null, IdiomaInterfaz.T("FormatosRegalo.ArchivoDanadoPuedeLeer")); }
        return g is null ? new(null, IdiomaInterfaz.F("FormatosRegalo.ArchivoTieneTamanoTarjeta", ext, datos.Length)) : new(g, null);
    }

    /// <summary>
    /// Por qué no se puede sacar el código QR de una tarjeta (<c>ExportQRFromView</c>), o null si se puede: ranura vacía y el Ticket Eón de Gen. 6, que no llega bien por QR. Los mensajes son los del Core.
    /// </summary>
    public static string? MotivoSinQr(DataMysteryGift regalo, SaveFile sav)
    {
        if (regalo.IsEmpty) return MessageStrings.MsgMysteryGiftSlotNone;
        if (sav.Generation == 6 && regalo is { ItemID: 726, IsItem: true }) return $"{MessageStrings.MsgMysteryGiftQREonTicket} {MessageStrings.MsgMysteryGiftQREonTicketAdvice}";
        return null;
    }

    /// <summary>El texto del código QR de una tarjeta (<c>QREncode.GenerateQRCode(DataMysteryGift)</c> = <c>QRMessageUtil.GetMessage</c>).</summary>
    public static string MensajeQr(DataMysteryGift regalo) => QRMessageUtil.GetMessage(regalo);

    /// <summary>La descripción bajo el código (<c>$"({mg.Type}) {mg.GetDescription()}"</c> + la firma de PKHeX).</summary>
    public static string DescripcionQr(DataMysteryGift regalo, GameStrings textos)
        => $"({regalo.Type}) {string.Join(Environment.NewLine, regalo.GetDescription(textos))}{Environment.NewLine}PKHeX Wonder Card @ ProjectPokemon.org";
}
