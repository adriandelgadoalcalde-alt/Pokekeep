// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using System.Text.Json.Serialization;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Ajustes;

/// <summary>Quien guarda los ajustes de Pokekeep: la pantalla de Ajustes los edita y el motor, el coordinador y la Librería los leen en cada operación (nunca una copia vieja).</summary>
public interface IAjustesPokekeep
{
    /// <summary>Los ajustes vigentes (el mismo objeto mientras no se carguen de nuevo).</summary>
    AjustesPokekeep Actual { get; }
    /// <summary>Se dispara tras guardar un cambio (la Librería se recarga sola si cambió el ajuste de HOME).</summary>
    event Action? Cambiado;
    /// <summary>Escribe los ajustes actuales en el almacén. Devuelve false si no se pudo (y los deja vigentes igualmente en memoria).</summary>
    bool Guardar();
    /// <summary>Texto si el fichero de ajustes estaba dañado y se volvió a los valores por defecto (null si todo bien).</summary>
    string? Aviso { get; }
}

/// <summary>Ajustes en memoria (pruebas y capturas sin almacén).</summary>
public sealed class AjustesEnMemoria(AjustesPokekeep? inicial = null) : IAjustesPokekeep
{
    public AjustesPokekeep Actual { get; } = inicial ?? new AjustesPokekeep();
    public event Action? Cambiado;
    public bool Guardar() { Cambiado?.Invoke(); return true; }
    public string? Aviso => null;
}

/// <summary>
/// Ajustes de Pokekeep en <c>ajustes.json</c> dentro de la carpeta privada de la app (nunca en una carpeta compartida: nada sale del dispositivo). Escritura atómica (fichero temporal + reemplazo);
/// un fichero dañado se sustituye por los valores por defecto y se avisa; un valor fuera de rango se recorta como en el escritorio (los plazos entre 10 y 3600 s, los procesos entre 1 y 8).
/// </summary>
public sealed class AlmacenAjustes : IAjustesPokekeep
{
    public const string NombreFichero = "ajustes.json";
    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _ruta;

    public AlmacenAjustes(string carpeta)
    {
        Directory.CreateDirectory(carpeta);
        _ruta = Path.Combine(carpeta, NombreFichero);
        Actual = Cargar(out var aviso);
        Aviso = aviso;
    }

    public AjustesPokekeep Actual { get; private set; }
    public string? Aviso { get; private set; }
    public event Action? Cambiado;
    public string Ruta => _ruta;

    private AjustesPokekeep Cargar(out string? aviso)
    {
        aviso = null;
        if (!File.Exists(_ruta)) return new AjustesPokekeep();
        try
        {
            return JsonSerializer.Deserialize<AjustesPokekeep>(File.ReadAllText(_ruta), Opciones) ?? new AjustesPokekeep();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            aviso = IdiomaInterfaz.T("AlmacenAjustes.ArchivoAjustesPokekeepEstaba");
            return new AjustesPokekeep();
        }
    }

    public bool Guardar()
    {
        try
        {
            var temporal = _ruta + ".tmp";
            File.WriteAllText(temporal, JsonSerializer.Serialize(Actual, Opciones), new System.Text.UTF8Encoding(false));
            File.Move(temporal, _ruta, overwrite: true);
            Cambiado?.Invoke();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Cambiado?.Invoke();
            return false;
        }
    }
}
