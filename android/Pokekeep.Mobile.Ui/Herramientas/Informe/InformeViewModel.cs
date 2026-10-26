// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.Informe;

/// <summary>
/// «Más &gt; Herramientas &gt; Informe de la caja» (<c>Menu_Report</c> → <c>ReportGrid</c>; PK-SUB-006, PK-MENU-017, PK-PKME-013, PK-IO-042). Una tarjeta por Pokémon con su sprite (la imagen de
/// <c>EntitySummaryImage</c>), filtro, orden por cualquier columna y el detalle de todas las columnas al tocarla. Lee los ajustes <c>Report.ExtraProperties</c> y <c>Report.HiddenProperties</c> AL GENERAR (nunca
/// una copia vieja). Donde PKHeX pregunta al cerrar «¿Guardar todos los datos en CSV?» y al copiar «¿Copiar como tabla formateada?», aquí hay botones (regla 4). SOLO LEE: la partida no cambia.
/// </summary>
public sealed partial class InformeViewModel : HerramientaViewModel
{
    private readonly IReadOnlyList<SlotCache>? _fuente;

    public InformeViewModel(ContextoHerramienta contexto, Action volver, IReadOnlyList<SlotCache>? fuente = null) : base(contexto, volver)
    {
        _fuente = fuente?.Select(s => new SlotCache(s.Source, s.Entity.Clone(), s.SAV)).ToArray();
        Tabla = new TablaResultadosViewModel([new ColumnaTabla("pokemon", IdiomaInterfaz.T("Nav.Pokemon"), o => ((FilaInforme)o).Valor(0) ?? string.Empty)], AlTocar);
    }

    public override string Titulo => _fuente is null ? IdiomaInterfaz.T("CatalogoHerramientas.InformeCaja") : IdiomaInterfaz.T("Informe.InformeBaseDatos");
    public string Subtitulo => _fuente is null
        ? IdiomaInterfaz.T("Informe.TodosPokemonPartidaTabla")
        : IdiomaInterfaz.T("Informe.ResultadosBusquedaTablaOrdenalos");
    public bool HayPartida => Sesion is not null;
    public TablaResultadosViewModel Tabla { get; private set; }
    public ResultadoInforme? Informe { get; private set; }
    public bool HayInforme => Informe is not null;
    [ObservableProperty] private string _mensaje = string.Empty;
    public bool HayMensaje => !string.IsNullOrEmpty(Mensaje);
    partial void OnMensajeChanged(string value) => OnPropertyChanged(nameof(HayMensaje));

    [RelayCommand]
    public async Task Generar()
    {
        if (Sesion is null) return;
        var sav = Sesion.Guardado;
        var extra = Ajustes.Report.ExtraProperties.ToList();   // los ajustes VIGENTES, leídos al generar
        var ocultas = Ajustes.Report.HiddenProperties.ToList();
        var copias = _fuente ?? ModeloInforme.Instantanea(sav);   // solo copias pasan al trabajo de fondo
        var r = await EjecutarAsync(IdiomaInterfaz.T("Informe.LeyendoPokemonPartida"), (cancelar, avance) => ModeloInforme.Construir(copias, extra, ocultas, cancelar, avance));
        if (r is null) return;
        Mostrar(r);
    }

    /// <summary>Genera en este hilo (gate visual y pruebas: el estado «con resultado» sin trabajo en segundo plano).</summary>
    public void GenerarSincrono()
    {
        if (Sesion is null) return;
        Mostrar(ModeloInforme.Construir(_fuente ?? ModeloInforme.Instantanea(Sesion.Guardado), Ajustes.Report.ExtraProperties, Ajustes.Report.HiddenProperties));
    }

    /// <summary>Columnas de la tarjeta además del título: las de PKHeX que mejor resumen la fila (si no están ocultas).</summary>
    private static string[] EnTarjeta => ["Level", "Nature", "Ability", IdiomaInterfaz.T("Informe.Columna.Legal")];

    /// <summary>Pone el informe en la tabla: una columna de pantalla por cada columna visible (las ocultas no salen ni en la tarjeta, ni en el detalle, ni en el orden, ni en el filtro).</summary>
    public void Mostrar(ResultadoInforme r)
    {
        Informe = r;
        var especie = r.Indice("Species"); var mote = r.Indice("Nickname");
        var columnas = new List<ColumnaTabla>
        {
            new("pokemon", IdiomaInterfaz.T("Nav.Pokemon"), o => TituloFila((FilaInforme)o, especie, mote)),
            new("donde", IdiomaInterfaz.T("Informe.Donde"), o => ((FilaInforme)o).Donde),
        };
        foreach (var i in r.Visibles)
        {
            var c = r.Columnas[i];
            if (c.Propiedad == "Position") continue;   // «Dónde» ya lo dice en castellano; el identificador de PKHeX sigue en el CSV y en el detalle
            var indice = i;
            // Se ordena como número si la columna es numérica o si todas sus celdas lo son. PS, Ataque... son texto en EntitySummary y la rejilla de PKHeX los ordena como texto («99» después de «344»); aquí como número.
            var numerica = c.Numerica || r.Filas.All(f => f.Valor(indice) is not { Length: > 0 } v || long.TryParse(v, out _));
            columnas.Add(new ColumnaTabla(c.Propiedad, c.Titulo, o => Pantalla(((FilaInforme)o).Valor(indice)), numerica ? o => Numero(((FilaInforme)o).Valor(indice)) : null, Visible: EnTarjeta.Contains(c.Propiedad)));
        }
        Tabla = new TablaResultadosViewModel(columnas, AlTocar) { SolicitarOrden = h => Contexto.AbrirHoja(h), SpriteDe = o => Sprite(((FilaInforme)o).Entidad) };
        Tabla.Establecer(r.Filas);
        Estado = r.Filas.Count == 1 ? IdiomaInterfaz.T("Informe.PokemonInforme") : IdiomaInterfaz.F("Informe.PokemonInforme2", r.Filas.Count);
        Mensaje = string.Empty;
        OnPropertyChanged(nameof(Tabla)); OnPropertyChanged(nameof(Informe)); OnPropertyChanged(nameof(HayInforme));
    }

