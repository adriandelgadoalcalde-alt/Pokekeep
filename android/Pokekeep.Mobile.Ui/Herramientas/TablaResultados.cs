// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas;

/// <summary>
/// Una columna de la tabla de resultados (lo que en PKHeX es una columna del <c>DataGridView</c> de <c>ReportGrid</c> o <c>SAV_Database</c>). <see cref="Valor"/> da el texto de la celda; <see cref="Clave"/> el valor
/// con el que se ordena (números como números, no como texto); <see cref="Visible"/> dice si sale en la tarjeta de la fila (las demás solo salen en el detalle y en la exportación).
/// </summary>
public sealed record ColumnaTabla(string Id, string Titulo, Func<object, string> Valor, Func<object, IComparable?>? Clave = null, bool Visible = true)
{
    public IComparable? ClaveDe(object fila) => Clave is null ? Valor(fila) : Clave(fila);
}

/// <summary>Una fila ya pintada de la tabla: título (primera columna visible), líneas secundarias y la fila original (para abrir el detalle).</summary>
/// <param name="Sprite">El sprite de la fila (bloque 10-7: la imagen de <c>EntitySummaryImage</c> en el informe de cajas); null = fila sin imagen.</param>
public sealed record FilaTabla(object Origen, string Titulo, string Subtitulo, IReadOnlyList<string> Celdas, int Posicion, SpriteFila? Sprite = null)
{
    public PKHeX.Core.GameVersion VersionLogo { get; init; }
    public bool HayLogo => VersionLogo != PKHeX.Core.GameVersion.Any;
    public string Accesible => string.IsNullOrEmpty(Subtitulo) ? Titulo : $"{Titulo}. {Subtitulo}";
    public bool HaySprite => Sprite is not null;
}

/// <summary>Lo que dibuja el sprite de una fila de la tabla (los mismos datos que una casilla: especie, forma, sexo, variocolor, huevo y contexto).</summary>
public sealed record SpriteFila(int Especie, int Forma, int Genero, bool Brillante, bool Huevo, int Contexto);

/// <summary>
/// LA REJILLA DE RESULTADOS de la F10 (ADR 0072): sustituye al <c>DataGridView</c> + <c>SortableBindingList</c> de WinForms (PK-PKME-014/015, «no aplica») para Informe de cajas, Base de datos, MGDB, Generador de encuentros y
/// Verificar todos. En el móvil una fila es una TARJETA (título + línea de datos), no una fila de ocho columnas que no cabe: las columnas se eligen y se ordenan desde un selector, el texto filtra sin tildes ni
/// mayúsculas y la lista se muestra por páginas (<see cref="TamanoPagina"/>) para no pintar 30.000 filas de golpe. Es un modelo SIN pantalla: se prueba en el host y la vista (<c>TablaResultadosView</c>) solo lo pinta.
/// Exporta CSV (UTF-8 sin BOM, cultura invariante, comillas como PKHeX) y tabla de Reddit con el mismo orden y filtro que se ven.
/// </summary>
public sealed partial class TablaResultadosViewModel : ObservableObject
{
    public const int TamanoPagina = 100;
    private readonly IReadOnlyList<ColumnaTabla> _columnas;
    private readonly Action<object>? _alTocar;
    private IReadOnlyList<object> _origen = [];
    private List<object> _filtradas = [];

    public TablaResultadosViewModel(IReadOnlyList<ColumnaTabla> columnas, Action<object>? alTocar = null)
    {
        if (columnas.Count == 0) throw new ArgumentException("Una tabla necesita al menos una columna.", nameof(columnas));
        _columnas = columnas; _alTocar = alTocar;
        ColumnaOrden = columnas[0];
    }

    public IReadOnlyList<ColumnaTabla> Columnas => _columnas;

    /// <summary>
    /// Filas por página si la herramienta la fija con un ajuste suyo (p. ej. <c>EncounterDb.ResultsGridRowCount</c>); se lee EN EL MOMENTO de pintar cada página, nunca una copia. Null = <see cref="TamanoPagina"/>.
    /// </summary>
    public Func<int>? Pagina { get; set; }
    public int TamanoPaginaActual => Math.Max(1, Pagina?.Invoke() ?? TamanoPagina);

