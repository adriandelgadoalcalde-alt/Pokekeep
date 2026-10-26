// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Partida;
using Pokekeep.Mobile.Ui.Partida.Formularios;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Un grupo de ajustes de PKHeX en la lista de «Más &gt; Ajustes» (General, Copias de seguridad, Legalidad...): nombre, cuántos ajustes tiene y cómo se abre.</summary>
public sealed class EntradaGrupoPkhex(string nombre, int cantidad, ICommand abrir) : ObservableObject
{
    public string Nombre => RegistroAjustesPkhex.NombreGrupo(nombre);
    public int Cantidad { get; } = cantidad;
    public string Detalle => IdiomaInterfaz.P("Ajustes.Pkhex.Cantidad", Cantidad);
    public ICommand Abrir { get; } = abrir;
    public string Accesible => $"{Nombre}, {Detalle}";
    public void ActualizarIdioma() { OnPropertyChanged(nameof(Nombre)); OnPropertyChanged(nameof(Detalle)); OnPropertyChanged(nameof(Accesible)); }
}

/// <summary>
/// UNA FILA de un ajuste de PKHeX: se crea desde un <see cref="NodoAjustePkhex"/> del registro (nunca una lista de filas escrita a mano). Cada cambio se aplica al Core y se guarda al momento (como los de Pokekeep).
/// Los textos son los de PKHeX en castellano (<see cref="RegistroAjustesPkhex.Etiqueta"/> / <see cref="RegistroAjustesPkhex.Descripcion"/>); si faltan, la fila lo dice con el nombre de la propiedad y la prueba de localización falla.
/// </summary>
public abstract partial class FilaPkhex : ObservableObject
{
    protected FilaPkhex(NodoAjustePkhex nodo, IAjustesPkhex almacen)
    {
        Nodo = nodo; Almacen = almacen;
    }

    public NodoAjustePkhex Nodo { get; }
    protected IAjustesPkhex Almacen { get; }
    public string Ruta => Nodo.Ruta;
    public string Nombre => RegistroAjustesPkhex.Etiqueta(Nodo) ?? Nodo.Nombre;
    public string Descripcion => RegistroAjustesPkhex.Descripcion(Nodo) ?? string.Empty;
    public bool HayDescripcion => Descripcion.Length > 0;
    public string Accesible => HayDescripcion ? $"{Nombre}. {Descripcion}" : Nombre;
    public abstract string ValorTexto { get; }
    public abstract void Releer();
    public virtual void ActualizarIdioma()
    {
        OnPropertyChanged(nameof(Nombre)); OnPropertyChanged(nameof(Descripcion)); OnPropertyChanged(nameof(HayDescripcion));
        OnPropertyChanged(nameof(Accesible)); OnPropertyChanged(nameof(ValorTexto));
    }

    protected object? Leer() => Nodo.Leer(Almacen.Actual);
    protected void Escribir(object? valor) { Nodo.Escribir(Almacen.Actual, valor); Almacen.Guardar(); }
}

public sealed partial class FilaPkhexInterruptor : FilaPkhex
{
    private bool _releyendo;
    public FilaPkhexInterruptor(NodoAjustePkhex nodo, IAjustesPkhex almacen) : base(nodo, almacen) { _releyendo = true; Activo = (bool)Leer()!; _releyendo = false; }
    [ObservableProperty] private bool _activo;
    public override string ValorTexto => IdiomaInterfaz.T(Activo ? "Comun.Si" : "Comun.No");
    partial void OnActivoChanged(bool value) { if (_releyendo) return; Escribir(value); OnPropertyChanged(nameof(ValorTexto)); }
    public override void Releer() { _releyendo = true; Activo = (bool)Leer()!; _releyendo = false; OnPropertyChanged(nameof(ValorTexto)); }
}

