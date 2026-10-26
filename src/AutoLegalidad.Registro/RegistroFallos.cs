// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using System.Text;

namespace AutoLegalidad.Registro;

public enum Nivel { Info, Aviso, Error }

/// <summary>
/// Registro de fallos de Pokekeep (ADR 0040). UN fichero por dia (<c>pokekeep-AAAA-MM-DD.log</c>) en <c>registros\</c> junto al ejecutable (portable) o, si ahi no se puede escribir,
/// en <c>%LOCALAPPDATA%\Pokekeep\registros</c> (y el propio registro lo dice). Cada entrada: fecha y hora, version de Pokekeep, hash del Core, proceso, hilo, nivel, mensaje y la excepcion
/// completa con su traza interna. Escritura segura entre procesos (Workers en paralelo) con un Mutex con nombre y ficheros abiertos con <c>FileShare.ReadWrite</c>; rotacion de 30 dias o 20 MB.
/// <b>Nunca lanza</b>: un fallo del propio registro se traga (contador <see cref="FalloInterno"/>) y la aplicacion sigue.
/// </summary>
public static class RegistroFallos
{
    public const int DiasConservados = 30;
    public const long BytesMaximos = 20L * 1024 * 1024;

    private static readonly object Cerrojo = new();
    private static volatile Escritor? _escritor;
    private static string _proceso = "Pokekeep";
    private static Func<string?>? _hashCore;
    private static RegistroRutas? _rutas;
    private static int _sesionAbierta;
    private static int _capturaInstalada;
    private static Fija? _fija;
    // Una misma excepcion que pasa por varios ganchos (p. ej. ThreadException y ErrorWindow) se anota entera UNA vez; las demas dicen que ya figura.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Exception, object> YaAnotadas = new();

    private sealed record Fija(string Carpeta, string Version, string Hash, DateTime? Ahora, string? Alternativa);

    /// <summary>Cuantas veces fallo el propio registro (diagnostico y pruebas).</summary>
    public static int FalloInterno => Escritor.Fallos;

    /// <summary>Carpeta en uso (o <c>null</c> si aun no se escribio nada o no hay ninguna escribible).</summary>
    public static string? Carpeta => _escritor?.Carpeta;

    /// <summary>Carpeta del registro (creandola si hace falta) para el menu «Abrir carpeta de registros»; <c>null</c> si no hay ninguna escribible.</summary>
    public static string? CarpetaParaAbrir()
    {
        try { return Obtener()?.Carpeta; }
        catch (Exception) { return null; }
    }

    /// <summary>
    /// Define el nombre del proceso en las entradas («PKHeX», «Worker»...), opcionalmente quien da el hash del Core y, desde Android F1, las <paramref name="rutas"/> (carpetas, versión, cerrojo y sufijo
    /// del fichero) que el programa anfitrión inyecta. Sin <paramref name="rutas"/> se usa <see cref="RegistroRutas.Escritorio"/> (junto al ejecutable, como siempre).
    /// </summary>
    public static void Configurar(string proceso, Func<string?>? hashCore = null, RegistroRutas? rutas = null)
    {
        _proceso = proceso;
        if (hashCore is not null) _hashCore = hashCore;
        if (rutas is not null) _rutas = rutas;
    }

    /// <summary>Solo para pruebas: carpeta de registro fija y reinicio de todo el estado.</summary>
    internal static void Reiniciar(string? carpetaFija, string proceso = "Prueba", string version = "0.0-prueba", string hashCore = "00000000", DateTime? ahora = null, string? carpetaAlternativa = null)
    {
        lock (Cerrojo)
        {
            _escritor?.Dispose();
            _escritor = null;
            _sesionAbierta = 0;
            _proceso = proceso;
            _rutas = null;
            Escritor.Fallos = 0;
            _fija = carpetaFija is null ? null : new Fija(carpetaFija, version, hashCore, ahora, carpetaAlternativa);
        }
    }

    private static Escritor? Obtener()
    {
        var e = _escritor;
        if (e is not null) return e;
        lock (Cerrojo)
        {
            if (_escritor is not null) return _escritor;
            try
            {
                if (_fija is { } f)
                    _escritor = Escritor.Crear(f.Carpeta, f.Alternativa, f.Version, () => f.Hash, f.Ahora, null, string.Empty);
                else
                {
                    var r = _rutas ?? RegistroRutas.Escritorio(_hashCore);
                    _escritor = Escritor.Crear(r.CarpetaPreferida, r.CarpetaAlternativa, r.Version, _hashCore ?? r.HashCore, null, r.FabricaLock, r.SufijoFichero);
                }
            }
            catch (Exception) { Escritor.Fallos++; }
            return _escritor;
        }
    }

