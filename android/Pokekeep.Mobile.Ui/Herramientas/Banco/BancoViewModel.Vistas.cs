// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Herramientas.Banco;

/// <summary>Un grupo de repetidos listo para pintar, con lo que se puede hacer con él.</summary>
public sealed class GrupoDuplicadosVm(GrupoDuplicados grupo, string titulo, string explicacion, IReadOnlyList<FilaBancoVm> filas, int sobrantes, ICommand? enviarSobrantes, ICommand? compararVersiones)
{
    public GrupoDuplicados Grupo { get; } = grupo;
    public string Titulo { get; } = titulo;
    public string Explicacion { get; } = explicacion;
    public IReadOnlyList<FilaBancoVm> Filas { get; } = filas;
    public int Sobrantes { get; } = sobrantes;
    public bool Identicos => Grupo.Identicos;
    public bool Versiones => !Grupo.Identicos;
    public ICommand? EnviarSobrantes { get; } = enviarSobrantes;
    public ICommand? CompararVersiones { get; } = compararVersiones;
    public string TextoEnviar => TextosBanco.EnviarSobrantes(Sobrantes);
    public string AccesibleEnviar => TextosBanco.EnviarSobrantesAccesible(Titulo, Sobrantes);
    public string AccesibleComparar => TextosBanco.CompararVersionesAccesible(Titulo);
}

/// <summary>Una fila de progreso por generación.</summary>
public sealed partial class FilaProgresoVm : ObservableObject
{
    public FilaProgresoVm(FilaProgresoGeneracion f, GameStrings t)
    {
        Datos = f;
        Titulo = TextosBanco.GeneracionN(f.Generacion);
        Normales = TextosBanco.Fraccion(f.Normales, f.Total);
        Variocolor = TextosBanco.Fraccion(f.Variocolor, f.Total);
        PorcentajeNormales = f.FraccionNormales * 100;
        PorcentajeVariocolor = f.FraccionVariocolor * 100;
        TextoFaltan = f.Faltan.Count == 0 ? string.Empty : string.Join(", ", f.Faltan.Select(s => s < t.Species.Count ? t.Species[s] : "#" + s));
        Accesible = TextosBanco.GeneracionAccesible(f.Generacion, f.Normales, f.Variocolor, f.Total);
    }

    public FilaProgresoGeneracion Datos { get; }
    public string Titulo { get; }
    public string Normales { get; }
    public string Variocolor { get; }
    public double PorcentajeNormales { get; }
    public double PorcentajeVariocolor { get; }
    public string TextoFaltan { get; }
    public bool HayFaltan => TextoFaltan.Length > 0;
    public string Accesible { get; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(TextoVerFaltan))] private bool _faltanAbiertas;
    public string TextoVerFaltan => FaltanAbiertas ? TextosBanco.OcultarFaltan : TextosBanco.VerFaltan(Datos.Faltan.Count);
    [RelayCommand] private void AlternarFaltan() => FaltanAbiertas = !FaltanAbiertas;
}

public sealed record GrupoComparacionVm(string Titulo, IReadOnlyList<FilaComparacion> Filas);

public sealed partial class BancoViewModel
{
    // ---------------------------------------------------------------------------------------------------------------- duplicados (M3)
    public ObservableCollection<GrupoDuplicadosVm> Grupos { get; } = [];
    public bool HayGrupos => Grupos.Count > 0;
    public bool SinGrupos => Grupos.Count == 0;
    public int SobrantesTotales => Grupos.Where(g => g.Identicos).Sum(g => g.Sobrantes);
    public bool HaySobrantes => SobrantesTotales > 0;
    public string TextoEnviarTodos => TextosBanco.EnviarSobrantes(SobrantesTotales);

    private void RecalcularDuplicados()
    {
        Grupos.Clear();
        var t = Textos;
        foreach (var g in ModeloDuplicados.Buscar(Banco.Entradas))
        {
            var primero = g.Entradas[0];
            var filas = g.Entradas.Select(Fila).ToList();
            if (g.Identicos)
            {
                var sobrantes = ModeloDuplicados.Sobrantes(g);
                Grupos.Add(new GrupoDuplicadosVm(g, TextosBanco.TituloIdenticos(primero.Titulo(t), g.Entradas.Count), TextosBanco.ExplicacionIdenticos, filas, sobrantes.Count,
                    new RelayCommand(() => EnviarSobrantes(sobrantes)), null));
            }
            else
            {
                var par = new[] { g.Entradas[0], g.Entradas[^1] };
                Grupos.Add(new GrupoDuplicadosVm(g, TextosBanco.TituloVersiones(primero.Titulo(t), g.Entradas.Count), TextosBanco.ExplicacionVersiones, filas, 0, null,
                    new RelayCommand(() => CompararEntradas(par[0], par[1]))));
            }
        }
        foreach (var n in new[] { nameof(HayGrupos), nameof(SinGrupos), nameof(SobrantesTotales), nameof(HaySobrantes), nameof(TextoEnviarTodos), nameof(TextoModoDuplicados) }) OnPropertyChanged(n);
    }

    private void EnviarSobrantes(IReadOnlyList<EntradaBanco> sobrantes)
    {
        if (sobrantes.Count == 0) return;
        if (Banco.AlaPapelera(sobrantes.Select(e => e.Id)) is { } fallo) { Avisar(TextosBanco.FalloAlGuardar(fallo), false); return; }
        Refrescar();
        Avisar(TextosBanco.SobrantesEnviados(sobrantes.Count), true);
    }

