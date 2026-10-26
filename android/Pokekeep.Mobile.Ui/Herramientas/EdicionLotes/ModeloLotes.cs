// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.EdicionLotes;

/// <summary>Sobre qué trabaja el editor por lotes: los botones de opción <c>RB_Boxes</c>, <c>RB_Party</c> y <c>RB_Path</c> de PKHeX.</summary>
public enum FuenteLotes { Cajas, Equipo, Carpeta }

/// <summary>
/// Lo que dicen las instrucciones antes de ejecutarlas (<c>BatchEditor.TryGetInstructionSets</c> sin cuadros de diálogo): los conjuntos, o el error con el texto de PKHeX en castellano, y las propiedades a las que se
/// asigna un valor vacío (PKHeX pregunta «¿Continuar?»; aquí la pantalla lo avisa y pide una segunda pulsación).
/// </summary>
public sealed record ValidacionLotes(StringInstructionSet[] Conjuntos, string? Error, IReadOnlyList<string> PropiedadesVacias)
{
    public bool Valida => Error is null;
    public bool HayVacias => PropiedadesVacias.Count > 0;
    /// <summary>El aviso de PKHeX para valores vacíos (<c>MsgBEPropertyEmpty</c> + las propiedades), sin la pregunta.</summary>
    public string AvisoVacias => HayVacias ? MessageStrings.MsgBEPropertyEmpty + " " + string.Join(", ", PropiedadesVacias) : string.Empty;
}

/// <summary>El resultado de ejecutar: las entradas modificadas (copias ya editadas, con su casilla de origen) y el mensaje de PKHeX (<c>EntityBatchProcessor.GetEditorResults</c>).</summary>
public sealed record ResultadoLotes(IReadOnlyList<SlotCache> Modificadas, string Mensaje);

/// <summary>Un archivo de la carpeta ya procesado: su nombre y los bytes que PKHeX escribiría (datos de equipo descifrados, <c>WriteDecryptedDataParty</c>).</summary>
public sealed record ArchivoLotes(string Nombre, byte[] Datos);

/// <summary>
/// EDITOR POR LOTES de PKHeX (<c>Menu_BatchEditor</c> → <c>BatchEditor</c>, PK-PKME-001, PK-MENU-022) SIN pantalla: las mismas llamadas del Core que el formulario (<c>StringInstructionSet.GetBatchSets</c>,
/// <c>EntityBatchEditor.ScreenStrings</c>, <c>EntityBatchEditor.IsFilterMatchMeta</c>, <c>BatchEditingUtil.IsFilterMatch</c>, <c>EntityBatchProcessor.Process</c>/<c>GetEditorResults</c>). Trabaja sobre COPIAS de los
/// Pokémon (<see cref="SlotCache"/> leídas con <c>SlotInfoLoader</c>); escribir en la partida es cosa de quien llama, por el historial de la sesión (un Deshacer), y en la carpeta, por los documentos del usuario
/// en una carpeta que él elige (regla 13: el original no se toca).
/// </summary>
public static class ModeloLotes
{
    /// <summary>El contador de PKHeX en castellano (<c>BatchEditor.L_Count</c> de <c>lang_es.txt</c>).</summary>
    public static string FormatoCoincidencias => IdiomaInterfaz.T("ModeloLotes.Coincidencias");

    /// <summary>Las casillas de las cajas (<c>CreateBoxData</c>: todas, también las vacías) o del equipo (<c>CreatePartyData</c>: solo las ocupadas). Copias: la partida no cambia.</summary>
    public static IReadOnlyList<SlotCache> DatosPartida(SaveFile sav, FuenteLotes fuente)
    {
        var data = new List<SlotCache>();
        if (fuente == FuenteLotes.Equipo) { if (sav.HasParty) SlotInfoLoader.AddPartyData(sav, data); }
        else if (sav.HasBox) SlotInfoLoader.AddBoxData(sav, data);
        return data;
    }

