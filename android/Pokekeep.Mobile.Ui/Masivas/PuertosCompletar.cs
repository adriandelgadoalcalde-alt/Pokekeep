// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Ajustes;
using Pokekeep.Mobile.Ui.Modelo;

namespace Pokekeep.Mobile.Ui.Masivas;

/// <summary>
/// Las opciones de «Completar el juego» (las del diálogo del escritorio, <c>LivingDexOptions</c>): formas, orden, caja de inicio (0 = la primera), omitir lo que ya tengo, preferir la versión de evento y la casilla de HOME
/// tal como está en la vista previa (la casilla cambia el ajuste solo si se pulsa «Generar», como en el escritorio).
/// </summary>
public sealed record OpcionesCompletar(FormasCompletar Formas, OrdenCompletar Orden, int CajaInicio, bool OmitirLoQueTengo, bool PreferirEvento, bool IncluirHome, bool ReemplazarTodo = false);

/// <summary>Una fila de «Lo que no se genera, con su motivo». <see cref="Estado"/> es el nombre del <c>BulkStatus</c> (estable) y <see cref="Etiqueta"/> su texto del núcleo; <see cref="MotivoCompleto"/> es el del detalle.</summary>
public sealed record FilaNoSeGenera(string Nombre, string Estado, string Etiqueta, string Motivo, string MotivoCompleto, string Detalle)
{
    public bool EsError => EstadosMasivos.EsError(Estado);
    public bool EsAviso => EstadosMasivos.EsAviso(Estado);
    public string Accesible => $"{Nombre}, {Etiqueta}. {Motivo}";
}

/// <summary>
/// La vista previa de «Completar el juego», calculada SIN llamar al motor ni tocar la partida: los textos son los del núcleo (<c>LivingDexPreview</c>), los mismos que enseña el escritorio. <see cref="Plan"/> es el plan del núcleo,
/// opaco para la interfaz: «Generar» lo pasa tal cual a <see cref="ICompletarJuego.EjecutarCompletarAsync"/>.
/// </summary>
public sealed record VistaPreviaCompletar(
    OpcionesCompletar Opciones, string Resumen, string Capacidad, string Estimado, string TextoGenerar, bool PuedeGenerar, bool MostrarHome, string TituloLista, IReadOnlyList<FilaNoSeGenera> Filas,
    int Planificados, int Caben, int CasillasLibres, object Plan)
{
    public int Sustituidos { get; init; }
    public bool HayFilas => Filas.Count > 0;
    public bool HayEstimado => !string.IsNullOrEmpty(Estimado);
}

/// <summary>
/// Puerto de «Completar el juego» (R-006, bloque 9b de la F9). Es el segundo uso del asistente masivo: el MISMO <c>BulkCoordinator</c>, <c>LivingDexPlanner</c> y textos del escritorio; UNA transacción con un único Deshacer
/// al final y la UNA pregunta si se cancela con Pokémon ya generados. Interfaz aparte de <see cref="IMasivas"/> (que la amplía) para no pisar los métodos que añade el bloque 9a; ADR 0069.
/// </summary>
public interface ICompletarJuego : IMasivas
{
    /// <summary>Por qué no se puede completar esta partida ahora (sin partida, sin nombre de entrenador, operación en curso, sin cajas, sin catálogo, motor no disponible); null si se puede. Los textos del escritorio.</summary>
    string? MotivoCompletar(Sesion sesion);

    /// <summary>«Platino · LUCAS · idioma de los Pokémon nuevos: español (o el que el origen permita)».</summary>
    string LineaJuego(Sesion sesion);

    /// <summary>Etiquetas de «Formas» en el orden de <see cref="FormasCompletar"/>.</summary>
    IReadOnlyList<string> EtiquetasFormas { get; }

    /// <summary>Etiquetas de «Orden»: la Pokédex regional solo si PKHeX trae su número para este juego (SV, SWSH, BDSP, LA, Z-A).</summary>
    IReadOnlyList<string> EtiquetasOrden(Sesion sesion);

    /// <summary>Especie-forma de los Pokémon LEGALES que ya hay en cajas o equipo (análisis oficial de cada uno, fuera del hilo de la interfaz). Null si el análisis falla: «Omitir lo que ya tengo» no omite nada.</summary>
    Task<IReadOnlySet<(int Especie, int Forma)>?> AnalizarLoQueTienesAsync(Sesion sesion, CancellationToken cancelar);

    /// <summary>La vista previa con estas opciones (y <paramref name="procesos"/> para el tiempo estimado). No llama al motor ni toca la partida.</summary>
    VistaPreviaCompletar Planificar(Sesion sesion, OpcionesCompletar opciones, IReadOnlySet<(int Especie, int Forma)>? tienes, int procesos);

    /// <summary>La UNA pregunta al cancelar con Pokémon ya generados (el texto del escritorio: <c>BulkPlacementDialog</c>).</summary>
    string TextoColocar(int generados);

    /// <summary>
    /// Genera lo planificado (solo en casillas libres; nunca sobrescribe) y lo escribe todo al final en UNA transacción con segunda opinión del Core y un único Deshacer. Cancelar descarta todo salvo que
    /// <paramref name="colocarGenerados"/> diga que sí; entonces los generados se colocan SEGUIDOS desde la primera casilla libre. Si la partida activa cambia mientras tanto se descarta TODO.
    /// </summary>
    Task<ResultadoMasivo> EjecutarCompletarAsync(Sesion sesion, VistaPreviaCompletar vista, int procesos, IProgress<ProgresoMasivo>? progreso, Func<int, Task<bool>>? colocarGenerados, CancellationToken cancelar);
}
