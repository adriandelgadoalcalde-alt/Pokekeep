// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Ui;
using Pokekeep.Mobile.Ui.Masivas;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Operaciones.OperacionLarga;

/// <summary>
/// Las operaciones masivas con el servicio en primer plano, la notificación, el Cancelar duro, el checkpoint y la política de energía del bloque 9c (ADR 0070) alrededor: decora el <see cref="IMasivas"/>
/// real (<see cref="MasivasPokekeep"/>) sin tocarlo. Todo lo que no es ejecutar se reenvía tal cual. Una operación que se añada a <see cref="IMasivas"/> o a <see cref="ICompletarJuego"/> (bloque 9b) se reenvía
/// aquí con <see cref="SupervisorOperacionLarga.EjecutarAsync"/>, igual que la edición: si no, cae en la implementación por defecto de la interfaz (<c>NotSupportedException</c>) y la prueba
/// <c>OperacionLargaTests.ElDecoradorReenviaTodosLosMiembros...</c> lo detecta. Implementa también <see cref="ICompletarJuego"/> (sin ello la Shell, que abre «Completar el juego» solo si <c>masivas is ICompletarJuego</c>, dejaría la entrada de «Más» inerte).
/// </summary>
public sealed class MasivasSupervisadas(IMasivas interior, SupervisorOperacionLarga supervisor) : IMasivas, ICompletarJuego
{
    public IMasivas Interior => interior;
    public SupervisorOperacionLarga Supervisor => supervisor;

    public string? NoDisponible => interior.NoDisponible;
    public bool Ocupado => interior.Ocupado || supervisor.EnCurso;
    public IReadOnlyList<OperacionMasivaInfo> Operaciones() => interior.Operaciones();
    public IReadOnlyList<string> OrdenDeEjecucion() => interior.OrdenDeEjecucion();
    public string? MotivoEdicion(Sesion sesion) => supervisor.EnCurso ? Textos.OperacionEnCurso : interior.MotivoEdicion(sesion);
    public ResumenAmbito Resolver(Sesion sesion, AlcanceMasivo alcance) => interior.Resolver(sesion, alcance);

    /// <summary>El máximo del dispositivo, rebajado por la energía de ahora (ahorro de energía o calor: 1). El ajuste «Procesos en paralelo» nunca lo supera.</summary>
    public int ProcesosMaximos => supervisor.Decidir(interior.ProcesosMaximos, interior.ProcesosMaximos).Procesos;

    public TimeSpan Estimar(int unidades, int procesos) => interior.Estimar(unidades, Math.Min(procesos, ProcesosMaximos));
    public void SesionActiva(Sesion? sesion) => interior.SesionActiva(sesion);

    public Task<ResultadoMasivo> EjecutarEdicionAsync(Sesion sesion, AlcanceMasivo alcance, IReadOnlyCollection<string> operaciones, int procesos,
        IProgress<ProgresoMasivo>? progreso, Func<int, Task<bool>>? colocarGenerados, CancellationToken cancelar)
    {
        // Sin trabajo (ocupado, nada que editar, ninguna operación): el núcleo responde al momento; no se arranca el servicio ni se deja registro.
        if (interior.Ocupado || operaciones.Count == 0) return interior.EjecutarEdicionAsync(sesion, alcance, operaciones, procesos, progreso, colocarGenerados, cancelar);
        var resumen = interior.Resolver(sesion, alcance);
        if (!resumen.HayAlgoQueEditar) return interior.EjecutarEdicionAsync(sesion, alcance, operaciones, procesos, progreso, colocarGenerados, cancelar);
        return supervisor.EjecutarAsync(Textos.EdicionMasiva, alcance.Ambito.ToString(), resumen.Incluidos, procesos, interior.ProcesosMaximos,
            (p, prog, colocar, ct) => interior.EjecutarEdicionAsync(sesion, alcance, operaciones, p, prog, colocar, ct), progreso, colocarGenerados, cancelar);
    }

    // ============================================================================================ bloque 9a: Edición masiva completa
    public TextosEdicionMasiva TextosEdicion => interior.TextosEdicion;
    public IReadOnlyList<CajaMasiva> Cajas(Sesion sesion) => interior.Cajas(sesion);
    public IReadOnlyList<CandidatoMasivo> Candidatos(Sesion sesion, IReadOnlyList<int> cajas) => interior.Candidatos(sesion, cajas);
    public (string Texto, bool Activo) AplicarEventos(int aceptados, bool otrasOperaciones) => interior.AplicarEventos(aceptados, otrasOperaciones);

