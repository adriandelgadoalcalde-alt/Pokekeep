// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Herramientas.Showdown;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Cajas;

/// <summary>Lo que hay dentro de un archivo que el usuario quiere llevar a una casilla o a una caja (la clasificacion de <c>Main.LoadFile</c> de PKHeX).</summary>
public abstract record Importado;
/// <summary>Un Pokemon ya convertido al formato de la partida, con los avisos de compatibilidad del Core (vacio = ninguno).</summary>
public sealed record ImportadoPokemon(PKM Pokemon, IReadOnlyList<string> Avisos, string Origen) : Importado;
/// <summary>Un volcado de caja (<c>boxdata.bin</c>) o de todo el PC (<c>pcdata.bin</c>) del mismo formato que la partida.</summary>
public sealed record ImportadoCajas(byte[] Datos, bool TodoElPc) : Importado;
/// <summary>Un grupo de Pokemon (equipos de alquiler de Gen. 8-9, videos de batalla): se vuelcan en la caja actual.</summary>
public sealed record ImportadoGrupo(IPokeGroup Grupo, int Cantidad) : Importado;
public sealed record ImportadoError(string Mensaje) : Importado;

/// <summary>
/// Importar y exportar entidades y cajas con el Core de PKHeX: <c>.pk*</c> (descifrado) y <c>.ek*</c> (cifrado), el texto de Showdown, el resumen del Pokemon y los volcados de caja.
/// Nada de esto toca el guardado: devuelven datos para que la interfaz los ponga por la via del historial (<see cref="EditorCasillas"/>).
/// </summary>
public static class Intercambio
{
    // ---- Exportar ----
    /// <summary>Nombre de archivo del Pokemon como lo propone PKHeX (<c>FileName</c>: especie, PID y extension del formato), sin caracteres no validos.</summary>
    public static string NombreArchivo(PKM pk, bool cifrado = false)
    {
        var baseNombre = pk.FileNameWithoutExtension;
        return PathUtil.CleanFileName(cifrado ? $"{baseNombre}.ek{pk.Format}" : $"{baseNombre}.{pk.Extension}");
    }

    /// <summary>
    /// Bytes del archivo como los exporta PKHeX (<c>.pk*</c>: datos descifrados con la parte de equipo, <c>SIZE_PARTY</c>; <c>.ek*</c>: la misma cifrada). El Core los acepta al importar y rellena lo que falta.
    /// En Gen. 1-2 no hay version cifrada.
    /// </summary>
    public static byte[] Bytes(PKM pk, bool cifrado = false)
    {
        var datos = new byte[pk.SIZE_PARTY];
        if (cifrado && pk.Format >= 3) pk.WriteEncryptedDataParty(datos); else pk.WriteDecryptedDataParty(datos);
        return datos;
    }

    /// <summary>Texto de un set de Showdown (ingles, el idioma que Showdown entiende) listo para pegar.</summary>
    public static string Showdown(PKM pk) => ShowdownParsing.GetShowdownText(pk);

    /// <summary>El set de Showdown con los ajustes <c>BattleTemplate.Export</c> vigentes (idioma, estilos y orden de PKHeX; bloque 10-9).</summary>
    public static string Showdown(PKM pk, AjustesPkhex ajustes) => ModeloShowdown.Exportar(pk, ajustes);

    /// <summary>
    /// Aplica un set de Showdown (ingles) a un Pokemon como el editor de PKHeX (<c>LoadShowdownSet</c>: <c>ApplySetDetails</c>). Devuelve el motivo si el texto no es un set o el Core no pudo
    /// aplicarlo; en ese caso el Pokemon queda como estaba.
    /// </summary>
    public static string? AplicarShowdown(PKM pk, string texto)
    {
        var copia = pk.Data.ToArray();
        try
        {
            var set = ShowdownParsing.GetShowdownSets(texto).FirstOrDefault() ?? new ShowdownSet(string.Empty);   // como PKHeX: SOLO el primer set (y sin descargar nada, regla 15)
            if (set.Species == 0 || set.Species > pk.MaxSpeciesID) return IdiomaInterfaz.T("Intercambio.TextoContieneSetShowdown");
            lock (Pokekeep.Sprites.LegalityGate.Cerrojo) pk.ApplySetDetails(set);
            return null;
        }
        catch (Exception ex)
        {
            copia.CopyTo(pk.Data);
            return IdiomaInterfaz.T("Intercambio.PodidoAplicarSet") + ex.Message;
        }
    }

    public static string Showdown(IEnumerable<PKM> pokemon) => ShowdownParsing.GetShowdownSets(pokemon.Where(p => p.Species != 0), Environment.NewLine + Environment.NewLine);

    /// <summary>Los sets de varios Pokémon con los ajustes <c>BattleTemplate.Export</c> vigentes (los huecos se saltan).</summary>
    public static string Showdown(IEnumerable<PKM> pokemon, EntityContext contexto, AjustesPkhex ajustes) => ModeloShowdown.ExportarLista(pokemon, contexto, ajustes);

    /// <summary>
    /// Resumen del Pokemon en castellano (el texto del hover de PKHeX, <c>SummaryPreviewer</c>): especie, naturaleza, habilidad, objeto, movimientos y estadisticas; con <paramref name="encuentro"/>, el encuentro que el analisis encontro.
    /// </summary>
    public static string Resumen(PKM pk, StorageSlotType tipo = StorageSlotType.None, bool encuentro = false, AjustesPkhex? ajustes = null)
    {
        string texto;
        try { texto = ajustes is null ? ShowdownParsing.GetLocalizedPreviewText(pk, new BattleTemplateExportSettings(Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma)) : ModeloShowdown.TextoVista(pk, ajustes); }   // con ajustes: el hover de PKHeX (BattleTemplate.Hover)
        catch { texto = ShowdownParsing.GetShowdownText(pk); }
        if (!encuentro) return texto;
        var la = Pokekeep.Mobile.Ui.Modelo.AnalizadorLegalidad.AnalizarPara(pk, tipo);
        if (la is null) return texto;
        var lineas = new List<string> { texto, string.Empty };
        LegalityFormatting.AddEncounterInfo(LegalityLocalizationContext.Create(la, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma), lineas);
        return string.Join(Environment.NewLine, lineas);
    }

