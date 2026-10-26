// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.Encuentros;

/// <summary>Una fila de la pestaña «Criterios»: un valor elegido de una lista (sexo, habilidad, naturaleza…) o un número (IV, nivel, forma).</summary>
public abstract partial class FilaCriterio(string id, string titulo, string descripcion) : ObservableObject
{
    public string Id { get; } = id;
    public string Titulo { get; } = titulo;
    public string Descripcion { get; } = descripcion;
    public abstract string ValorTexto { get; }
    public string Accesible => $"{Titulo}: {ValorTexto}";
    internal void Avisar() { OnPropertyChanged(nameof(ValorTexto)); OnPropertyChanged(nameof(Accesible)); }
}

/// <summary>Fila de opción: abre un selector con las opciones (las de PKHeX, en castellano).</summary>
public sealed partial class FilaCriterioOpcion(string id, string titulo, string descripcion, IReadOnlyList<OpcionSelector> opciones, Func<int> leer, Action<int> escribir, Action<HojaViewModel> abrirHoja)
    : FilaCriterio(id, titulo, descripcion)
{
    public IReadOnlyList<OpcionSelector> Opciones { get; } = opciones;
    public int Valor => leer();
    public override string ValorTexto => Opciones.FirstOrDefault(o => o.Id == leer())?.Texto ?? leer().ToString(System.Globalization.CultureInfo.InvariantCulture);
    public void Elegir(int valor) { escribir(valor); Avisar(); }

    [RelayCommand]
    private void Abrir() => abrirHoja(new SelectorHojaViewModel(Titulo, Opciones, leer(), o => Elegir(o.Id)));
}

/// <summary>Fila numérica con su rango (lo que admite el campo del Core); un texto fuera de rango o que no es un número se rechaza con su motivo y el valor no cambia.</summary>
public sealed partial class FilaCriterioNumero(string id, string titulo, string descripcion, int minimo, int maximo, Func<int> leer, Action<int> escribir, string? textoMinimo = null)
    : FilaCriterio(id, titulo, descripcion)
{
    public int Minimo { get; } = minimo;
    public int Maximo { get; } = maximo;
    public int Valor => leer();
    public override string ValorTexto => textoMinimo is not null && leer() == Minimo ? textoMinimo : leer().ToString(System.Globalization.CultureInfo.InvariantCulture);
    [ObservableProperty] private string _texto = string.Empty;
    [ObservableProperty] private string _error = string.Empty;
    public bool HayError => Error.Length > 0;
    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HayError));

    internal void Sincronizar() { Texto = leer().ToString(System.Globalization.CultureInfo.InvariantCulture); Error = string.Empty; Avisar(); }

    /// <summary>Confirma el texto escrito: true si se aceptó.</summary>
    public bool Confirmar()
    {
        if (!int.TryParse(Texto.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var v) || v < Minimo || v > Maximo)
        {
            Error = IdiomaInterfaz.F("CriteriosEditables.EscribeNumeroEntre", Minimo, Maximo);
            return false;
        }
        escribir(v); Sincronizar();
        return true;
    }

    [RelayCommand] private void Aplicar() => Confirmar();
}

/// <summary>Un cambio permitido (<c>EncounterMutation</c>) como interruptor.</summary>
public sealed partial class FilaMutacion(EncounterMutation bandera, string texto, Func<EncounterMutation> leer, Action<EncounterMutation> escribir) : ObservableObject
{
    public EncounterMutation Bandera { get; } = bandera;
    public string Texto { get; } = texto;
    public bool Activa { get => (leer() & Bandera) == Bandera; set { escribir(value ? leer() | Bandera : leer() & ~Bandera); OnPropertyChanged(); } }
    internal void Avisar() => OnPropertyChanged(nameof(Activa));
}

/// <summary>
/// La pestaña «Criterios» de <c>SAV_Encounters</c> (PK-SUB-013): el <c>PropertyGrid</c> de <c>EncounterCriteria</c> como filas del móvil, con TODAS sus propiedades editables (sexo, habilidad, naturaleza, variocolor, los
/// seis IV, nivel mínimo y máximo, tipo de Poder Oculto, forma y cambios permitidos; <c>Shiny.FixedValue</c> no sale porque PKHeX lo oculta con <c>[Browsable(false)]</c>), «Reiniciar» (<c>EncounterCriteria.Unrestricted</c>)
/// y «Del editor» (<c>BuildCriteriaFromTabs</c> con el Pokémon de referencia). Las etiquetas son las de <c>lang_es.txt</c> (<c>PropertyGrid.*</c>) donde existen.
/// </summary>
public sealed partial class CriteriosEditables : ObservableObject
{
    private EncounterCriteria _valor = EncounterCriteria.Unrestricted;

