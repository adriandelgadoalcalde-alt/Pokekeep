// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui;

/// <summary>Acciones del motor sobre un Pokémon: «Legalizar automáticamente» y «Optimizar stats» (plan 5.3).</summary>
public enum AccionPokemon { Legalizar, Optimizar }

public enum ClaseResultado { Aplicado, AplicadoNecesitaHome, SinCambios, YaCorrecto, NoSePudo, Cancelado, CambioDeDestino, ValidacionFallida, FalloDelMotor, Ocupado }

/// <summary>De dónde sale la operación (como <c>OperationOrigin</c> del núcleo): una casilla, el borrador del editor o una entrada de la Librería.</summary>
public enum OrigenAccion { Casilla, Editor, Libreria }

public sealed record ProgresoAccion(string Texto, double? Fraccion);

/// <summary>Una fila de «Ver cambios»: campo, valor antes y después ya legibles en castellano, y el motivo. Los rasgos relajados (el origen no admitía el original) van en cursiva.</summary>
public sealed record CambioCampo(string Campo, string Antes, string Despues, string Motivo, bool Relajado)
{
    public string Accesible => IdiomaInterfaz.F("PuertosAcciones.DeA", Campo, Antes, Despues, Motivo);
}

/// <summary>Casilla donde acabó una entrada de la Librería («Añadir a la caja»).</summary>
public readonly record struct DestinoCasilla(bool Equipo, int Caja, int Indice);

/// <summary>Cómo deshacer lo aplicado: solo vale mientras el destino siga exactamente como lo dejó Pokekeep.</summary>
public interface IAccionDeshacer
{
    bool PuedeDeshacer();
    bool Deshacer();
}

/// <summary>
/// Resultado de una acción para la interfaz: honesto (nunca «Legalizado» si no lo está) y con la razón concreta de un fallo, sin lista de alternativas. Los textos son los del núcleo compartido
/// (<c>ResultMessages</c>, <c>Textos</c>): la interfaz no los reescribe.
/// </summary>
public sealed record ResultadoAccion(AccionPokemon Accion, ClaseResultado Clase, string Titular, string? Detalle, IReadOnlyList<string> Resumen, TimeSpan Duracion, IAccionDeshacer? Deshacer)
{
    public bool Aplicado => Clase is ClaseResultado.Aplicado or ClaseResultado.AplicadoNecesitaHome;
    public bool EsFallo => Clase is ClaseResultado.NoSePudo or ClaseResultado.CambioDeDestino or ClaseResultado.ValidacionFallida or ClaseResultado.FalloDelMotor;
    /// <summary>Aplicado sin la matrícula de HOME, o ya estaba así y no se ha escrito nada: hay que pasar por HOME (nunca se presenta como «Legalizado»).</summary>
    public bool NecesitaHome => Clase is ClaseResultado.AplicadoNecesitaHome or ClaseResultado.SinCambios;

    public OrigenAccion Origen { get; init; } = OrigenAccion.Casilla;
    /// <summary>«Pikachu · Caja 1, casilla 3»: el destino al empezar (null en las pruebas que no lo informan).</summary>
    public string? Objetivo { get; init; }
    /// <summary>Nombre del <c>OutcomeKind</c> del núcleo (Applied, AlreadyLegal, TargetChanged...).</summary>
    public string Clasificacion { get; init; } = string.Empty;
    /// <summary>Nombre del <c>ResponseStatus</c> del contrato (Success, NoSolution, Timeout...).</summary>
    public string Estado { get; init; } = string.Empty;
    public IReadOnlyList<CambioCampo> Cambios { get; init; } = [];
    /// <summary>Las notas de «Ver cambios» (resumen, detalle, optimización, validación independiente, origen, versión del núcleo y tiempo del motor), línea a línea.</summary>
    public IReadOnlyList<string> Notas { get; init; } = [];
    /// <summary>Dónde se ha colocado una entrada de la Librería.</summary>
    public DestinoCasilla? Casilla { get; init; }
    /// <summary>Intentos del motor (cuando los informó).</summary>
    public int Intentos { get; init; }
}

