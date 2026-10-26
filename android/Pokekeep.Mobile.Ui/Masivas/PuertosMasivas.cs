// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Masivas;

/// <summary>Qué casillas entran en una operación masiva (los cuatro ámbitos del escritorio: <c>BulkScopeKind</c>).</summary>
public enum AmbitoMasivo { Todo, Cajas, Marcados, Este, Ilegales }

/// <summary>Qué se pide resolver: el ámbito y, según él, las cajas elegidas o las casillas marcadas (la de «Este Pokémon» es la única de <see cref="Casillas"/>).</summary>
public sealed record AlcanceMasivo(AmbitoMasivo Ambito, IReadOnlyList<int>? Cajas = null, IReadOnlyList<ISlotInfo>? Casillas = null, bool IncluirEquipo = false)
{
    public static AlcanceMasivo DeTodo() => new(AmbitoMasivo.Todo);
    public static AlcanceMasivo DeIlegales() => new(AmbitoMasivo.Ilegales);
    public static AlcanceMasivo DeCajas(IEnumerable<int> cajas, bool equipo = false) => new(AmbitoMasivo.Cajas, [.. cajas], null, equipo);
    public static AlcanceMasivo DeMarcados(IEnumerable<ISlotInfo> casillas) => new(AmbitoMasivo.Marcados, null, [.. casillas]);
    public static AlcanceMasivo DeEste(ISlotInfo casilla) => new(AmbitoMasivo.Este, null, [casilla]);
}

/// <summary>Una operación de la «Edición masiva» lista para pintar. Los textos son los del catálogo ÚNICO del núcleo (<c>BulkOperationCatalog</c>): la interfaz no escribe ninguno.</summary>
public sealed record OperacionMasivaInfo(string Clave, string Titulo, bool PorDefecto, string Ayuda, string NombreCorto)
{
    /// <summary>La ayuda empieza por «AVISO»: la operación cambia el origen del Pokémon y se pinta como aviso.</summary>
    public bool EsAviso => Ayuda.StartsWith("AVISO", StringComparison.Ordinal);
    public string Accesible => $"{Titulo}. {Ayuda}";
}

/// <summary>Una zona extra de PKHeX que Pokekeep NO modifica, con el motivo (se lista para que se sepa que no es un olvido).</summary>
public sealed record ZonaSinTocar(string Nombre, int Pokemon, string Motivo)
{
    public string Texto => $"{Nombre} ({Pokemon}): {Motivo}";
}

/// <summary>Lo que entra y lo que se omite en un ámbito, con el texto del recuento que da el núcleo («147 Pokémon en 6 cajas + equipo (6). Se omiten: 3 huevos, 2 casillas bloqueadas.»).</summary>
public sealed record ResumenAmbito(AmbitoMasivo Ambito, int Incluidos, int Huevos, int Bloqueadas, int Cajas, int Equipo, string Texto, IReadOnlyList<ZonaSinTocar> Zonas)
{
    public int Omitidos => Huevos + Bloqueadas;
    public bool HayAlgoQueEditar => Incluidos > 0;
}

/// <summary>Progreso de una operación masiva: el texto que compone el núcleo («Procesando 213 de 866 · Pikachu · 1 en paralelo · quedan ~3 min»), las cifras reales y los últimos resultados.</summary>
public sealed record ProgresoMasivo(string Texto, int Hechos, int Total, int EnVuelo, string Actual, int EnParalelo, TimeSpan Transcurrido, TimeSpan? Restante, IReadOnlyList<string> Recientes)
{
    /// <summary>Null hasta que hay un primer avance real (la barra es una marquesina hasta entonces).</summary>
    public double? Fraccion => Total <= 0 || Hechos < 0 ? null : Math.Clamp((double)Hechos / Total, 0, 1);
    public bool Determinado => Total > 0;
}

/// <summary>Cómo terminó una operación masiva (los <c>OutcomeKind</c> que pueden salir de ella).</summary>
public enum ClaseMasiva { Aplicado, AplicadoNecesitaHome, SinCambios, Cancelado, CambioDeDestino, NoSePudo, FalloDelMotor, Ocupado }

/// <summary>Deshacer de un lote entero: solo vale mientras TODAS las casillas sigan como las dejó Pokekeep; si no, dice cuántas cambiaste (<see cref="Motivo"/>).</summary>
public interface IDeshacerMasivo : IAccionDeshacer
{
    /// <summary>Por qué no se puede deshacer ahora (null si se puede).</summary>
    string? Motivo { get; }
    /// <summary>Casillas que el lote escribió.</summary>
    int Casillas { get; }
}

