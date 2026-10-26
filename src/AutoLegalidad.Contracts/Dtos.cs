// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace AutoLegalidad.Contracts;

// NOTA (Android F1, paso 7): las propiedades con valor por defecto distinto del tipo (SlotType, OptimizeStats, IncludeReport, CoherentHistory, Basis, Changes, Summary, Operations) son `set` y no
// `init` a proposito: con los metadatos generados de System.Text.Json una propiedad `init` omitida en el JSON recibe default(T) y pisa el inicializador (con la reflexion no). Con `set`, el JSON sin ese campo
// conserva el valor por defecto, como siempre (prueba JsonContextTests.LosCamposOmitidosConservanSusValoresPorDefecto).

/// <summary>Datos del entrenador del destino (la partida). Ids crudos de 16 bits: sirven igual para Gen3-6 y para Gen7+ (ID32 = SID16 &lt;&lt; 16 | TID16).</summary>
public sealed record TrainerInfo
{
    public required string Ot { get; init; }
    public required int Tid16 { get; init; }
    public required int Sid16 { get; init; }
    /// <summary>Valor de <c>LanguageID</c> del Core (1 = japones, 2 = ingles, 5 = espanol, ...).</summary>
    public required int Language { get; init; }
    /// <summary>0 = masculino, 1 = femenino.</summary>
    public required int Gender { get; init; }
}

/// <summary>Contexto minimo del destino (plan 6). El Worker NO recibe rutas ni bytes de la partida: reconstruye un destino virtual con estos datos.</summary>
public sealed record TargetInfo
{
    /// <summary>Nombre del enum <c>GameVersion</c> del Core de un juego concreto (p. ej. <c>SL</c>, <c>FR</c>, <c>C</c>).</summary>
    public required string GameVersion { get; init; }
    /// <summary>Tipo de entidad que guarda el destino (PK1..PA9, CK3, XK3...). Informativo: el Worker lo comprueba contra el que deduce del juego.</summary>
    public required string SaveRuntimeType { get; init; }
    /// <summary>Nombre del enum <c>EntityContext</c> del destino (Gen1..Gen9a, Gen7b, Gen8a, Gen8b...).</summary>
    public required string Context { get; init; }
    /// <summary>Nombre del enum <c>StorageSlotType</c> (Box, Party, ...). <c>None</c> si la prueba no depende de la casilla.</summary>
    public string SlotType { get; set; } = "None";
    /// <summary>False si es un contexto virtual (entidad abierta sin partida real): se distingue de la validacion de una partida real.</summary>
    public bool IsRealSave { get; init; }
    public required TrainerInfo Trainer { get; init; }
    public string? PersonalTableKey { get; init; }
    /// <summary>Banderas serializables del destino que cambian restricciones (p. ej. <c>VirtualConsole=true</c>, <c>CartEra=false</c>).</summary>
    public IReadOnlyDictionary<string, string>? SaveFlags { get; init; }
}

/// <summary>Seleccion de la UI (editor o casilla). Solo informativa: el Worker nunca la usa como ruta de escritura.</summary>
public sealed record SelectionInfo
{
    public required string Kind { get; init; } // Editor | Box
    public int? Box { get; init; }
    public int? Slot { get; init; }
}