    /// <summary>El título de la tarjeta: la especie y, si lo tiene, el mote («Pikachu «Chispa»»).</summary>
    private static string TituloFila(FilaInforme f, int especie, int mote)
    {
        var e = (especie >= 0 ? f.Valor(especie) : null) ?? f.Entidad.Species.ToString();
        return f.Entidad.IsNicknamed && mote >= 0 && f.Valor(mote) is { Length: > 0 } m && m != e ? IdiomaInterfaz.F("InformeViewModel.EspecieConMote", e, m) : e;
    }

    /// <summary>En pantalla los «True»/«False» de PKHeX se leen «Sí»/«No» (en el CSV quedan como en PKHeX).</summary>
    public static string Pantalla(string? v) => v switch { "True" => IdiomaInterfaz.T("Comun.Si"), "False" => IdiomaInterfaz.T("Comun.No"), null => string.Empty, _ => v };

    private static IComparable? Numero(string? v) => long.TryParse(v, out var n) ? n : null;

    private static SpriteFila Sprite(PKHeX.Core.PKM pk) => new(pk.Species, pk.Form, pk.Gender, pk.IsShiny, pk.IsEgg, (int)pk.Context);

    /// <summary>Tocar una fila: el detalle con TODAS las columnas visibles (lo que en PKHeX es la fila entera de la rejilla) y «Copiar».</summary>
    private void AlTocar(object o)
    {
        if (Informe is not { } r || o is not FilaInforme f) return;
        Contexto.AbrirHoja(Detalle(r, f, Contexto.Portapapeles() is { } clip ? t => clip.CopiarAsync(t) : null));
    }

    public static InformeHojaViewModel Detalle(ResultadoInforme r, FilaInforme f, Func<string, Task>? copiar)
    {
        var lineas = r.Visibles.Select(i => $"{r.Columnas[i].Titulo}: {Pantalla(f.Valor(i))}").ToList();
        return new InformeHojaViewModel(f.Donde, string.Join("\n", lineas), lineas, copiar is null ? null : async t => await copiar(t));
    }

    // ------------------------------------------------------------------------------------------------- exportar
    /// <summary>«Guardar en CSV» (<c>PromptSaveCSV</c> → <c>Export_CSV</c>): el archivo que propone PKHeX, en el orden que se ve, con TODAS las columnas (también las ocultas, como PKHeX).</summary>
    [RelayCommand]
    public async Task GuardarCsv()
    {
        if (Informe is not { } r) return;
        if (Contexto.Documentos() is not { } docs) { Mensaje = IdiomaInterfaz.T("Informe.GuardarArchivosDisponibleVentana"); return; }
        var destino = await docs.ElegirDestinoAsync(ModeloInforme.NombreCsv, CancellationToken.None);
        if (destino is null) return;
        await docs.EscribirAsync(destino, ModeloInforme.BytesCsv(CsvVisible()), CancellationToken.None);
        Mensaje = IdiomaInterfaz.F("Informe.GuardadoPokemon", destino.Nombre, Tabla.NumeroDeFiltradas);
    }

    /// <summary>El CSV de lo que se ve (con el filtro y el orden de la tabla; sin filtro, todo el informe).</summary>
    public string CsvVisible() => Informe is { } r ? ModeloInforme.Csv(r, Tabla.Filtradas.Cast<FilaInforme>()) : string.Empty;

    /// <summary>«Copiar como tabla formateada» (la respuesta «Sí» de <c>MsgReportExportTable</c>): la tabla de Reddit de PKHeX con las columnas visibles.</summary>
    [RelayCommand]
    public async Task CopiarTabla()
    {
        if (Informe is null || Contexto.Portapapeles() is not { } clip) return;
        Mensaje = await clip.CopiarAsync(TablaReddit()) ? IdiomaInterfaz.T("Informe.TablaCopiadaPortapapeles") : IdiomaInterfaz.T("Informe.PudoCopiar");
    }

    /// <summary>«Copiar como texto» (la respuesta «No»: el texto de la rejilla, separado por tabuladores).</summary>
    [RelayCommand]
    public async Task CopiarTexto()
    {
        if (Informe is null || Contexto.Portapapeles() is not { } clip) return;
        Mensaje = await clip.CopiarAsync(TextoTabulado()) ? IdiomaInterfaz.T("Informe.TextoCopiadoPortapapeles") : IdiomaInterfaz.T("Informe.PudoCopiar");
    }

    /// <summary>Las filas que se ven (filtro y orden) con las columnas visibles, separadas por tabuladores y con la cabecera, como el texto que copia la rejilla de PKHeX.</summary>
    public string TextoTabulado()
    {
        if (Informe is not { } r) return string.Empty;
        var vis = r.Visibles.ToList();
        var sb = new StringBuilder();
        sb.Append(string.Join('\t', vis.Select(i => r.Columnas[i].Propiedad)));
        foreach (var f in Tabla.Filtradas.Cast<FilaInforme>()) sb.Append("\r\n").Append(string.Join('\t', vis.Select(i => f.Valor(i) ?? string.Empty)));
        return sb.ToString();
    }

    public string TablaReddit() => ModeloInforme.TablaReddit(TextoTabulado());
}
