// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PKHeX.Core;
using Pokekeep.Sprites;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.EstadisticasBase;

/// <summary>Una celda de estadística: etiqueta corta, valor con ceros («045») y su color de fondo (el de KChart; el texto es negro como en PKHeX).</summary>
public sealed record CeldaEstadistica(string Etiqueta, string Valor, IBrush Fondo, string Accesible);

/// <summary>Una fila de la tabla como la pinta la pantalla: la fila del modelo más lo que la vista necesita (sprite, celdas coloreadas, textos).</summary>
public sealed class FilaEstadisticaVm
{
    public FilaEstadisticaVm(FilaEstadistica f, int contexto, ModoSprites modo)
    {
        Fila = f; Contexto = contexto; Modo = modo;
        string Tres(int n) => n.ToString("000", CultureInfo.InvariantCulture);
        IBrush Pincel((byte R, byte G, byte B) c) => new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
        CeldaEstadistica Celda(string etiqueta, string nombre, int v) => new(etiqueta, Tres(v), Pincel(ColoresEstadisticas.DeEstadistica(v)), $"{nombre} {v}");
        Estadisticas =
        [
            Celda(IdiomaInterfaz.T("Informe.Columna.HP"), IdiomaInterfaz.T("EstadisticasBase.PuntosSalud"), f.Ps), Celda(IdiomaInterfaz.T("EstadisticasBase.AbrAtaque"), IdiomaInterfaz.T("Informe.Columna.ATK"), f.Ataque), Celda(IdiomaInterfaz.T("EstadisticasBase.AbrDefensa"), IdiomaInterfaz.T("Informe.Columna.DEF"), f.Defensa),
            Celda(IdiomaInterfaz.T("EstadisticasBase.AbrAtaqueEsp"), IdiomaInterfaz.T("EstadisticasBase.AtaqueEspecial"), f.AtaqueEspecial), Celda(IdiomaInterfaz.T("EstadisticasBase.AbrDefensaEsp"), IdiomaInterfaz.T("EstadisticasBase.DefensaEspecial"), f.DefensaEspecial), Celda(IdiomaInterfaz.T("EstadisticasBase.AbrVelocidad"), IdiomaInterfaz.T("Informe.Columna.SPE"), f.Velocidad),
        ];
        Total = new CeldaEstadistica("Total", Tres(f.Bst), Pincel(ColoresEstadisticas.DeTotal(f.Bst)), IdiomaInterfaz.F("EstadisticasBase.TotalEstadisticasBase", f.Bst));
        Tipos = f.UnSoloTipo ? f.NombreTipo1 : $"{f.NombreTipo1} · {f.NombreTipo2}";
        Etiquetas = f.UnSoloTipo ? [new(f.Tipo1, f.NombreTipo1)] : [new(f.Tipo1, f.NombreTipo1), new(f.Tipo2, f.NombreTipo2)];
        Captura = IdiomaInterfaz.F("EstadisticasBase.RatioCaptura", Tres(f.Captura));
        Habilidades = IdiomaInterfaz.F("EstadisticasBase.Oculta", f.Habilidad1, f.Habilidad2, f.HabilidadOculta) + (f.Alfa is null ? string.Empty : IdiomaInterfaz.F("EstadisticasBase.MovAlfa", f.Alfa));
        Nativo = f.Nativo ? IdiomaInterfaz.T("EstadisticasBase.Nativo") : IdiomaInterfaz.T("EstadisticasBase.NoNativo");
        Accesible = IdiomaInterfaz.F("EstadisticasBase.Accesible", f.Nombre, f.Numero, Tipos, Total.Accesible, string.Join(", ", Estadisticas.Select(e => e.Accesible)), Captura, Habilidades, Nativo);
    }

    public FilaEstadistica Fila { get; }
    /// <summary>Los tipos como etiquetas de color.</summary>
    public IReadOnlyList<Modelo.TipoVm> Etiquetas { get; }
    public int Contexto { get; }
    public ModoSprites Modo { get; }
    public int Especie => Fila.Especie;
    public int Forma => Fila.Forma;
    public string Nombre => Fila.Nombre;
    public string Numero => Fila.Numero;
    public IReadOnlyList<CeldaEstadistica> Estadisticas { get; }
    public CeldaEstadistica Total { get; }
    public string Tipos { get; }
    public string Captura { get; }
    public string Habilidades { get; }
    public string Nativo { get; }
    public string Accesible { get; }
}

