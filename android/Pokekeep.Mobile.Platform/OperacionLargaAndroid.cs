// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Pokekeep.Mobile.Operaciones.OperacionLarga;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Platform;

/// <summary>
/// Servicio en primer plano <c>dataSync</c> del proceso de la INTERFAZ (F9X-02, ADR 0070, decisión 2): quien tiene que seguir vivo mientras dura una edición masiva o «Completar el juego» es el proceso que tiene
/// la partida y la transacción, no solo el motor. Lo arranca <see cref="PlataformaOperacionLargaAndroid.Iniciar"/> y lo para <see cref="PlataformaOperacionLargaAndroid.Terminar"/>. La notificación lleva el progreso
/// real y la acción «Cancelar» (Cancelar duro, F9X-04); <see cref="OnTimeout(int, ForegroundService)"/> (Android 15+, límite de tiempo de <c>dataSync</c>) cancela igual, sin aplicar nada.
/// </summary>
[Service(Name = "com.incredibad.pokekeep.OperacionLargaService", Exported = false, ForegroundServiceType = ForegroundService.TypeDataSync)]
public sealed class OperacionLargaService : Service
{
    public const string Etiqueta = "POKEKEEP-OPLARGA";
    public const int IdNotificacion = 4022;
    internal const string Canal = "operaciones-largas";
    internal const string AccionIniciar = "com.incredibad.pokekeep.OPLARGA_INICIAR";
    internal const string AccionCancelar = "com.incredibad.pokekeep.OPLARGA_CANCELAR";
    internal const string AccionTerminar = "com.incredibad.pokekeep.OPLARGA_TERMINAR";

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        switch (intent?.Action)
        {
            case AccionIniciar:
                {
                    var estado = PlataformaOperacionLargaAndroid.Estado ?? EstadoNotificacion.Inicial("Pokekeep", null);
                    StartForeground(IdNotificacion, PlataformaOperacionLargaAndroid.Construir(this, estado), ForegroundService.TypeDataSync);
                    Log.Info(Etiqueta, IdiomaInterfaz.T("OperacionLargaAndroid.ServicioEnPrimerPlano"));
                    break;
                }
            case AccionCancelar:
                Log.Info(Etiqueta, IdiomaInterfaz.T("OperacionLargaAndroid.CancelarDesdeLaNotificacion"));
                PlataformaOperacionLargaAndroid.AlCancelar?.Invoke();
                break;
            case AccionTerminar:
            default:
                StopForeground(StopForegroundFlags.Remove);
                StopSelf();
                break;
        }
        return StartCommandResult.NotSticky;   // si Android mata el proceso, no se reanuda: al reabrir, el registro de la operación avisa (F9X-06)
    }

    /// <summary>Android 14: se agotó el tiempo del servicio (la firma antigua). Igual que la de Android 15+.</summary>
    public override void OnTimeout(int startId) => OnTimeout(startId, ForegroundService.TypeDataSync);

    /// <summary>Android 15+: se agotó el tiempo que concede a un servicio <c>dataSync</c>. Hay que dejar el primer plano enseguida; la operación se cancela sin aplicar nada.</summary>
    public override void OnTimeout(int startId, ForegroundService fgsType)
    {
        Log.Warn(Etiqueta, IdiomaInterfaz.F("OperacionLargaAndroid.OntimeoutTipoSeCancela", fgsType));
        var agotar = PlataformaOperacionLargaAndroid.AlAgotarTiempo;
        PlataformaOperacionLargaAndroid.FueraDePrimerPlano();   // lo que llegue después (progreso de la cancelación) ya no publica la notificación
        agotar?.Invoke();
        StopForeground(StopForegroundFlags.Remove);
        ((NotificationManager?)GetSystemService(NotificationService))?.Cancel(IdNotificacion);
        StopSelf();
    }
}

/// <summary>La plataforma Android de las operaciones largas: servicio, notificación (canal de importancia baja, progreso por unidad, «Cancelar») y lectura de ahorro de energía y temperatura (F9X-07).</summary>
/// <remarks>
/// Estado estático (B4 de la auditoría): el servicio lo instancia Android, así que el estado de la notificación y las acciones de cancelar viven en campos estáticos. Hay una sola operación larga a la vez
/// (el supervisor lo garantiza) y se limpian en <see cref="Terminar"/> y en <c>onTimeout</c>.
/// </remarks>
public sealed class PlataformaOperacionLargaAndroid(Context contexto) : IPlataformaOperacionLarga
{
    internal static volatile EstadoNotificacion? Estado;
    internal static volatile Action? AlCancelar;
    internal static volatile Action? AlAgotarTiempo;
    private static volatile bool _activa;

    /// <summary>Android ha sacado el servicio del primer plano (<c>onTimeout</c>): se olvida el estado y no se publican más notificaciones de esta operación.</summary>
    internal static void FueraDePrimerPlano() { _activa = false; AlCancelar = null; AlAgotarTiempo = null; Estado = null; }

    private static readonly TimeSpan CacheEnergia = TimeSpan.FromSeconds(1);
    private EstadoEnergia? _energia;
    private long _energiaLeida;

    private Intent Intencion(string accion) => new Intent(contexto, typeof(OperacionLargaService)).SetAction(accion);