/// <summary>Una operación de un Pokémon del informe (columna de la edición masiva): nombre corto, símbolo (✔ cambiado · = ya óptimo · — no aplicable · ✖ fallo) y su texto.</summary>
public sealed record OperacionDeFila(string Clave, string Nombre, string Simbolo, string Estado, string Mensaje, string? Motivo);

/// <summary>
/// Una fila del informe de una operación masiva. <see cref="Estado"/> es el nombre del <c>BulkStatus</c> del núcleo (estable, para filtrar) y <see cref="Etiqueta"/> su texto en castellano
/// (<c>BulkStatusText.Label</c>, nunca reescrito aquí). Nunca lleva bytes de la partida (reglas 13 y 15).
/// </summary>
public sealed record FilaInformeMasivo(
    int Numero, string Pokemon, string? Forma, string Estado, int OrdenEstado, string Etiqueta, string? Casilla, string? Origen, int? Nivel, string? Ivs, string? Idioma,
    string? Motivo, IReadOnlyList<OperacionDeFila> Operaciones, IReadOnlyList<string> Detalles)
{
    public string Nombre => string.IsNullOrEmpty(Forma) ? Pokemon : $"{Pokemon} ({Forma})";
    public string Accesible => $"{Numero}. {Nombre}, {Etiqueta}" + (Casilla is null ? string.Empty : $", {Casilla}") + (string.IsNullOrEmpty(Motivo) ? string.Empty : $". {Motivo}");
}

/// <summary>El informe final: las filas, el texto agrupado por estado y el CSV (el mismo del núcleo: <c>BulkReport.ToText</c> y <c>ToCsv</c>).</summary>
public sealed record InformeMasivo(string Titulo, bool EsEdicion, string PalabraStat, IReadOnlyList<FilaInformeMasivo> Filas, string Texto, string Csv)
{
    public static InformeMasivo Vacio(string titulo) => new(titulo, true, "IV", [], titulo + "\n", "n;pokemon;forma;estado;casilla;origen;nivel;iv;idioma;operaciones;motivo\n");
}

/// <summary>Resultado de una operación masiva: honesto (nunca «aplicado» si no se escribió nada), con el informe por Pokémon y UN Deshacer del lote entero.</summary>
public sealed record ResultadoMasivo(ClaseMasiva Clase, string Titular, string? Detalle, InformeMasivo Informe, int Escritos, TimeSpan Duracion, IDeshacerMasivo? Deshacer)
{
    public bool Aplicado => Clase is ClaseMasiva.Aplicado or ClaseMasiva.AplicadoNecesitaHome;
    public bool EsFallo => Clase is ClaseMasiva.NoSePudo or ClaseMasiva.FalloDelMotor or ClaseMasiva.CambioDeDestino;
}

/// <summary>Una caja de la partida para el ámbito «Cajas concretas»: su número (desde 0), el nombre como lo enseña el escritorio («3. Caja de agua» o «Caja 3») y cuántos Pokémon editaría (sin huevos ni casillas bloqueadas, como el resumen del ámbito).</summary>
public sealed record CajaMasiva(int Indice, string Nombre, int Pokemon);

/// <summary>Un Pokémon que se puede marcar en «Elegir Pokémon…»: su casilla (texto del núcleo: «Caja 1, casilla 5»), el nombre de la especie y el nivel.</summary>
public sealed record CandidatoMasivo(ISlotInfo Casilla, string TextoCasilla, string Pokemon, int Nivel)
{
    public string Accesible => IdiomaInterfaz.F("PuertosMasivas.Nivel", Pokemon, Nivel, TextoCasilla);
}