    /// <summary>Quien aloja la tabla enseña la hoja de orden (la tabla no conoce la Shell).</summary>
    public Action<HojaViewModel>? SolicitarOrden { get; set; }
    [ObservableProperty] private string _filtro = string.Empty;
    [ObservableProperty] private ColumnaTabla _columnaOrden;
    [ObservableProperty] private bool _descendente;
    [ObservableProperty] private IReadOnlyList<FilaTabla> _filas = [];
    [ObservableProperty] private string _resumen = IdiomaInterfaz.T("Selector.SinResultados");
    [ObservableProperty] private int _total;
    [ObservableProperty] private int _coinciden;
    [ObservableProperty] private int _mostradas;

    public bool HayFilas => Filas.Count > 0;
    public bool HayMas => Mostradas < Coinciden;
    public bool SinFilas => Filas.Count == 0;
    public string TextoOrden => IdiomaInterfaz.F("TablaResultados.OrdenarColumna", ColumnaOrden.Titulo, Descendente ? "↓" : "↑");
    public string TextoMas => IdiomaInterfaz.F("TablaResultados.Mostrar", Math.Min(TamanoPaginaActual, Coinciden - Mostradas));

    private bool _suspendido;
    partial void OnFiltroChanged(string value) => Aplicar();
    partial void OnColumnaOrdenChanged(ColumnaTabla value) { OnPropertyChanged(nameof(TextoOrden)); Aplicar(); }
    partial void OnDescendenteChanged(bool value) { OnPropertyChanged(nameof(TextoOrden)); Aplicar(); }
    partial void OnFilasChanged(IReadOnlyList<FilaTabla> value) { OnPropertyChanged(nameof(HayFilas)); OnPropertyChanged(nameof(SinFilas)); }
    partial void OnMostradasChanged(int value) { OnPropertyChanged(nameof(HayMas)); OnPropertyChanged(nameof(TextoMas)); }

    /// <summary>Sustituye todos los resultados (los que hubiera se descartan) y vuelve a la primera página.</summary>
    public void Establecer(IEnumerable<object> filas)
    {
        _origen = [.. filas];
        Total = _origen.Count;
        Aplicar();
    }

    /// <summary>Normaliza como el buscador del selector: minúsculas y sin diacríticos (« Hélice » = «helice»).</summary>
    private static string Norm(string s) => SelectorHojaViewModel.Normalizar(s);

    private void Aplicar()
    {
        if (_suspendido) return;
        var f = Norm(Filtro.Trim());
        IEnumerable<object> q = _origen;
        if (f.Length > 0) q = q.Where(o => _columnas.Any(c => Norm(c.Valor(o)).Contains(f, StringComparison.Ordinal)));
        // Orden estable: a igual clave, el orden original (como el SortableBindingList de WinForms).
        var indexada = q.Select((o, i) => (o, i)).ToList();
        var ordenada = Descendente
            ? indexada.OrderByDescending(x => ColumnaOrden.ClaveDe(x.o), ComparadorClaves).ThenBy(x => x.i)
            : indexada.OrderBy(x => ColumnaOrden.ClaveDe(x.o), ComparadorClaves).ThenBy(x => x.i);
        _filtradas = [.. ordenada.Select(x => x.o)];
        Coinciden = _filtradas.Count;
        Mostradas = 0;
        Filas = [];
        MostrarMas();
    }

    private static readonly Comparer<IComparable?> ComparadorClaves = Comparer<IComparable?>.Create((a, b) =>
    {
        if (a is null) return b is null ? 0 : -1;
        if (b is null) return 1;
        if (a is string sa && b is string sb) return string.Compare(sa, sb, CultureInfo.GetCultureInfo("es-ES"), CompareOptions.IgnoreCase);
        return a.CompareTo(b);
    });