    public void Iniciar(EstadoNotificacion estado, Action cancelarDuro, Action limiteDeTiempo)
    {
        Estado = estado; AlCancelar = cancelarDuro; AlAgotarTiempo = limiteDeTiempo; _activa = true;
        CrearCanal(contexto);
        PedirPermisoDeNotificaciones();
        contexto.StartForegroundService(Intencion(OperacionLargaService.AccionIniciar));
    }

    public void Actualizar(EstadoNotificacion estado)
    {
        if (!_activa) return;   // ya terminada o fuera del primer plano: publicarla dejaría una notificación «en curso» huérfana (M3 de la auditoría)
        Estado = estado;
        var gestor = (NotificationManager?)contexto.GetSystemService(Context.NotificationService);
        gestor?.Notify(OperacionLargaService.IdNotificacion, Construir(contexto, estado));
    }

    public void Terminar()
    {
        var seguiaActiva = _activa;
        FueraDePrimerPlano();
        // Pendiente de dispositivo (revision del coordinador, 9c): tras onTimeout el servicio ya se paro; arrancarlo otra vez con la app en segundo plano lanza IllegalStateException (Android 8+). Se para sin arrancar nada.
        if (seguiaActiva) contexto.StartService(Intencion(OperacionLargaService.AccionTerminar));
        else contexto.StopService(Intencion(OperacionLargaService.AccionTerminar));
        // StopForeground solo quita la notificación mientras el servicio está en primer plano; tras onTimeout la 4022 sería una notificación corriente: se quita aquí también.
        ((NotificationManager?)contexto.GetSystemService(Context.NotificationService))?.Cancel(OperacionLargaService.IdNotificacion);
    }

    /// <summary>Ahorro de energía y temperatura, con una caché de 1 s: la vista previa lo lee en cada estimación (B3 de la auditoría) y cada lectura son dos llamadas al sistema.</summary>
    public EstadoEnergia LeerEnergia()
    {
        var ahora = System.Environment.TickCount64;
        if (_energia is { } cache && ahora - Interlocked.Read(ref _energiaLeida) < CacheEnergia.TotalMilliseconds) return cache;
        var pm = (PowerManager?)contexto.GetSystemService(Context.PowerService);
        var leida = pm is null ? EstadoEnergia.Normal : new EstadoEnergia(pm.IsPowerSaveMode, (EstadoTermico)Math.Clamp((int)pm.CurrentThermalStatus, 0, 6));
        _energia = leida;
        Interlocked.Exchange(ref _energiaLeida, ahora);
        return leida;
    }

    private static void CrearCanal(Context contexto)
    {
        var gestor = (NotificationManager?)contexto.GetSystemService(Context.NotificationService);
        if (gestor is null || gestor.GetNotificationChannel(OperacionLargaService.Canal) is not null) return;
        gestor.CreateNotificationChannel(new NotificationChannel(OperacionLargaService.Canal, EstadoNotificacion.NombreCanal, NotificationImportance.Low) { Description = EstadoNotificacion.DescripcionCanal });
    }

    /// <summary>
    /// Android 13+: sin permiso la operación sigue (el servicio en primer plano no lo necesita), pero la notificación no se ve. Se pide al empezar la operación, desde la actividad viva; el diálogo del sistema sale
    /// encima de la pantalla de progreso y no la detiene (ADR 0070).
    /// </summary>
    private void PedirPermisoDeNotificaciones()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33)) return;
        var permiso = global::Android.Manifest.Permission.PostNotifications;
        if (contexto.CheckSelfPermission(permiso) == Permission.Granted) return;
        var actividad = PuenteActividad.Actual;
        actividad?.RunOnUiThread(() => actividad.RequestPermissions([permiso], 7900));
    }

    /// <summary>La notificación tal cual la describe <see cref="EstadoNotificacion"/>: sin texto propio de la plataforma.</summary>
    internal static Notification Construir(Context contexto, EstadoNotificacion e)
    {
        CrearCanal(contexto);
        var b = new Notification.Builder(contexto, OperacionLargaService.Canal)
            .SetContentTitle(e.Titulo)
            .SetContentText(e.Texto)
            .SetStyle(new Notification.BigTextStyle().BigText(e.TextoLargo))
            .SetSmallIcon(global::Android.Resource.Drawable.StatSysDownload)
            .SetOngoing(true)
            .SetOnlyAlertOnce(true)
            .SetCategory(Notification.CategoryProgress)
            .SetProgress(e.Maximo, e.Valor, e.Indeterminada);
        if (OperatingSystem.IsAndroidVersionAtLeast(31)) b.SetForegroundServiceBehavior((int)NotificationForegroundService.Immediate);   // API 31; el APK admite Android 10 (M2 de la auditoría)
        if (contexto.PackageManager?.GetLaunchIntentForPackage(contexto.PackageName!) is { } abrir)
            b.SetContentIntent(PendingIntent.GetActivity(contexto, 0, abrir.SetFlags(ActivityFlags.SingleTop), PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent));
        if (e.PuedeCancelar)
        {
            var cancelar = PendingIntent.GetService(contexto, 1, new Intent(contexto, typeof(OperacionLargaService)).SetAction(OperacionLargaService.AccionCancelar), PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
            b.AddAction(new Notification.Action.Builder(global::Android.Graphics.Drawables.Icon.CreateWithResource(contexto, global::Android.Resource.Drawable.IcMenuCloseClearCancel), EstadoNotificacion.TextoCancelar, cancelar).Build());
        }
        return b.Build();
    }
}
