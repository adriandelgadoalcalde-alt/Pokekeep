// SPDX-License-Identifier: GPL-3.0-or-later
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Threading;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;

namespace Pokekeep.Mobile.Ui.Localizacion;

public sealed record OpcionIdioma(string Codigo, string Nombre);

/// <summary>Dos idiomas de interfaz; los idiomas de los Pokémon/guardados son datos distintos.</summary>
public sealed class ServicioIdioma : INotifyPropertyChanged
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Tablas = Cargar();
    private readonly Action<ServicioIdioma>? _alAplicar;
    private IAjustesPkhex? _almacen;
    private string _codigo = "es-ES";
    public ServicioIdioma(Action<ServicioIdioma>? alAplicar = null) => _alAplicar = alAplicar;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Cambiado;
    public string Codigo => _codigo;
    public string CodigoCore => Codigo == "en" ? "en" : "es";
    public CultureInfo Cultura => CultureInfo.GetCultureInfo(Codigo);
    public IReadOnlyList<OpcionIdioma> Opciones { get; } = [new("es-ES", Tablas["es-ES"]["Idioma.esES"]), new("en", Tablas["en"]["Idioma.en"])];
    public OpcionIdioma Seleccionado
    {
        get => Opciones.Single(o => o.Codigo == Codigo);
        set { if (value is not null) Cambiar(value.Codigo); }
    }
    public string this[string clave] => Tablas[Codigo].TryGetValue(clave, out var texto) ? texto : throw new KeyNotFoundException(clave);
    public string Formato(string clave, params object?[] valores) => string.Format(Cultura, this[clave], valores);
    /// <summary>Singular only for 1 in es-ES/en. The count is {0}; extra arguments start at {1}.</summary>
    public string Plural(string clave, long n, params object?[] argumentos)
    {
        var sufijo = n == 1 ? ".uno" : ".otro";
        var tabla = Tablas[Codigo];
        var texto = tabla.TryGetValue(clave + sufijo, out var encontrado) ? encontrado : this[clave + ".otro"];
        object?[] valores = [n, .. argumentos];
        return string.Format(Cultura, texto, valores);
    }
    public static IReadOnlyDictionary<string, string> Recursos(string codigo) => Tablas[codigo];
    public static string DelSistema(string? codigo) => codigo?.Split('-', '_')[0].Equals("en", StringComparison.OrdinalIgnoreCase) == true ? "en" : "es-ES";

    public void Vincular(IAjustesPkhex almacen)
    {
        if (!ReferenceEquals(_almacen, almacen))
        {
            if (_almacen is not null) _almacen.Cambiado -= Sincronizar;
            _almacen = almacen; _almacen.Cambiado += Sincronizar;
        }
        Sincronizar();
    }

    public bool Cambiar(string codigo)
    {
        if (codigo is not ("es-ES" or "en")) throw new ArgumentOutOfRangeException(nameof(codigo));
        if (_almacen is null) throw new InvalidOperationException("Idioma sin almacén");
        if (Codigo == codigo && _almacen.Actual.Startup.Language == CodigoCore) return true;
        _almacen.Actual.Startup.Language = codigo == "en" ? "en" : "es";
        var guardado = _almacen.Guardar();
        Sincronizar();
        return guardado;
    }

    private void Sincronizar()
    {
        var codigo = DelSistema(_almacen?.Actual.Startup.Language);
        if (_codigo == codigo) return;
        _codigo = codigo;
        _alAplicar?.Invoke(this);
        PropertyChanged?.Invoke(this, new(nameof(Codigo)));
        PropertyChanged?.Invoke(this, new(nameof(CodigoCore)));
        PropertyChanged?.Invoke(this, new(nameof(Cultura)));
        PropertyChanged?.Invoke(this, new(nameof(Seleccionado)));
        PropertyChanged?.Invoke(this, new("Item[]"));
        Cambiado?.Invoke();
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Cargar()
    {
        var tablas = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var codigo in new[] { "es-ES", "en" })
        {
            using var flujo = typeof(ServicioIdioma).Assembly.GetManifestResourceStream("localizacion/" + codigo + ".json") ?? throw new InvalidOperationException(codigo);
            using var json = JsonDocument.Parse(flujo);
            tablas.Add(codigo, json.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal));
        }
        return tablas;
    }
}

public static class IdiomaInterfaz
{
    public static ServicioIdioma Actual { get; } = Crear();
    private static ServicioIdioma Crear()
    {
        var servicio = new ServicioIdioma(Aplicar);
        servicio.Vincular(ProgramaPkhex.Ajustes);
        return servicio;
    }
    public static string T(string clave) => Actual[clave];
    public static string F(string clave, params object?[] valores) => Actual.Formato(clave, valores);
    public static string P(string clave, long n, params object?[] argumentos) => Actual.Plural(clave, n, argumentos);

    public static void InstalarRecursos() => Aplicar(Actual);
    private static Avalonia.Controls.ResourceDictionary? _instalado;
    private static void Aplicar(ServicioIdioma idioma)
    {
        CulturaApp.Fijar(idioma.Codigo);
        Ajustes.Pkhex.TextosNucleo.Preparar();   // los textos de programa del Core siguen al idioma de la interfaz
        if (Application.Current is not { } app) return;
        void Instalar()
        {
            CulturaApp.Fijar(idioma.Codigo);
            // Un solo diccionario nuevo y UN aviso de cambio de recursos: asignar las ~4.500 claves una a una recorría el árbol visual entero por cada clave.
            var nuevo = new Avalonia.Controls.ResourceDictionary();
            foreach (var (clave, valor) in ServicioIdioma.Recursos(idioma.Codigo)) nuevo["Loc." + clave] = valor;
            var mezclados = app.Resources.MergedDictionaries;
            var i = _instalado is null ? -1 : mezclados.IndexOf(_instalado);
            if (i >= 0) mezclados[i] = nuevo; else mezclados.Add(nuevo);
            _instalado = nuevo;
        }
        if (Dispatcher.UIThread.CheckAccess()) Instalar(); else Dispatcher.UIThread.Post(Instalar);
    }
}

/// <summary>
/// Un valor que depende del idioma de la interfaz y se calcula UNA vez por idioma (catálogos de herramientas, listas de países...): lo que se guardaba con <c>Lazy</c> quedaba fijado en el idioma del primer uso y, al cambiar de idioma en vivo,
/// seguía enseñando el anterior.
/// </summary>
public sealed class PorIdioma<T>(Func<T> crear)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<T>> _valores = new(StringComparer.Ordinal);
    public T Value => _valores.GetOrAdd(IdiomaInterfaz.Actual.Codigo, _ => new Lazy<T>(crear)).Value;
}
