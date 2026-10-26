// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Views;
using Android.OS;
using Avalonia.Android;
using Pokekeep.Mobile.Platform;
using Pokekeep.Mobile.Ui.Abrir;
using Pokekeep.Mobile.Ui.ViewModels;

namespace Pokekeep.Mobile;

/// <summary>
/// Actividad unica. «Abrir con Pokekeep» y «Compartir con Pokekeep» (bloque 10-2, F10-ABRIR-001/002): filtros de ACTION_VIEW y ACTION_SEND sobre <c>content://</c> con los tipos que dan los gestores de
/// archivos a una partida o un Pokemon (<c>application/octet-stream</c>, los .zip) y, para los proveedores que si ponen la extension en la ruta, las extensiones que lee PKHeX; (pathPattern no admite clases [1-9]: cada una va escrita; su «.*» no retrocede y se para en el PRIMER carácter que sigue, así que cada extensión va también con uno y dos puntos
/// delante para que casen «partida.bak.sav» o «main.2024.zip»; solo minúsculas; lo normal es que el archivo entre por su tipo MIME); lo que llega lo decide el Core
/// (el despachador de la Shell). <c>SingleTask</c>: con la app abierta, el archivo llega por <see cref="OnNewIntent"/> a la misma actividad.
/// Teclado: Teclado: <c>AdjustResize</c> (Android encoge la ventana y TopLevel.InputPane.OccludedRect mide ese mismo hueco): NO se suma otro relleno por el teclado (plan 8.4.4, ADR 0056).
/// </summary>
// X23: los proveedores pueden entregar una URI opaca (sin extensión) y MIME variable.
// SEND lleva el documento en EXTRA_STREAM: no se puede filtrar su ruta en el resolver.
// En ambos casos la aceptación final sigue siendo del Core, antes de abrir la partida.
[IntentFilter(new[] { Intent.ActionSend }, Categories = new[] { Intent.CategoryDefault }, DataMimeType = "*/*")]
[IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault }, DataScheme = "content", DataMimeType = "*/*")]
[IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault }, DataScheme = "file", DataMimeType = "*/*")]
// Lista de SaveFileMetadata.CustomSaveExtensions del Core; bin/raw/bak ya se admitían.
// Dos barras REALES en el XML: las cadenas verbatim las conservan; aapt consume
// una y PatternMatcher recibe el escape del punto literal (X30).
[IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault }, DataSchemes = new[] { "content", "file" }, DataHost = "*", DataMimeType = "*/*",
    DataPathPatterns = new[] { @".*\\.sav", @".*\\..*\\.sav", @".*\\..*\\..*\\.sav", @".*\\.dat", @".*\\.gci", @".*\\.dsv", @".*\\.srm", @".*\\.fla", @".*\\.SaveRAM", @".*/main" })]
