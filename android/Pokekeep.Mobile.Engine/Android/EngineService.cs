// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using AutoLegalidad.Contracts;
using AutoLegalidad.Engine.Catalog;
using AutoLegalidad.Registro;
using AutoLegalidad.Worker;
using Pokekeep.Mobile.Engine.Ipc;

[assembly: UsesPermission(Name = "android.permission.FOREGROUND_SERVICE")]
[assembly: UsesPermission(Name = "android.permission.FOREGROUND_SERVICE_DATA_SYNC")]
[assembly: UsesPermission(Name = "android.permission.POST_NOTIFICATIONS")]

namespace Pokekeep.Mobile.Engine.Plataforma;

/// <summary>
/// El proceso <c>:motor</c> (plan 5.3): servicio sin <c>IsolatedProcess</c> (necesita <c>FilesDir</c>), no exportado, que hospeda <c>WorkerHost.Run</c> tal cual y habla con la interfaz por un socket de nombre
/// abstracto. Vive entre solicitudes (calienta Mono, el Core y el catalogo) y la interfaz lo recicla al cancelar o vencer un plazo. El vigilante duro del Worker (matar el proceso tras responder Timeout) es aqui
/// <c>Process.KillProcess</c>. Pasa a primer plano (<c>dataSync</c>, con notificacion) solo para «Completar el juego» y las operaciones masivas (F9): aqui queda el andamiaje y su prueba.
/// </summary>
[Service(Name = "com.incredibad.pokekeep.EngineService", Process = ":motor", Exported = false, ForegroundServiceType = ForegroundService.TypeDataSync)]
public class EngineService : Service
{
    public const string Etiqueta = "POKEKEEP-MOTOR";
    public const string ExtraSocket = "pk.socket";
    public const string ExtraGeneracion = "pk.generacion";
    public const string ExtraCatalogo = "pk.catalogo";
    public const string ExtraSoluciones = "pk.soluciones";
    public const string ExtraVersionCode = "pk.versioncode";
    public const string ExtraPruebas = "pk.pruebas";
    public const string AccionPrimerPlano = "com.incredibad.pokekeep.PRIMER_PLANO";
    public const string AccionSegundoPlano = "com.incredibad.pokekeep.SEGUNDO_PLANO";
    public const string ExtraTitulo = "pk.titulo";
    private const string Canal = "operaciones-largas";