    /// <summary>
    /// Los Pokémon de una carpeta (<c>CreateFolderData</c>): cada archivo de tamaño plausible (<c>EntityDetection.IsSizePlausible</c>) que el Core reconoce con <c>FileUtil.TryGetPKM</c> (la extensión y la partida
    /// abierta como pista de formato, igual que PKHeX). El origen de cada uno es <c>SlotInfoFileSingle(nombre)</c>, así que <c>IdentifierContains</c> filtra por el nombre del archivo.
    /// </summary>
    public static IReadOnlyList<SlotCache> DatosCarpeta(IEnumerable<(string Nombre, byte[] Datos)> archivos, SaveFile sav)
    {
        var result = new List<SlotCache>();
        foreach (var (nombre, datos) in archivos)
        {
            if (!EntityDetection.IsSizePlausible(datos.Length)) continue;
            // El Core descifra in situ y reutiliza el array si mide lo del equipo: se le pasa SIEMPRE una copia (los bytes leídos no se tocan).
            if (FileUtil.TryGetPKM(datos.ToArray(), out var pk, Path.GetExtension(nombre), sav))
                result.Add(new SlotCache(new SlotInfoFileSingle(nombre), pk));
        }
        return result;
    }

    /// <summary>
    /// Comprueba las instrucciones como <c>TryGetInstructionSets</c>: vacío = nada que hacer; línea en blanco o texto que el Core no entiende = <c>MsgBEInstructionInvalid</c>; un filtro sin valor =
    /// <c>MsgBEFilterEmpty</c>; un conjunto sin asignaciones = <c>MsgBEInstructionNone</c> (con <paramref name="soloFiltros"/>, para contar coincidencias, basta con que alguno tenga filtros).
    /// </summary>
    public static ValidacionLotes Validar(string? texto, bool soloFiltros = false)
    {
        Ajustes.Pkhex.TextosNucleo.Preparar();
        if (string.IsNullOrEmpty(texto)) return new([], string.Empty, []);
        if (StringInstructionSet.HasEmptyLine(texto)) return new([], MessageStrings.MsgBEInstructionInvalid, []);
        StringInstructionSet[] sets;
        try { sets = StringInstructionSet.GetBatchSets(texto); }
        catch { return new([], MessageStrings.MsgBEInstructionInvalid, []); }
        if (Array.Exists(sets, s => s.Filters.Any(z => string.IsNullOrWhiteSpace(z.PropertyValue)))) return new([], MessageStrings.MsgBEFilterEmpty, []);
        if (Array.Exists(sets, z => z.Instructions.Count == 0))
            return soloFiltros && sets.Any(z => z.Filters.Count != 0) ? new(sets, null, []) : new([], MessageStrings.MsgBEInstructionNone, []);
        var vacias = sets.SelectMany(s => s.Instructions.Where(z => string.IsNullOrWhiteSpace(z.PropertyValue))).Select(z => z.PropertyName).ToList();
        return new(sets, null, vacias);
    }

    private static bool EsMeta(StringInstruction filtro) => BatchFilters.FilterMeta.Any(z => z.IsMatch(filtro.PropertyName));

    /// <summary>Una casilla de caja con protección de sobrescritura (bloqueada, equipo de combate...): PKHeX no la cuenta ni la edita.</summary>
    private static bool Protegida(SaveFile? sav, SlotCache entry) => sav is not null && entry.Source is SlotInfoBox info && sav.GetBoxSlotFlags(info.Box, info.Slot).IsOverwriteProtected();

    /// <summary>
    /// Las entradas protegidas de <paramref name="datos"/>, leídas de la partida viva. Se llama en el hilo de la interfaz (ADR 0078 §9) y el conteo en segundo plano usa solo este conjunto, sin volver a
    /// mirar el guardado (M-1 de la auditoría de Fable).
    /// </summary>
    public static IReadOnlySet<ISlotInfo> Protegidas(IReadOnlyList<SlotCache> datos, SaveFile sav)
    {
        var r = new HashSet<ISlotInfo>(ReferenceEqualityComparer.Instance);
        foreach (var e in datos) if (Protegida(sav, e)) r.Add(e.Source);
        return r;
    }

