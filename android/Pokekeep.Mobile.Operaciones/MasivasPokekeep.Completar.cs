// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Diagnostics;
using AutoLegalidad.Ui;
using AutoLegalidad.Ui.Bulk;
using AutoLegalidad.Ui.Library;
using AutoLegalidad.Ui.Operation;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Masivas;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Partida;

namespace Pokekeep.Mobile.Operaciones;

/// <summary>
/// «Completar el juego» en el móvil (bloque 9b de la F9, ADR 0069): el MISMO <see cref="BulkCoordinator"/> del escritorio (disponibilidad, <see cref="LivingDexPlanner"/>, una solicitud por especie-forma con reintento por origen,
/// segunda opinión del Core y UNA transacción con un único Deshacer). Los textos de la vista previa son los de <see cref="LivingDexPreview"/>, los mismos que lee <c>LivingDexPreviewForm</c>.
/// <para>
/// Lo propio del móvil, igual que en la Edición masiva: el semáforo común de «una operación a la vez», la sesión activa (otra partida = «Resultado descartado»), UNA entrada en el historial único de la sesión con Deshacer
/// exacto de las casillas y los procesos en paralelo recortados a <see cref="ProcesosMaximos"/>. La casilla de HOME de la vista previa solo cambia el plan (el ajuste lo guarda la pantalla al pulsar «Generar»).
/// </para>
/// </summary>
public sealed partial class MasivasPokekeep : ICompletarJuego
{
    /// <summary>El plan del núcleo con la partida para la que se calculó (si al generar ya no es la de la sesión, no se aplica nada: regla 7).</summary>
    private sealed record PlanCompletar(LivingDexPlan Plan, SaveFile Guardado, SaveFile? Borrador = null, string? Huella = null);

    /// <summary>El coordinador con el ajuste de HOME que marca la vista previa (sin tocar los ajustes guardados).</summary>
    private BulkCoordinator CoordinadorCon(Sesion sesion, bool home)
    {
        var ajustesConHome = acciones.Ajustes();
        ajustesConHome.AllowHomeTrackerless = home;
        return new(acciones.Entornos.Anfitrion(sesion, acciones.Entornos.De(sesion).Env, 0), motor, () => ajustesConHome, datos);
    }

    public string? MotivoCompletar(Sesion sesion)
    {
        if (acciones.Ocupado) return Textos.OperacionEnCurso;
        using var coord = Coordinador(sesion);
        var av = coord.GetLivingDexAvailability();
        return av.Enabled ? null : av.Reason;
    }

    public string LineaJuego(Sesion sesion) => LivingDexPreview.GameLine(sesion.Guardado);

    public IReadOnlyList<string> EtiquetasFormas => LivingDexPreview.FormLabels;

    public IReadOnlyList<string> EtiquetasOrden(Sesion sesion) => LivingDexPreview.OrderLabels(sesion.Guardado);

    public string TextoColocar(int generados) => LivingDexPreview.PlacementQuestion(generados, edit: false);

    public async Task<IReadOnlySet<(int Especie, int Forma)>?> AnalizarLoQueTienesAsync(Sesion sesion, CancellationToken cancelar)
    {
        try
        {
            using var coord = Coordinador(sesion);
            return await coord.ScanOwnedAsync(cancelar).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Como el escritorio (BulkDialogs.OpenLivingDexAsync): sin análisis de lo que ya hay, «Omitir lo que ya tengo» no omite nada (se genera igual).
            AutoLegalidad.Registro.RegistroFallos.Aviso(IdiomaInterfaz.T("MasivasPokekeepCompletar.CompletarElJuegoNo"), ex);
            return null;
        }
    }

    internal static LivingDexOptions OpcionesDelNucleo(OpcionesCompletar o) => new()
    {
        Forms = Enum.Parse<LivingDexForms>(o.Formas.ToString()),
        Order = Enum.Parse<LivingDexOrder>(o.Orden.ToString()),
        StartBox = Math.Max(0, o.CajaInicio),
        SkipOwned = o.OmitirLoQueTengo,
        PreferEvent = o.PreferirEvento,
        IncludeHome = o.IncluirHome,
    };

