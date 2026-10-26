// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Editor;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>
/// Un campo del editor en pantalla: envuelve un <see cref="CampoDef"/> y lo escribe por el <see cref="EditorViewModel"/> (que lo lleva al borrador por el modelo y refresca los demas).
/// Los de texto libre y numeros se aplican al escribir; hexadecimal, decimales y fechas, al salir del campo (o al aplicar), para no dejar valores a medias.
/// </summary>
public sealed partial class CampoVm : ObservableObject
{
    private readonly EditorViewModel _ed;
    private bool _releyendo;

    public CampoVm(EditorViewModel editor, CampoDef def)
    {
        _ed = editor; Def = def;
        Releer();
    }

    public CampoDef Def { get; }
    public string Clave => Def.Clave;
    public TipoCampo Tipo => Def.Tipo;
    public bool EnFoco { get; set; }
    /// <summary>Hay texto escrito que todavia no se ha aplicado (se aplica al salir del campo o al pulsar Aplicar).</summary>
    public bool Pendiente { get; private set; }

    [ObservableProperty] private string _etiqueta = string.Empty;
    [ObservableProperty] private string _valor = string.Empty;
    [ObservableProperty] private bool _marcado;
    [ObservableProperty] private double _numero;
    [ObservableProperty] private double _maximo = 100;
    [ObservableProperty] private string? _motivo;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _detalle;
    [ObservableProperty] private bool _visible = true;

    // ---- Que plantilla pintar ----
    public bool EsCampoNumerico => Def.Tipo is TipoCampo.Entero or TipoCampo.Decimal;
    public bool EsEnteroSinSigno => Def.Tipo == TipoCampo.Entero && Def.Min >= 0;
    public bool EsNumeroConSigno => Def.Tipo == TipoCampo.Decimal || (Def.Tipo == TipoCampo.Entero && Def.Min < 0);
    public bool EsTextoLibre => Def.Tipo == TipoCampo.Texto;
    public bool EsCampoEnLinea => Def.Tipo is TipoCampo.Hex or TipoCampo.Fecha or TipoCampo.FechaHora;
    public int LongitudMaxima
    {
        get
        {
            if (Def.MaxLongitud is { } f) return f(_ed.Modelo.Contexto);
            return Def.Tipo switch { TipoCampo.Hex => Def.Digitos, TipoCampo.Entero => 11, _ => 0 };
        }
    }
    public bool EsCampoTexto => Def.Tipo is TipoCampo.Texto or TipoCampo.Hex or TipoCampo.Fecha or TipoCampo.FechaHora;
    public bool EsOpcion => Def.Tipo == TipoCampo.Opcion;
    public bool SeleccionInvalida => EsOpcion && !_ed.Modelo.Opciones(Def).Any(o => o.Id == _ed.Modelo.IdActual(Def));
    public Avalonia.Media.IBrush ColorSeleccionInvalida => Ajustes.Pkhex.PresentacionPkhex.Pincel(Ajustes.Pkhex.ProgramaPkhex.Ajustes.Actual.Draw.InvalidSelection, "#e9967a");
    public bool HayColorMarca => Def.Grupo == "marca" && EsOpcion && _ed.Modelo.IdActual(Def) is 1 or 2;
    public Avalonia.Media.IBrush ColorMarca => _ed.Modelo.IdActual(Def) == 2
        ? Ajustes.Pkhex.PresentacionPkhex.Pincel(Ajustes.Pkhex.ProgramaPkhex.Ajustes.Actual.Draw.MarkPink, "#ff75b3")
        : Ajustes.Pkhex.PresentacionPkhex.Pincel(Ajustes.Pkhex.ProgramaPkhex.Ajustes.Actual.Draw.MarkBlue, "#00bfff");
    public void RefrescarColores()
    {
        OnPropertyChanged(nameof(SeleccionInvalida)); OnPropertyChanged(nameof(ColorSeleccionInvalida));
        OnPropertyChanged(nameof(HayColorMarca)); OnPropertyChanged(nameof(ColorMarca));
    }
    /// <summary>El campo es la especie del Pokemon: su fila lleva el sprite de la especie elegida.</summary>
    public int EspecieSprite => Def.Tipo == TipoCampo.Opcion && Def.Clave == "PK-ED-012" && _ed.Modelo.IdActual(Def) is var id && id > 0 && id < 2000 ? id : 0;
    public bool HayEspecieSprite => EspecieSprite > 0;
    /// <summary>Juego de la partida (X53): la fila de la especie lleva el icono de caja del propio juego cuando esta integrado.</summary>
    public int VersionJuego => _ed.VersionJuego;
    public bool EsCasilla => Def.Tipo == TipoCampo.Casilla;
    public bool EsInfo => Def.Tipo == TipoCampo.Info;
    public bool EsAccion => Def.Tipo == TipoCampo.Accion;
    public bool EsBarra => Def.Tipo == TipoCampo.Barra;
    public bool EsEditable => Motivo is null;
    public bool EsFijo => Motivo is not null && !EsInfo;
    public bool HayError => !string.IsNullOrEmpty(Error);
    public bool HayDetalle => !string.IsNullOrEmpty(Detalle);
    /// <summary>Un valor largo (una frase de recuerdo, un lugar con nombre largo) va debajo de la etiqueta, a todo el ancho, en lugar de al lado.</summary>
    public bool ValorLargo => (Valor?.Length ?? 0) > 24;
    public bool ValorCorto => !ValorLargo;
    public bool HayValorAccion => EsAccion && !string.IsNullOrEmpty(Valor);
    public string Marcador => Def.Tipo switch { TipoCampo.Fecha => IdiomaInterfaz.T("Form.MarcadorFechaCorta"), TipoCampo.FechaHora => IdiomaInterfaz.T("Form.MarcadorFecha"), TipoCampo.Hex => new string('0', Math.Max(1, Def.Digitos)), _ => string.Empty };
    public string TextoCasilla => Marcado ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No");
    public string Accesible => Def.Tipo switch
    {
        TipoCampo.Opcion => IdiomaInterfaz.F("EditorCamposVm.PulsaCambiar", Etiqueta, Valor),
        TipoCampo.Casilla => $"{Etiqueta}: {TextoCasilla}",
        TipoCampo.Info => $"{Etiqueta}: {Valor}",
        TipoCampo.Accion => Etiqueta,
        _ => Etiqueta,
    };
    public string Ayuda => Motivo ?? Def.Ayuda ?? string.Empty;