    /// <summary>
    /// El contador «Coincidencias: m / t» (<c>TryGetFilterMessage</c>): t = Pokémon no vacíos; m = los que cumplen algún conjunto (filtros meta de la casilla + filtros de la entidad), sin casillas protegidas ni
    /// especies fuera de la partida. Con instrucciones que no se entienden, «-». Null si se canceló.
    /// </summary>
    public static string? Coincidencias(IReadOnlyList<SlotCache> datos, SaveFile sav, string? texto, CancellationToken cancelar = default, IReadOnlySet<ISlotInfo>? protegidas = null)
    {
        var total = datos.Count(z => z.Entity.Species != 0);
        if (total == 0) return string.Format(FormatoCoincidencias, 0, 0);
        var v = Validar(texto, soloFiltros: true);
        if (!v.Valida) return string.Format(FormatoCoincidencias, "-", total);
        foreach (var set in v.Conjuntos) EntityBatchEditor.ScreenStrings(set.Filters);
        var coinciden = 0;
        var max = sav.MaxSpeciesID;
        foreach (var entry in datos)
        {
            if (cancelar.IsCancellationRequested) return null;
            var pk = entry.Entity;
            if (pk.Species == 0 || pk.Species > max) continue;
            if (protegidas is null ? Protegida(sav, entry) : protegidas.Contains(entry.Source)) continue;
            if (v.Conjuntos.Any(set => Coincide(entry, set))) coinciden++;
        }
        return string.Format(FormatoCoincidencias, coinciden, total);
    }