    private readonly object _cerrojo = new();
    private string? _nombre;
    private CancellationTokenSource? _parar;
    private IAceptador? _aceptador;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == AccionPrimerPlano) { ElevarAPrimerPlano(intent.GetStringExtra(ExtraTitulo) ?? "Pokekeep está trabajando"); return StartCommandResult.NotSticky; }
        if (intent?.Action == AccionSegundoPlano) { StopForeground(StopForegroundFlags.Remove); return StartCommandResult.NotSticky; }
        var nombre = intent?.GetStringExtra(ExtraSocket);
        if (nombre is null) return StartCommandResult.NotSticky;   // reinicio del sistema sin intent: la interfaz volvera a pedirlo
        lock (_cerrojo)
        {
            if (_nombre == nombre) return StartCommandResult.NotSticky;
            Detener();
            _nombre = nombre;
            var generacion = intent!.GetLongExtra(ExtraGeneracion, 0);
            var catalogo = intent.GetStringExtra(ExtraCatalogo)!;
            var soluciones = intent.GetStringExtra(ExtraSoluciones);
            var versionCode = intent.GetIntExtra(ExtraVersionCode, 0);
            var pruebas = intent.GetStringExtra(ExtraPruebas);
            _parar = new CancellationTokenSource();
            var parar = _parar.Token;
            var hilo = new Thread(() => Servir(nombre, generacion, catalogo, soluciones, versionCode, pruebas, parar)) { IsBackground = true, Name = "pokekeep-motor" };
            hilo.Start();
        }
        return StartCommandResult.NotSticky;
    }

    private void Servir(string nombre, long generacion, string catalogo, string? soluciones, int versionCode, string? pruebas, CancellationToken parar)
    {
        try
        {
            ConfigurarProceso(pruebas);
            var aceptador = new AceptadorSocketAbstracto(nombre);
            lock (_cerrojo) _aceptador = aceptador;
            Log.Info(Etiqueta, $"motor listo: pid {Process.MyPid()}, socket {nombre}, generación {generacion}");
            string? Manifiesto()
            {
                using var cat = new CatalogReader(catalogo);
                return System.Text.Encoding.UTF8.GetString(Json.SerializeToUtf8(WorkerHost.BuildManifest(cat)));
            }
            var servidor = new MotorServidor(aceptador,
                (entrada, salida, registro, cancelar, paradaDura) => WorkerHost.Run(entrada, salida, catalogo, registro, soluciones, cancelar, paradaDura),
                Manifiesto,
                new Saludo(Process.MyPid(), versionCode, generacion, Protocol.Version, "Pokekeep.Motor"),
                codigo => { Log.Warn(Etiqueta, $"parada dura (código {codigo}): se mata el proceso del motor"); Process.KillProcess(Process.MyPid()); });
            servidor.Servir(parar);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Error(Etiqueta, "el motor terminó con error: " + ex);
            RegistroFallos.Error("El servidor del motor terminó con error.", ex, "EngineService.Servir");
        }
        finally { Log.Info(Etiqueta, "motor detenido"); }
    }

    /// <summary>Identidad embebida, cultura es-ES y registro de ESTE proceso (un fichero por proceso, sufijo «motor»). Las variables de prueba solo se aceptan en builds de prueba.</summary>
    private void ConfigurarProceso(string? pruebas)
    {
        EmbeddedBinaryIdentity.Instalar();
        var es = new CultureInfo("es-ES");
        CultureInfo.DefaultThreadCurrentCulture = es; CultureInfo.DefaultThreadCurrentUICulture = es; CultureInfo.CurrentCulture = es; CultureInfo.CurrentUICulture = es;
        RegistroAndroid.Configurar(this, "motor");
#if POKEKEEP_PRUEBAS
        if (!string.IsNullOrEmpty(pruebas))
            foreach (var par in pruebas.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var i = par.IndexOf('=');
                if (i > 0) System.Environment.SetEnvironmentVariable(par[..i], par[(i + 1)..]);   // POKEKEEP_TEST_DELAY_MS, POKEKEEP_TEST_THROW: solo existen en builds de prueba
            }
#endif
    }

    private void Detener()
    {
        try { _parar?.Cancel(); } catch { }
        try { _aceptador?.Dispose(); } catch { }
        _aceptador = null; _parar = null; _nombre = null;
    }

    public override void OnDestroy()
    {
        lock (_cerrojo) Detener();
        base.OnDestroy();
    }

    /// <summary>Andamiaje de «Completar el juego» y las masivas (F9): el servicio pasa a primer plano <c>dataSync</c> con una notificacion de baja importancia y el boton Cancelar llegara con F9.</summary>
    private void ElevarAPrimerPlano(string titulo)
    {
        var gestor = (NotificationManager)GetSystemService(NotificationService)!;
        if (gestor.GetNotificationChannel(Canal) is null)
            gestor.CreateNotificationChannel(new NotificationChannel(Canal, "Operaciones largas", NotificationImportance.Low) { Description = "Completar el juego y ediciones masivas en curso." });
        var notificacion = new Notification.Builder(this, Canal).SetContentTitle(titulo).SetSmallIcon(global::Android.Resource.Drawable.StatSysDownload).SetOngoing(true).Build();
        StartForeground(4021, notificacion, ForegroundService.TypeDataSync);
        Log.Info(Etiqueta, "servicio en primer plano (dataSync)");
    }
}

/// <summary>
/// Segundo y tercer proceso del pool de motores (F9X-01, ADR 0070, decisión 1): el MISMO servicio en otro proceso (<c>:motor2</c>, <c>:motor3</c>). Android solo crea un proceso por servicio declarado, así que cada
/// motor del pool es una clase con su atributo. Ningún producto los usa mientras la medida en emulador no diga que compensan (<c>LanzadorAndroid.Servicios</c>, <c>Arranque</c>: 1 motor).
/// </summary>
[Service(Name = "com.incredibad.pokekeep.EngineServiceMotor2", Process = ":motor2", Exported = false, ForegroundServiceType = ForegroundService.TypeDataSync)]
public sealed class EngineServiceMotor2 : EngineService;

[Service(Name = "com.incredibad.pokekeep.EngineServiceMotor3", Process = ":motor3", Exported = false, ForegroundServiceType = ForegroundService.TypeDataSync)]
public sealed class EngineServiceMotor3 : EngineService;
