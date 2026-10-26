// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

/// <summary>JSON generado en compilación (sin reflexión en ejecución, apto para el recorte de Android): enumeraciones como texto y legible a mano, como el <c>cfg.json</c> de PKHeX.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AjustesPkhex))]
internal sealed partial class AjustesPkhexJson : JsonSerializerContext;

/// <summary>Quien guarda los ajustes de PKHeX: la pantalla los edita y el Core, las herramientas y la interfaz los leen en cada operación (nunca una copia vieja).</summary>
public interface IAjustesPkhex
{
    AjustesPkhex Actual { get; }
    /// <summary>Se dispara tras guardar un cambio, en el hilo de quien guardó. El Core se pone al día en segundo plano (<see cref="AplicadorPkhex.EsperarAplicacion"/> espera a que acabe).</summary>
    event Action? Cambiado;
    /// <summary>Escribe los ajustes en el almacén y los aplica al Core en segundo plano y en orden (nunca bloquea al hilo de interfaz esperando al cerrojo de legalidad). Devuelve false si no se pudo escribir (siguen vigentes en memoria).</summary>
    bool Guardar();
    /// <summary>
    /// Solo escribe en el almacén: ni toca el Core ni avisa a <see cref="Cambiado"/>. Para lo que el Core no lee (partidas recientes, versión ejecutada); PKHeX tampoco recarga nada al abrir una partida, solo persiste.
    /// Lo que el Core sí lee (rutas de copias, legalidad, conversión, regalos...) se guarda con <see cref="Guardar"/>. Devuelve false si no se pudo escribir.
    /// </summary>
    bool Persistir();
    /// <summary>Vuelve a los valores de fábrica de TODOS los ajustes (o solo los de un grupo de pantalla) y los guarda.</summary>
    void Restablecer(string? grupo = null);
    /// <summary>Texto si el archivo estaba dañado en todo o en parte y se usaron los valores por defecto de lo dañado (null si todo bien).</summary>
    string? Aviso { get; }
}

/// <summary>
/// Aplica los ajustes al Core con la MISMA llamada que PKHeX (<see cref="StartupUtil.ReloadSettings"/>: rutas de copias, idioma de guardados antiguos, escritura de ranuras, importación de sets, legalidad y
/// conversión). Es estado estático del Core (PKHeX igual): quien cambie ajustes en una prueba lo restaura al terminar (<c>FixtureAjustesPkhex</c>).
/// La segunda opinión de legalidad de Pokekeep NO hereda la severidad que el usuario cambie aquí (ADR 0053): <c>LegalityCheck</c> sigue usando los valores por defecto del Core.
/// </summary>
public static partial class AplicadorPkhex
{
    private static readonly Lock _cerrojo = new();
    private static readonly Lock _turno = new();
    private static Task _cola = Task.CompletedTask;

    /// <summary>Aplica ahora, en el hilo que llama (arranque y pruebas): espera al cerrojo de legalidad. Desde la interfaz se usa <see cref="AplicarEnSegundoPlano"/>.</summary>
    public static void Aplicar(AjustesPkhex a, string? carpetaLocal = null)
    {
        if (carpetaLocal is not null) a.LocalResources.SetLocalPath(carpetaLocal);
        AplicarYa(a);
    }

    /// <summary>
    /// Aplica una COPIA de los ajustes (para que la interfaz pueda seguir editando los suyos) en un hilo de segundo plano, en el mismo orden en que se pidió y sin que la interfaz espere al cerrojo de legalidad
    /// (un análisis largo no congela la pantalla). Orden de cerrojos (ADR 0077): <c>LegalityGate.Cerrojo</c> primero y después el propio; este método y <see cref="EnCola"/> son lo único que los toma desde una pantalla.
    /// </summary>
    public static Task AplicarEnSegundoPlano(AjustesPkhex a, string? carpetaLocal = null)
    {
        var copia = JsonSerializer.Deserialize(JsonSerializer.Serialize(a, AjustesPkhexJson.Default.AjustesPkhex), AjustesPkhexJson.Default.AjustesPkhex) ?? new AjustesPkhex();
        if (carpetaLocal is not null) copia.LocalResources.SetLocalPath(carpetaLocal);
        return EnCola(() => AplicarYa(copia));
    }

