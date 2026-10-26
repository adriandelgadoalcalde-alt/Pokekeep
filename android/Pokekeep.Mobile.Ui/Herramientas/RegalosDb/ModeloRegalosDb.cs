// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using PKHeX.Core.Searching;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Herramientas.BaseDatos;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.RegalosDb;

/// <summary>El comparador del formato (<c>CB_FormatComparator</c>: «Cualquiera», «&gt;=», «==», «&lt;=»).</summary>
public enum ComparadorFormato { Cualquiera = 0, MayorOIgual = 1, Igual = 2, MenorOIgual = 3 }

/// <summary>
/// Los filtros de las pestañas «General» y «Avanzado» de <c>SAV_MysteryGiftDB</c> (PK-SUB-019, PK-SUB-020): formato (comparador y generación; <see cref="ModeloRegalosDb.FormatoCualquiera"/> es la entrada
/// «Cualquiera» de la lista), especie y objeto (-1 = cualquiera), hasta cuatro movimientos, variocolor y huevo (null = indiferente, la casilla «a medias» de WinForms) y las instrucciones de filtro.
/// </summary>
public sealed record FiltrosRegalos(ComparadorFormato Comparador, byte Formato, int Especie, int Objeto, IReadOnlyList<ushort> Movimientos, bool? Variocolor, bool? Huevo, string Instrucciones)
{
    /// <summary>Como el formulario al abrirse: «&lt;=» la generación de la partida (<c>ChangeFormatFilter</c>) y el resto a «Cualquiera».</summary>
    public static FiltrosRegalos Iniciales(SaveFile sav) => new(ComparadorFormato.MenorOIgual, ModeloRegalosDb.FormatoInicial(sav), -1, -1, [], null, null, string.Empty);
}

/// <summary>El Pokémon de una tarjeta ya convertido al formato de la partida, o por qué no se pudo (texto del Core: <c>EntityConverter</c> o <c>MsgPKMMysteryGiftFail</c>).</summary>
public sealed record RegaloGenerado(PKM? Pokemon, string? Error)
{
    public bool Correcto => Pokemon is not null;
}

/// <summary>
/// «Base de datos de regalos misteriosos» (<c>SAV_MysteryGiftDB</c>, PK-SUB-018) sin pantalla, con las MISMAS llamadas del Core y en el mismo orden que el formulario: la carga (<c>LoadDatabase</c>), las listas
/// (<c>PopulateComboBoxes</c>), la búsqueda (<c>B_Search_Click</c>), «Ver» (<c>ClickView</c>), «Guardar PK» (<c>ClickSavePK</c>), «Guardar regalo» (<c>ClickSaveMG</c>), «Exportar resultados a carpeta»
/// (<c>Menu_Export_Click</c>) e «Importar resultados a partida» (<c>Menu_Import_Click</c>). Además «Abrir un archivo de regalo» (<c>Main.OpenMysteryGift</c>, PK-IO-016). Los ajustes <c>MysteryDb</c> se reciben
/// en cada llamada (el que llama pasa los VIGENTES). Solo lee la partida salvo <see cref="ImportarACajas"/>, que escribe por <c>Sesion.Casillas</c> (un Deshacer) y solo por orden del usuario (regla 13).
/// </summary>
public static class ModeloRegalosDb
{
    /// <summary><c>MAXFORMAT</c> = <c>Latest.Generation</c>.</summary>
    public const byte FormatoMaximo = Latest.Generation;

    /// <summary>La entrada «Cualquiera» de <c>CB_Format</c> (índice 0): <c>MAXFORMAT + 1 - 0</c>.</summary>
    public const byte FormatoCualquiera = FormatoMaximo + 1;

    /// <summary>Las entradas de <c>CB_Format</c> en su orden, con la generación que da cada una (<c>MAXFORMAT + 1 - índice</c>) y su texto de PKHeX (la primera, «Cualquiera»).</summary>
    public static IReadOnlyList<(byte Formato, string Texto)> Formatos =>
    [
        (FormatoCualquiera, AccionesResultado.Cualquiera), (9, ".wc9"), (8, ".wc8"), (7, ".wc7"), (6, ".wc6"), (5, ".pgf"), (4, ".pcd/pgt/.wc4"),
    ];

    /// <summary><c>ChangeFormatFilter</c> con un comparador distinto de «Cualquiera»: la generación de la partida si está en la lista; si no (Gen. 1-3), «Cualquiera».</summary>
    public static byte FormatoInicial(SaveFile sav)
    {
        var indice = FormatoMaximo - sav.Generation + 1;
        return indice < Formatos.Count ? Formatos[indice].Formato : FormatoCualquiera;
    }