    public VistaPreviaCompletar Planificar(Sesion sesion, OpcionesCompletar opciones, IReadOnlySet<(int Especie, int Forma)>? tienes, int procesos)
    {
        var borrador = opciones.ReemplazarTodo ? PrepararReemplazo(sesion.Guardado) : null;
        using var coord = borrador is null ? CoordinadorCon(sesion, opciones.IncluirHome) : CoordinadorBorrador(sesion, borrador);
        var efectivas = opciones.ReemplazarTodo ? opciones with { CajaInicio = 0, OmitirLoQueTengo = false, IncluirHome = false } : opciones;
        var plan = coord.PlanLivingDex(OpcionesDelNucleo(efectivas), opciones.ReemplazarTodo ? null : tienes);
        var v = LivingDexPreview.Build(plan, sesion.Guardado, efectivas.IncluirHome, Math.Clamp(procesos, 1, ProcesosMaximos));
        return new VistaPreviaCompletar(opciones, v.Summary, v.Capacity, v.Estimate, v.GoText, v.GoEnabled, v.ShowHomeOption, v.ListTitle,
            [.. v.Rows.Select(f => new FilaNoSeGenera(f.Name, f.Status.ToString(), f.StatusLabel, f.Shown, f.Reason, LivingDexPreview.Detail(f)))],
            plan.Planned, plan.Fit, plan.FreeSlots.Count, new PlanCompletar(plan, sesion.Guardado, borrador, borrador is null ? null : EscrituraPartida.Huella(sesion.Guardado)))
        { Sustituidos = borrador is null ? 0 : CasillasReemplazables(sesion.Guardado).Count(s => s.Read(sesion.Guardado).Species != 0) };
    }

    internal static IReadOnlyList<ISlotInfo> CasillasReemplazables(SaveFile sav)
        => [.. Enumerable.Range(0, sav.HasBox ? sav.BoxCount : 0).SelectMany(b => Enumerable.Range(0, sav.BoxSlotCount).Select(i => (ISlotInfo)new SlotInfoBox(b, i, sav))).Where(s => s.CanWriteTo(sav))];

    internal static SaveFile PrepararReemplazo(SaveFile sav)
    {
        var copia = sav.Clone();
        foreach (var slot in CasillasReemplazables(copia)) slot.WriteTo(copia, copia.BlankPKM, EntityImportSettings.None);
        return copia;
    }

    private BulkCoordinator CoordinadorBorrador(Sesion original, SaveFile borrador)
    {
        var config = acciones.Ajustes();
        config.AllowHomeTrackerless = false;
        var env = new CoreEditEnvironment(borrador, new BorradorEditor(borrador.BlankPKM));
        return new(acciones.Entornos.Anfitrion(original, env, 0), motor, () => config, datos);
    }

    private sealed class DeshacerReemplazo(Sesion sesion, int version, int casillas) : IDeshacerMasivo
    {
        public int Casillas => casillas;
        public string? Motivo => PuedeDeshacer() ? null : IdiomaInterfaz.T("CompletarJuego.ReemplazoCambio");
        public bool PuedeDeshacer() => sesion.Casillas.Version == version && sesion.Casillas.PuedeDeshacer;
        public bool Deshacer() => PuedeDeshacer() && sesion.Casillas.Deshacer().Correcto;
    }