/// <summary>Los textos de la ventana de la Edición masiva que no son de una operación (ámbitos, botones, «Preferir la versión de evento» y su AVISO...). Los da el núcleo (<c>BulkEditTexts</c>): la interfaz no escribe ninguno.</summary>
public sealed record TextosEdicionMasiva(
    string Todo, string Cajas, string Marcados, string Este, string Todas, string Ninguna, string ElegirPokemon, string MarcarTodos, string DesmarcarTodos, string Aceptar,
    string GrupoEvento, string PreferirEvento, string AyudaEvento, string TituloEventos, string AvisoCambioDeOrigen, string ProcesosEnParalelo, int ProcesosMinimo, int ProcesosMaximo,
    IReadOnlyList<string> ColumnasEventos, string NadaQueEditar, string PrefijoZonas,
    string GrupoMenu, string MenuEstaCaja, string MenuTodasLasCajas, string MenuEstePokemon, string AyudaEstePokemon, string MenuCompletar,
    string Ambito, string TituloSelector, string Nivel, string Cancelar)
{
    public string NombreAmbito(AmbitoMasivo a) => a switch { AmbitoMasivo.Todo => Todo, AmbitoMasivo.Cajas => Cajas, AmbitoMasivo.Ilegales => IdiomaInterfaz.T("Cajas.SoloIlegales"), AmbitoMasivo.Marcados => Marcados, _ => Este };

    /// <summary>Cabecera de la sección «Ámbito» con el estilo de etiqueta del móvil (en mayúsculas; el texto es el del núcleo).</summary>
    public string EtiquetaAmbito => Ambito.ToUpperInvariant();

    /// <summary>Cabecera del selector «Elegir Pokémon» con el estilo de etiqueta del móvil.</summary>
    public string EtiquetaSelector => TituloSelector.ToUpperInvariant();

    /// <summary>Nivel de un Pokémon del selector («Nv. 50»), con la abreviatura de la columna del escritorio.</summary>
    public string TextoNivel(int nivel) => $"{Nivel} {nivel}";
}

/// <summary>
/// Una fila de la vista previa de «Preferir la versión de evento»: un legendario, sublegendario o singular del ámbito. <see cref="Estado"/> es el nombre del <c>EventSwapStatus</c> del núcleo (<see cref="EstadosEvento"/>);
/// los textos (estado, detalle, idioma, «Rayquaza (salvaje) → evento...») son los del núcleo. Solo lo PROPUESTO se puede marcar; solo lo marcado se aplica.
/// </summary>
public sealed record FilaEventoMasivo(int Indice, string Casilla, string Pokemon, string Categoria, string OrigenActual, string Estado, string? Evento, int? Ano, string Idioma, string Columna,
    string TextoEstado, string Descripcion, IReadOnlyList<string> Detalle, bool NecesitaHome)
{
    public bool Propuesto => Estado == EstadosEvento.Proposed;
}

/// <summary>Lo que salió de buscar las versiones de evento (sin tocar la partida): las filas, el resumen del núcleo y, si no se pudo, por qué (cancelado o un problema).</summary>
public sealed record PlanEventosMasivo(IReadOnlyList<FilaEventoMasivo> Filas, string Resumen, bool Cancelado = false, string? Problema = null)
{
    public IReadOnlyList<FilaEventoMasivo> Propuestas => [.. Filas.Where(f => f.Propuesto)];
}

/// <summary>
/// Puerto de las operaciones masivas («Edición masiva» y, desde el bloque 9b, «Completar el juego»): lo implementa <c>Pokekeep.Mobile.Operaciones</c> con el <c>BulkCoordinator</c> del núcleo compartido
/// y las pruebas con motores guionizados. UNA operación a la vez; la transacción escribe todo al final en una sola entrada del historial único (Deshacer exacto). ADR 0069.
/// </summary>
public interface IMasivas
{
    /// <summary>Por qué el motor no está disponible (null si lo está).</summary>
    string? NoDisponible { get; }

    /// <summary>Hay otra operación de Pokekeep en curso (solo se admite una a la vez, sea de una casilla o masiva).</summary>
    bool Ocupado { get; }

    /// <summary>Las operaciones de la edición masiva en el orden de la ventana, con sus textos del núcleo.</summary>
    IReadOnlyList<OperacionMasivaInfo> Operaciones();

    /// <summary>Orden FIJO de ejecución de las operaciones dentro de cada Pokémon (<c>AdjustOperationNames.Order</c>).</summary>
    IReadOnlyList<string> OrdenDeEjecucion();

    /// <summary>Por qué no se puede lanzar una edición masiva sobre esta partida ahora (sin partida, sin nombre de entrenador, operación en curso, sin cajas ni equipo, motor no disponible); null si se puede.</summary>
    string? MotivoEdicion(Sesion sesion);

    /// <summary>Resuelve el ámbito: qué se procesa y qué se omite (huevos, casillas bloqueadas, zonas extra), sin tocar nada.</summary>
    ResumenAmbito Resolver(Sesion sesion, AlcanceMasivo alcance);