    /// <summary>
    /// <c>LoadDatabase</c>: todos los regalos con Pokémon del Core y de la carpeta de la base de datos (<c>EncounterEvent.GetAllEvents()</c>, ya ordenados por especie), sin los que la partida no puede recibir si el
    /// ajuste <c>MysteryDb.FilterUnavailableSpecies</c> está activo (<c>EntityPresenceFilters.GetFilterGift</c>), y con <c>GiftUsed = false</c> (las tarjetas de la base son plantillas).
    /// </summary>
    public static List<MysteryGift> Cargar(SaveFile sav, MysteryGiftDatabaseSettings ajustes)
    {
        Pokekeep.Mobile.Ui.Ajustes.Pkhex.TextosNucleo.Preparar();
        var db = EncounterEvent.GetAllEvents();
        if (ajustes.FilterUnavailableSpecies && EntityPresenceFilters.GetFilterGift<MysteryGift>(sav.Context, sav.Generation) is { } filtro)
            db = db.Where(filtro);
        var lista = db.ToList();
        foreach (var mg in lista) mg.GiftUsed = false;
        return lista;
    }

    /// <summary><c>PopulateComboBoxes</c>: las especies de la lista de la partida que tienen algún regalo en la base (las demás no se ofrecen).</summary>
    public static IEnumerable<ComboItem> Especies(IEnumerable<ComboItem> especiesFiltradas, IReadOnlyList<MysteryGift> todos)
    {
        var presentes = todos.Select(mg => (int)mg.Species).ToHashSet();
        return especiesFiltradas.Where(z => presentes.Contains(z.Value));
    }

    /// <summary><c>B_Search_Click</c>: formato, especie, objeto, movimientos, variocolor, huevo e instrucciones, en ese orden y con las mismas comparaciones.</summary>
    public static List<MysteryGift> Buscar(IEnumerable<MysteryGift> todos, FiltrosRegalos f)
    {
        var res = todos;
        var format = f.Formato;
        switch (f.Comparador)
        {
            case ComparadorFormato.MayorOIgual: res = res.Where(mg => mg.Generation >= format); break;
            case ComparadorFormato.Igual: res = res.Where(mg => mg.Generation == format); break;
            case ComparadorFormato.MenorOIgual: res = res.Where(mg => mg.Generation <= format); break;
        }
        if (f.Especie != -1) res = res.Where(pk => pk.Species == f.Especie);
        if (f.Objeto != -1) res = res.Where(pk => pk.HeldItem == f.Objeto);
        foreach (var m in f.Movimientos) res = res.Where(mg => mg.HasMove(m));
        if (f.Variocolor is { } s) res = res.Where(pk => pk.IsShiny == s);
        if (f.Huevo is { } e) res = res.Where(pk => pk.IsEgg == e);
        if (InstruccionesFiltro.Crear<MysteryGift>(f.Instrucciones) is { } instr) res = res.Where(instr);
        return [.. res];
    }