/// <summary>Un entero con su rango REAL (el que recorta el <c>set</c> del Core, sondeado) y botones de menos y más (48 dp); con rango amplio (opacidades 0-255) el paso es mayor.</summary>
public sealed partial class FilaPkhexEntero : FilaPkhex
{
    private bool _releyendo;
    public FilaPkhexEntero(NodoAjustePkhex nodo, IAjustesPkhex almacen) : base(nodo, almacen)
    {
        (Minimo, Maximo) = RegistroAjustesPkhex.Rango(nodo);
        Paso = Maximo - Minimo > 200 ? 5 : 1;
        _releyendo = true; Valor = Convert.ToInt64(Leer()); _releyendo = false;
    }
    public long Minimo { get; }
    public long Maximo { get; }
    public long Paso { get; }
    [ObservableProperty] private long _valor;
    public string Rango => IdiomaInterfaz.F("Ajustes.Rango", Minimo, Maximo);
    public override void ActualizarIdioma() { base.ActualizarIdioma(); OnPropertyChanged(nameof(Rango)); }
    public override string ValorTexto => Valor.ToString(CultureInfo.CurrentCulture);
    public bool PuedeBajar => Valor > Minimo;
    public bool PuedeSubir => Valor < Maximo;
    /// <summary>Pone un valor: el <c>set</c> del Core lo recorta (como en el escritorio) y la fila enseña el valor real.</summary>
    public void Poner(long v)
    {
        Escribir(Convert.ChangeType(Math.Clamp(v, Minimo, Maximo), Nodo.Tipo));
        var real = Convert.ToInt64(Leer());
        if (real != Valor) Valor = real; else Avisar();
    }
    partial void OnValorChanged(long value) { if (_releyendo) { Avisar(); return; } if (Convert.ToInt64(Leer()) != value) Poner(value); else Avisar(); }
    private void Avisar() { OnPropertyChanged(nameof(ValorTexto)); OnPropertyChanged(nameof(PuedeBajar)); OnPropertyChanged(nameof(PuedeSubir)); }
    [RelayCommand] private void Bajar() => Poner(Valor - Paso);
    [RelayCommand] private void Subir() => Poner(Valor + Paso);
    public override void Releer() { _releyendo = true; Valor = Convert.ToInt64(Leer()); _releyendo = false; Avisar(); }
}

/// <summary>Un decimal entre 0 y 1 (opacidad, escala de grises) con paso de 0,05.</summary>
public sealed partial class FilaPkhexDecimal : FilaPkhex
{
    public FilaPkhexDecimal(NodoAjustePkhex nodo, IAjustesPkhex almacen) : base(nodo, almacen) => Valor = Convert.ToDouble(Leer(), CultureInfo.InvariantCulture);
    [ObservableProperty] private double _valor;
    public override string ValorTexto => Valor.ToString("0.00", CultureInfo.CurrentCulture);
    public bool PuedeBajar => Valor > 0.0001;
    public bool PuedeSubir => Valor < 0.9999;
    public void Poner(double v)
    {
        v = Math.Round(Math.Clamp(v, 0, 1), 2);
        Escribir(Convert.ChangeType(v, Nodo.Tipo, CultureInfo.InvariantCulture));
        Valor = Convert.ToDouble(Leer(), CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(ValorTexto)); OnPropertyChanged(nameof(PuedeBajar)); OnPropertyChanged(nameof(PuedeSubir));
    }
    [RelayCommand] private void Bajar() => Poner(Valor - 0.05);
    [RelayCommand] private void Subir() => Poner(Valor + 0.05);
    public override void Releer() { Valor = Convert.ToDouble(Leer(), CultureInfo.InvariantCulture); OnPropertyChanged(nameof(ValorTexto)); OnPropertyChanged(nameof(PuedeBajar)); OnPropertyChanged(nameof(PuedeSubir)); }
}

/// <summary>Una enumeración: el valor actual y una hoja con todos los valores en castellano (<c>lang_es.txt</c> «Tipo.Valor»; si no está, el respaldo con motivo).</summary>
public sealed partial class FilaPkhexOpcion : FilaPkhex
{
    private readonly Action<HojaViewModel> _abrirHoja;
    private readonly Dictionary<string, string>? _etiquetas;
    private readonly Func<IReadOnlyList<(string Valor, string Etiqueta)>>? _etiquetasActuales;
    /// <param name="valores">Solo estos valores, con su etiqueta (un enum enorme como <c>GameVersion</c> donde el ajuste solo admite unos pocos; bloque 10-3). Sin ellos, todos los del enum.</param>
    public FilaPkhexOpcion(NodoAjustePkhex nodo, IAjustesPkhex almacen, Action<HojaViewModel> abrirHoja, IReadOnlyList<(string Valor, string Etiqueta)>? valores = null, Func<IReadOnlyList<(string Valor, string Etiqueta)>>? etiquetasActuales = null) : base(nodo, almacen)
    {
        _abrirHoja = abrirHoja;
        _etiquetas = valores?.ToDictionary(v => v.Valor, v => v.Etiqueta, StringComparer.Ordinal);
        _etiquetasActuales = etiquetasActuales;
        Valores = valores is null ? [.. Enum.GetNames(nodo.Tipo).Where(v => !EsOculto(nodo.Tipo, v))] : [.. valores.Select(v => v.Valor)];
    }

