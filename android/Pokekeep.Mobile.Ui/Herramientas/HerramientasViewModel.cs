// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas;

/// <summary>Una fila del concentrador: la herramienta y, si no está disponible, por qué (nunca un botón muerto sin explicación).</summary>
public sealed class EntradaHerramienta(DefHerramienta def, string? motivo, ICommand? abrir)
{
    public DefHerramienta Def { get; } = def;
    public string Id => Def.Id;
    public string Titulo => Def.Titulo;
    public string Glifo => Def.Glifo;
    public string? Motivo { get; } = motivo;
    public string Linea => Motivo ?? Def.Descripcion;
    public bool Disponible => Motivo is null;
    public bool NoDisponible => Motivo is not null;
    public ICommand? Abrir { get; } = abrir;
    public string Accesible => Motivo is null ? $"{Titulo}. {Def.Descripcion}" : IdiomaInterfaz.F("Herramientas.Disponible", Titulo, Motivo);
}

public sealed record GrupoHerramientas(string Nombre, IReadOnlyList<EntradaHerramienta> Entradas);

/// <summary>
/// «Más &gt; Herramientas»: el concentrador de las herramientas de PKHeX (Menú «Herramientas» + botones de la pestaña SAV que no son editores de partida), agrupadas por categoría. Se alimenta de
/// <see cref="CatalogoHerramientas"/> (cada bloque añade las suyas); una herramienta que necesita partida y no la hay sale desactivada con su motivo.
/// </summary>
public sealed partial class HerramientasViewModel : PantallaMasViewModel
{
    private readonly ContextoHerramienta _contexto;

    public HerramientasViewModel(ContextoHerramienta contexto, Action volver, IEnumerable<DefHerramienta>? catalogo = null) : base(volver)
    {
        _contexto = contexto;
        var todas = (catalogo ?? CatalogoHerramientas.Todas).ToList();
        Grupos = [.. CatalogoHerramientas.Categorias.Concat(todas.Select(t => t.Categoria)).Distinct()
            .Select(c => new GrupoHerramientas(c, [.. todas.Where(t => t.Categoria == c).Select(Entrada)])).Where(g => g.Entradas.Count > 0)];
    }

    private EntradaHerramienta Entrada(DefHerramienta def)
    {
        var motivo = def.Motivo(_contexto.Sesion);
        return new EntradaHerramienta(def, motivo, motivo is null ? new RelayCommand(() => Abrir(def)) : null);
    }

    public override string Titulo => IdiomaInterfaz.T("Menu.Mas.Herramientas.Titulo");
    public string Subtitulo => IdiomaInterfaz.T("Herramientas.HerramientasPKHeXBuscarComprobar");
    public IReadOnlyList<GrupoHerramientas> Grupos { get; }
    public bool HayHerramientas => Grupos.Count > 0;
    public bool NoHayHerramientas => Grupos.Count == 0;
    public string TextoVacio => IdiomaInterfaz.T("Herramientas.TodaviaHerramientasVersion");

    /// <summary>Abre una herramienta; su «Volver» regresa a este concentrador.</summary>
    public void Abrir(DefHerramienta def) => _contexto.Abrir(def.Crear(_contexto, () => _contexto.Abrir(this)));
}

/// <summary>Donde cada bloque registra la vista de su pantalla: <c>r.Registrar&lt;MiViewModel&gt;(vm =&gt; new MiView { DataContext = vm })</c>.</summary>
public sealed class RegistroVistas
{
    internal Dictionary<Type, Func<object, Control>> Mapa { get; } = [];
    public void Registrar<TVm>(Func<TVm, Control> crear) where TVm : class => Mapa[typeof(TVm)] = o => crear((TVm)o);
}

/// <summary>
/// Dispatcher de vistas de «Más» que viven fuera de <c>ShellView.axaml</c>: el XAML de la Shell lleva UNA línea con esta plantilla y cada bloque registra aquí la vista de su herramienta
/// (<c>VistasHerramientas.B10_N.cs</c>, método <c>RegistrarB10_N</c>) sin tocar la Shell, así que dos bloques en paralelo no se pisan el XAML.
/// </summary>
public sealed partial class VistasHerramientas : IDataTemplate
{
    private static readonly Lazy<IReadOnlyDictionary<Type, Func<object, Control>>> _vistas = new(() =>
    {
        var r = new RegistroVistas();
        r.Registrar<HerramientasViewModel>(vm => new Views.HerramientasView { DataContext = vm });
        RegistrarPiloto(r);
        RegistrarB10_1(r); RegistrarB10_2(r); RegistrarB10_3(r); RegistrarB10_4(r); RegistrarB10_5(r); RegistrarB10_6(r); RegistrarB10_7(r);
        RegistrarB10_8(r); RegistrarB10_9(r); RegistrarB10_10(r); RegistrarB10_11(r); RegistrarB10_12(r); RegistrarB10_13(r);
        RegistrarX37(r);
        RegistrarX38(r);
        return r.Mapa;
    });

    public static bool TieneVista(Type tipoVm) => _vistas.Value.ContainsKey(tipoVm);

    /// <summary>Los tipos de pantalla que tienen vista registrada (para la prueba de que no falta ninguna).</summary>
    public static IReadOnlyCollection<Type> Registradas => [.. _vistas.Value.Keys];

    public bool Match(object? data) => data is not null && _vistas.Value.ContainsKey(data.GetType());

    public Control? Build(object? param)
    {
        if (param is null || !_vistas.Value.TryGetValue(param.GetType(), out var crear)) return null;
        return crear(param);
    }

    static partial void RegistrarPiloto(RegistroVistas r);
    static partial void RegistrarB10_1(RegistroVistas r);
    static partial void RegistrarB10_2(RegistroVistas r);
    static partial void RegistrarB10_3(RegistroVistas r);
    static partial void RegistrarB10_4(RegistroVistas r);
    static partial void RegistrarB10_5(RegistroVistas r);
    static partial void RegistrarB10_6(RegistroVistas r);
    static partial void RegistrarB10_7(RegistroVistas r);
    static partial void RegistrarB10_8(RegistroVistas r);
    static partial void RegistrarB10_9(RegistroVistas r);
    static partial void RegistrarB10_10(RegistroVistas r);
    static partial void RegistrarB10_11(RegistroVistas r);
    static partial void RegistrarB10_12(RegistroVistas r);
    static partial void RegistrarB10_13(RegistroVistas r);
    static partial void RegistrarX37(RegistroVistas r);
    static partial void RegistrarX38(RegistroVistas r);
}