    /// <summary>Pone un trabajo detrás de todo lo que ya esté en cola (una sola cola, un solo hilo a la vez). Un fallo no corta la cola: queda en <see cref="UltimoError"/>.</summary>
    public static Task EnCola(Action trabajo)
    {
        lock (_turno)
        {
            _cola = _cola.ContinueWith(_ =>
            {
                try { trabajo(); }
                catch (Exception ex) when (ex is not OutOfMemoryException) { UltimoError = ex; }
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            return _cola;
        }
    }

    /// <summary>Se completa cuando todo lo pedido hasta ahora ya está aplicado al Core (pruebas, y quien necesite el Core al día).</summary>
    public static Task EsperarAplicacion() { lock (_turno) return _cola; }

    /// <summary>El último fallo de un trabajo en segundo plano (null si ninguno).</summary>
    public static Exception? UltimoError { get; private set; }

    private static void AplicarYa(AjustesPkhex a)
    {
        TextosNucleo.Preparar();
        // ReloadSettings muta ParseSettings y EncounterEvent: con el cerrojo de legalidad, para no cambiarlos bajo un análisis en segundo plano (ADR 0053).
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo)
        lock (_cerrojo)
        {
            // StartupUtil.ReloadSettings relee los regalos en una tarea suya FUERA de cualquier cerrojo. Aquí se hace la misma llamada con la carpeta de regalos apartada y los regalos se releen
            // con el mismo RefreshMGDB dentro del cerrojo: el resultado es el mismo, pero ningún análisis ve los regalos a medio cargar y quien espera la aplicación espera también a los regalos.
            var regalos = a.LocalResources.GetMGDatabasePath();
            var original = a.LocalResources.MGDatabasePath;
            a.LocalResources.MGDatabasePath = Path.Combine(Path.GetTempPath(), "pokekeep-sin-regalos-" + Guid.NewGuid().ToString("N"));
            try { StartupUtil.ReloadSettings(a); }
            finally { a.LocalResources.MGDatabasePath = original; }
            if (Directory.Exists(regalos)) EncounterEvent.RefreshMGDB(regalos);
            TrasRecargarB10_1(a);
            TrasRecargarB10_4(a);
            Modelo.HuellaLegalidad.Publicar(a.Legality);
        }
    }

    // Lo que un bloque debe poner al día en el Core justo después de StartupUtil.ReloadSettings, con los cerrojos tomados: cada bloque lo implementa en su propio fichero parcial
    // (AplicadorPkhex.B10_N.cs) sin tocar este, igual que ConsumidoresPkhex; sin implementación desaparece al compilar.
    static partial void TrasRecargarB10_1(AjustesPkhex a);
    static partial void TrasRecargarB10_4(AjustesPkhex a);
}

/// <summary>Ajustes de PKHeX en memoria (pruebas y capturas sin almacén). Aplica al Core igual que el almacén real, salvo que se pida lo contrario.</summary>
public sealed class AjustesPkhexEnMemoria(AjustesPkhex? inicial = null, bool aplicar = true) : IAjustesPkhex
{
    public AjustesPkhex Actual { get; private set; } = inicial ?? new AjustesPkhex();
    public event Action? Cambiado;
    public string? Aviso => null;
    public bool Guardar() { if (aplicar) AplicadorPkhex.Aplicar(Actual); Cambiado?.Invoke(); return true; }
    public bool Persistir() => true;
    public void Restablecer(string? grupo = null) { AlmacenAjustesPkhex.RestablecerEn(Actual, grupo, out var nuevo); Actual = nuevo; Guardar(); }
}

/// <summary>
/// Ajustes de PKHeX en <c>ajustes-pkhex.json</c> dentro de la carpeta privada de la app (nunca en una carpeta compartida: nada sale del dispositivo). Escritura atómica (temporal + reemplazo). Un archivo dañado
/// NO tira todos los ajustes: cada grupo se lee por separado y solo el dañado vuelve a sus valores por defecto, con aviso. Un valor fuera de rango se recorta como en el escritorio (los <c>set</c> del Core recortan).
/// </summary>
public sealed class AlmacenAjustesPkhex : IAjustesPkhex
{
    public const string NombreFichero = "ajustes-pkhex.json";
    private readonly string _carpeta;
    private readonly bool _aplicar;