    /// <summary>Valores del enum que no se ofrecen: los que el Core no usa (<c>LanguageID.UNUSED_6</c>) y el estilo «Personalizado» de estadísticas, que en PKHeX lanza una excepción al exportar (ver <c>ModeloShowdown</c>).</summary>
    private static bool EsOculto(Type tipo, string valor) => valor.StartsWith("UNUSED", StringComparison.Ordinal) || (tipo == typeof(PKHeX.Core.StatDisplayStyle) && valor == nameof(PKHeX.Core.StatDisplayStyle.Custom));
    public IReadOnlyList<string> Valores { get; }
    public static string Etiqueta(Type tipo, string valor)
    {
        var clave = $"{tipo.Name}.{valor}";
        if (ProgramaPkhex.Idioma != "en" && RespaldoAjustesPkhex.Etiquetas.TryGetValue(clave, out var r)) return r;
        if (EtiquetasLang.Obtener(clave) is { } t && !string.IsNullOrWhiteSpace(t)) return t.Trim();
        // Enumeraciones propias de Pokekeep (sprites): el castellano sigue enseñando el nombre del valor y el inglés lo da en claro.
        var propia = "Ajustes.Pkhex.Enum." + clave;
        if (ServicioIdioma.Recursos("es-ES").ContainsKey(propia)) return IdiomaInterfaz.T(propia);
        return valor.Replace('_', ' ');
    }
    public string EtiquetaDe(string valor) => _etiquetasActuales?.Invoke().FirstOrDefault(o => o.Valor == valor).Etiqueta ?? _etiquetas?.GetValueOrDefault(valor) ?? Etiqueta(Nodo.Tipo, valor);
    public override string ValorTexto => EtiquetaDe(Leer()!.ToString()!);
    public void Elegir(string valor) { Escribir(Enum.Parse(Nodo.Tipo, valor)); OnPropertyChanged(nameof(ValorTexto)); }
    [RelayCommand]
    private void Abrir()
    {
        var actual = Leer()!.ToString();
        IReadOnlyList<OpcionSelector> Opciones() => [.. Valores.Select((v, i) => new OpcionSelector(i, EtiquetaDe(v)))];
        _abrirHoja(new SelectorHojaViewModel(Nombre, Opciones(), Valores.ToList().IndexOf(actual!), o => Elegir(Valores[o.Id]), actualizarOpciones: Opciones, tituloActual: () => Nombre));
    }
    public override void Releer() => OnPropertyChanged(nameof(ValorTexto));
}

/// <summary>Un texto libre (una ruta, una lista separada por comas): se guarda al terminar de escribir.</summary>
public sealed partial class FilaPkhexTexto : FilaPkhex
{
    public FilaPkhexTexto(NodoAjustePkhex nodo, IAjustesPkhex almacen) : base(nodo, almacen) => _texto = (string?)Leer() ?? string.Empty;
    [ObservableProperty] private string _texto;
    public override string ValorTexto => Texto;
    public void Confirmar() { if ((string?)Leer() != Texto) { Escribir(Texto); Texto = (string?)Leer() ?? string.Empty; OnPropertyChanged(nameof(ValorTexto)); } }
    public override void Releer() { Texto = (string?)Leer() ?? string.Empty; OnPropertyChanged(nameof(ValorTexto)); }
}