    public static void Info(string mensaje, string? contexto = null) => Escribir(Nivel.Info, mensaje, null, contexto);
    public static void Aviso(string mensaje, Exception? ex = null, string? contexto = null) => Escribir(Nivel.Aviso, mensaje, ex, contexto);
    public static void Error(string mensaje, Exception? ex = null, string? contexto = null) => Escribir(Nivel.Error, mensaje, ex, contexto);

    public static void Escribir(Nivel nivel, string mensaje, Exception? ex = null, string? contexto = null)
    {
        try
        {
            var e = Obtener();
            if (e is null) return;
            if (ex is not null)
            {
                if (YaAnotadas.TryGetValue(ex, out _)) { mensaje += " [la excepción ya figura completa en una entrada anterior]"; ex = null; }
                else YaAnotadas.Add(ex, string.Empty);
            }
            e.Escribir(Formatear(nivel, mensaje, ex, contexto, e));
        }
        catch (Exception) { Escritor.Fallos++; }
    }

    /// <summary>Aviso de INICIO de sesion (una vez por proceso) y, al terminar el proceso ordenadamente, el de CIERRE: un inicio sin su cierre delata un cierre abrupto.</summary>
    public static void IniciarSesion(string detalle = "")
    {
        if (Interlocked.Exchange(ref _sesionAbierta, 1) == 1) return;
        Info("INICIO de sesión" + (detalle.Length > 0 ? " · " + detalle : ""));
        try { AppDomain.CurrentDomain.ProcessExit += (_, _) => CerrarSesion(); }
        catch (Exception) { Escritor.Fallos++; }
    }

    public static void CerrarSesion()
    {
        if (Interlocked.Exchange(ref _sesionAbierta, 0) == 1) Info("CIERRE ordenado de la sesión");
    }

    /// <summary>Engancha las excepciones no controladas de cualquier hilo y las no observadas de las tareas. (<c>Application.ThreadException</c> lo engancha WinForms, ver el parche 0101.)</summary>
    public static void InstalarCapturaGlobal()
    {
        if (Interlocked.Exchange(ref _capturaInstalada, 1) == 1) return;
        try
        {
            AppDomain.CurrentDomain.UnhandledException += (_, a) =>
                Error(a.IsTerminating ? "Excepción no controlada (el proceso se cierra)" : "Excepción no controlada",
                    a.ExceptionObject as Exception ?? new InvalidOperationException(Convert.ToString(a.ExceptionObject, CultureInfo.InvariantCulture)), "AppDomain.UnhandledException");
            TaskScheduler.UnobservedTaskException += (_, a) =>
                Error("Excepción de una tarea que nadie observó", a.Exception, "TaskScheduler.UnobservedTaskException");
        }
        catch (Exception) { Escritor.Fallos++; }
    }