    partial void OnMotivoChanged(string? value) { OnPropertyChanged(nameof(EsEditable)); OnPropertyChanged(nameof(EsFijo)); OnPropertyChanged(nameof(Ayuda)); OnPropertyChanged(nameof(Accesible)); }
    partial void OnErrorChanged(string? value) { OnPropertyChanged(nameof(HayError)); RefrescarColores(); }
    partial void OnDetalleChanged(string? value) => OnPropertyChanged(nameof(HayDetalle));
    partial void OnEtiquetaChanged(string value) => OnPropertyChanged(nameof(Accesible));
    partial void OnMarcadoChanged(bool value) { OnPropertyChanged(nameof(TextoCasilla)); OnPropertyChanged(nameof(Accesible)); if (!_releyendo) _ed.EscribirMarca(this, value); }
    partial void OnValorChanged(string value)
    {
        RefrescarColores();
        OnPropertyChanged(nameof(Accesible)); OnPropertyChanged(nameof(HayValorAccion)); OnPropertyChanged(nameof(ValorLargo)); OnPropertyChanged(nameof(ValorCorto)); OnPropertyChanged(nameof(EspecieSprite)); OnPropertyChanged(nameof(HayEspecieSprite));
        if (_releyendo || EsOpcion || EsInfo || EsAccion) return;
        _ed.EscribirTexto(this, value, final: false);
    }