/// <summary>Un ajuste visible cuyo tipo de control aún no tiene fila (lo implementa el bloque dueño): en vez de ocultarlo o romper, lo dice claramente. La prueba de ajustes lo rechaza si un bloque cierra la fila sin implementarlo.</summary>
public sealed partial class FilaPkhexSinControl : FilaPkhex
{
    public FilaPkhexSinControl(NodoAjustePkhex nodo, IAjustesPkhex almacen) : base(nodo, almacen) { }
    public override string ValorTexto => IdiomaInterfaz.T("Ajustes.SinControl");
    public override void Releer() { }
}

/// <summary>Crea la fila de un ajuste. Cada bloque puede aportar la de un tipo de control propio (listas, colores, estadísticas) en su fichero parcial, sin tocar este.</summary>
public static partial class FabricaFilasPkhex
{
    public static FilaPkhex Crear(NodoAjustePkhex n, IAjustesPkhex almacen, Action<HojaViewModel> abrirHoja)
    {
        FilaPkhex? propia = null;
        PersonalizadaB10_1(n, almacen, abrirHoja, ref propia); PersonalizadaB10_2(n, almacen, abrirHoja, ref propia); PersonalizadaB10_3(n, almacen, abrirHoja, ref propia);
        PersonalizadaB10_4(n, almacen, abrirHoja, ref propia); PersonalizadaB10_5(n, almacen, abrirHoja, ref propia); PersonalizadaB10_6(n, almacen, abrirHoja, ref propia);
        PersonalizadaB10_7(n, almacen, abrirHoja, ref propia); PersonalizadaB10_8(n, almacen, abrirHoja, ref propia); PersonalizadaB10_9(n, almacen, abrirHoja, ref propia);
        PersonalizadaB10_10(n, almacen, abrirHoja, ref propia); PersonalizadaB10_11(n, almacen, abrirHoja, ref propia); PersonalizadaB10_12(n, almacen, abrirHoja, ref propia);
        PersonalizadaB10_13(n, almacen, abrirHoja, ref propia);
        if (propia is not null) return propia;
        return RegistroAjustesPkhex.ClaseEfectiva(n) switch
        {
            TipoAjustePkhex.Interruptor => new FilaPkhexInterruptor(n, almacen),
            TipoAjustePkhex.Entero => new FilaPkhexEntero(n, almacen),
            TipoAjustePkhex.Decimal => new FilaPkhexDecimal(n, almacen),
            TipoAjustePkhex.Opcion => new FilaPkhexOpcion(n, almacen, abrirHoja),
            TipoAjustePkhex.Texto => new FilaPkhexTexto(n, almacen),
            _ => new FilaPkhexSinControl(n, almacen),
        };
    }

    static partial void PersonalizadaB10_1(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila);
    static partial void PersonalizadaB10_2(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila);
    static partial void PersonalizadaB10_3(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila);
    static partial void PersonalizadaB10_4(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila);
    static partial void PersonalizadaB10_5(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila);
    static partial void PersonalizadaB10_6(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila);
    static partial void PersonalizadaB10_7(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila);
    static partial void PersonalizadaB10_8(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila);
    static partial void PersonalizadaB10_9(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila);
    static partial void PersonalizadaB10_10(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila);
    static partial void PersonalizadaB10_11(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila);
    static partial void PersonalizadaB10_12(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila);
    static partial void PersonalizadaB10_13(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila);
}

/// <summary>
/// La pantalla de UN grupo de ajustes de PKHeX (<c>Más &gt; Ajustes &gt; General</c>...). Solo salen los ajustes con consumidor declarado (<see cref="ConsumidoresPkhex"/>): ninguno que no haga nada. Cada cambio se
/// aplica al Core y se guarda al momento. «Restablecer este grupo» vuelve a los valores de fábrica solo de este grupo.
/// </summary>
public sealed partial class AjustesPkhexGrupoViewModel : PantallaMasViewModel
{
    private readonly IAjustesPkhex _almacen;
    private readonly Action<string, string, string> _informar;

