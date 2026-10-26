// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Operaciones.OperacionLarga;

/// <summary>Una operación larga que no llegó a cerrarse (Android mató la app a mitad): lo que se sabe de ella para decírselo al usuario al volver.</summary>
public sealed record OperacionInterrumpida(string Id, string Tipo, string Ambito, int Hechos, int Total, DateTimeOffset Inicio, DateTimeOffset? UltimaUnidad)
{
    /// <summary>El aviso al reabrir (ADR 0070, decisión 4). La transacción se aplica ENTERA al final y en memoria: si la app murió antes, no se aplicó nada.</summary>
    public string Aviso => IdiomaInterfaz.T("RegistroOperacionLarga.LaOperacionMasivaSe");

    /// <summary>Detalle para el registro de fallos: tipo, ámbito y unidades hechas (nunca datos de la partida).</summary>
    public string Detalle => IdiomaInterfaz.F("RegistroOperacionLarga.DeHechos", Tipo, Ambito, Hechos, Total);
}

/// <summary>
/// Checkpoint por unidad de una operación larga (F9X-05/06, ADR 0070, decisión 4): un fichero pequeño en la carpeta PRIVADA de la app que se crea al empezar (escritura atómica: temporal y renombrar),
/// recibe una línea por unidad hecha y se borra al terminar, fallar o cancelar. Si al abrir la app existe, la operación se interrumpió: <see cref="LeerInterrumpida"/> lo dice y lo borra.
/// <para>Nunca lleva datos de la partida (ni bytes, ni nombres de entrenador, ni identificadores): solo un id aleatorio, el tipo, el nombre del ámbito, el total y las cuentas.</para>
/// </summary>
public sealed class RegistroOperacionLarga
{
    public const string Fichero = "operacion-larga.txt";
    private const string Cabecera = "pokekeep-operacion-larga 1";

    private readonly string _ruta;
    private readonly object _cerrojo = new();
    private int _hechos;
    private bool _abierto;

    public RegistroOperacionLarga(string carpetaPrivada)
    {
        CarpetaPrivada = carpetaPrivada;
        _ruta = Path.Combine(carpetaPrivada, Fichero);
    }

    public string CarpetaPrivada { get; }
    public string Ruta => _ruta;
    public string? Id { get; private set; }
    public bool Abierto => _abierto;

    private static string Limpio(string s) => s.Replace('\n', ' ').Replace('\r', ' ').Replace(';', ',');

    /// <summary>Empieza una operación: crea el fichero de forma atómica con su cabecera. Si quedaba uno de otra operación (no debería: se lee al abrir), se sustituye.</summary>
    public void Empezar(string tipo, string ambito, int total, DateTimeOffset? ahora = null)
    {
        lock (_cerrojo)
        {
            Directory.CreateDirectory(CarpetaPrivada);
            Id = Guid.NewGuid().ToString("N");
            var inicio = (ahora ?? DateTimeOffset.UtcNow).ToString("O", CultureInfo.InvariantCulture);
            var tmp = _ruta + ".tmp";
            File.WriteAllText(tmp, $"{Cabecera}\nid;{Id}\ntipo;{Limpio(tipo)}\nambito;{Limpio(ambito)}\ntotal;{total}\ninicio;{inicio}\n");
            File.Move(tmp, _ruta, overwrite: true);
            _hechos = 0;
            _abierto = true;
        }
    }

    /// <summary>Anota que ya hay <paramref name="hechos"/> unidades hechas (una línea por avance; si no avanzó, no escribe).</summary>
    public void Unidades(int hechos, DateTimeOffset? ahora = null)
    {
        lock (_cerrojo)
        {
            if (!_abierto || hechos <= _hechos) return;
            _hechos = hechos;
            using var f = new FileStream(_ruta, FileMode.Append, FileAccess.Write, FileShare.Read);
            var linea = System.Text.Encoding.UTF8.GetBytes($"hecho;{hechos};{(ahora ?? DateTimeOffset.UtcNow).ToString("O", CultureInfo.InvariantCulture)}\n");
            f.Write(linea);
            f.Flush(flushToDisk: true);
        }
    }

    /// <summary>La operación terminó (con el resultado que sea): se borra el registro.</summary>
    public void Cerrar()
    {
        lock (_cerrojo)
        {
            _abierto = false;
            try { File.Delete(_ruta); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Al abrir la app: si quedó un registro sin cerrar, devuelve qué se interrumpió y lo borra (el aviso se da una sola vez). Un registro ilegible también se borra y se informa sin cifras.</summary>
    public static OperacionInterrumpida? LeerInterrumpida(string carpetaPrivada)
    {
        var ruta = Path.Combine(carpetaPrivada, Fichero);
        try { File.Delete(ruta + ".tmp"); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        if (!File.Exists(ruta)) return null;
        string[] lineas;
        try { lineas = File.ReadAllLines(ruta); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { lineas = []; }
        try { File.Delete(ruta); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        string? Valor(string clave) => lineas.Select(l => l.Split(';')).FirstOrDefault(p => p.Length >= 2 && p[0] == clave)?[1];
        var hechos = lineas.Select(l => l.Split(';')).Where(p => p.Length >= 2 && p[0] == "hecho").Select(p => int.TryParse(p[1], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        DateTimeOffset? Fecha(string? s) => DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d : null;
        var ultima = lineas.Select(l => l.Split(';')).LastOrDefault(p => p.Length >= 3 && p[0] == "hecho") is { } u ? Fecha(u[2]) : null;
        return new OperacionInterrumpida(Valor("id") ?? "?", Valor("tipo") ?? IdiomaInterfaz.T("RegistroOperacionLarga.OperacionMasiva"), Valor("ambito") ?? "?", hechos, int.TryParse(Valor("total"), out var t) ? t : 0,
            Fecha(Valor("inicio")) ?? DateTimeOffset.MinValue, ultima);
    }
}