    partial void OnNumeroChanged(double value)
    {
        if (_releyendo || !EsBarra) return;
        _ed.EscribirTexto(this, ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture), final: true);
    }

    /// <summary>Marca el campo como pendiente (texto escrito sin aplicar).</summary>
    public void MarcarPendiente(bool p) => Pendiente = p;

    /// <summary>Vuelve a leer el valor del modelo (tras otra escritura). El campo que se esta escribiendo conserva su texto hasta que pierda el foco.</summary>
    public void Releer()
    {
        var m = _ed.Modelo;
        Visible = Def.EsVisible(m.Contexto);
        if (!Visible) return;
        _releyendo = true;
        try
        {
            Etiqueta = Def.TextoEtiqueta(m.Contexto);
            Motivo = m.Rechazo(Def);
            Detalle = Def.Detalle?.Invoke(m.Contexto);
            switch (Def.Tipo)
            {
                case TipoCampo.Casilla: Marcado = m.Marcado(Def); break;
                case TipoCampo.Barra:
                    Valor = m.Valor(Def);
                    Maximo = Def.MaximoEn(m.Contexto);
                    Numero = double.TryParse(Valor, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
                    break;
                default:
                    if (!(EnFoco || Pendiente) || EsOpcion || EsInfo || EsAccion) Valor = m.Valor(Def);
                    break;
            }
        }
        finally { _releyendo = false; }
    }

    /// <summary>Al salir del campo: aplica lo pendiente y muestra el valor normalizado del modelo.</summary>
    public void PerderFoco()
    {
        EnFoco = false;
        if (Pendiente) _ed.EscribirTexto(this, Valor, final: true);
        Pendiente = false;
        _releyendo = true;
        try { if (!EsOpcion && !EsInfo && !EsAccion && !EsCasilla && !EsBarra) Valor = _ed.Modelo.Valor(Def); }
        finally { _releyendo = false; }
    }

    public void PonerError(string? error) => Error = error;

    [RelayCommand] private void Elegir() => _ed.AbrirSelector(this);
    [RelayCommand] private void Ejecutar() => _ed.EjecutarAccion(this);
}

/// <summary>Una seccion de un editor o subeditor: titulo y sus campos.</summary>
public abstract partial class SeccionBaseVm : ObservableObject
{
    public string Titulo { get; init; } = string.Empty;
    public bool HayTitulo => !string.IsNullOrWhiteSpace(Titulo);
    [ObservableProperty] private bool _visible = true;
    public abstract IEnumerable<CampoVm> TodosLosCampos { get; }
    public virtual void Releer() { foreach (var c in TodosLosCampos) c.Releer(); Visible = TodosLosCampos.Any(c => c.Visible); }
}

public sealed class SeccionPlanaVm : SeccionBaseVm
{
    public ObservableCollection<CampoVm> Campos { get; } = [];
    public override IEnumerable<CampoVm> TodosLosCampos => Campos;
}

/// <summary>Una fila de la tabla de estadisticas: nombre con la senal de la naturaleza y sus campos (valor final, IV, EV, AV, GV, hiperentrenamiento).</summary>
public sealed partial class FilaStatVm(int indice, string nombre) : ObservableObject
{
    public int Indice { get; } = indice;
    public string Nombre { get; } = nombre;
    public CampoVm? Final { get; init; }
    public CampoVm? Iv { get; init; }
    public CampoVm? Ev { get; init; }
    public CampoVm? Av { get; init; }
    public CampoVm? Gv { get; init; }
    public CampoVm? Ht { get; init; }
    public CampoVm? Base { get; init; }
    [ObservableProperty] private string _senal = string.Empty;
    [ObservableProperty] private string _finalTexto = string.Empty;
    public string NombreAccesible => string.IsNullOrEmpty(Senal) ? Nombre : $"{Nombre}, {(Senal == "+" ? IdiomaInterfaz.T("EditorCamposVm.SubeNaturaleza") : IdiomaInterfaz.T("EditorCamposVm.BajaNaturaleza"))}";
    partial void OnSenalChanged(string value) => OnPropertyChanged(nameof(NombreAccesible));
    public string FinalAccesible => IdiomaInterfaz.F("Catalogo.ValorFinal", Nombre);
}