    public CriteriosEditables(GameStrings textos, Action<HojaViewModel> abrirHoja)
    {
        FilaCriterioOpcion Op(string id, string t, string d, IReadOnlyList<OpcionSelector> o, Func<EncounterCriteria, int> l, Func<EncounterCriteria, int, EncounterCriteria> e)
            => new(id, t, d, o, () => l(_valor), v => Cambiar(e(_valor, v)), abrirHoja);
        FilaCriterioNumero Num(string id, string t, string d, int min, int max, Func<EncounterCriteria, int> l, Func<EncounterCriteria, int, EncounterCriteria> e, string? tmin = null)
            => new(id, t, d, min, max, () => l(_valor), v => Cambiar(e(_valor, v)), tmin);
        var IvAzar = IdiomaInterfaz.T("CriteriosEditables.Azar");
        Opciones =
        [
            Op("sexo", IdiomaInterfaz.T("Catalogo.Sexo"), IdiomaInterfaz.T("CriteriosEditables.SexoPokemonGenerado"), [new((int)Gender.Random, IdiomaInterfaz.T("ModeloLotes.Cualquiera")), new((int)Gender.Male, IdiomaInterfaz.T("Form.Trainer8.Male")), new((int)Gender.Female, IdiomaInterfaz.T("Form.Trainer8.Female")), new((int)Gender.Genderless, IdiomaInterfaz.T("Form.Trainer8.Genderless"))],
                c => (int)c.Gender, (c, v) => c with { Gender = (Gender)v }),
            Op("habilidad", IdiomaInterfaz.T("Informe.Columna.Ability"), IdiomaInterfaz.T("CriteriosEditables.HabilidadesAdmiten"), [new((int)AbilityPermission.Any12H, IdiomaInterfaz.T("CriteriosEditables.CualquieraUOculta")), new((int)AbilityPermission.Any12, IdiomaInterfaz.T("CriteriosEditables.LaOLa")),
                new((int)AbilityPermission.OnlyFirst, IdiomaInterfaz.T("CriteriosEditables.Solo")), new((int)AbilityPermission.OnlySecond, IdiomaInterfaz.T("CriteriosEditables.Solo2")), new((int)AbilityPermission.OnlyHidden, IdiomaInterfaz.T("CriteriosEditables.SoloOculta"))],
                c => (int)c.Ability, (c, v) => c with { Ability = (AbilityPermission)v }),
            Op("naturaleza", IdiomaInterfaz.T("Informe.Columna.Nature"), IdiomaInterfaz.T("CriteriosEditables.NaturalezaPokemonGenerado"), [new((int)Nature.Random, IdiomaInterfaz.T("ModeloLotes.Cualquiera")), .. Enumerable.Range(0, 25).Select(i => new OpcionSelector(i, textos.natures[i]))],
                c => (int)c.Nature, (c, v) => c with { Nature = (Nature)v }),
            Op("variocolor", IdiomaInterfaz.T("Catalogo.Variocolor"), IdiomaInterfaz.T("CriteriosEditables.PokemonGeneradoVariocolor"), [new((int)Shiny.Random, IdiomaInterfaz.T("CriteriosEditables.Azar")), new((int)Shiny.Never, IdiomaInterfaz.T("CriteriosEditables.Nunca")), new((int)Shiny.Always, IdiomaInterfaz.T("CriteriosEditables.Siempre")),
                new((int)Shiny.AlwaysStar, IdiomaInterfaz.T("CriteriosEditables.SiempreEstrellas")), new((int)Shiny.AlwaysSquare, IdiomaInterfaz.T("CriteriosEditables.SiempreCuadros"))],
                c => (int)c.Shiny, (c, v) => c with { Shiny = (Shiny)v }),
            Op("poder-oculto", IdiomaInterfaz.T("Informe.Columna.HP_Type"), IdiomaInterfaz.T("CriteriosEditables.TipoPoderOcultoDeben"), [new(-1, IdiomaInterfaz.T("ModeloLotes.Cualquiera")), .. Enumerable.Range(0, 16).Select(i => new OpcionSelector(i, textos.types[i + 1]))],
                c => c.HiddenPowerType, (c, v) => c with { HiddenPowerType = (sbyte)v }),
        ];
        Numeros =
        [
            Num("iv-ps", "IV PS", IdiomaInterfaz.T("CriteriosEditables.Azar2"), -1, 31, c => c.IV_HP, (c, v) => c with { IV_HP = (sbyte)v }, IvAzar),
            Num("iv-ataque", IdiomaInterfaz.T("CriteriosEditables.IVAtaque"), IdiomaInterfaz.T("CriteriosEditables.Azar2"), -1, 31, c => c.IV_ATK, (c, v) => c with { IV_ATK = (sbyte)v }, IvAzar),
            Num("iv-defensa", IdiomaInterfaz.T("CriteriosEditables.IVDefensa"), IdiomaInterfaz.T("CriteriosEditables.Azar2"), -1, 31, c => c.IV_DEF, (c, v) => c with { IV_DEF = (sbyte)v }, IvAzar),
            Num("iv-ataque-esp", IdiomaInterfaz.T("CriteriosEditables.IVAtEspecial"), IdiomaInterfaz.T("CriteriosEditables.Azar2"), -1, 31, c => c.IV_SPA, (c, v) => c with { IV_SPA = (sbyte)v }, IvAzar),
            Num("iv-defensa-esp", IdiomaInterfaz.T("CriteriosEditables.IVDefEspecial"), IdiomaInterfaz.T("CriteriosEditables.Azar2"), -1, 31, c => c.IV_SPD, (c, v) => c with { IV_SPD = (sbyte)v }, IvAzar),
            Num("iv-velocidad", IdiomaInterfaz.T("CriteriosEditables.IVVelocidad"), IdiomaInterfaz.T("CriteriosEditables.Azar2"), -1, 31, c => c.IV_SPE, (c, v) => c with { IV_SPE = (sbyte)v }, IvAzar),
            Num("nivel-min", IdiomaInterfaz.T("CriteriosEditables.NivelMinimo"), IdiomaInterfaz.T("CriteriosEditables.Encuentro"), 0, 100, c => c.LevelMin, (c, v) => c with { LevelMin = (byte)v }),
            Num("nivel-max", IdiomaInterfaz.T("MenuCaja.NivelMaximo"), IdiomaInterfaz.T("CriteriosEditables.Encuentro"), 0, 100, c => c.LevelMax, (c, v) => c with { LevelMax = (byte)v }),
            Num("forma", IdiomaInterfaz.T("Catalogo.Forma"), IdiomaInterfaz.T("CriteriosEditables.FormaCuandoEncuentroSortea"), -1, 127, c => c.Form, (c, v) => c with { Form = (sbyte)v }, IvAzar),
        ];
        Mutaciones =
        [
            new(EncounterMutation.CanMintNature, IdiomaInterfaz.T("CriteriosEditables.PuedeCambiarNaturalezaMenta"), () => _valor.Mutations, m => Cambiar(_valor with { Mutations = m })),
            new(EncounterMutation.CanMaxIndividualStat, IdiomaInterfaz.T("CriteriosEditables.PuedenSubirIVEntrenamiento"), () => _valor.Mutations, m => Cambiar(_valor with { Mutations = m })),
            new(EncounterMutation.CanAbilityCapsule, IdiomaInterfaz.T("CriteriosEditables.PuedeCambiarHabilidadCapsula"), () => _valor.Mutations, m => Cambiar(_valor with { Mutations = m })),
            new(EncounterMutation.CanAbilityPatch, IdiomaInterfaz.T("CriteriosEditables.PuedeCambiarHabilidadParche"), () => _valor.Mutations, m => Cambiar(_valor with { Mutations = m })),
            new(EncounterMutation.AllowOnlyNeutralNature, IdiomaInterfaz.T("CriteriosEditables.SoloNaturalezasNeutras"), () => _valor.Mutations, m => Cambiar(_valor with { Mutations = m })),
        ];
        Sincronizar();
    }

    public IReadOnlyList<FilaCriterioOpcion> Opciones { get; }
    public IReadOnlyList<FilaCriterioNumero> Numeros { get; }
    public IReadOnlyList<FilaMutacion> Mutaciones { get; }

    /// <summary>El valor actual (lo que PKHeX tiene en <c>_criteriaValue</c>).</summary>
    public EncounterCriteria Valor => _valor;

    public void Establecer(EncounterCriteria valor) { _valor = valor; Sincronizar(); }

    private void Cambiar(EncounterCriteria nuevo) { _valor = nuevo; OnPropertyChanged(nameof(Valor)); }

    private void Sincronizar()
    {
        foreach (var o in Opciones) o.Avisar();
        foreach (var n in Numeros) n.Sincronizar();
        foreach (var m in Mutaciones) m.Avisar();
        OnPropertyChanged(nameof(Valor));
    }
}