public sealed record EngineOptions
{
    /// <summary>R-001: optimizar IV tras legalizar (activado por defecto).</summary>
    public bool OptimizeStats { get; set; } = true;
    /// <summary>Limite de intentos de generacion por encuentro candidato (cota de busqueda). 0 = valor por defecto del motor.</summary>
    public int MaxAttemptsPerEncounter { get; init; }
    /// <summary>Si es true, se pide tambien el informe tecnico detallado en <c>proof.report</c>.</summary>
    public bool IncludeReport { get; set; } = true;
    /// <summary>
    /// R-009: «Permitir Pokemon que necesitan pasar por HOME sin matricula (con riesgo)». Desactivado por defecto (imposible demostrado sin servidor de HOME). Activado, el motor puede devolver
    /// <see cref="ResponseStatus.SuccessNeedsHome"/>: rastreador 0 (nunca un numero inventado) y validacion con la severidad Fishy de <c>HOMETransferTrackerNotPresent</c> SOLO durante la solicitud;
    /// cualquier otro fallo del Core sigue rechazando el candidato.
    /// </summary>
    public bool AllowHomeTrackerless { get; init; }
    /// <summary>
    /// Hito 4b (R-006 «Completar el juego»): tras elegir el candidato valido y ANTES de optimizar stats, sube el nivel al mayor (hasta 100) con el que el Core oficial lo valida; <c>MetLevel</c> no cambia.
    /// Solo en <see cref="LegalizeAction.Legalize"/>.
    /// </summary>
    public bool MaxLevel { get; init; }
    /// <summary>
    /// Hito 4b: la solicitud GENERA un Pokemon nuevo (no repara uno del usuario) aunque no lleve <c>originEventKey</c>: aplica la politica de idioma de generar (ES siempre que sea legal, R-005). Solo en <see cref="LegalizeAction.Legalize"/>.
    /// </summary>
    public bool NewPokemon { get; init; }
    /// <summary>Hito 4b (R-007): operaciones pedidas. Obligatorio (y con al menos una activa) si la accion es <see cref="LegalizeAction.Adjust"/>; prohibido en las demas.</summary>
    public AdjustOptions? Adjust { get; init; }
    /// <summary>
    /// Hito 4b: presupuesto en segundos de la optimizacion de IV DENTRO de «Legalizar» (o de la operacion Optimizar de Adjust). 0 = el del motor (20 s). Las operaciones masivas lo acortan
    /// para que miles de Pokemon no esperen cada uno hasta agotar la busqueda exhaustiva de semillas; si se agota se devuelve el mejor candidato hallado (el informe lo dice).
    /// </summary>
    public int OptimizeBudgetSeconds { get; init; }
    /// <summary>
    /// Hito 4c (R-010): fase «Historia» (cintas que solo otorga el origen, fechas dentro de la ventana real, memorias y entrenador anterior coherentes con el recorrido, valores de historial plausibles).
    /// Activada por defecto; se aplica a lo que el motor CONSTRUYE (Legalizar con origen reconstruido, Libreria, Completar el juego, Edicion masiva), nunca a la accion Optimizar.
    /// </summary>
    public bool CoherentHistory { get; set; } = true;
    /// <summary>
    /// Hito 5d (ADR 0046): idioma (valor de <c>LanguageID</c>) con el que generar cuando el origen fijado NO lo fija el propio Core, antes que el español (p. ej. una tarjeta de evento exclusiva de Japón con el idioma libre en el Core:
    /// japonés). 0 = ninguno (comportamiento de siempre). Solo si el formato lo admite; el Core sigue decidiendo la legalidad y un idioma que el origen fija manda sobre este.
    /// </summary>
    public int RequestedLanguage { get; init; }
}

/// <summary>Operaciones de la edicion masiva (R-007) para UN Pokemon. El orden de ejecucion es fijo (ver <see cref="AdjustOperationNames.Order"/>), no el de las propiedades.</summary>
public sealed record AdjustOptions
{
    /// <summary>«Legalizar los ilegales primero»: si el Pokemon es ilegal, <see cref="LegalizeAction.Legalize"/> (puede cambiar el origen) antes del resto.</summary>
    public bool LegalizeIfIllegal { get; init; }
    /// <summary>Nivel maximo legal (el mayor ≤ 100 que el Core valida; huevos no).</summary>
    public bool MaxLevel { get; init; }
    /// <summary>Optimizar stats (R-002: conserva todo el origen).</summary>
    public bool Optimize { get; init; }
    /// <summary>Amistad al maximo (255) solo si el Core lo valida; nunca se busca otro valor.</summary>
    public bool MaxFriendship { get; init; }
    /// <summary>Idioma espanol cuando el Core lo valida (cambia el idioma de origen).</summary>
    public bool SpanishLanguage { get; init; }
    /// <summary>PP maximos (Mas PP al tope en los movimientos que lo admiten) y PP restaurados.</summary>
    public bool MaxPP { get; init; }
    /// <summary>Restaurar PS y estado alterado (solo en formatos de equipo que los guardan).</summary>
    public bool HealParty { get; init; }
    /// <summary>Curar el Pokerus si esta infectado (dias a 0, conserva la cepa); nunca vacuna.</summary>
    public bool CurePokerus { get; init; }

    public bool Any => LegalizeIfIllegal || MaxLevel || Optimize || MaxFriendship || SpanishLanguage || MaxPP || HealParty || CurePokerus;
}

/// <summary>Nombres estables de las operaciones de <see cref="AdjustOptions"/> (clave de <see cref="OperationOutcome.Operation"/>).</summary>
public static class AdjustOperationNames
{
    public const string Legalize = "Legalize";
    public const string MaxLevel = "MaxLevel";
    public const string Optimize = "Optimize";
    public const string MaxFriendship = "MaxFriendship";
    public const string SpanishLanguage = "SpanishLanguage";
    public const string MaxPP = "MaxPP";
    public const string HealParty = "HealParty";
    public const string CurePokerus = "CurePokerus";

