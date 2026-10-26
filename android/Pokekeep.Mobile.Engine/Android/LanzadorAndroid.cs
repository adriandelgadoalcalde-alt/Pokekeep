// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Android.Content;
using Android.OS;
using Pokekeep.Mobile.Engine.Ipc;

namespace Pokekeep.Mobile.Engine.Plataforma;

/// <summary>
/// Arranca y para el servicio <c>:motor</c> (o, en el pool, <c>:motor2</c>/<c>:motor3</c>: <paramref name="servicio"/>), se conecta a su socket y lo mata (<c>Process.KillProcess</c>) cuando la interfaz lo ordena.
/// </summary>
public sealed class LanzadorAndroid(Context contexto, string? catalogo, string? soluciones, int versionCode, string? pruebas = null, string servicio = LanzadorAndroid.ServicioPrincipal) : IMotorLanzador
{
    public const string ServicioPrincipal = "com.incredibad.pokekeep.EngineService";

    /// <summary>Los procesos de motor declarados, en orden (<see cref="EngineService"/>, <see cref="EngineServiceMotor2"/>, <see cref="EngineServiceMotor3"/>): el pool usa los N primeros.</summary>
    public static readonly IReadOnlyList<string> Servicios = [ServicioPrincipal, "com.incredibad.pokekeep.EngineServiceMotor2", "com.incredibad.pokekeep.EngineServiceMotor3"];

    /// <summary>
    /// Pool de <paramref name="motores"/> procesos (1 a <see cref="Servicios"/>.Count), cada uno con su pasarela y su lanzador (F9X-01). Con 1 es la pasarela única de siempre dentro de un pool de uno.
    /// </summary>
    public static PoolMotores Pool(Context contexto, string? catalogo, string? soluciones, int versionCode, int motores, OpcionesGateway? opciones = null, Action<string>? registro = null, string? pruebas = null)
        => new([.. Servicios.Take(Math.Clamp(motores, 1, Servicios.Count)).Select((s, i) =>
            (AutoLegalidad.Ui.IEngineGateway)new AndroidEngineGateway(new LanzadorAndroid(contexto, catalogo, soluciones, versionCode, pruebas, s), opciones, registro is null ? null : t => registro($"[motor{i + 1}] {t}")))]);

    public TimeSpan EsperaConexion { get; init; } = TimeSpan.FromSeconds(20);

    public string? NoDisponible => string.IsNullOrEmpty(catalogo) || !File.Exists(catalogo) ? "El catálogo de datos de la Librería no está disponible: reinicia la app para que se copie de nuevo." : null;

    private Intent Intencion(string nombre, long generacion)
        => new Intent().SetClassName(contexto.PackageName!, servicio)
            .PutExtra(EngineService.ExtraSocket, nombre).PutExtra(EngineService.ExtraGeneracion, generacion)
            .PutExtra(EngineService.ExtraCatalogo, catalogo).PutExtra(EngineService.ExtraSoluciones, soluciones)
            .PutExtra(EngineService.ExtraVersionCode, versionCode).PutExtra(EngineService.ExtraPruebas, pruebas);

    public async Task<Stream> ConectarAsync(string nombre, long generacion, CancellationToken cancelar)
    {
        contexto.StartService(Intencion(nombre, generacion));
        return await ConectorSocketAbstracto.ConectarAsync(nombre, EsperaConexion, cancelar).ConfigureAwait(false);
    }

    public void Matar(int pid) => global::Android.OS.Process.KillProcess(pid);

    public void Parar() => contexto.StopService(new Intent().SetClassName(contexto.PackageName!, servicio));

    /// <summary>Andamiaje de F9: el motor pasa a primer plano (notificacion <c>dataSync</c>) mientras dura una operacion larga, y vuelve a segundo plano al terminar.</summary>
    public void ElevarAPrimerPlano(string titulo)
        => contexto.StartForegroundService(new Intent().SetClassName(contexto.PackageName!, servicio).SetAction(EngineService.AccionPrimerPlano).PutExtra(EngineService.ExtraTitulo, titulo));

    public void VolverASegundoPlano()
        => contexto.StartService(new Intent().SetClassName(contexto.PackageName!, servicio).SetAction(EngineService.AccionSegundoPlano));
}