    /// <param name="carpetas">El selector de carpetas del sistema de la Shell que abre la pantalla (bloque 10-4, «Otras rutas de copias»); null = esta ventana no puede elegir carpetas.</param>
    public AjustesPkhexGrupoViewModel(IAjustesPkhex almacen, string grupo, Action volver, Action<HojaViewModel> abrirHoja, Action<string, string, string> informar, Func<Almacen.IAlmacenCarpetas?>? carpetas = null, string? rutaInicial = null) : base(volver)
    {
        _almacen = almacen; Grupo = grupo; _informar = informar;
        Filas = [.. AjustesPkhexCatalogo.HojasVisibles(grupo).Select(n => FabricaFilasPkhex.Crear(n, almacen, abrirHoja)), .. ExtrasAjustesPkhex.Filas(grupo, almacen, abrirHoja)];
        if (rutaInicial is not null) Filas = [.. Filas.Where(f => f.Ruta == rutaInicial)];
        if (carpetas is not null) foreach (var f in Filas.OfType<FilaPkhexCarpetasExtra>()) f.ProveedorCarpetas = carpetas;
    }

    public string Grupo { get; }
    public override string Titulo => RegistroAjustesPkhex.NombreGrupo(Grupo);
    public IReadOnlyList<FilaPkhex> Filas { get; }
    public string Subtitulo => IdiomaInterfaz.T("Ajustes.Pkhex.Subtitulo");
    public override void ActualizarIdioma()
    {
        base.ActualizarIdioma(); OnPropertyChanged(nameof(Subtitulo));
        foreach (var fila in Filas) fila.ActualizarIdioma();
    }

    [RelayCommand]
    private void Restablecer()
    {
        _almacen.Restablecer(Grupo);
        foreach (var f in Filas) f.Releer();
        _informar(IdiomaInterfaz.F("Ajustes.Pkhex.Restablecidos", RegistroAjustesPkhex.NombreGrupo(Grupo)), "ok", "CircleCheck");
    }
}

/// <summary>Qué grupos y qué ajustes salen en la pantalla: el registro filtrado por consumidor declarado.</summary>
public static class AjustesPkhexCatalogo
{
    public static IReadOnlyList<NodoAjustePkhex> HojasVisibles(string grupo)
        => [.. RegistroAjustesPkhex.Hojas.Where(h => h.Grupo == grupo && ConsumidoresPkhex.EsVisible(h))];

    /// <summary>Los grupos con al menos un ajuste visible, en el orden de la pantalla.</summary>
    public static IReadOnlyList<(string Grupo, int Cantidad)> GruposVisibles()
        => [.. RegistroAjustesPkhex.OrdenGrupos.Select(g => (Grupo: g, Cantidad: HojasVisibles(g).Count + ExtrasAjustesPkhex.Cantidad(g))).Where(x => x.Cantidad > 0)];

    /// <summary>Los grupos del censo que aún no tienen ningún ajuste visible (para decir honestamente qué falta).</summary>
    public static IReadOnlyList<string> GruposPendientes()
        => [.. RegistroAjustesPkhex.OrdenGrupos.Where(g => HojasVisibles(g).Count == 0 && ExtrasAjustesPkhex.Cantidad(g) == 0 && RegistroAjustesPkhex.Hojas.Any(h => h.Grupo == g))];
}

/// <summary>
/// Filas de ajustes que PKHeX no enseña en su cuadrícula (<c>Browsable(false)</c>) pero sí cambia con un control propio: el idioma (menú «Idioma») y la versión de la partida en blanco (selector de la ventana de
/// Ajustes). Siguen saliendo del registro (un <see cref="NodoAjustePkhex"/> oculto) y su bloque las añade en su fichero parcial (<c>FilasB10_N</c>), sin tocar la cuadrícula ni <c>AjustesContraWinFormsTests</c>.
/// </summary>
public static partial class ExtrasAjustesPkhex
{
    public static IReadOnlyList<FilaPkhex> Filas(string grupo, IAjustesPkhex almacen, Action<HojaViewModel> abrirHoja)
    {
        var l = new List<FilaPkhex>();
        FilasB10_1(grupo, almacen, abrirHoja, l);
        return l;
    }

    /// <summary>Cuántas filas extra tiene un grupo (sin crearlas: la lista de grupos se pinta antes de abrir ninguno).</summary>
    public static int Cantidad(string grupo)
    {
        var n = 0;
        CantidadB10_1(grupo, ref n);
        return n;
    }

    static partial void FilasB10_1(string grupo, IAjustesPkhex almacen, Action<HojaViewModel> abrirHoja, List<FilaPkhex> filas);
    static partial void CantidadB10_1(string grupo, ref int cantidad);
}
