// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace AutoLegalidad.Contracts;

/// <summary>Las DOS acciones del producto (R-002).</summary>
public enum LegalizeAction
{
    /// <summary>«Legalizar automaticamente»: puede cambiar de origen/encuentro para conseguir algo valido y despues optimiza los stats (R-001).</summary>
    Legalize = 0,

    /// <summary>«Optimizar stats»: Pokemon ya capturado; conserva TODO el origen y mejora solo lo que ese encuentro permite.</summary>
    Optimize = 1,

    /// <summary>Hito 4b (R-007, «Edicion masiva»): operaciones de edicion sobre un Pokemon ya existente (nivel, amistad, idioma, PP, PS, Pokerus, stats), cada una con su regla de legalidad y su resultado propio en <c>operations</c>.</summary>
    Adjust = 2,
}

/// <summary>Estado de UNA operacion de <see cref="LegalizeAction.Adjust"/> sobre un Pokemon (informe por Pokemon de la edicion masiva).</summary>
public enum OperationStatus
{
    /// <summary>La operacion cambio algo y el resultado sigue validando.</summary>
    Changed,

    /// <summary>La operacion no tenia nada que mejorar (ya estaba al maximo legal / ya optimo).</summary>
    AlreadyOptimal,

    /// <summary>No se puede aplicar a este Pokemon (huevo, formato sin ese dato, el Core la rechaza...). El motivo va en <c>reason</c>.</summary>
    NotApplicable,

    /// <summary>La operacion se intento y fallo (error o agotamiento de tiempo); se deshizo y las demas siguieron.</summary>
    Failed,
}

/// <summary>
/// Estado final honesto de una operacion. Solo <see cref="Success"/>, <see cref="AlreadyLegal"/> y (sin candidato) <see cref="AlreadyOptimal"/>
/// son resultados positivos; un <see cref="Timeout"/> NUNCA se presenta como <see cref="NoSolution"/> ni como exito (regla 5).
/// </summary>
public enum ResponseStatus
{
    /// <summary>Candidato validado de forma independiente (Core oficial + destino + round-trip). Lleva candidato.</summary>
    Success,

    /// <summary>Legalizar: la entidad ya era legal y compatible con el destino. Lleva la entidad original SIN cambios (mismos bytes).</summary>
    AlreadyLegal,

    /// <summary>Optimizar: el encuentro no permite mejorar mas los stats (p. ej. evento con IV fijos). Sin candidato; explicacion en el mensaje.</summary>
    AlreadyOptimal,

    /// <summary>Se recorrio el dominio de busqueda definido y no se obtuvo candidato. <c>searchComplete</c> dice si el dominio era finito y se agoto.</summary>
    NoSolution,

    /// <summary>El juego/formato de destino no esta soportado por esta version.</summary>
    UnsupportedSave,

    /// <summary>La entidad o el dato necesario (p. ej. un evento que el Core fijado no admite) no esta soportado.</summary>
    UnsupportedData,

    /// <summary>La especie no existe en el juego de destino: no hay correccion que conserve la especie (plan 7, Fase D). Original intacto.</summary>
    SpeciesNotInDestination,

    /// <summary>Optimizar: el Pokemon NO es legal de partida; B no legaliza («primero hay que legalizarlo; usa Legalizar automaticamente»). Sin candidato.</summary>
    NotLegalFirst,

    /// <summary>Optimizar: la unica mejora posible exigia cambiar campos de origen y se descarto (guarda de seguridad). Sin candidato.</summary>
    OriginPreserved,

    /// <summary>Se agoto el tiempo antes de terminar la busqueda. No prueba imposibilidad.</summary>
    Timeout,

    Cancelled,

    /// <summary>Release/snapshot/protocolo/Core/ALM distintos de los del Worker: no se genera nada.</summary>
    VersionMismatch,

    /// <summary>Solicitud mal formada, hash/tamano/tipo/contexto inconsistentes o entidad ilegible.</summary>
    InvalidInput,

    /// <summary>Lo decide SIEMPRE la UI (compara la seleccion actual con su captura); el Worker nunca lo devuelve.</summary>
    TargetChanged,

    /// <summary>Se obtuvo un candidato pero la revalidacion independiente lo rechazo: se descarta (nunca se devuelve como Success).</summary>
    ValidationFailed,

    InternalError,

    /// <summary>
    /// R-009 (ajuste explicito del usuario, desactivado por defecto): candidato que el Core valida en TODO salvo el rastreador de transferencia de Pokemon HOME, que vale 0 (el producto nunca
    /// inventa uno, regla 6). NO es «legal»: PKHeX oficial lo da por invalido hasta que pase por HOME, que le asigna su rastreador real. Lleva candidato y prueba.
    /// </summary>
    SuccessNeedsHome,
}

/// <summary>Fase del algoritmo (plan 7) alcanzada al terminar.</summary>
public enum Phase
{
    None = 0,
    A_CapturaYAnalisis = 1,
    B_ReparacionMinima = 2,
    C_Regeneracion = 3,
    D_RelajacionAutomatica = 4,
    E_ValidacionIndependiente = 5,
}