    public async Task<ResultadoMasivo> EjecutarCompletarAsync(Sesion sesion, VistaPreviaCompletar vista, int procesos, IProgress<ProgresoMasivo>? progreso, Func<int, Task<bool>>? colocarGenerados,
        CancellationToken cancelar)
    {
        if (!await acciones.Exclusion.WaitAsync(0, CancellationToken.None))
            return new ResultadoMasivo(ClaseMasiva.Ocupado, Textos.OperacionEnCurso, null, InformeMasivo.Vacio(Textos.CompletarElJuego), 0, TimeSpan.Zero, null);
        var reloj = Stopwatch.StartNew();
        try
        {
            if (vista.Plan is not PlanCompletar pc || !ReferenceEquals(pc.Guardado, sesion.Guardado))
                return new ResultadoMasivo(ClaseMasiva.CambioDeDestino, IdiomaInterfaz.T("MasivasPokekeepCompletar.ResultadoDescartado"), Textos.PartidaCambio, InformeMasivo.Vacio(Textos.CompletarElJuego), 0, reloj.Elapsed, null);
            var plan = pc.Plan;
            if (pc.Borrador is not null)
                return await EjecutarReemplazo(sesion, pc, procesos, progreso, cancelar, reloj);
            if (plan.Fit == 0)
                return new ResultadoMasivo(ClaseMasiva.SinCambios, IdiomaInterfaz.T("MasivasPokekeepCompletar.NoHayNadaQue"), IdiomaInterfaz.T("MasivasPokekeepCompletar.NoSeHaModificado"), InformeMasivo.Vacio(Textos.CompletarElJuego), 0, reloj.Elapsed, null);
            // Las casillas libres del plan con sus bytes de ANTES (vacías): tras aplicar se sabe cuáles escribió el lote (las previstas y las de reserva para recolocar).
            var antes = plan.FreeSlots.Select(s => (Slot: s, Datos: s.Read(sesion.Guardado).Data.ToArray())).ToList();

            using var coord = Coordinador(sesion);
            coord.ProgressChanged += (_, p) => progreso?.Report(Mapear(p, reloj.Elapsed));
            using var registro = cancelar.Register(coord.Cancel);
            var salida = await coord.RunLivingDexAsync(plan, Math.Clamp(procesos, 1, ProcesosMaximos), colocarGenerados);

            IDeshacerMasivo? deshacer = null;
            if (salida.Applied)
            {
                var tocadas = new List<ISlotInfo>();
                foreach (var (slot, datos0) in antes)
                    if (!slot.Read(sesion.Guardado).Data.SequenceEqual(datos0)) tocadas.Add(slot);
                // UNA entrada MÁS del historial único de la sesión: Historial, «cambios pendientes» y Deshacer cuentan lo mismo. El registro en la Pokédex (ajuste) no se deshace, como en el escritorio.
                // La cuenta es la del lote (lo escrito), no la de casillas cambiadas: si el usuario ocupó una casilla libre mientras tanto, esa es suya (tiene su propia entrada).
                sesion.Casillas.Notificar(EditorCasillas.TipoCambio.Masivo, tocadas, $"{Textos.CompletarElJuego}: {salida.Written} Pokémon");
                if (salida.Undo is { } token) deshacer = new DeshacedorLote(token, sesion, sesion.Casillas.Version);
            }
            else if (salida.Undo is { } t2) deshacer = new DeshacedorLote(t2, sesion, -1);
            return new ResultadoMasivo(Clase(salida), salida.Headline, salida.Detail, Mapear(salida.Report), salida.Written, salida.Elapsed == default ? reloj.Elapsed : salida.Elapsed, deshacer);
        }
        finally { acciones.Exclusion.Release(); }
    }

    /// <summary>Copias de seguridad que se conservan de «Completar con reemplazo» (la recién verificada incluida); antes crecían sin límite en la carpeta privada (auditoría uso10, A11).</summary>
    internal const int CopiasCompletarConservadas = 5;