    /// <summary>Informe de legalidad en castellano (el de <c>Legalidad</c> del menu de casilla de PKHeX), detallado si <paramref name="detalle"/>.</summary>
    public static (bool Valido, string Texto) InformeLegalidad(PKM pk, StorageSlotType tipo, bool detalle = false)
    {
        var la = Pokekeep.Mobile.Ui.Modelo.AnalizadorLegalidad.AnalizarPara(pk, tipo);
        if (la is null) return (false, IdiomaInterfaz.T("Intercambio.PodidoAnalizarPokemon"));
        return (la.Valid, la.Report(Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma, detalle));
    }

    // ---- Importar ----
    /// <summary>
    /// Clasifica y convierte el contenido de un archivo como hace PKHeX al soltarlo sobre una casilla: <c>.pk*</c>/<c>.ek*</c>, tarjetas de regalo misterioso con entidad, volcados de caja del mismo formato
    /// y grupos. El Pokemon se convierte al formato de la partida (<c>EntityConverter</c>); si no se puede, el error dice por que.
    /// </summary>
    public static Importado Analizar(byte[] datos, string nombre, SaveFile sav)
    {
        if (datos.Length == 0) return new ImportadoError(IdiomaInterfaz.T("Intercambio.ArchivoVacio"));
        if (FileUtil.IsFileTooBig(datos.Length)) return new ImportadoError(IdiomaInterfaz.T("Intercambio.ArchivoDemasiadoGrandeSer"));
        object? contenido;
        try { contenido = FileUtil.GetSupportedFile(datos, Path.GetExtension(nombre.AsSpan()), sav); }
        catch (Exception ex) { return new ImportadoError(IdiomaInterfaz.T("Editor.PodidoLeerArchivo") + ex.GetType().Name); }
        switch (contenido)
        {
            case PKM pk: return Convertir(pk, sav, nombre);
            case ConcatenatedEntitySet cj:
                return new ImportadoCajas(cj.Data.ToArray(), cj.Data.Length == sav.SIZE_BOXSLOT * sav.SlotCount);
            case IPokeGroup g:
                var n = g.Contents.Count();
                return n == 0 ? new ImportadoError(IdiomaInterfaz.T("Intercambio.GrupoContienePokemon")) : new ImportadoGrupo(g, n);
            case MysteryGift mg:
                if (!mg.IsEntity) return new ImportadoError(IdiomaInterfaz.T("Intercambio.TarjetaRegaloMisteriosoContiene"));
                return Convertir(mg.ConvertToPKM(sav), sav, nombre);
            case IEncounterConvertible enc:
                return Convertir(enc.ConvertToPKM(sav), sav, nombre);
            case SaveFile:
                return new ImportadoError(IdiomaInterfaz.T("Intercambio.ArchivoGuardadoPokemonAbrelo"));
            default:
                return new ImportadoError(MessageStrings.MsgPKMUnsupported);
        }
    }

    private static Importado Convertir(PKM origen, SaveFile sav, string nombre)
    {
        var destino = sav.PKMType;
        var pk = EntityConverter.ConvertToType(origen, destino, out var resultado);
        if (pk is null) return new ImportadoError(resultado.GetDisplayString(origen, destino));
        if (sav is ILangDeviantSave il && !EntityConverter.IsCompatibleGB(origen, il.Japanese, pk.Japanese))
            return new ImportadoError(EntityConverterResult.IncompatibleLanguageGB.GetIncompatibleGBMessage(pk, il.Japanese));
        var avisos = sav.EvaluateCompatibility(pk, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);
        return new ImportadoPokemon(pk, avisos, nombre);
    }

    // ---- Cajas ----
    /// <summary>Nombre de archivo del volcado de caja (<c>boxdata &lt;nombre&gt;.bin</c>) o del PC entero (<c>pcdata.bin</c>), como PKHeX.</summary>
    public static string NombreVolcado(string nombreCaja, bool todas) => PathUtil.CleanFileName(todas ? "pcdata.bin" : $"boxdata {nombreCaja}.bin");

    public static byte[] VolcarCaja(SaveFile sav, int caja) => sav.GetBoxBinary(caja);
    public static byte[] VolcarTodo(SaveFile sav) => sav.GetPCBinary();

    /// <summary>
    /// Mensaje de por que NO se puede cargar un volcado en la partida (casillas bloqueadas por Cajas de combate o tamano de otra generacion); null si cabe. Igual que <c>SAVEditor.OpenPCBoxBin</c>.
    /// </summary>
    public static string? MotivoNoSeCarga(SaveFile sav, byte[] datos, int caja)
    {
        if (sav.GetPCBinary().Length == datos.Length)
            return sav.IsAnySlotLockedInBox(0, sav.BoxCount - 1) ? MessageStrings.MsgSaveBoxImportPCFailBattle : null;
        if (sav.GetBoxBinary(caja).Length == datos.Length)
            return sav.IsAnySlotLockedInBox(caja, caja) ? MessageStrings.MsgSaveBoxImportBoxFailBattle : null;
        return string.Format(MessageStrings.MsgSaveCurrentGeneration, sav.Generation);
    }
}