public sealed partial class SeccionStatsVm : SeccionBaseVm, Design.IConAnchoDisponible
{
    public ObservableCollection<FilaStatVm> Filas { get; } = [];
    public ObservableCollection<CampoVm> Resto { get; } = [];
    [ObservableProperty] private bool _hayEv, _hayAv, _hayGv, _hayHt, _hayFinalEditable;
    [ObservableProperty] private string _avisoEv = string.Empty;
    [ObservableProperty] private bool _hayAvisoEv;
    [ObservableProperty] private bool _apilada;
    /// <summary>Texto grande (x1,3 o mas): la tabla siempre se apila.</summary>
    [ObservableProperty] private bool _porEscala;
    /// <summary>Ancho real de la tarjeta (lo pone la vista con <c>d:AnchoDisponible.Medir</c>); 0 mientras no se ha medido.</summary>
    [ObservableProperty] private double _anchoDisponible;
    public bool NoApilada => !Apilada;
    partial void OnApiladaChanged(bool value) => OnPropertyChanged(nameof(NoApilada));
    public bool NoHayEv => !HayEv;
    partial void OnHayEvChanged(bool value) { OnPropertyChanged(nameof(NoHayEv)); Recalcular(); }
    partial void OnHayAvChanged(bool value) => Recalcular();
    partial void OnHayGvChanged(bool value) => Recalcular();
    partial void OnHayHtChanged(bool value) => Recalcular();
    partial void OnPorEscalaChanged(bool value) => Recalcular();
    partial void OnAnchoDisponibleChanged(double value) => Recalcular();

    /// <summary>Lo que ocupa la tabla en una fila: el nombre mas largo con su senal («Def. especial −», ~94 dp a TamDato, con margen) y cada
    /// columna visible con su separacion de 6. Si no cabe, la tabla se apila (F5: con IV, EV y HT o GV el nombre pisaba el valor final en 384 dp).</summary>
    public double AnchoNecesario => 104 + 6 + 48 + (Filas.Any(f => f.Iv?.Visible == true) ? 62 : 0) + (HayEv ? 74 : 0) + (HayAv ? 62 : 0) + (HayGv ? 58 : 0) + (HayHt ? 54 : 0);
    internal void Recalcular() => Apilada = PorEscala || (AnchoDisponible > 0 && AnchoDisponible < AnchoNecesario);
    public override IEnumerable<CampoVm> TodosLosCampos => Filas.SelectMany(f => new[] { f.Final, f.Iv, f.Ev, f.Av, f.Gv, f.Ht }).OfType<CampoVm>().Concat(Resto);
}

/// <summary>Una fila de movimiento: el movimiento (se elige de la lista), sus PP y sus PP Ups.</summary>
public sealed class FilaMovVm(int indice, CampoVm mov, CampoVm? pp, CampoVm? ups)
{
    public int Indice { get; } = indice;
    public CampoVm Mov { get; } = mov;
    public CampoVm? Pp { get; } = pp;
    public CampoVm? PpUps { get; } = ups;
}

public sealed class SeccionMovsVm : SeccionBaseVm
{
    public ObservableCollection<FilaMovVm> Filas { get; } = [];
    public override IEnumerable<CampoVm> TodosLosCampos => Filas.SelectMany(f => new[] { f.Mov, f.Pp, f.PpUps }).OfType<CampoVm>();
}

/// <summary>Una pestana del editor.</summary>
public sealed partial class PestanaVm(string id, string titulo, EditorViewModel editor) : ObservableObject
{
    public string Id { get; } = id;
    public string Titulo { get; } = titulo;
    private bool _activa;
    /// <summary>Marcada por el RadioButton de la barra de pestanas.</summary>
    public bool Activa
    {
        get => _activa;
        set { if (SetProperty(ref _activa, value) && value) editor.SeleccionarPestana(this); }
    }
    public void Fijar(bool activa) { _activa = activa; OnPropertyChanged(nameof(Activa)); }
}