    /// <summary>Orden fijo de ejecucion dentro de cada Pokemon: el nivel habilita el Entrenamiento extremo y el idioma va despues de optimizar (Optimizar conserva el idioma).</summary>
    public static readonly IReadOnlyList<string> Order = [Legalize, MaxLevel, Optimize, MaxFriendship, SpanishLanguage, MaxPP, HealParty, CurePokerus];

    public static IReadOnlyList<string> Requested(AdjustOptions o)
    {
        var l = new List<string>();
        if (o.LegalizeIfIllegal) l.Add(Legalize);
        if (o.MaxLevel) l.Add(MaxLevel);
        if (o.Optimize) l.Add(Optimize);
        if (o.MaxFriendship) l.Add(MaxFriendship);
        if (o.SpanishLanguage) l.Add(SpanishLanguage);
        if (o.MaxPP) l.Add(MaxPP);
        if (o.HealParty) l.Add(HealParty);
        if (o.CurePokerus) l.Add(CurePokerus);
        return l;
    }
}

/// <summary>Resultado de UNA operacion de <see cref="LegalizeAction.Adjust"/> (una columna del informe por Pokemon).</summary>
public sealed record OperationOutcome
{
    /// <summary>Nombre estable (<see cref="AdjustOperationNames"/>).</summary>
    public required string Operation { get; init; }
    public required OperationStatus Status { get; init; }
    /// <summary>Texto corto en espanol para el usuario.</summary>
    public required string Message { get; init; }
    /// <summary>Motivo tecnico honesto (p. ej. el primer fallo del Core que impidio la operacion).</summary>
    public string? Reason { get; init; }
}

/// <summary>Solicitud al Worker (PLAN-TECNICO.md 6 + R-002: <see cref="Action"/>).</summary>
public sealed record LegalizeRequest
{
    public required int ProtocolVersion { get; init; }
    public required string RequestId { get; init; }
    public required string ReleaseId { get; init; }
    public required LegalizeAction Action { get; init; }
    /// <summary>Bytes de la entidad en su representacion canonica exportada por el formato (no una conversion a PK9).</summary>
    public required string OriginalEntityBase64 { get; init; }
    public required string OriginalEntitySha256 { get; init; }
    /// <summary>Nombre del enum <c>EntityContext</c> de la entidad.</summary>
    public required string EntityContext { get; init; }
    public required string EntityRuntimeType { get; init; }
    public required TargetInfo Target { get; init; }
    public SelectionInfo? Selection { get; init; }
    public required string PolicyVersion { get; init; }
    public required string DatasetSnapshotId { get; init; }
    public DateTimeOffset? DeadlineUtc { get; init; }
    /// <summary>Solo si se reconocio una identidad de evento especifica y comprobada; nunca inferida de un nombre.</summary>
    public string? OriginEventKey { get; init; }
    public EngineOptions? Options { get; init; }
}

/// <summary>Un cambio concreto entre la entidad original y el candidato, con su razon (el resumen de cambios de R-001/R-002).</summary>
public sealed record ChangeEntry
{
    public required string Field { get; init; }
    public string? Before { get; init; }
    public string? After { get; init; }
    /// <summary>Codigo estable de la razon (p. ej. <c>EV_OVER_LIMIT</c>, <c>IV_OPTIMIZED</c>, <c>ENCOUNTER_REGENERATED</c>).</summary>
    public required string ReasonCode { get; init; }
    /// <summary>Texto en espanol de la razon.</summary>
    public required string Reason { get; init; }
    public string? SourceRef { get; init; }
}

public sealed record ProofCheck
{
    public required string Name { get; init; }
    public required bool Passed { get; init; }
    public string? Detail { get; init; }
}

/// <summary>Prueba de la revalidacion INDEPENDIENTE del candidato (nunca basta el bool de ALM).</summary>
public sealed record ValidationProof
{
    public required string ValidatorVersion { get; init; }
    public required string ValidatorCommit { get; init; }
    public required IReadOnlyList<string> Report { get; init; }
    public required IReadOnlyList<ProofCheck> Checks { get; init; }
    public string? EncounterKey { get; init; }
    public string? EncounterType { get; init; }
    public required IReadOnlyList<string> SourceRefs { get; init; }
    public required string RuleProfileHash { get; init; }
    public required bool RoundTripValid { get; init; }
    public required bool DestinationCompatible { get; init; }
    public bool SpeciesIdentity { get; init; }
    public bool OfficialValid { get; init; }
}