/// <summary>
/// «Estadísticas base» (KChart, PK-SUB-005, PK-MENU-019): la tabla de todas las especies y formas del juego de la partida con su total (BST), ratio de captura, tipos, estadísticas base y habilidades. Solo lee;
/// no cambia la partida. En lugar de una rejilla de 17 columnas, cada especie es una tarjeta con las seis estadísticas coloreadas (el degradado de KChart); el texto filtra por cualquier dato (nombre, número, tipo,
/// habilidad) y el orden se elige por cualquier columna, como al pulsar la cabecera en PKHeX. Las filas salen de <see cref="ModeloEstadisticasBase"/>.
/// </summary>
public sealed partial class EstadisticasBaseViewModel : HerramientaViewModel
{
    public EstadisticasBaseViewModel(ContextoHerramienta contexto, Action volver) : base(contexto, volver)
    {
        var sesion = Sesion ?? throw new InvalidOperationException(DefHerramienta.SinPartida);
        Pokekeep.Mobile.Ui.Ajustes.Pkhex.TextosNucleo.Preparar();
        var sav = sesion.Guardado;
        _contexto = (int)sav.Context;
        _modo = sesion.ModoSprites;
        Filas = ModeloEstadisticasBase.Filas(sav, sesion.Textos, GameInfo.GenderSymbolUnicode);
        List<ColumnaTabla> columnas =
        [
            new("numero", "N.º", o => ((FilaEstadistica)o).Numero, o => ((FilaEstadistica)o).Clave),
            new("nombre", IdiomaInterfaz.T("EstadisticasBase.Nombre"), o => ((FilaEstadistica)o).Nombre),
            new("nativo", IdiomaInterfaz.T("EstadisticasBase.Nativo"), o => ((FilaEstadistica)o).Nativo ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No"), Visible: false),
            new("bst", "Total", o => Tres(((FilaEstadistica)o).Bst), o => ((FilaEstadistica)o).Bst),
            new("captura", IdiomaInterfaz.T("Catalogo.RatioCaptura"), o => Tres(((FilaEstadistica)o).Captura), o => ((FilaEstadistica)o).Captura),
            new("tipo1", IdiomaInterfaz.T("EstadisticasBase.Tipo"), o => ((FilaEstadistica)o).NombreTipo1),
            new("tipo2", IdiomaInterfaz.T("EstadisticasBase.Tipo2"), o => ((FilaEstadistica)o).NombreTipo2),
            new("ps", IdiomaInterfaz.T("Informe.Columna.HP"), o => Tres(((FilaEstadistica)o).Ps), o => ((FilaEstadistica)o).Ps),
            new("ataque", IdiomaInterfaz.T("Informe.Columna.ATK"), o => Tres(((FilaEstadistica)o).Ataque), o => ((FilaEstadistica)o).Ataque),
            new("defensa", IdiomaInterfaz.T("Informe.Columna.DEF"), o => Tres(((FilaEstadistica)o).Defensa), o => ((FilaEstadistica)o).Defensa),
            new("ataque-esp", IdiomaInterfaz.T("EstadisticasBase.AtaqueEspecial"), o => Tres(((FilaEstadistica)o).AtaqueEspecial), o => ((FilaEstadistica)o).AtaqueEspecial),
            new("defensa-esp", IdiomaInterfaz.T("EstadisticasBase.DefensaEspecial"), o => Tres(((FilaEstadistica)o).DefensaEspecial), o => ((FilaEstadistica)o).DefensaEspecial),
            new("velocidad", IdiomaInterfaz.T("Informe.Columna.SPE"), o => Tres(((FilaEstadistica)o).Velocidad), o => ((FilaEstadistica)o).Velocidad),
            new("hab1", IdiomaInterfaz.T("EstadisticasBase.Habilidad"), o => ((FilaEstadistica)o).Habilidad1),
            new("hab2", IdiomaInterfaz.T("EstadisticasBase.Habilidad2"), o => ((FilaEstadistica)o).Habilidad2),
            new("hab-oculta", IdiomaInterfaz.T("EstadisticasBase.HabilidadOculta"), o => ((FilaEstadistica)o).HabilidadOculta),
        ];
        if (Filas.Any(f => f.Alfa is not null)) columnas.Add(new("alfa", IdiomaInterfaz.T("EstadisticasBase.MovimientoAlfa"), o => ((FilaEstadistica)o).Alfa ?? string.Empty));
        Tabla = new TablaResultadosViewModel(columnas);
        Tabla.SolicitarOrden = h => Contexto.AbrirHoja(h);
        Tabla.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TablaResultadosViewModel.Filas)) Reconstruir(); };
        Tabla.Establecer(Filas);
        Reconstruir();
        Estado = IdiomaInterfaz.F("EstadisticasBase.EspeciesFormasJuego", Filas.Count);
    }

    private readonly int _contexto;
    private readonly ModoSprites _modo;
    private static string Tres(int n) => n.ToString("000", CultureInfo.InvariantCulture);

    public override string Titulo => IdiomaInterfaz.T("CatalogoHerramientas.EstadisticasBase");
    public string Subtitulo => IdiomaInterfaz.T("EstadisticasBase.TotalRatioCapturaTipos");

    /// <summary>Todas las filas del modelo (las de KChart), antes de filtrar.</summary>
    public IReadOnlyList<FilaEstadistica> Filas { get; }
    public TablaResultadosViewModel Tabla { get; }

    /// <summary>Lo que se ve ahora (la página actual de la tabla, ya filtrada y ordenada).</summary>
    [ObservableProperty] private IReadOnlyList<FilaEstadisticaVm> _visibles = [];

    private void Reconstruir() => Visibles = [.. Tabla.Filas.Select(f => new FilaEstadisticaVm((FilaEstadistica)f.Origen, _contexto, _modo))];
}