    internal static string Formatear(Nivel nivel, string mensaje, Exception? ex, string? contexto, Escritor e)
    {
        var sb = new StringBuilder(256);
        sb.Append(e.Ahora().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
          .Append(" | v").Append(e.Version)
          .Append(" | core=").Append(e.HashCore())
          .Append(" | ").Append(_proceso).Append(':').Append(Environment.ProcessId)
          .Append(" | hilo ").Append(Environment.CurrentManagedThreadId)
          .Append(" | ").Append(nivel switch { Nivel.Info => "INFO", Nivel.Aviso => "AVISO", _ => "ERROR" })
          .Append(" | ").Append(Saneador.Sanear(mensaje).ReplaceLineEndings(" // "));
        if (!string.IsNullOrEmpty(contexto)) sb.Append("\n    contexto: ").Append(Saneador.Sanear(contexto).ReplaceLineEndings("\n    "));
        if (ex is not null) sb.Append("\n    ").Append(Saneador.Sanear(ex.ToString()).ReplaceLineEndings("\n    "));
        if (e.NotaPendiente is { } nota) sb.Append("\n    nota: ").Append(nota);
        sb.Append('\n');
        return sb.ToString();
    }

    /// <summary>Quita lo que el registro conserva fuera de plazo: ficheros con mas de <paramref name="dias"/> dias y, si el total pasa de <paramref name="maxBytes"/>, los mas antiguos hasta quedar por debajo (el de hoy nunca).</summary>
    internal static void Rotar(string carpeta, DateTime hoy, long maxBytes = BytesMaximos, int dias = DiasConservados)
    {
        try
        {
            var ficheros = new DirectoryInfo(carpeta).GetFiles("pokekeep-*.log")
                .Select(f => (f, fecha: FechaDe(f.Name))).Where(x => x.fecha is not null)
                .Select(x => (x.f, fecha: x.fecha!.Value)).OrderBy(x => x.fecha).ToList();
            foreach (var x in ficheros.Where(x => x.fecha < hoy.Date.AddDays(-dias)).ToList())
                if (Borrar(x.f)) ficheros.Remove(x);
            var total = ficheros.Sum(x => x.f.Length);
            foreach (var x in ficheros.Where(x => x.fecha < hoy.Date).ToList())
            {
                if (total <= maxBytes) break;
                var len = x.f.Length;
                if (Borrar(x.f)) total -= len;
            }
        }
        catch (Exception) { Escritor.Fallos++; }
    }

    private static bool Borrar(FileInfo f)
    {
        try { f.Delete(); return true; }
        catch (Exception) { return false; }   // otro proceso lo tiene abierto: se intenta en la siguiente rotacion
    }

    /// <summary>Fecha de un fichero de registro: <c>pokekeep-AAAA-MM-DD.log</c> o, con sufijo de proceso (Android), <c>pokekeep-AAAA-MM-DD-motor.log</c>.</summary>
    internal static DateTime? FechaDe(string nombre)
    {
        if (nombre.Length < "pokekeep-yyyy-MM-dd.log".Length || !nombre.StartsWith("pokekeep-", StringComparison.Ordinal) || !nombre.EndsWith(".log", StringComparison.Ordinal)) return null;
        var resto = nombre.AsSpan(19, nombre.Length - 19 - 4);   // lo que hay entre la fecha y ".log"
        if (resto.Length > 0 && (resto[0] != '-' || resto.Length == 1)) return null;
        return DateTime.TryParseExact(nombre.AsSpan(9, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    /// <summary>Escritor de una carpeta concreta: escribe bajo Mutex y rota al cambiar de dia.</summary>
    internal sealed class Escritor : IDisposable
    {
        internal static int Fallos;
        private readonly Func<DateTime> _ahora;
        private readonly Func<string?> _hash;
        private readonly IRegistroLock? _lock;
        private readonly string _sufijo;
        private string? _hashCache;
        private DateTime _diaRotado = DateTime.MinValue;

        public string Carpeta { get; }
        public string Version { get; }
        /// <summary>Texto que se anade a la primera entrada escrita (p. ej. «no se pudo escribir junto al ejecutable»).</summary>
        public string? NotaPendiente { get; set; }

        private Escritor(string carpeta, string version, Func<string?> hash, Func<DateTime> ahora, string? nota, Func<string, IRegistroLock>? fabricaLock, string sufijo)
        {
            Carpeta = carpeta;
            Version = version;
            _hash = hash;
            _ahora = ahora;
            NotaPendiente = nota;
            _sufijo = string.IsNullOrEmpty(sufijo) ? string.Empty : "-" + sufijo;
            try { _lock = (fabricaLock ?? RegistroRutas.CerrojoDeEscritorio)(carpeta); }
            catch (Exception) { _lock = null; Fallos++; }
        }

        public DateTime Ahora() => _ahora();
        public string HashCore() => _hashCache ??= _hash() ?? "?";

        public static Escritor? Crear(string preferida, string? alternativa, string version, Func<string?> hash, DateTime? ahoraFijo, Func<string, IRegistroLock>? fabricaLock, string sufijo)
        {
            Func<DateTime> reloj = ahoraFijo is { } fijo ? () => fijo : () => DateTime.Now;
            if (SePuedeEscribir(preferida)) return new Escritor(preferida, version, hash, reloj, null, fabricaLock, sufijo);
            if (alternativa is not null && SePuedeEscribir(alternativa))
                return new Escritor(alternativa, version, hash, reloj, $"no se pudo escribir en «{Saneador.Sanear(preferida)}»; se usa esta carpeta alternativa", fabricaLock, sufijo);
            return null;
        }

        private static bool SePuedeEscribir(string carpeta)
        {
            try
            {
                Directory.CreateDirectory(carpeta);
                var prueba = Path.Combine(carpeta, $".escritura-{Environment.ProcessId}-{Guid.NewGuid():N}.tmp");
                using (File.Create(prueba, 1, FileOptions.DeleteOnClose)) { }
                return true;
            }
            catch (Exception) { return false; }
        }

        public void Escribir(string texto)
        {
            var ahora = _ahora();
            var bloqueado = false;
            try
            {
                bloqueado = _lock?.Adquirir(TimeSpan.FromSeconds(3)) ?? false;
                var ruta = Path.Combine(Carpeta, $"pokekeep-{ahora:yyyy-MM-dd}{_sufijo}.log");
                if (_diaRotado != ahora.Date) { _diaRotado = ahora.Date; Rotar(Carpeta, ahora); }
                var bytes = new UTF8Encoding(false).GetBytes(texto);
                using var fs = new FileStream(ruta, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.WriteThrough);
                fs.Write(bytes, 0, bytes.Length);
                NotaPendiente = null;
            }
            catch (Exception) { Fallos++; }
            finally
            {
                if (bloqueado) { try { _lock!.Liberar(); } catch (Exception) { } }
            }
        }

        public void Dispose() => _lock?.Dispose();
    }
}
