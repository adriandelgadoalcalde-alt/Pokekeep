// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Android.Util;
using Avalonia.Controls;
using Avalonia.Threading;
using PKHeX.Core;
using Pokekeep.Mobile.Engine.Plataforma;
using Pokekeep.Mobile.Engine.Ipc;
using Pokekeep.Mobile.Operaciones;
using Pokekeep.Mobile.Operaciones.OperacionLarga;
using Pokekeep.Mobile.Platform;
using Pokekeep.Mobile.Ui.Ajustes;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Design;
using Pokekeep.Mobile.Ui.Views;

namespace Pokekeep.Mobile;

/// <summary>Arranque: pantalla de marca con el progreso real de la copia de datos y, despues, la Shell (con la partida que el usuario abra, recupere o, en builds de diseno, la de demostracion).</summary>
internal static class Arranque
{
    public const string Etiqueta = "POKEKEEP-UI";

#if POKEKEEP_PRUEBAS
    /// <summary>Solo builds de prueba: <c>am start ... --es pk_pantalla editor-teclado</c> lleva la app a una pantalla sin tocar (capturas y medidas del emulador sin depender de coordenadas).</summary>
    public static string? PantallaInicial { get; set; }
    public static string? TemaInicial { get; set; }
    /// <summary>Solo builds de prueba: <c>--es pk_retraso_motor_ms 20000</c> hace que el motor tarde ese tiempo (variable POKEKEEP_TEST_DELAY_MS del proceso :motor) para poder ver y pulsar «Cancelar» en el arbol de UI.</summary>
    public static string? RetrasoMotorMs { get; set; }
#endif

    private static string? PruebasMotor()
    {
#if POKEKEEP_PRUEBAS
        return int.TryParse(RetrasoMotorMs, out var ms) && ms > 0 ? "POKEKEEP_TEST_DELAY_MS=" + ms : null;
#else
        return null;
#endif
    }

    /// <summary>Paquetes de sprites de PKHeX empaquetados como assets (<c>sprites/manifest.json</c> y un <c>.pkpack</c> por familia, sin comprimir). Cada paquete se lee entero la primera vez que se pide un recurso suyo.</summary>
    private static void ConfigurarSprites(global::Android.Content.Context contexto)
    {
        LogosDeJuego.Actual = null;
        try
        {
            byte[] LeerLogo(string nombre)
            {
                using var s = contexto.Assets!.Open(LogosDeJuego.Carpeta + "/" + nombre, global::Android.Content.Res.Access.Streaming);
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                return ms.ToArray();
            }
            LogosDeJuego.Actual = new LogosDeJuego(LeerLogo(LogosDeJuego.Manifiesto), LeerLogo);
        }
        catch (Exception ex) when (ex is Java.IO.IOException or Java.Lang.Exception or IOException or System.Text.Json.JsonException)
        { Log.Warn(Etiqueta, "Logos no cargados; se conserva el icono de partida / Game icons unavailable: " + ex.GetType().Name); }
        byte[] Leer(string nombre)
        {
            using var s = contexto.Assets!.Open("sprites/" + nombre, global::Android.Content.Res.Access.Streaming);
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }
        Pokekeep.Mobile.Ui.Design.Sprites.AvisoRecurso += n => Log.Warn(Etiqueta, "AVISO sprite ausente: " + n);
        // Decisión posterior del propietario: las imágenes del producto son las que distribuye PKHeX.
        // X14: MT/MO/DR por tipo y objetos que PKHeX no dibuja (PokeAPI/sprites, revisión fijada). Sin el paquete (APK compilado sin él) la app sigue con solo los de PKHeX.
        byte[]? objetosJson = null;
        byte[] LeerObjetos(string nombre)
        {
            using var s = contexto.Assets!.Open("objetos/" + nombre, global::Android.Content.Res.Access.Streaming);
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }
        try { objetosJson = LeerObjetos("manifest.json"); }
        catch (Exception ex) when (ex is Java.IO.IOException or Java.Lang.Exception or IOException) { Log.Warn(Etiqueta, "AVISO sin paquete de objetos adicionales: " + ex.Message); }
        Pokekeep.Mobile.Ui.Design.Sprites.Configurar(Leer("manifest.json"), Leer, objetosJson: objetosJson, abrirPaqueteObjetos: LeerObjetos);
        // X51: sprites propios de cada juego (Gen. 1 a 5) para la vista previa grande; cada manifest*.json de la carpeta suma sus paquetes (X49: cajas). Sin ellos, la vista previa sigue con PKHeX.
        try
        {
            byte[] LeerJuegos(string nombre)
            {
                using var s = contexto.Assets!.Open(Pokekeep.Sprites.SpritesDeJuego.Carpeta + "/" + nombre, global::Android.Content.Res.Access.Streaming);
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                return ms.ToArray();
            }
            var manifiestos = (contexto.Assets!.List(Pokekeep.Sprites.SpritesDeJuego.Carpeta) ?? []).Where(n => n.StartsWith("manifest", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal)).Order(StringComparer.Ordinal).Select(LeerJuegos).ToList();
            if (manifiestos.Count > 0) Pokekeep.Mobile.Ui.Design.Sprites.ConfigurarJuegos(manifiestos, LeerJuegos);
            else Log.Warn(Etiqueta, "AVISO sin sprites propios de cada juego (APK compilado sin ellos)");
        }
        catch (Exception ex) when (ex is Java.IO.IOException or Java.Lang.Exception or IOException or InvalidDataException) { Log.Warn(Etiqueta, "AVISO sprites de juego no cargados: " + ex.GetType().Name); }
        try
        {
            byte[] LeerLogo(string nombre)
            {
                using var flujo = contexto.Assets!.Open(LogosDeJuego.Carpeta + "/" + nombre, global::Android.Content.Res.Access.Streaming);
                using var memoria = new MemoryStream(); flujo.CopyTo(memoria); return memoria.ToArray();
            }
            LogosDeJuego.Actual = new LogosDeJuego(LeerLogo(LogosDeJuego.Manifiesto), LeerLogo);
        }
        catch (Exception ex) when (ex is Java.IO.IOException or Java.Lang.Exception or IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            LogosDeJuego.Actual = null;
            Log.Warn(Etiqueta, "AVISO logos de juegos no disponibles: " + ex.GetType().Name);
        }
    }