/// <summary>Versiones del motor para la ficha exportada de la Librería (solo versiones; nada del usuario).</summary>
public sealed record VersionesMotor(string Motor, string Adaptador, string Alm, string AlmCommit, string Core, string CoreCommit, string CoreSha256);

/// <summary>Puerto del motor para la interfaz: lo implementa <c>Pokekeep.Mobile.Operaciones</c> (PokekeepCoordinator + AndroidEngineGateway) o las pruebas (un motor falso). Una acción a la vez.</summary>
public interface IAccionesPokekeep
{
    /// <summary>Por qué el motor no está disponible (null si lo está).</summary>
    string? NoDisponible { get; }

    Task<ResultadoAccion> EjecutarEnCasillaAsync(AccionPokemon accion, Modelo.Sesion sesion, bool equipo, int caja, int indice, IProgress<ProgresoAccion>? progreso, CancellationToken cancelar);

    /// <summary>Sobre el borrador del editor; si se aplica, el resultado se vuelca EN el borrador.</summary>
    Task<ResultadoAccion> EjecutarEnBorradorAsync(AccionPokemon accion, Modelo.Sesion sesion, PKHeX.Core.PKM borrador, IProgress<ProgresoAccion>? progreso, CancellationToken cancelar);

    /// <summary>La partida que la app tiene delante ahora: si cambia, las respuestas del motor pendientes quedan descartadas y las operaciones en curso acaban en «Resultado descartado» (regla 7).</summary>
    void SesionActiva(Modelo.Sesion? sesion) { }

    /// <summary>Hay otra operación de Pokekeep en curso (solo se admite una a la vez).</summary>
    bool Ocupado => false;

    /// <summary>Por qué la acción no se puede lanzar sobre esa casilla ahora mismo (el mismo texto que el escritorio da como ayuda del elemento desactivado); null si se puede.</summary>
    string? MotivoCasilla(AccionPokemon accion, Modelo.Sesion sesion, bool equipo, int caja, int indice) => NoDisponible;

    /// <summary>Por qué la acción no se puede lanzar sobre el Pokémon del editor ahora mismo; null si se puede.</summary>
    string? MotivoBorrador(AccionPokemon accion, Modelo.Sesion sesion, PKHeX.Core.PKM borrador) => NoDisponible;

    /// <summary>
    /// Por qué «Añadir a la caja» no se puede lanzar con esta entrada (partida sin cajas, sin nombre de entrenador, no disponible, HOME sin el ajuste, sin hueco en la caja activa...); null si se puede.
    /// <paramref name="destino"/> es la casilla elegida al soltar (null = la primera libre de <paramref name="cajaActiva"/>).
    /// </summary>
    string? MotivoAnadir(Modelo.Sesion sesion, string claveEntrada, int cajaActiva, DestinoCasilla? destino) => IdiomaInterfaz.T("PuertosAcciones.AnadirCajaDisponibleVentana");
    /// <summary>Por qué no se puede ELEGIR casilla para una entrada: como <see cref="MotivoAnadir"/> pero sin el «no quedan casillas libres en la caja activa» (con la caja llena, elegir otra caja o casilla es justo la salida).</summary>
    string? MotivoElegirCasilla(Modelo.Sesion sesion, string claveEntrada, int cajaActiva) => MotivoAnadir(sesion, claveEntrada, cajaActiva, null);

    /// <summary>«Añadir a la caja»: genera la entrada con el mismo motor y la misma validación y la coloca en una casilla LIBRE (nunca sobrescribe).</summary>
    Task<ResultadoAccion> AnadirDeLibreriaAsync(Modelo.Sesion sesion, string claveEntrada, int cajaActiva, DestinoCasilla? destino, IProgress<ProgresoAccion>? progreso, CancellationToken cancelar)
        => throw new NotSupportedException(IdiomaInterfaz.T("PuertosAcciones.AnadirALaCaja"));

    /// <summary>Versiones del motor (consulta corta de 15 s al motor; no es una operación ni toca la partida). Null si no responde.</summary>
    Task<VersionesMotor?> VersionesMotorAsync(CancellationToken cancelar) => Task.FromResult<VersionesMotor?>(null);
}