    /// <summary>Manda a la papelera las copias idénticas que sobran de todos los grupos (se conserva una de cada grupo: la favorita o, si no hay, la más antigua).</summary>
    [RelayCommand]
    public void EnviarTodosLosSobrantes()
    {
        var todos = Grupos.Where(g => g.Identicos).SelectMany(g => ModeloDuplicados.Sobrantes(g.Grupo)).ToList();
        if (todos.Count == 0) { Avisar(TextosBanco.SinSobrantes, false); return; }
        EnviarSobrantes(todos);
    }

    // ---------------------------------------------------------------------------------------------------------------- progreso (M4)
    [ObservableProperty] private ProgresoBanco _progreso = new(0, 0, 0, 0, 0, 0, []);
    public ObservableCollection<FilaProgresoVm> FilasProgreso { get; } = [];
    public bool HayEnProgreso => Banco.CantidadActivas > 0;
    public string TextoEspecies => TextosBanco.Fraccion(Progreso.Especies, Progreso.TotalEspecies);
    public string TextoEspeciesVariocolor => TextosBanco.Fraccion(Progreso.EspeciesVariocolor, Progreso.TotalEspecies);
    public string TextoFormas => TextosBanco.Fraccion(Progreso.Formas, Progreso.TotalFormas);
    public string TextoFormasVariocolor => TextosBanco.Fraccion(Progreso.FormasVariocolor, Progreso.TotalFormas);
    public double PorcentajeEspecies => Progreso.FraccionEspecies * 100;
    public double PorcentajeEspeciesVariocolor => Progreso.FraccionVariocolor * 100;
    public double PorcentajeFormas => Progreso.FraccionFormas * 100;
    public double PorcentajeFormasVariocolor => Progreso.FraccionFormasVariocolor * 100;
    public string AccesibleEspecies => TextosBanco.ProgresoAccesible(TextosBanco.EspeciesTitulo, Progreso.Especies, Progreso.TotalEspecies);
    public string AccesibleEspeciesVariocolor => TextosBanco.ProgresoAccesible(TextosBanco.EspeciesVariocolorTitulo, Progreso.EspeciesVariocolor, Progreso.TotalEspecies);
    public string AccesibleFormas => TextosBanco.ProgresoAccesible(TextosBanco.FormasTitulo, Progreso.Formas, Progreso.TotalFormas);
    public string AccesibleFormasVariocolor => TextosBanco.ProgresoAccesible(TextosBanco.FormasVariocolorTitulo, Progreso.FormasVariocolor, Progreso.TotalFormas);

    private void RefrescarProgreso()
    {
        FilasProgreso.Clear();
        foreach (var g in Progreso.Generaciones) FilasProgreso.Add(new FilaProgresoVm(g, Textos));
        foreach (var n in new[] { nameof(HayEnProgreso), nameof(TextoEspecies), nameof(TextoEspeciesVariocolor), nameof(TextoFormas), nameof(TextoFormasVariocolor), nameof(PorcentajeEspecies), nameof(PorcentajeEspeciesVariocolor), nameof(PorcentajeFormas), nameof(PorcentajeFormasVariocolor), nameof(AccesibleEspecies), nameof(AccesibleEspeciesVariocolor), nameof(AccesibleFormas), nameof(AccesibleFormasVariocolor) }) OnPropertyChanged(n);
    }

    // ---------------------------------------------------------------------------------------------------------------- comparar (M3)
    public ObservableCollection<GrupoComparacionVm> Comparacion { get; } = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(NoComparando))] private bool _comparando;
    [ObservableProperty] private string _tituloComparacion = string.Empty;
    [ObservableProperty] private string _nombreA = string.Empty;
    [ObservableProperty] private string _nombreB = string.Empty;
    [ObservableProperty] private string _resumenComparacion = string.Empty;
    public bool NoComparando => !Comparando;

    [RelayCommand(CanExecute = nameof(PuedeComparar))]
    public void Comparar()
    {
        var e = Elegidas;
        if (e.Count != 2) { Avisar(TextosBanco.CompararNecesitaDos, false); return; }
        CompararEntradas(e[0], e[1]);
    }

    private void CompararEntradas(EntradaBanco a, EntradaBanco b)
    {
        var la = Banco.Leer(a.Id); var lb = Banco.Leer(b.Id);
        if (la.Pokemon is null || lb.Pokemon is null) { Avisar(la.Error ?? lb.Error ?? TextosBanco.ArchivoIlegible(a.Archivo), false); return; }
        var t = Textos;
        var filas = ModeloComparacion.Comparar(la.Pokemon, lb.Pokemon, t);
        Comparacion.Clear();
        foreach (var g in filas.GroupBy(f => f.Grupo)) Comparacion.Add(new GrupoComparacionVm(g.Key, [.. g]));
        NombreA = a.Titulo(t); NombreB = b.Titulo(t);
        TituloComparacion = TextosBanco.TituloComparacion(NombreA, NombreB);
        ResumenComparacion = TextosBanco.ResumenComparacion(filas.Count(f => f.Difiere), filas.Count);
        Comparando = true;
    }

    [RelayCommand]
    public void CerrarComparacion() => SalirDeComparar();

    private void SalirDeComparar()
    {
        if (!Comparando) return;
        Comparando = false; Comparacion.Clear();
    }
}