    [RelayCommand]
    public void MostrarMas()
    {
        var hasta = Math.Min(Coinciden, Mostradas + TamanoPaginaActual);
        var nuevas = new List<FilaTabla>(Filas);
        for (var i = Mostradas; i < hasta; i++) nuevas.Add(Pintar(_filtradas[i], i));
        Filas = nuevas; Mostradas = hasta;
        Resumen = Total == 0 ? IdiomaInterfaz.T("Selector.SinResultados") : Coinciden == Total ? IdiomaInterfaz.F("TablaResultados.Resultados", Total) : IdiomaInterfaz.F("TablaResultados.Resultados2", Coinciden, Total);
    }

    private FilaTabla Pintar(object o, int i)
    {
        var visibles = _columnas.Where(c => c.Visible).ToList();
        var titulo = visibles.Count > 0 ? visibles[0].Valor(o) : _columnas[0].Valor(o);
        var resto = visibles.Skip(1).Select(c => $"{c.Titulo}: {c.Valor(o)}");
        return new FilaTabla(o, titulo, string.Join(" · ", resto), [.. _columnas.Select(c => c.Valor(o))], i + 1, SpriteDe?.Invoke(o))
            { VersionLogo = LogoDe?.Invoke(o) ?? PKHeX.Core.GameVersion.Any };
    }

    /// <summary>Ordena por una columna: la misma columna alterna ascendente/descendente (como pulsar la cabecera en WinForms).</summary>
    public void OrdenarPor(ColumnaTabla columna)
    {
        if (ReferenceEquals(columna, ColumnaOrden)) { Descendente = !Descendente; return; }
        _suspendido = true; Descendente = false; ColumnaOrden = columna; _suspendido = false;
        Aplicar();   // una sola pasada al cambiar de columna
    }

    /// <summary>Hoja con las columnas para elegir el orden.</summary>
    public HojaViewModel HojaOrden() => new SelectorHojaViewModel(IdiomaInterfaz.T("TablaResultados.Ordenar"), [.. _columnas.Select((c, i) => new OpcionSelector(i, c.Titulo))], _columnas.ToList().IndexOf(ColumnaOrden),
        o => OrdenarPor(_columnas[o.Id]), IdiomaInterfaz.T("TablaResultados.BuscarColumna"));

    [RelayCommand]
    private void Tocar(FilaTabla? fila) { if (fila is not null) _alTocar?.Invoke(fila.Origen); }

    // ----------------------------------------------------------------------------------------------- exportación
    /// <summary>
    /// CSV de TODAS las filas que coinciden (no solo las páginas mostradas), en el orden que se ve y con todas las columnas. UTF-8 sin BOM (lo escribe quien guarda), separador coma, comillas dobles cuando hace falta,
    /// fin de línea <c>\r\n</c>; la cultura es la invariante, así que un decimal nunca lleva coma.
    /// </summary>
    public string Csv()
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(',', _columnas.Select(c => CampoCsv(c.Titulo)))).Append("\r\n");
        foreach (var o in _filtradas) sb.Append(string.Join(',', _columnas.Select(c => CampoCsv(c.Valor(o))))).Append("\r\n");
        return sb.ToString();
    }

    private static string CampoCsv(string s) => s.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    /// <summary>Tabla de Markdown para pegar en Reddit (la que PKHeX copia con Ctrl+C en el informe): cabecera, línea de guiones y una línea por fila, con el mismo filtro y orden.</summary>
    public string TablaReddit()
    {
        var sb = new StringBuilder();
        sb.Append(string.Join('|', _columnas.Select(c => c.Titulo))).Append('\n');
        sb.Append(string.Join('|', _columnas.Select(_ => "---"))).Append('\n');
        foreach (var o in _filtradas) sb.Append(string.Join('|', _columnas.Select(c => c.Valor(o).Replace("|", "\\|")))).Append('\n');
        return sb.ToString();
    }

    public int NumeroDeFiltradas => _filtradas.Count;
    public IReadOnlyList<object> Filtradas => _filtradas;

    /// <summary>El sprite de cada fila, si la herramienta lo da (bloque 10-7, informe de cajas). Null = filas sin imagen.</summary>
    public Func<object, SpriteFila?>? SpriteDe { get; set; }
    /// <summary>Logo por versión para filas de partidas; no afecta a las tablas de Pokémon.</summary>
    public Func<object, PKHeX.Core.GameVersion>? LogoDe { get; set; }
}