    public AlmacenAjustesPkhex(string carpeta, bool aplicar = true)
    {
        _carpeta = carpeta; _aplicar = aplicar;
        Directory.CreateDirectory(carpeta);
        Ruta = Path.Combine(carpeta, NombreFichero);
        Actual = Cargar(Ruta, out var aviso);
        Aviso = aviso;
        if (_aplicar) AplicadorPkhex.Aplicar(Actual, _carpeta);
    }

    public string Ruta { get; }
    public AjustesPkhex Actual { get; private set; }
    public string? Aviso { get; private set; }
    public event Action? Cambiado;

    public static AjustesPkhex Cargar(string ruta, out string? aviso)
    {
        aviso = null;
        var a = new AjustesPkhex();
        if (!File.Exists(ruta))
        {
            a.Startup.Language = Localizacion.ServicioIdioma.DelSistema(CulturaApp.Sistema) == "en" ? "en" : "es";
            return a;
        }
        JsonObject? raiz;
        try { raiz = JsonNode.Parse(File.ReadAllText(ruta)) as JsonObject; }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            aviso = IdiomaInterfaz.T("AlmacenAjustesPkhex.ArchivoAjustesPKHeXEstaba");
            return a;
        }
        if (raiz is null) { aviso = IdiomaInterfaz.T("AlmacenAjustesPkhex.ArchivoAjustesPKHeXEstaba"); return a; }
        var malos = new List<string>();
        foreach (var p in typeof(AjustesPkhex).GetProperties().Where(p => p.CanWrite && p.GetIndexParameters().Length == 0))
        {
            var clave = raiz.FirstOrDefault(kv => string.Equals(kv.Key, p.Name, StringComparison.OrdinalIgnoreCase));
            if (clave.Value is null) continue;
            try
            {
                var info = AjustesPkhexJson.Default.GetTypeInfo(p.PropertyType) ?? throw new NotSupportedException();
                if (clave.Value.Deserialize(info) is { } valor) p.SetValue(a, valor);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException) { malos.Add(p.Name); }
        }
        if (malos.Count > 0) aviso = IdiomaInterfaz.F("AlmacenAjustesPkhex.AlgunosDanados", string.Join(", ", malos));
        return a;
    }

    public bool Guardar()
    {
        var ok = Escribir();
        if (_aplicar) _ = AplicadorPkhex.AplicarEnSegundoPlano(Actual, _carpeta);   // el Core se pone al día en segundo plano: la interfaz no espera al cerrojo de legalidad
        Cambiado?.Invoke();
        return ok;
    }

    public bool Persistir() => Escribir();

    private bool Escribir()
    {
        try
        {
            var temporal = Ruta + ".tmp";
            File.WriteAllText(temporal, JsonSerializer.Serialize(Actual, AjustesPkhexJson.Default.AjustesPkhex), new System.Text.UTF8Encoding(false));
            File.Move(temporal, Ruta, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Restablecer(string? grupo = null)
    {
        RestablecerEn(Actual, grupo, out var nuevo);
        Actual = nuevo;
        Guardar();
    }

    /// <summary>Restablece todo (devuelve un objeto nuevo) o solo las hojas de un grupo de pantalla (el mismo objeto con esas hojas a su valor de fábrica).</summary>
    internal static void RestablecerEn(AjustesPkhex actual, string? grupo, out AjustesPkhex resultado)
    {
        if (grupo is null) { resultado = new AjustesPkhex(); return; }
        var fabrica = new AjustesPkhex();
        foreach (var h in RegistroAjustesPkhex.Hojas.Where(h => h.Grupo == grupo)) h.Escribir(actual, h.Leer(fabrica));
        resultado = actual;
    }
}