    /// <summary>La BÚSQUEDA de versiones de evento no toca la partida (solo propone un plan): se reenvía sin servicio ni registro; la edición que la aplica sí pasa por el supervisor (abajo).</summary>
    public Task<PlanEventosMasivo> BuscarVersionesDeEventoAsync(Sesion sesion, AlcanceMasivo alcance, IReadOnlyCollection<string> operaciones, int procesos, IProgress<ProgresoMasivo>? progreso, CancellationToken cancelar)
        => interior.BuscarVersionesDeEventoAsync(sesion, alcance, operaciones, Math.Min(procesos, ProcesosMaximos), progreso, cancelar);

    /// <summary>La edición con las versiones de evento aceptadas: igual que la edición de siempre, con el supervisor alrededor.</summary>
    public Task<ResultadoMasivo> EjecutarEdicionAsync(Sesion sesion, AlcanceMasivo alcance, IReadOnlyCollection<string> operaciones, int procesos, PlanEventosMasivo? eventos, IReadOnlyCollection<int> aceptadas,
        IProgress<ProgresoMasivo>? progreso, Func<int, Task<bool>>? colocarGenerados, CancellationToken cancelar)
    {
        if (eventos is null) return EjecutarEdicionAsync(sesion, alcance, operaciones, procesos, progreso, colocarGenerados, cancelar);
        if (interior.Ocupado) return interior.EjecutarEdicionAsync(sesion, alcance, operaciones, procesos, eventos, aceptadas, progreso, colocarGenerados, cancelar);
        var resumen = interior.Resolver(sesion, alcance);
        if (!resumen.HayAlgoQueEditar) return interior.EjecutarEdicionAsync(sesion, alcance, operaciones, procesos, eventos, aceptadas, progreso, colocarGenerados, cancelar);
        return supervisor.EjecutarAsync(Textos.EdicionMasiva, alcance.Ambito.ToString(), resumen.Incluidos, procesos, interior.ProcesosMaximos,
            (p, prog, colocar, ct) => interior.EjecutarEdicionAsync(sesion, alcance, operaciones, p, eventos, aceptadas, prog, colocar, ct), progreso, colocarGenerados, cancelar);
    }

    // ============================================================================================ bloque 9b: Completar el juego
    private ICompletarJuego Completar => interior as ICompletarJuego ?? throw new NotSupportedException(IdiomaInterfaz.T("MasivasSupervisadas.ElPuertoInteriorNo"));

    public string? MotivoCompletar(Sesion sesion) => supervisor.EnCurso ? Textos.OperacionEnCurso : interior is ICompletarJuego c ? c.MotivoCompletar(sesion) : IdiomaInterfaz.T("MasivasSupervisadas.CompletarElJuegoNo");
    public string LineaJuego(Sesion sesion) => Completar.LineaJuego(sesion);
    public IReadOnlyList<string> EtiquetasFormas => Completar.EtiquetasFormas;
    public IReadOnlyList<string> EtiquetasOrden(Sesion sesion) => Completar.EtiquetasOrden(sesion);
    public Task<IReadOnlySet<(int Especie, int Forma)>?> AnalizarLoQueTienesAsync(Sesion sesion, CancellationToken cancelar) => Completar.AnalizarLoQueTienesAsync(sesion, cancelar);
    public VistaPreviaCompletar Planificar(Sesion sesion, OpcionesCompletar opciones, IReadOnlySet<(int Especie, int Forma)>? tienes, int procesos) => Completar.Planificar(sesion, opciones, tienes, Math.Min(procesos, ProcesosMaximos));
    public string TextoColocar(int generados) => Completar.TextoColocar(generados);

    /// <summary>«Completar el juego» pasa por el supervisor igual que la edición: servicio en primer plano, notificación con progreso, Cancelar duro, checkpoint y energía. Sin nada que generar (o ocupado) el núcleo responde al momento y no se arranca nada.</summary>
    public Task<ResultadoMasivo> EjecutarCompletarAsync(Sesion sesion, VistaPreviaCompletar vista, int procesos, IProgress<ProgresoMasivo>? progreso, Func<int, Task<bool>>? colocarGenerados, CancellationToken cancelar)
    {
        var completar = Completar;
        if (interior.Ocupado || vista.Caben == 0) return completar.EjecutarCompletarAsync(sesion, vista, procesos, progreso, colocarGenerados, cancelar);
        return supervisor.EjecutarAsync(Textos.CompletarElJuego, "Pokédex", vista.Caben, procesos, interior.ProcesosMaximos,
            (p, prog, colocar, ct) => completar.EjecutarCompletarAsync(sesion, vista, p, prog, colocar, ct), progreso, colocarGenerados, cancelar);
    }
}