[Activity(
    Label = "Pokekeep",
    Theme = "@style/Theme.Pokekeep",
    Icon = "@mipmap/ic_launcher",
    RoundIcon = "@mipmap/ic_launcher_round",
    MainLauncher = true,
    WindowSoftInputMode = SoftInput.AdjustResize,
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.SmallestScreenSize | ConfigChanges.ScreenLayout | ConfigChanges.UiMode | ConfigChanges.FontScale | ConfigChanges.Density | ConfigChanges.KeyboardHidden | ConfigChanges.Keyboard)]
[IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault }, DataScheme = "content", DataMimeTypes = new[] { "application/octet-stream", "application/zip", "application/x-zip-compressed", "application/x-binary" })]
[IntentFilter(new[] { Intent.ActionSend }, Categories = new[] { Intent.CategoryDefault }, DataMimeTypes = new[] { "application/octet-stream", "application/zip", "application/x-zip-compressed", "application/x-binary" })]
[IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault }, DataScheme = "content", DataHost = "*", DataMimeType = "*/*",
    DataPathPatterns = new[] {
        @".*\\.sav", @".*\\..*\\.sav", @".*\\..*\\..*\\.sav", @".*\\.dsv", @".*\\..*\\.dsv", @".*\\..*\\..*\\.dsv", @".*\\.gci", @".*\\..*\\.gci", @".*\\..*\\..*\\.gci", @".*\\.raw",
        @".*\\..*\\.raw", @".*\\..*\\..*\\.raw", @".*\\.bin", @".*\\..*\\.bin", @".*\\..*\\..*\\.bin", @".*\\.bak", @".*\\..*\\.bak", @".*\\..*\\..*\\.bak", @".*\\.zip", @".*\\..*\\.zip",
        @".*\\..*\\..*\\.zip", @".*\\.pk1", @".*\\..*\\.pk1", @".*\\..*\\..*\\.pk1", @".*\\.pk2", @".*\\..*\\.pk2", @".*\\..*\\..*\\.pk2", @".*\\.pk3", @".*\\..*\\.pk3", @".*\\..*\\..*\\.pk3",
        @".*\\.pk4", @".*\\..*\\.pk4", @".*\\..*\\..*\\.pk4", @".*\\.pk5", @".*\\..*\\.pk5", @".*\\..*\\..*\\.pk5", @".*\\.pk6", @".*\\..*\\.pk6", @".*\\..*\\..*\\.pk6", @".*\\.pk7",
        @".*\\..*\\.pk7", @".*\\..*\\..*\\.pk7", @".*\\.pk8", @".*\\..*\\.pk8", @".*\\..*\\..*\\.pk8", @".*\\.pk9", @".*\\..*\\.pk9", @".*\\..*\\..*\\.pk9", @".*\\.ek3", @".*\\..*\\.ek3",
        @".*\\..*\\..*\\.ek3", @".*\\.ek4", @".*\\..*\\.ek4", @".*\\..*\\..*\\.ek4", @".*\\.ek5", @".*\\..*\\.ek5", @".*\\..*\\..*\\.ek5", @".*\\.ek6", @".*\\..*\\.ek6", @".*\\..*\\..*\\.ek6",
        @".*\\.ek7", @".*\\..*\\.ek7", @".*\\..*\\..*\\.ek7", @".*\\.ek8", @".*\\..*\\.ek8", @".*\\..*\\..*\\.ek8", @".*\\.ek9", @".*\\..*\\.ek9", @".*\\..*\\..*\\.ek9", @".*\\.pb7",
        @".*\\..*\\.pb7", @".*\\..*\\..*\\.pb7", @".*\\.pb8", @".*\\..*\\.pb8", @".*\\..*\\..*\\.pb8", @".*\\.pa8", @".*\\..*\\.pa8", @".*\\..*\\..*\\.pa8", @".*\\.pa9", @".*\\..*\\.pa9",
        @".*\\..*\\..*\\.pa9", @".*\\.pkh", @".*\\..*\\.pkh", @".*\\..*\\..*\\.pkh", @".*\\.ck3", @".*\\..*\\.ck3", @".*\\..*\\..*\\.ck3", @".*\\.xk3", @".*\\..*\\.xk3", @".*\\..*\\..*\\.xk3",
        @".*\\.bk4", @".*\\..*\\.bk4", @".*\\..*\\..*\\.bk4", @".*\\.rk4", @".*\\..*\\.rk4", @".*\\..*\\..*\\.rk4", @".*\\.sk2", @".*\\..*\\.sk2", @".*\\..*\\..*\\.sk2", @".*\\.wc4",
        @".*\\..*\\.wc4", @".*\\..*\\..*\\.wc4", @".*\\.wc5", @".*\\..*\\.wc5", @".*\\..*\\..*\\.wc5", @".*\\.wc6", @".*\\..*\\.wc6", @".*\\..*\\..*\\.wc6", @".*\\.wc7", @".*\\..*\\.wc7",
        @".*\\..*\\..*\\.wc7", @".*\\.wc8", @".*\\..*\\.wc8", @".*\\..*\\..*\\.wc8", @".*\\.wc9", @".*\\..*\\.wc9", @".*\\..*\\..*\\.wc9", @".*\\.wc6full", @".*\\..*\\.wc6full",
        @".*\\..*\\..*\\.wc6full", @".*\\.wc7full", @".*\\..*\\.wc7full", @".*\\..*\\..*\\.wc7full", @".*\\.pgf", @".*\\..*\\.pgf", @".*\\..*\\..*\\.pgf", @".*\\.pcd", @".*\\..*\\.pcd",
        @".*\\..*\\..*\\.pcd", @".*\\.pgt", @".*\\..*\\.pgt", @".*\\..*\\..*\\.pgt", @".*\\.wa8", @".*\\..*\\.wa8", @".*\\..*\\..*\\.wa8", @".*\\.wa9", @".*\\..*\\.wa9", @".*\\..*\\..*\\.wa9",
        @".*\\.wb7", @".*\\..*\\.wb7", @".*\\..*\\..*\\.wb7", @".*\\.wb8", @".*\\..*\\.wb8", @".*\\..*\\..*\\.wb8", @".*\\.gp1", @".*\\..*\\.gp1", @".*\\..*\\..*\\.gp1" })]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