    private static bool Coincide(SlotCache entry, StringInstructionSet set)
    {
        var meta = set.Filters.Where(EsMeta).ToArray();
        var filtros = set.Filters.Where(z => !EsMeta(z)).ToArray();
        if (!EntityBatchEditor.IsFilterMatchMeta(meta, entry)) return false;
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo) return filtros.Length == 0 || BatchEditingUtil.IsFilterMatch(filtros, entry.Entity);
    }

    /// <summary>
    /// Ejecuta los conjuntos sobre las COPIAS de <paramref name="datos"/> (<c>RunBatchEditSaveFile</c>/<c>ProcessSAV</c>, o <c>ProcessFolder</c> si <paramref name="sav"/> es null: la carpeta no mira especie
    /// máxima ni casillas protegidas). Las entradas tocadas se devuelven una vez aunque varios conjuntos las cambien. La validación ya está hecha (<see cref="Validar"/>).
    /// </summary>
    public static ResultadoLotes Ejecutar(IReadOnlyList<SlotCache> datos, SaveFile? sav, StringInstructionSet[] sets, CancellationToken cancelar = default, IReadOnlySet<ISlotInfo>? protegidas = null)
    {
        Ajustes.Pkhex.TextosNucleo.Preparar();
        foreach (var set in sets) { EntityBatchEditor.ScreenStrings(set.Filters); EntityBatchEditor.ScreenStrings(set.Instructions); }
        var editor = new EntityBatchProcessor();
        var modificadas = new List<SlotCache>();
        var vistas = new HashSet<ISlotInfo>(ReferenceEqualityComparer.Instance);
        foreach (var set in sets)
        {
            var filtros = (IReadOnlyList<StringInstruction>)set.Filters;
            var meta = filtros.Where(EsMeta).ToArray();
            if (meta.Length != 0) filtros = [.. filtros.Where(z => !EsMeta(z))];
            var max = sav?.MaxSpeciesID ?? ushort.MaxValue;
            foreach (var entry in datos)
            {
                cancelar.ThrowIfCancellationRequested();
                var pk = entry.Entity;
                if (sav is not null)
                {
                    if (pk.Species == 0 || pk.Species > max) continue;
                    if (protegidas is null ? Protegida(sav, entry) : protegidas.Contains(entry.Source)) continue;
                }
                if (!EntityBatchEditor.IsFilterMatchMeta(meta, entry)) continue;
                bool cambiado;
                lock (Pokekeep.Sprites.LegalityGate.Cerrojo) cambiado = editor.Process(pk, filtros, set.Instructions);
                if (cambiado && vistas.Add(entry.Source)) modificadas.Add(entry);
            }
        }
        return new ResultadoLotes(modificadas, editor.GetEditorResults(sets));
    }

    /// <summary>
    /// Escribe en la partida las entradas modificadas: UNA entrada del historial del Core sobre sus casillas (el equipo, con una sola reversión de equipo entero, <c>GetChangelogSlots</c>) y
    /// <c>slot.WriteTo(sav, pk, EntityImportSettings.None)</c> para las cajas. DIVERGENCIA CON PKHEX (ADR 0082 §1 y §2): <c>B_Save_Click</c> recorre solo las casillas del historial y por eso escribe únicamente el primer
    /// miembro del equipo modificado (defecto de PKHeX); aquí se escriben TODOS, y el equipo se escribe ENTERO DE UNA VEZ (<see cref="EscribirEquipo"/>), porque <c>SlotInfoParty.WriteTo</c> con especie 0 borra y
    /// compacta el equipo, y escribir casilla a casilla acabaría borrando «uno de cada dos». Devuelve las casillas anotadas (vacío si no había nada que escribir).
    /// </summary>
    public static IReadOnlyList<ISlotInfo> Escribir(SaveFile sav, SlotChangelog historial, IReadOnlyList<SlotCache> modificadas)
    {
        if (modificadas.Count == 0) return [];
        var slots = modificadas.Select(m => m.Source).Where(z => z is not SlotInfoParty).ToList();
        if (modificadas.Any(z => z.Source is SlotInfoParty)) slots.Add(modificadas.First(z => z.Source is SlotInfoParty).Source);
        using var cambio = historial.Begin(slots);
        foreach (var m in modificadas) if (m.Source is not SlotInfoParty) m.Source.WriteTo(sav, m.Entity, EntityImportSettings.None);
        EscribirEquipo(sav, modificadas.Where(z => z.Source is SlotInfoParty).ToList());
        cambio.Commit();
        return slots;
    }

    /// <summary>
    /// Escribe de una vez los miembros del equipo modificados: el equipo resultante (los miembros que quedan con especie, en su orden) se coloca desde la primera casilla que cambia y las sobrantes del final
    /// se vacían. Con especie 0 un miembro desaparece y los siguientes suben, igual que al borrarlo en el editor, sin que la escritura de uno altere la posición de los demás.
    /// </summary>
    private static void EscribirEquipo(SaveFile sav, IReadOnlyList<SlotCache> modificadas)
    {
        if (modificadas.Count == 0) return;
        var cambios = new Dictionary<int, PKM>();
        foreach (var m in modificadas) cambios[((SlotInfoParty)m.Source).Slot] = m.Entity;
        var antes = sav.PartyCount;
        var resultado = new List<(PKM Pk, int Origen, bool Modificado)>();
        for (var i = 0; i < antes; i++)
        {
            var modificado = cambios.TryGetValue(i, out var pk);
            pk ??= sav.GetPartySlotAtIndex(i);
            if (pk.Species != 0) resultado.Add((pk, i, modificado));
        }
        for (var k = 0; k < resultado.Count; k++)
            if (resultado[k].Modificado || resultado[k].Origen != k) sav.SetPartySlotAtIndex(resultado[k].Pk, k, EntityImportSettings.None);
        for (var k = resultado.Count; k < antes; k++) sav.SetPartySlotAtIndex(sav.BlankPKM, k, EntityImportSettings.None);
    }

    /// <summary>Los bytes que PKHeX escribe por cada Pokémon de carpeta modificado (<c>ForcePartyData</c> + <c>WriteDecryptedDataParty</c>), con el mismo nombre de archivo.</summary>
    public static ArchivoLotes BytesCarpeta(SlotCache entrada)
    {
        var pk = entrada.Entity;
        var datos = new byte[pk.SIZE_PARTY];
        pk.ForcePartyData();
        pk.WriteDecryptedDataParty(datos);
        var nombre = entrada.Source is SlotInfoFileSingle s ? Path.GetFileName(s.Path) : pk.FileName;
        return new ArchivoLotes(nombre, datos);
    }

    // ------------------------------------------------------------------------------------------------- constructor de instrucciones
    /// <summary>Los prefijos del constructor (<c>StringInstruction.Prefixes</c>): el primero es «Asignar» (<c>.</c>), luego los de filtro y luego las operaciones.</summary>
    public static IReadOnlyList<char> Prefijos { get; } = [.. StringInstruction.Prefixes.ToArray()];

    /// <summary>Qué hace cada prefijo, en castellano (PKHeX enseña solo el símbolo, salvo «Set»).</summary>
    public static string Significado(char prefijo) => prefijo switch
    {
        '.' => IdiomaInterfaz.T("ModeloLotes.Asignar"),
        '=' => IdiomaInterfaz.T("ModeloLotes.FiltroIgual"),
        '!' => IdiomaInterfaz.T("ModeloLotes.FiltroIgual2"),
        '>' => IdiomaInterfaz.T("ModeloLotes.FiltroMayor"),
        '≥' => IdiomaInterfaz.T("ModeloLotes.FiltroMayorIgual"),
        '<' => IdiomaInterfaz.T("ModeloLotes.FiltroMenor"),
        '≤' => IdiomaInterfaz.T("ModeloLotes.FiltroMenorIgual"),
        '+' => "Sumar (+)",
        '-' => "Restar (-)",
        '*' => "Multiplicar (*)",
        '/' => IdiomaInterfaz.T("ModeloLotes.Dividir"),
        '%' => IdiomaInterfaz.T("ModeloLotes.RestoDividir"),
        '&' => IdiomaInterfaz.T("ModeloLotes.YBitABit"),
        '|' => IdiomaInterfaz.T("ModeloLotes.OBitABit"),
        '^' => IdiomaInterfaz.T("ModeloLotes.OExclusivoBitA"),
        '»' => IdiomaInterfaz.T("ModeloLotes.DesplazarBitsDerecha"),
        '«' => IdiomaInterfaz.T("ModeloLotes.DesplazarBitsIzquierda"),
        _ => prefijo.ToString(),
    };

    /// <summary>
    /// Los formatos del selector (<c>CB_Format</c>): «Cualquiera», un tipo por formato de <c>EntityBatchEditor.Instance.Types</c> (pk1...pa9) y «Todos». PKHeX enseña <c>MsgAny</c>/<c>MsgAll</c>, pero su
    /// <c>MessageStrings_es.txt</c> traduce «Any» como «Ninguno», que dice lo contrario: aquí «Cualquiera».
    /// </summary>
    public static IReadOnlyList<string> Formatos => [IdiomaInterfaz.T("ModeloLotes.Cualquiera"), .. EntityBatchEditor.Instance.Types.Select(t => t.Name.ToLowerInvariant()), IdiomaInterfaz.T("ModeloLotes.Todos")];

    /// <summary>Las propiedades de un formato (<c>EntityBatchEditor.Instance.Properties[formato]</c>, lo que carga <c>CB_Property</c>).</summary>
    public static IReadOnlyList<string> Propiedades(int formato) => EntityBatchEditor.Instance.Properties[Math.Clamp(formato, 0, EntityBatchEditor.Instance.Properties.Length - 1)];

    /// <summary>El tipo de la propiedad en ese formato (<c>L_PropType</c>), o «Unknown» como PKHeX.</summary>
    public static string Tipo(string propiedad, int formato) => EntityBatchEditor.Instance.TryGetPropertyType(propiedad, out var t, formato) ? t : "Unknown";

    /// <summary>El valor de la propiedad en el Pokémon de referencia (<c>L_PropValue</c>: el del editor en PKHeX; aquí el que el usuario elige), o null si no la tiene.</summary>
    public static string? ValorEn(PKM? pk, string propiedad)
    {
        if (pk is null || !EntityBatchEditor.Instance.TryGetHasProperty(pk, propiedad, out var pi)) return null;
        var t = pi.PropertyType;
        if (t.IsGenericType && t.GetGenericTypeDefinition().IsByRefLike) return t.ToString();
        return pi.GetValue(pk)?.ToString() ?? "null";
    }

    /// <summary>La línea que añade «Añadir» (<c>EntityInstructionBuilder.Create</c>: <c>{prefijo}{propiedad}=</c>), con el valor si se escribe.</summary>
    public static string Linea(char prefijo, string propiedad, string? valor = null) => $"{prefijo}{propiedad}{StringInstruction.SplitInstruction}{valor?.Trim()}";

    /// <summary>Añade la línea al texto como <c>B_Add_Click</c>: si ya hay texto y no acaba en salto de línea, primero uno.</summary>
    public static string Anadir(string? texto, string linea)
    {
        texto ??= string.Empty;
        if (texto.Length != 0 && !texto.EndsWith('\n')) texto += "\n";
        return texto + linea;
    }
}