    private sealed record ServiciosDelProceso(AutoLegalidad.Ui.Library.LibraryData DatosLibreria, CatalogoLibreriaDatos Catalogo, AlmacenAjustes Ajustes, AlmacenAjustesPkhex AjustesPkhex,
        AccionesPokekeep Acciones, SupervisorOperacionLarga Supervisor, MasivasSupervisadas Masivas);

    private static readonly UnoPorProceso<ServiciosDelProceso> _proceso = new();
    private static readonly RecuperacionOperacionLarga _recuperacion = new();
#if POKEKEEP_PRUEBAS
    internal static SupervisorOperacionLarga? SupervisorPruebas { get; private set; }
#endif

    /// <summary>Procesos de motor que trabajan a la vez: núcleos menos uno (para la interfaz), entre 1 y 3 (los servicios declarados); 1 en un móvil de poca memoria.</summary>
    internal static int MotoresDelDispositivo(global::Android.Content.Context contexto)
    {
        var poca = ((global::Android.App.ActivityManager?)contexto.GetSystemService(global::Android.Content.Context.ActivityService))?.IsLowRamDevice == true;
        return poca ? 1 : Math.Clamp(Environment.ProcessorCount - 1, 1, LanzadorAndroid.Servicios.Count);
    }

    private static ServiciosDelProceso CrearServiciosDelProceso(global::Android.Content.Context contexto, AlmacenDatos almacenDatos, string privada)
    {
        var datosLibreria = new AutoLegalidad.Ui.Library.LibraryData(File.Exists(almacenDatos.Ruta("autolegalidad.sqlite")) ? almacenDatos.Ruta("autolegalidad.sqlite") : null);
        var catalogo = new CatalogoLibreriaDatos(datosLibreria);
        var ajustes = new AlmacenAjustes(privada);   // ajustes.json en la carpeta privada: nada sale del dispositivo
        var ajustesPkhex = new AlmacenAjustesPkhex(privada);   // ajustes-pkhex.json: los de PKHeX (F10); al crearlo ya se aplican al Core, como al abrir PKHeX
        // La segunda opinion de legalidad y el analisis de casillas en segundo plano comparten UN cerrojo, siempre en el mismo orden (ADR 0060).
        AutoLegalidad.Ui.Operation.LegalityCheck.ExternalGate = Pokekeep.Sprites.LegalityGate.Cerrojo;
        // El motor (proceso :motor) arranca la primera vez que se pide una accion; la interfaz solo conoce el puerto IAccionesPokekeep.
        var versionCode = (int)contexto.PackageManager!.GetPackageInfo(contexto.PackageName!, 0)!.LongVersionCode;
        var soluciones = File.Exists(almacenDatos.Ruta("soluciones.sqlite")) ? almacenDatos.Ruta("soluciones.sqlite") : null;
        // F9X-01 (C5): pool de N procesos de motor. Medido en emulador (4 núcleos, API 34, lote de 18 con 6 búsquedas largas): 270,4 s con 1, 135,3 s con 2 (x2,00), 90,4 s con 3 (x2,99), 18/18 Success, PSS de los motores 157/341/527 MB,
        // térmico None. Gana de forma clara y cabe en memoria; la política de energía y temperatura baja a 1 con ahorro o calor y detiene con calor grave. Un núcleo se deja a la interfaz; un móvil de poca RAM se queda con 1.
        var motores = MotoresDelDispositivo(contexto);
        var motor = LanzadorAndroid.Pool(contexto, almacenDatos.Ruta("autolegalidad.sqlite"), soluciones, versionCode, motores, new OpcionesGateway { VersionCodeEsperado = versionCode }, linea => Log.Info(Etiqueta, "motor: " + linea), PruebasMotor());
        Log.Info(Etiqueta, $"motores del pool: {motores}");
        var acciones = new AccionesPokekeep(motor, ajustes, datosLibreria, () => motor.NuevaGeneracion());
        // Operaciones masivas: el mismo pool de motores y el mismo semaforo de «una operacion a la vez» (procesos del pool: ADR 0070, decision 1, medida de C5).
        // Alrededor, el supervisor del bloque 9c: servicio dataSync del proceso de la interfaz, notificacion con progreso y Cancelar duro, checkpoint por unidad y politica de energia.
        var supervisor = new SupervisorOperacionLarga(new PlataformaOperacionLargaAndroid(contexto), new RegistroOperacionLarga(privada), motor.MatarTodos);
#if POKEKEEP_PRUEBAS
        SupervisorPruebas = supervisor;
#endif
        var masivas = new MasivasSupervisadas(new MasivasPokekeep(acciones, motor, ajustes, datosLibreria, () => motores), supervisor);
        return new ServiciosDelProceso(datosLibreria, catalogo, ajustes, ajustesPkhex, acciones, supervisor, masivas);
    }