    internal static void LimpiarCopiasAntiguas(string carpeta, string nueva)
    {
        try
        {
            var viejas = new DirectoryInfo(carpeta).GetFiles("antes-*.sav").Where(f => !string.Equals(f.FullName, Path.GetFullPath(nueva), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.LastWriteTimeUtc).Skip(CopiasCompletarConservadas - 1);
            foreach (var f in viejas) f.Delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { AutoLegalidad.Registro.RegistroFallos.Aviso("copias-completar", ex); }
    }

    private async Task<ResultadoMasivo> EjecutarReemplazo(Sesion sesion, PlanCompletar pc, int procesos, IProgress<ProgresoMasivo>? progreso, CancellationToken cancelar, Stopwatch reloj)
    {
        ResultadoMasivo SinCambios() => new(ClaseMasiva.NoSePudo, IdiomaInterfaz.T("CompletarJuego.ReemplazoSinCambios"), null, InformeMasivo.Vacio(Textos.CompletarElJuego), 0, reloj.Elapsed, null);
        // Nada que reemplazar no es un fallo (auditoría uso10, A11): «sin cambios», no «no se pudo».
        if (pc.Plan.Fit == 0) return SinCambios() with { Clase = ClaseMasiva.SinCambios };
        if (sesion.CarpetaPrivada is null || pc.Huella != EscrituraPartida.Huella(sesion.Guardado)) return SinCambios();
        using var coord = CoordinadorBorrador(sesion, pc.Borrador!);
        coord.ProgressChanged += (_, p) => progreso?.Report(Mapear(p, reloj.Elapsed));
        using var registro = cancelar.Register(coord.Cancel);
        // El borrador solo se coloca si finaliza entero; cancelar o un fallo conserva la partida original.
        var salida = await coord.RunLivingDexAsync(pc.Plan, Math.Clamp(procesos, 1, ProcesosMaximos), _ => Task.FromResult(false));
        if (!salida.Applied || salida.Written != pc.Plan.Fit || cancelar.IsCancellationRequested ||
            !ReferenceEquals(acciones.Entornos.Activa, sesion) || pc.Huella != EscrituraPartida.Huella(sesion.Guardado)) return SinCambios();
        var carpeta = Path.Combine(sesion.CarpetaPrivada, "copias-completar");
        var ruta = Path.Combine(carpeta, $"antes-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.sav");
        try
        {
            Directory.CreateDirectory(carpeta);
            var bytes = sesion.Guardado.Write().ToArray();
            await File.WriteAllBytesAsync(ruta, bytes, cancelar);
            if (!(await File.ReadAllBytesAsync(ruta, cancelar)).AsSpan().SequenceEqual(bytes)) throw new IOException(IdiomaInterfaz.T("CompletarJuego.CopiaNoVerificada"));
            LimpiarCopiasAntiguas(carpeta, ruta);
        }
        catch (OperationCanceledException) when (cancelar.IsCancellationRequested)
        {
            return SinCambios() with { Clase = ClaseMasiva.Cancelado };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AutoLegalidad.Registro.RegistroFallos.Aviso(IdiomaInterfaz.T("CompletarJuego.CopiaNoVerificada"), ex);
            return SinCambios() with { Detalle = IdiomaInterfaz.T("CompletarJuego.CopiaNoVerificada") };
        }
        if (cancelar.IsCancellationRequested || !ReferenceEquals(acciones.Entornos.Activa, sesion) || pc.Huella != EscrituraPartida.Huella(sesion.Guardado)) return SinCambios();
        var slots = CasillasReemplazables(sesion.Guardado);
        var aplicado = EscrituraPartida.Aplicar(sesion, Textos.CompletarElJuego, () =>
        {
            foreach (var slot in slots) slot.WriteTo(sesion.Guardado, slot.Read(pc.Borrador!), EntityImportSettings.None);
        });
        return new(aplicado.Aplicado ? ClaseMasiva.Aplicado : ClaseMasiva.SinCambios, salida.Headline,
            IdiomaInterfaz.T("CompletarJuego.CopiaGuardada"), Mapear(salida.Report), aplicado.Aplicado ? salida.Written : 0, reloj.Elapsed,
            aplicado.Aplicado ? new DeshacerReemplazo(sesion, sesion.Casillas.Version, slots.Count) : null);
    }
}