    /// <summary>Procesos del motor que trabajan a la vez como mucho EN ESTE DISPOSITIVO (1 mientras no haya pool de procesos: F9X-01). El ajuste «Procesos en paralelo» nunca lo supera.</summary>
    int ProcesosMaximos { get; }

    /// <summary>Tiempo estimado de una edición de <paramref name="unidades"/> Pokémon con <paramref name="procesos"/> procesos (1,2 s por Pokémon entre los procesos, como el escritorio).</summary>
    TimeSpan Estimar(int unidades, int procesos);

    /// <summary>
    /// Edición masiva: cada Pokémon del ámbito por el motor (una solicitud por Pokémon, un fallo no para el lote) y, al final, UNA transacción con segunda opinión del Core. Cancelar descarta todo salvo que
    /// <paramref name="colocarGenerados"/> (UNA pregunta: «¿colocar los N ya preparados?») diga que sí. La partida activa que cambie mientras tanto descarta TODO.
    /// </summary>
    Task<ResultadoMasivo> EjecutarEdicionAsync(Sesion sesion, AlcanceMasivo alcance, IReadOnlyCollection<string> operaciones, int procesos,
        IProgress<ProgresoMasivo>? progreso, Func<int, Task<bool>>? colocarGenerados, CancellationToken cancelar);

    /// <summary>La partida que la app tiene delante ahora (como <c>IAccionesPokekeep.SesionActiva</c>).</summary>
    void SesionActiva(Sesion? sesion) { }

    // ============================================================================================ bloque 9a: Edición masiva completa
    /// <summary>Textos de la ventana (ámbitos, botones, «Preferir la versión de evento»...), del núcleo.</summary>
    TextosEdicionMasiva TextosEdicion => throw new NotSupportedException();

    /// <summary>Las cajas de la partida (para «Cajas concretas»), con su nombre.</summary>
    IReadOnlyList<CajaMasiva> Cajas(Sesion sesion) => throw new NotSupportedException();

    /// <summary>Los Pokémon que «Elegir Pokémon…» ofrece: los de las cajas elegidas o, si no hay ninguna, de todas (sin el equipo, como el escritorio), sin huevos ni casillas bloqueadas.</summary>
    IReadOnlyList<CandidatoMasivo> Candidatos(Sesion sesion, IReadOnlyList<int> cajas) => throw new NotSupportedException();

    /// <summary>
    /// «Preferir la versión de evento», fase de BÚSQUEDA: para cada legendario, sublegendario o singular del ámbito que no sea ya de evento, prueba con el motor sus tarjetas (como mucho
    /// <c>EventPreference.MaxAttemptsPerPokemon</c>) y propone la primera que sale legal. No toca la partida. Cancelar devuelve un plan <see cref="PlanEventosMasivo.Cancelado"/>.
    /// </summary>
    Task<PlanEventosMasivo> BuscarVersionesDeEventoAsync(Sesion sesion, AlcanceMasivo alcance, IReadOnlyCollection<string> operaciones, int procesos, IProgress<ProgresoMasivo>? progreso, CancellationToken cancelar)
        => throw new NotSupportedException();

    /// <summary>Texto del botón «Aplicar» de la vista previa de eventos (cuántos cambios de origen) y si se puede pulsar.</summary>
    (string Texto, bool Activo) AplicarEventos(int aceptados, bool otrasOperaciones) => throw new NotSupportedException();

    /// <summary>
    /// La edición masiva con las sustituciones por versión de evento ACEPTADAS en la vista previa (<paramref name="aceptadas"/>: índices de <see cref="FilaEventoMasivo.Indice"/>). El ajuste de HOME se relee aquí, al
    /// EJECUTAR: si se desactivó tras la búsqueda, las que necesitan pasar por HOME no se aplican. Sin plan de eventos es la edición de siempre.
    /// </summary>
    Task<ResultadoMasivo> EjecutarEdicionAsync(Sesion sesion, AlcanceMasivo alcance, IReadOnlyCollection<string> operaciones, int procesos, PlanEventosMasivo? eventos, IReadOnlyCollection<int> aceptadas,
        IProgress<ProgresoMasivo>? progreso, Func<int, Task<bool>>? colocarGenerados, CancellationToken cancelar)
        => eventos is null ? EjecutarEdicionAsync(sesion, alcance, operaciones, procesos, progreso, colocarGenerados, cancelar) : throw new NotSupportedException();
}