    /// <summary>
    /// <c>ClickView</c> (y <c>Main.OpenMysteryGift</c>): el Pokémon de la tarjeta sin restricciones, convertido al formato de la partida, adaptado a ella y con la suma recalculada. Bajo el cerrojo de legalidad
    /// porque el Core analiza al generar.
    /// </summary>
    public static RegaloGenerado Generar(MysteryGift regalo, SaveFile sav)
    {
        if (!regalo.IsEntity) return new(null, MessageStrings.MsgPKMMysteryGiftFail);
        Pokekeep.Mobile.Ui.Ajustes.Pkhex.TextosNucleo.Preparar();
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo)
        {
            var temp = regalo.ConvertToPKM(sav, EncounterCriteria.Unrestricted);
            var pk = EntityConverter.ConvertToType(temp, sav.PKMType, out var c);
            if (pk is null) return new(null, c.GetDisplayString(temp, sav.PKMType));
            sav.AdaptToSaveFile(pk);
            pk.RefreshChecksum();
            return new(pk, null);
        }
    }

    /// <summary><c>ClickSavePK</c>: el Pokémon de la tarjeta en SU formato (PKHeX no lo convierte al de la partida al guardarlo como archivo).</summary>
    public static PKM PokemonDeLaTarjeta(MysteryGift regalo, SaveFile sav)
    {
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo) return regalo.ConvertToPKM(sav);
    }

    /// <summary><c>ClickSaveMG</c>: la tarjeta que se puede guardar, o el motivo (<c>MsgExportWC3DataFail</c>: las de Gen. 3 no tienen datos).</summary>
    public static (DataMysteryGift? Tarjeta, string? Motivo) Tarjeta(MysteryGift regalo)
        => regalo is DataMysteryGift g ? (g, null) : (null, MessageStrings.MsgExportWC3DataFail);

    /// <summary><c>Menu_Export_Click</c>: un archivo por tarjeta con datos (las de Gen. 3 no tienen), con <c>PathUtil.CleanFileName(gift.FileName)</c> y <c>gift.Write()</c>.</summary>
    public static IEnumerable<(string Nombre, byte[] Datos)> ArchivosExportables(IEnumerable<MysteryGift> resultados)
        => resultados.OfType<DataMysteryGift>().Select(g => (FormatosRegalo.NombreArchivo(g), g.Write().ToArray()));

    /// <summary>
    /// Los ajustes de importación de <see cref="ImportarACajas"/>: el Pokémon se adapta a la partida como en PKHeX (<c>UpdateToSaveFile</c> = el ajuste global), pero NO se toca la Pokédex ni los récords, como en
    /// cualquier otra escritura en las cajas de la app (<c>SlotEditor</c> escribe con la importación desactivada): así «Deshacer» deja la partida byte a byte como estaba. En PKHeX, sin Deshacer para esta
    /// acción, la Pokédex se marca si el ajuste global lo dice.
    /// </summary>
    public static EntityImportSettings AjustesImportacion { get; } = new(EntityImportOption.UseDefault, EntityImportOption.Disable, EntityImportOption.Disable);

    /// <summary>
    /// <c>Menu_Import_Click</c>: <c>SaveFile.LoadBoxes(resultados, …)</c> desde <paramref name="caja"/>, vaciando antes las cajas desde ella si <paramref name="vaciar"/> (la pregunta <c>MsgSaveBoxImportClear</c>) y sin
    /// sobrescribir casillas ocupadas, como <c>GetBulkImportSettings</c>, con <see cref="AjustesImportacion"/>. Es UNA entrada del historial sobre todas las casillas desde esa caja (Deshacer exacto).
    /// </summary>
    public static InformeOperacion ImportarACajas(Sesion sesion, IReadOnlyList<MysteryGift> resultados, int caja, bool vaciar)
    {
        var sav = sesion.Guardado;
        if (!sav.HasBox) return InformeOperacion.Falla(TipoResultado.NoAdmitido, MessageStrings.MsgSaveBoxFailNone);
        var casillas = new List<ISlotInfo>();
        for (var b = caja; b < sav.BoxCount; b++) for (var s = 0; s < sav.BoxSlotCount; s++) casillas.Add(new SlotInfoBox(b, s, sav));
        var mensaje = string.Empty;
        int tocadas;
        try
        {
            lock (Pokekeep.Sprites.LegalityGate.Cerrojo)
                tocadas = sesion.Casillas.Lote(casillas, () =>
                {
                    var n = sav.LoadBoxes((IEnumerable<IEncounterConvertible>)resultados, out mensaje, caja, vaciar, false, AjustesImportacion);
                    // Menu_Import_Click: con ctr <= 0 no hace nada más (sin aviso de éxito). El Core ya vació las cajas ANTES de contar (LoadBoxes: ClearBoxes y luego ImportPKMs): si no entró ninguna tarjeta,
                    // se deshace ese vaciado (Lote revierte si la acción lanza) para que «no se ha importado nada» sea cierto y no se pierda lo que había sin posibilidad de Deshacer.
                    if (n <= 0) throw new NadaImportadoException(mensaje);
                    return n;
                }, descripcion: IdiomaInterfaz.T("ModeloRegalosDb.ImportarResultadosBaseRegalos"));
        }
        catch (NadaImportadoException e) { return InformeOperacion.Falla(TipoResultado.SinCambios, e.Message.Length > 0 ? e.Message : MessageStrings.MsgSaveBoxImportNoFiles); }
        return InformeOperacion.Ok(IdiomaInterfaz.F("Herramientas.PuedesDeshacerloConMensaje", mensaje.TrimEnd('.')), tocadas);
    }

    private sealed class NadaImportadoException(string mensaje) : Exception(mensaje);

    /// <summary>
    /// «Abrir un archivo de regalo» (<c>Main.OpenMysteryGift</c>, PK-IO-016) para CUALQUIER formato (PK-IO-025): la tarjeta leída por su extensión y su Pokémon en el formato de la partida; si la tarjeta no
    /// tiene Pokémon, <c>MsgPKMMysteryGiftFail</c>; si no se puede convertir, el motivo del Core.
    /// </summary>
    public static (DataMysteryGift? Tarjeta, RegaloGenerado Generado) Abrir(ReadOnlySpan<byte> datos, string nombre, SaveFile sav)
    {
        var leido = FormatosRegalo.Leer(datos, nombre);
        if (leido.Regalo is not { } g) return (null, new(null, leido.Motivo));
        return (g, Generar(g, sav));
    }
}