/// <summary>Detalle de la optimizacion de stats (R-001/R-002).</summary>
public sealed record OptimizationReport
{
    public required string Strategy { get; init; }
    public required IReadOnlyList<int> IvsBefore { get; init; }
    public required IReadOnlyList<int> IvsAfter { get; init; }
    public required int SumBefore { get; init; }
    public required int SumAfter { get; init; }
    /// <summary>Texto en espanol que explica el limite si no se alcanzo 6x31 (p. ej. «este encuentro de Gen3 no permite IV mas altos manteniendo su naturaleza»).</summary>
    public string? LimitReason { get; init; }
    public bool ExtremeTrainingApplied { get; init; }
    /// <summary>True si se enumero por completo el dominio de semillas/IV del metodo y la mejora hallada es la maxima de ese dominio.</summary>
    public bool Exhaustive { get; init; }
    /// <summary>Base del veredicto: <c>FixedIVs</c> (el encuentro fija los IV), <c>Enumeration</c> (dominio recorrido por completo), <c>CoreOracleProbe</c> (el Core rechazo el vector completo y cada IV por separado) o <c>BudgetExhausted</c> (se agoto el tiempo o la cota sin demostrar nada).</summary>
    public string Basis { get; set; } = "Enumeration";
}

/// <summary>Respuesta del Worker. Los campos de candidato SOLO existen con <see cref="ResponseStatus.Success"/>, <see cref="ResponseStatus.AlreadyLegal"/> o <see cref="ResponseStatus.SuccessNeedsHome"/>.</summary>
public sealed record LegalizeResponse
{
    public required int ProtocolVersion { get; init; }
    public required string RequestId { get; init; }
    public string? ReleaseId { get; init; }
    public string? DatasetSnapshotId { get; init; }
    public required LegalizeAction Action { get; init; }
    public required ResponseStatus Status { get; init; }
    /// <summary>Mensaje breve en espanol para mostrar al usuario.</summary>
    public required string Message { get; init; }
    /// <summary>Razon tecnica honesta (en espanol) de un fallo: por que no hay candidato.</summary>
    public string? Reason { get; init; }
    public string? CandidateEntityBase64 { get; init; }
    public string? CandidateSha256 { get; init; }
    public string? EntityContext { get; init; }
    public string? EntityRuntimeType { get; init; }
    public IReadOnlyList<ChangeEntry> Changes { get; set; } = [];
    /// <summary>Resumen legible de lo cambiado (p. ej. «IV optimizados: 24/31/20/31/31/28 -> 31/31/31/31/31/31»).</summary>
    public IReadOnlyList<string> Summary { get; set; } = [];
    public ValidationProof? Proof { get; init; }
    public OptimizationReport? Optimization { get; init; }
    public long ElapsedMilliseconds { get; init; }
    public int Attempts { get; init; }
    public Phase PhaseReached { get; init; }
    /// <summary>True solo si se termino el dominio finito definido; un muestreo RNG terminado no equivale a demostrar imposibilidad.</summary>
    public bool SearchComplete { get; init; }
    /// <summary>Hito 4b: resultado por operacion de una solicitud <see cref="LegalizeAction.Adjust"/> (vacio en las demas acciones). Se rellena aunque no haya candidato.</summary>
    public IReadOnlyList<OperationOutcome> Operations { get; set; } = [];
    /// <summary>Hito 4b (nivel maximo): nivel final del candidato, o null si no aplica.</summary>
    public int? FinalLevel { get; init; }
}

/// <summary>Identidad del Worker: lo que se fija en el manifiesto de entrega y se compara antes de generar.</summary>
public sealed record WorkerManifest
{
    public required int ProtocolVersion { get; init; }
    public required string ContractsVersion { get; init; }
    public required string EngineVersion { get; init; }
    public required string PolicyVersion { get; init; }
    public required string CoreVersion { get; init; }
    public required string CoreCommit { get; init; }
    public required string CoreDllSha256 { get; init; }
    public required string AlmVersion { get; init; }
    public required string AlmCommit { get; init; }
    public required string AdapterVersion { get; init; }
    public required string ReleaseId { get; init; }
    public required string RuleProfileHash { get; init; }
    public string? DatasetSnapshotId { get; init; }
    public int SchemaVersion { get; init; }
}