    public static Control CrearRaiz()
    {
        var relojInicio = System.Diagnostics.Stopwatch.StartNew();
        AplicarPreferenciasDelSistema(global::Android.App.Application.Context);
        AnalizadorLegalidad.Asincrono = true;   // los avisos de legalidad de la caja se rellenan en segundo plano: deslizar entre cajas no espera al analisis (120 Hz)
        var vm = new ArranqueViewModel();
        var host = new ContentControl { Content = new ArranqueView { DataContext = vm }, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch };
        var contexto = global::Android.App.Application.Context;
        _ = Task.Run(async () =>
        {
            try
            {
                ConfigurarSprites(contexto);
                var almacenDatos = new AlmacenDatos(contexto);
                var progreso = new Progress<(string Mensaje, double? Fraccion)>(p => Dispatcher.UIThread.Post(() => vm.Informar(p.Mensaje, p.Fraccion)));
                var copiados = await almacenDatos.PrepararAsync(progreso);
                Log.Info(Etiqueta, $"datos listos ({copiados} copiados) en {relojInicio.ElapsedMilliseconds} ms");
                var privada = contexto.FilesDir!.AbsolutePath;
                // UNO por proceso (revision del coordinador, 9c): Android recrea la actividad y esta raiz con el proceso vivo y, quiza, una operacion larga en marcha.
                // Pendiente de dispositivo/APK: salir y volver durante una edicion masiva no debe avisar «se interrumpio» ni crear un segundo supervisor/motor (la logica esta en UnoPorProceso y RecuperacionOperacionLarga, probada en el PC).
                var proceso = _proceso.ObtenerOCrear(() => CrearServiciosDelProceso(contexto, almacenDatos, privada));
                var datosLibreria = proceso.DatosLibreria; var catalogo = proceso.Catalogo; var ajustes = proceso.Ajustes; var ajustesPkhex = proceso.AjustesPkhex;
                var acciones = proceso.Acciones; var masivas = proceso.Masivas;
                var interrumpida = _recuperacion.TomarUnaVez(privada, proceso.Supervisor);
                if (interrumpida is not null) AutoLegalidad.Registro.RegistroFallos.Aviso("Operación larga interrumpida por Android: " + interrumpida.Detalle, null, "Arranque");
                AjustesVigentesPkhex.Fuente = ajustesPkhex;   // Ajustes del editor y privacidad, sobre la instancia única del proceso.
                var almacen = new AlmacenDocumentosAndroid(contexto);
                // Bloque 10-4: los recientes y la copia automática al cargar siguen los ajustes de PKHeX (Startup.RecentlyLoaded, Backup.BAKEnabled); el selector se abre en el guardado más reciente si se pide.
                var recientes = new RecientesStore(privada, ajustes: ajustesPkhex) { Avisar = (mensaje, ex) => Log.Warn(Etiqueta, mensaje + " " + ex.GetType().Name) };   // sin el mensaje de la excepción: podría llevar rutas del usuario
                var servicios = new ServiciosPartida(almacen, new GestorSesiones(almacen, privada), recientes, almacen, new CarpetaStore(privada));
                var carpetasPkhex = new Pokekeep.Mobile.Ui.Guardados.ModeloCarpetas(ajustesPkhex, almacen, almacen, servicios.Recientes);
                almacen.SugerirInicial = carpetasPkhex.SugerenciaAlAbrirAsync;
                var registros = new RegistrosAndroid(contexto);
                // Carpetas de trabajo de PKHeX (bloque 10-1): la copia de las carpetas del usuario elegidas en Ajustes se pone al día DESPUÉS de enseñar la Shell (abajo), sin hacer esperar al arranque.
                var carpetasTrabajo = new CarpetasTrabajoPkhex(ajustesPkhex, privada, () => almacen, new ExploradorCarpetasAndroid(contexto));
                var brillo = new BrilloAndroid();   // brillo al máximo mientras se enseña un código QR (bloque 10-9)
                var gritos = new GritosAndroid();
                var gritosIncluidos = GritosAndroid.CrearPaquete(contexto);   // X11: los gritos que trae el APK (null si se compiló sin ellos)
                Pokekeep.Mobile.Ui.Design.Haptica.Proveedor = new VibracionAndroid();   // X10: toque háptico al levantar un Pokémon
                var compartidor = new CompartirAndroid(contexto);   // «Compartir» por la hoja del sistema (bloque 10-9); limpia lo que quedó de envíos anteriores
                try { Pokekeep.Mobile.Ui.Cajas.ModeloCompartir.LimpiarCaducados(CompartirAndroid.RutaCarpeta(contexto)); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                var acercaDe = AcercaDeAndroid.Crear(contexto, almacenDatos);
                var demo = AbrirSesionDeDemostracion(contexto);
                ShellViewModel? actual = null;
                // Gancho de integración con el bloque 10-1 (PR #5): lo que 10-1 hace con la Shell que quede VISIBLE tras el arranque (`s.AlArrancar(inicio)`: aviso de HaX y novedades). Con «Cargar partida al iniciar» la
                // Shell se rehace al abrir la partida, y la nueva nunca habría recibido el aviso: por eso se llama sobre la Shell que queda visible DESPUÉS de la carga automática, no sobre la primera.
                Action<ShellViewModel>? alQuedarVisible = null;
                ShellViewModel Crear(Sesion? sesion, bool esDemo)
                {
                    var shell = new ShellViewModel(sesion, catalogo, cajaInicial: esDemo ? 8 : 0, seleccionarIlegal: esDemo, servicios, registros, acciones, masivas);
                    shell.SalirDeLaApp = () => PuenteActividad.Actividad?.RunOnUiThread(() => PuenteActividad.Actividad?.Finish());
                    shell.AlmacenDeAjustes = ajustes; shell.AjustesPkhex = ajustesPkhex; shell.ProveedorAcercaDe = acercaDe; shell.Compartidor = compartidor; shell.Brillo = brillo;
                    actual?.PararGrito();
                    shell.ReproductorGritos = gritos; shell.GritosIncluidos = gritosIncluidos;
                    shell.Activar();
                    shell.SesionNueva += s => Dispatcher.UIThread.Post(() => host.Content = new ShellView { DataContext = Crear(s, false) });
                    actual = shell;
                    // F10-2: lo que llego por «Abrir con» o «Compartir» antes de estar lista (o mientras se abria otra partida) lo atiende la Shell nueva.
                    Dispatcher.UIThread.Post(() => _ = shell.AtenderEntradasExternasAsync(), DispatcherPriority.Background);
                    return shell;
                }
                Pokekeep.Mobile.Ui.Abrir.EntradasExternas.Llegada += () => Dispatcher.UIThread.Post(() => _ = actual?.AtenderEntradasExternasAsync());

                // Arranque de PKHeX (bloque 10-1): versión ejecutada, registro de cambios, modo HaX y, la primera vez, el modo oscuro del sistema. En el hilo de la interfaz: cambia el tema.
                var version = ArranquePkhex.VersionDelPrograma(AutoLegalidad.Ui.AcercaDe.AcercaDeModelo.VersionEnsamblado());   // la de PokekeepVersion.props, la misma que enseña «Acerca de»
                var oscuro = (contexto.Resources!.Configuration!.UiMode & global::Android.Content.Res.UiMode.NightMask) == global::Android.Content.Res.UiMode.NightYes;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var inicio = ArranquePkhex.Iniciar(ajustesPkhex, version, oscuro, carpetasTrabajo);
                    var vista = new ShellView { DataContext = Crear(demo, demo is not null) };
                    host.Content = vista;
                    alQuedarVisible = visible =>
                    {
                        visible.AlArrancar(inicio);
                        if (interrumpida is not null) visible.Informar(interrumpida.Aviso, "aviso", "CircleAlert");
                    };
                    Log.Info(Etiqueta, $"shell lista en {relojInicio.ElapsedMilliseconds} ms");
                    // Ya con la Shell en pantalla: copia de las carpetas de trabajo al día en segundo plano; si algo cambió, el Core relee (p. ej. los regalos) desde el hilo de la interfaz.
                    _ = Task.Run(async () =>
                    {
                        try { if (await carpetasTrabajo.SincronizarTodasAsync()) Dispatcher.UIThread.Post(() => ajustesPkhex.Guardar()); }
                        catch (Exception ex) { Log.Warn(Etiqueta, "carpetas de trabajo: " + ex.GetType().Name); }   // sin el mensaje: podría llevar el nombre de una carpeta del usuario
                    });
                    // Bloque 10-4, «Cargar partida automáticamente al iniciar» (Startup.AutoLoadSaveOnStartup): DESPUÉS de enseñar la Shell (leer y reconocer los guardados de las carpetas SAF puede tardar). Sin partida de
                    // demostración y sin un archivo pendiente de «Abrir con Pokekeep» (como StartupArguments.ReadSettings), la copia más reciente o la última partida abierta; no pisa lo que el usuario haya abierto mientras tanto.
                    var primera = actual;
                    _ = Task.Run(async () =>
                    {
                        Pokekeep.Mobile.Ui.Guardados.GuardadoListado? alIniciar = null;
                        if (demo is null && Pokekeep.Mobile.Ui.Abrir.EntradasExternas.Pendientes == 0)
                        {
                            try { alIniciar = await carpetasPkhex.AlArrancarAsync(); }
                            catch (Exception ex) when (ex is not OutOfMemoryException) { Log.Warn(Etiqueta, "carga automática al iniciar: " + ex.GetType().Name); }   // nunca un bucle de caídas en cada arranque
                        }
                        try
                        {
                            await Dispatcher.UIThread.InvokeAsync(async () =>
                            {
                                if (alIniciar is not null && ReferenceEquals(actual, primera) && actual is { Ocupado: false } inicial && Pokekeep.Mobile.Ui.Abrir.EntradasExternas.Pendientes == 0)
                                    await inicial.AbrirGuardadoListadoAsync(alIniciar);
                            });
                            await Dispatcher.UIThread.InvokeAsync(() => { if (actual is { } visible) alQuedarVisible?.Invoke(visible); });   // la Shell que queda en pantalla, sea la primera o la de la partida cargada
                        }
                        catch (Exception ex) when (ex is not OutOfMemoryException) { Log.Warn(Etiqueta, "carga automática al iniciar (abrir): " + ex.GetType().Name); }
                    });
#if POKEKEEP_PRUEBAS
                    KeyboardAwareLayout.Medicion += linea => Log.Info("POKEKEEP-IME", linea);
                    if (TemaInicial is { } tema) Pokekeep.Mobile.Ui.PokekeepApp.FijarTema(tema == "oscuro" ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light);
                    if (PantallaInicial is { } pantalla && vista.DataContext is ShellViewModel vm)
                    {
                        // «editor-teclado» (F2-F4) enfoca el campo Apodo (hoy Mote); «editor:...:foco=<nombre accesible>» enfoca ese campo.
                        var foco = pantalla == "editor-teclado" ? "Mote" : pantalla.Split(':').FirstOrDefault(p => p.StartsWith("foco=", StringComparison.Ordinal)) is { } f ? f[5..].Replace('_', ' ').Replace('~', ',') : null;   // en la linea de adb no caben espacios ni comas: «Ataque~_IV» = «Ataque, IV»
                        var ok = vm.Navegar(pantalla == "editor-teclado" ? "editor" : pantalla);
                        Log.Info(Etiqueta, $"pantalla inicial «{pantalla}»: {(ok ? "OK" : "NO SE PUDO")}");
                        if (foco is not null) Dispatcher.UIThread.Post(() => Log.Info(Etiqueta, $"foco en {foco}: {vista.EnfocarCampo(foco)}"), Avalonia.Threading.DispatcherPriority.Loaded);
                    }
#endif
                });
            }
            catch (Exception ex)
            {
                Log.Error(Etiqueta, ex.ToString());
                Dispatcher.UIThread.Post(() => vm.Error = "No se pudo preparar la app. " + ex.GetType().Name);
            }
        });
        return host;
    }

    /// <summary>«Reducir movimiento» (escala de animaciones a 0 en Ajustes del sistema) y la escala de fuente del usuario: la interfaz las respeta sin que haya que tocar nada en Pokekeep.</summary>
    public static void AplicarPreferenciasDelSistema(global::Android.Content.Context contexto)
    {
        // Android 15+ con la app a pantalla completa ignora adjustResize (medido en API 36): el teclado se superpone y el layout rellena; antes encoge la ventana.
        KeyboardAwareLayout.SuponeVentanaRedimensionada = (int)global::Android.OS.Build.VERSION.SdkInt < 35;
        var animaciones = global::Android.Provider.Settings.Global.GetFloat(contexto.ContentResolver, global::Android.Provider.Settings.Global.AnimatorDurationScale, 1f);
        var fuente = contexto.Resources?.Configuration?.FontScale ?? 1f;
        Dispatcher.UIThread.Post(() => Pokekeep.Mobile.Ui.Design.Movimiento.Aplicar(animaciones <= 0f, fuente));
        Log.Info(Etiqueta, $"preferencias del sistema: animaciones x{animaciones}, fuente x{fuente}");
    }

    /// <summary>Solo builds de comprobacion de diseno (PokekeepDemo=true): una COPIA de un guardado de la coleccion publica RoC incluida como asset. Sin el asset, la app arranca sin partida.</summary>
    private static Sesion? AbrirSesionDeDemostracion(global::Android.Content.Context contexto)
    {
        try
        {
            const string nombre = "Pokemon Platinum.sav";
            using var s = contexto.Assets!.Open("demo/" + nombre);
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            var sav = SaveUtil.GetSaveFile(ms.ToArray(), nombre);
            return sav is null ? null : new Sesion(sav, nombre);
        }
        catch (Java.IO.FileNotFoundException) { return null; }
    }
}