#if POKEKEEP_PRUEBAS
        Arranque.PantallaInicial = Intent?.GetStringExtra("pk_pantalla");
        Arranque.TemaInicial = Intent?.GetStringExtra("pk_tema");
        Arranque.RetrasoMotorMs = Intent?.GetStringExtra("pk_retraso_motor_ms");
        ShellViewModel.DiagnosticoEntrada = texto => global::Android.Util.Log.Info("POKEKEEP-ENTRADA", texto);
#endif
        PuenteActividad.Registrar(this);   // los selectores del sistema (abrir, exportar) se lanzan desde esta actividad
        base.OnCreate(savedInstanceState);
        if (savedInstanceState is null) RecibirArchivo(Intent);   // arranque en frio desde «Abrir con» o «Compartir» (el equivalente de los argumentos de PKHeX, PK-MENU-045)
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        RecibirArchivo(intent);   // con la app ya abierta (SingleTask)
    }

    protected override void OnPause()
    {
        Pokekeep.Mobile.Ui.Design.CicloInteraccion.Interrumpir();
        base.OnPause();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        if (!hasFocus) Pokekeep.Mobile.Ui.Design.CicloInteraccion.Interrumpir();
        base.OnWindowFocusChanged(hasFocus);
    }

    protected override void OnStop()
    {
        Pokekeep.Mobile.Ui.ViewModels.ShellViewModel.PararGritoActivo();
        base.OnStop();
    }

    protected override void OnResume()
    {
        base.OnResume();
        ShellViewModel.RevalidarRecientesEnPrimerPlano();
    }

    /// <summary>Deja en el buzón de la interfaz el documento que trae el Intent; la Shell lo abre cuando esta lista (y si ya lo esta, al momento). Nada se lee aqui.</summary>
    private void RecibirArchivo(Intent? intent)
    {
        if (intent is null) return;
        global::Android.Net.Uri? uri;
        OrigenArchivo origen;
        if (intent.Action == Intent.ActionView) { uri = intent.Data; origen = OrigenArchivo.AbrirCon; }
        else if (intent.Action == Intent.ActionSend)
        {
#pragma warning disable CA1422, CS0618   // GetParcelableExtra(string) sigue siendo la via que funciona en todas las versiones que admite la app
            uri = intent.GetParcelableExtra(Intent.ExtraStream) as global::Android.Net.Uri;
#pragma warning restore CA1422, CS0618
            origen = OrigenArchivo.Compartir;
        }
        else return;
        if (uri is null || uri.Scheme != "content") return;   // solo content:// (sin permiso de almacenamiento, nunca rutas file://)
        var documento = new AlmacenDocumentosAndroid(this).DeIntent(uri, intent.Flags);
        Intent = new Intent(Intent.ActionMain);   // el mismo Intent no se vuelve a atender al recrear la actividad
        EntradasExternas.Recibir(new EntradaExterna(documento, origen));
    }

    public override void OnConfigurationChanged(global::Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        Arranque.AplicarPreferenciasDelSistema(this);   // el usuario cambio la escala de fuente con la app abierta
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (!PuenteActividad.Entregar(requestCode, resultCode, data)) base.OnActivityResult(requestCode, resultCode, data);
    }
}
