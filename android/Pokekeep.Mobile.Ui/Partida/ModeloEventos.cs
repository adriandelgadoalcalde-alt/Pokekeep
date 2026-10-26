// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;
using SesionM = Pokekeep.Mobile.Ui.Modelo.Sesion;

namespace Pokekeep.Mobile.Ui.Partida;

/// <summary>Un valor predefinido de una constante («Ninguno», «Sí»...); <see cref="Personalizado"/> deja escribir un número libre.</summary>
public sealed record OpcionConstante(string Nombre, int Valor, bool Personalizado);

/// <summary>Una bandera con nombre del juego: su número, su texto en castellano y el grupo al que pertenece.</summary>
public sealed record EtiquetaBandera(int Indice, string Nombre, string Grupo);

/// <summary>Una constante con nombre del juego y sus valores predefinidos.</summary>
public sealed record EtiquetaConstante(int Indice, string Nombre, string Grupo, IReadOnlyList<OpcionConstante> Opciones);

/// <summary>Lo que cambia entre dos guardados del mismo juego (la «investigación» de PKHeX): banderas puestas y quitadas y constantes distintas.</summary>
public sealed record DiferenciaEventos(IReadOnlyList<int> Puestas, IReadOnlyList<int> Quitadas, IReadOnlyList<string> Constantes, string? Motivo)
{
    public bool Valida => Motivo is null;
    public int Total => Puestas.Count + Quitadas.Count + Constantes.Count;
}

/// <summary>El almacén concreto de banderas y constantes de una generación, con las mismas llamadas de lectura y de guardado que el formulario de PKHeX.</summary>
internal abstract class RespaldoEventos
{
    public abstract string Juego { get; }
    public abstract int NumBanderas { get; }
    public abstract int NumConstantes { get; }
    public abstract long MaxConstante { get; }
    public abstract long MinConstante { get; }
    /// <summary>El juego permite editar cualquier bandera/constante por su número (no solo las que tienen nombre).</summary>
    public abstract bool PorNumero { get; }
    public abstract bool[] LeerBanderas();
    public abstract long[] LeerConstantes();
    public abstract IReadOnlyList<EtiquetaBandera> Banderas { get; }
    public abstract IReadOnlyList<EtiquetaConstante> Constantes { get; }
    /// <summary>Escribe el borrador como lo hace el boton Guardar del formulario.</summary>
    public abstract void Escribir(bool[] banderas, long[] constantes);
    public abstract DiferenciaEventos Comparar(SaveFile otro, bool otroEsNuevo);
}

internal static class TextoEventos
{
    /// <summary>Let's Go da sus nombres como «clave_interna (texto legible)»: en el movil va delante lo legible y la clave debajo, en gris.</summary>
    public static (string Legible, string? Clave) Partir(string nombre)
    {
        var m = System.Text.RegularExpressions.Regex.Match(nombre, @"^([A-Za-z0-9_]+) \((.+)\)$");
        return m.Success ? (m.Groups[2].Value, m.Groups[1].Value) : (nombre, null);
    }

    public static string Grupo(NamedEventType t) => t switch
    {
        NamedEventType.HiddenItem => IdiomaInterfaz.T("ModeloEventos.ObjetosOcultos"),
        NamedEventType.TrainerToggle => IdiomaInterfaz.T("Form.Respaldo.SAV_SecretBase3.L_Trainers"),
        NamedEventType.StoryProgress => IdiomaInterfaz.T("Ajustes.Grupo.1"),
        NamedEventType.FlyToggle => IdiomaInterfaz.T("ModeloEventos.Vuelo"),
        NamedEventType.Misc => IdiomaInterfaz.T("ModeloEventos.Varios"),
        NamedEventType.Statistic => IdiomaInterfaz.T("Form.Estadisticas"),
        NamedEventType.Achievement => IdiomaInterfaz.T("ModeloEventos.Logros"),
        NamedEventType.UsefulFeature => IdiomaInterfaz.T("ModeloEventos.FuncionesUtiles"),
        NamedEventType.EventEncounter => IdiomaInterfaz.T("ModeloEventos.EncuentrosEvento"),
        NamedEventType.GiftAvailable => IdiomaInterfaz.T("ModeloEventos.Regalos"),
        NamedEventType.Rebattle => IdiomaInterfaz.T("ModeloEventos.Revanchas"),
        _ => IdiomaInterfaz.T("ModeloEventos.Clasificar"),
    };

    public static string Sufijo(GameVersion version) => version switch
    {
        GameVersion.X or GameVersion.Y or GameVersion.XY => "xy",
        GameVersion.OR or GameVersion.AS or GameVersion.ORAS => "oras",
        GameVersion.SN or GameVersion.MN or GameVersion.SM => "sm",
        GameVersion.US or GameVersion.UM or GameVersion.USUM => "usum",
        GameVersion.D or GameVersion.P or GameVersion.DP => "dp",
        GameVersion.Pt or GameVersion.DPPt => "pt",
        GameVersion.HG or GameVersion.SS or GameVersion.HGSS => "hgss",
        GameVersion.B or GameVersion.W or GameVersion.BW => "bw",
        GameVersion.B2 or GameVersion.W2 or GameVersion.B2W2 => "b2w2",
        GameVersion.R or GameVersion.S or GameVersion.RS => "rs",
        GameVersion.E => "e",
        GameVersion.FR or GameVersion.LG or GameVersion.FRLG => "frlg",
        GameVersion.C => "c",
        GameVersion.GD or GameVersion.SI or GameVersion.GS => "gs",
        GameVersion.GP or GameVersion.GE or GameVersion.GG => "gg",
        _ => throw new ArgumentOutOfRangeException(nameof(version), version, null),
    };

    public static IReadOnlyList<OpcionConstante> Opciones(NamedEventWork w)
        => [.. w.PredefinedValues.Select(o => new OpcionConstante(o.IsCustom ? IdiomaInterfaz.T("ModeloEventos.OtroValor") : o.Name, o.IsCustom ? 0 : o.Value, o.IsCustom))];
}

/// <summary>Generaciones 3 a 7 (<c>IEventFlag37</c>, constantes de 16 bits) y Gen. 2 (constantes de 8 bits): el <c>EventWorkspace</c> de PKHeX.</summary>
internal sealed class RespaldoEventosEspacio<TSave, TWork> : RespaldoEventos where TSave : class, IEventFlagArray, IEventWorkArray<TWork> where TWork : unmanaged, IEquatable<TWork>
{
    private readonly TSave _origen;
    private readonly GameVersion _version;
    private readonly Func<TWork, long> _aLargo;
    private readonly Func<long, TWork> _deLargo;
    private readonly long _max;

    public RespaldoEventosEspacio(TSave origen, GameVersion version, string suf, Func<TWork, long> aLargo, Func<long, TWork> deLargo, long max)
    {
        _origen = origen; _version = version; _aLargo = aLargo; _deLargo = deLargo; _max = max;
        var ws = new EventWorkspace<TSave, TWork>(origen, version);
        NumBanderas = ws.Flags.Length; NumConstantes = ws.Values.Length;
        // Los textos del Core dependen del idioma global; se leen aqui en castellano sin tocar ese ajuste.
        var f = GameLanguage.GetStrings(suf is "gs" or "c" ? suf : suf, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma, "flags");
        var c = GameLanguage.GetStrings(suf, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma, "const");
        Banderas = [.. EventLabelParsing.GetFlags(f, NumBanderas).Select(b => new EtiquetaBandera(b.Index, b.Name, TextoEventos.Grupo(b.Type)))];
        Constantes = [.. EventLabelParsing.GetWork(c, NumConstantes).Select(w => new EtiquetaConstante(w.Index, w.Name, TextoEventos.Grupo(w.Type), TextoEventos.Opciones(w)))];
    }

    public override string Juego => _version.ToString();
    public override int NumBanderas { get; }
    public override int NumConstantes { get; }
    public override long MaxConstante => _max;
    public override long MinConstante => 0;
    public override bool PorNumero => true;
    public override IReadOnlyList<EtiquetaBandera> Banderas { get; }
    public override IReadOnlyList<EtiquetaConstante> Constantes { get; }
    public override bool[] LeerBanderas() => _origen.GetEventFlags();
    public override long[] LeerConstantes() => [.. _origen.GetAllEventWork().Select(_aLargo)];

    public override void Escribir(bool[] banderas, long[] constantes)
    {
        var ws = new EventWorkspace<TSave, TWork>(_origen, _version);
        for (var i = 0; i < ws.Flags.Length; i++) ws.Flags[i] = banderas[i];
        for (var i = 0; i < ws.Values.Length; i++) ws.Values[i] = _deLargo(constantes[i]);
        ws.Save();
    }

    public override DiferenciaEventos Comparar(SaveFile otro, bool otroEsNuevo)
    {
        if (!Bloque(otro, out var b, out var v)) return new([], [], [], IdiomaInterfaz.T("ModeloEventos.GuardadoMismoTipoJuego"));
        if (v != _version && !(otro.Version == _version)) return new([], [], [], IdiomaInterfaz.T("ModeloEventos.DosGuardadosSonVersiones"));
        var d = otroEsNuevo ? new EventBlockDiff<TSave, TWork>(_origen, b) : new EventBlockDiff<TSave, TWork>(b, _origen);
        return d.Message != EventWorkDiffCompatibility.Valid ? new([], [], [], IdiomaInterfaz.T("ModeloEventos.PuedenCompararEstosGuardados")) : new(d.SetFlags, d.ClearedFlags, d.WorkDiff, null);
    }

    private static bool Bloque(SaveFile s, out TSave b, out GameVersion v)
    {
        v = s.Version; b = null!;
        if (s is IEventFlagProvider37 p && p.EventWork is TSave t) { b = t; return true; }
        if (s is TSave u) { b = u; return true; }
        return false;
    }
}

/// <summary>Let's Go (<c>SAV_EventWork</c>): banderas y constantes de 32 bits; el formulario solo escribe las que tienen nombre.</summary>
internal sealed class RespaldoEventos7b : RespaldoEventos
{
    private readonly SAV7b _sav;
    private readonly string[] _work, _flag;

    public RespaldoEventos7b(SAV7b sav)
    {
        _sav = sav;
        var suf = TextoEventos.Sufijo(sav.Version);
        _flag = GameLanguage.GetStrings(suf, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma, "flags");
        _work = GameLanguage.GetStrings(suf, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma, "const");
        var ed = new SplitEventEditor<int>(sav.Blocks.EventWork, _work, _flag);
        Banderas = [.. ed.Flag.SelectMany(g => g.Vars.OfType<EventFlag>().Select(f => new EtiquetaBandera(f.RawIndex, f.Name, Grupo(g.Type))))];
        Constantes = [.. ed.Work.SelectMany(g => g.Vars.OfType<EventWork<int>>().Select(w => new EtiquetaConstante(w.RawIndex, w.Name, Grupo(g.Type),
            [.. w.Options.Select(o => new OpcionConstante(o.Custom ? IdiomaInterfaz.T("ModeloEventos.OtroValor") : o.Text, o.Value, o.Custom))])))];
    }

    private static string Grupo(EventVarType t) => t switch
    {
        EventVarType.Zone => IdiomaInterfaz.T("ModeloEventos.ZonasMapa"),
        EventVarType.System => IdiomaInterfaz.T("ModeloEventos.FuncionesJuego"),
        EventVarType.Vanish => IdiomaInterfaz.T("ModeloEventos.ElementosDesaparecen"),
        EventVarType.Event => IdiomaInterfaz.T("ModeloEventos.Eventos"),
        _ => IdiomaInterfaz.T("ModeloEventos.Clasificar"),
    };

    public override string Juego => _sav.Version.ToString();
    public override int NumBanderas => _sav.Blocks.EventWork.CountFlag;
    public override int NumConstantes => _sav.Blocks.EventWork.CountWork;
    public override long MaxConstante => int.MaxValue;
    public override long MinConstante => int.MinValue;
    public override bool PorNumero => false;
    public override IReadOnlyList<EtiquetaBandera> Banderas { get; }
    public override IReadOnlyList<EtiquetaConstante> Constantes { get; }
    public override bool[] LeerBanderas() { var e = _sav.Blocks.EventWork; var r = new bool[e.CountFlag]; for (var i = 0; i < r.Length; i++) r[i] = e.GetFlag(i); return r; }
    public override long[] LeerConstantes() { var e = _sav.Blocks.EventWork; var r = new long[e.CountWork]; for (var i = 0; i < r.Length; i++) r[i] = e.GetWork(i); return r; }

    public override void Escribir(bool[] banderas, long[] constantes)
    {
        var ed = new SplitEventEditor<int>(_sav.Blocks.EventWork, _work, _flag);
        foreach (var g in ed.Flag) foreach (var f in g.Vars.OfType<EventFlag>()) f.Flag = banderas[f.RawIndex];
        foreach (var g in ed.Work) foreach (var w in g.Vars.OfType<EventWork<int>>()) w.Value = (int)constantes[w.RawIndex];
        ed.Save();
        _sav.State.Edited = true;
    }

    public override DiferenciaEventos Comparar(SaveFile otro, bool otroEsNuevo)
    {
        if (otro is not SAV7b b) return new([], [], [], IdiomaInterfaz.T("ModeloEventos.GuardadoLetSGo"));
        var d = otroEsNuevo ? new EventWorkDiff7b(_sav, b) : new EventWorkDiff7b(b, _sav);
        return d.Message != EventWorkDiffCompatibility.Valid ? new([], [], [], IdiomaInterfaz.T("ModeloEventos.DosGuardadosSonVersiones")) : new(d.SetFlags, d.ClearedFlags, d.WorkDiff, null);
    }
}

/// <summary>
/// Eventos y progreso (<c>SAV_EventFlags</c>, <c>SAV_EventFlags2</c> y <c>SAV_EventWork</c>): banderas y constantes de evento con el nombre que les da el juego, edición por número y comparación entre dos guardados. Trabaja sobre un
/// borrador; «Aplicar» escribe TODO el lote como una sola entrada del historial (nunca una escritura por interruptor).
/// </summary>
public sealed class ModeloEventos
{
    private readonly SesionM _sesion;
    private readonly RespaldoEventos _r;
    private bool[] _banderas = [];
    private long[] _constantes = [];
    private bool[] _banderasOrig = [];
    private long[] _constantesOrig = [];

    public ModeloEventos(SesionM sesion)
    {
        _sesion = sesion;
        _r = Crear(sesion.Guardado) ?? throw new InvalidOperationException(NoAplica(sesion.Guardado));
        Cargar();
    }

    private static RespaldoEventos? Crear(SaveFile sav)
    {
        switch (sav)
        {
            case SAV7b b: return new RespaldoEventos7b(b);
            case IEventFlag37 g: return new RespaldoEventosEspacio<IEventFlag37, ushort>(g, sav.Version, TextoEventos.Sufijo(sav.Version), x => x, x => (ushort)x, ushort.MaxValue);
            case IEventFlagProvider37 p: return new RespaldoEventosEspacio<IEventFlag37, ushort>(p.EventWork, sav.Version, TextoEventos.Sufijo(sav.Version), x => x, x => (ushort)x, ushort.MaxValue);
            case SAV2 s2: return new RespaldoEventosEspacio<SAV2, byte>(s2, sav.Version, TextoEventos.Sufijo(sav.Version), x => x, x => (byte)x, byte.MaxValue);
            default: return null;
        }
    }

    /// <summary>Por que este juego NO tiene este editor (null si lo tiene). Gen. 1, 8 y 9 tienen sus propios editores de banderas en otras fases.</summary>
    public static string? NoAplica(SaveFile sav) => sav switch
    {
        SAV6AODemo => IdiomaInterfaz.F("Partida.NoAplica.EventosDemo", NombresJuego.Corto(GameVersion.OR), NombresJuego.Corto(GameVersion.AS)),
        SAV7b or IEventFlag37 or IEventFlagProvider37 or SAV2 => null,
        SAV1 => IdiomaInterfaz.T("Partida.NoAplica.Eventos1"),
        SAV8BS => IdiomaInterfaz.T("Partida.NoAplica.Eventos8b"),
        SAV9ZA => IdiomaInterfaz.T("Partida.NoAplica.Eventos9a"),
        _ when sav.Generation >= 8 => IdiomaInterfaz.T("Partida.NoAplica.EventosBloques"),
        _ => IdiomaInterfaz.T("Partida.NoAplica.Eventos"),
    };

    public string Juego => _r.Juego;
    public int NumBanderas => _r.NumBanderas;
    public int NumConstantes => _r.NumConstantes;
    public long MaxConstante => _r.MaxConstante;
    public long MinConstante => _r.MinConstante;
    public bool PorNumero => _r.PorNumero;
    public IReadOnlyList<EtiquetaBandera> Banderas => _r.Banderas;
    public IReadOnlyList<EtiquetaConstante> Constantes => _r.Constantes;

    private void Cargar()
    {
        _banderasOrig = _r.LeerBanderas(); _constantesOrig = _r.LeerConstantes();
        _banderas = (bool[])_banderasOrig.Clone(); _constantes = (long[])_constantesOrig.Clone();
    }

    public void Recargar() => Cargar();

    public bool Bandera(int i) => _banderas[i];
    public long Constante(int i) => _constantes[i];
    public bool BanderaModificada(int i) => _banderas[i] != _banderasOrig[i];
    public bool ConstanteModificada(int i) => _constantes[i] != _constantesOrig[i];

    public void PonerBandera(int i, bool v) => _banderas[i] = v;

    /// <summary>Pone una constante; devuelve el motivo si el valor no cabe en este juego (y no la cambia).</summary>
    public string? PonerConstante(int i, long v)
    {
        if (v < MinConstante || v > MaxConstante) return IdiomaInterfaz.F("ModeloEventos.ValorDebeEstarEntre", MinConstante, MaxConstante);
        _constantes[i] = v;
        return null;
    }

    public int NumCambios
    {
        get
        {
            var n = 0;
            for (var i = 0; i < _banderas.Length; i++) if (_banderas[i] != _banderasOrig[i]) n++;
            for (var i = 0; i < _constantes.Length; i++) if (_constantes[i] != _constantesOrig[i]) n++;
            return n;
        }
    }

    public bool HayCambios => NumCambios > 0;

    /// <summary>Escribe el lote entero en UNA entrada del historial. Sin cambios no escribe nada.</summary>
    public InformePartida Aplicar()
    {
        var n = NumCambios;
        if (n == 0) return new InformePartida(ResultadoPartida.SinCambios, string.Empty, 0);
        var inf = EscrituraPartida.Aplicar(_sesion, IdiomaInterfaz.P("ModeloEventos.AplicarCambios", n), () => _r.Escribir(_banderas, _constantes));
        Cargar();
        return inf;
    }

    // ---- Investigacion ----
    /// <summary>Compara la partida abierta con otro guardado (<paramref name="otroEsNuevo"/>: el otro es el estado posterior). No toca nada.</summary>
    public DiferenciaEventos Comparar(byte[] datos, bool otroEsNuevo)
    {
        if (datos.Length == 0 || datos.Length > 0x10_0000) return new([], [], [], datos.Length == 0 ? IdiomaInterfaz.T("Intercambio.ArchivoVacio") : IdiomaInterfaz.T("ModeloEventos.ArchivoDemasiadoGrandeSer"));
        SaveFile? otro;
        try { if (!SaveUtil.TryGetSaveFile(datos.ToArray(), out otro)) return new([], [], [], IdiomaInterfaz.T("ModeloEventos.ArchivoGuardadoPokemonReconocible")); }
        catch (Exception) { return new([], [], [], IdiomaInterfaz.T("ModeloEventos.ArchivoDanadoPuedeLeer")); }
        return _r.Comparar(otro!, otroEsNuevo);
    }

    /// <summary>Nombre de una bandera si el juego se lo da.</summary>
    public string? NombreBandera(int i) => Banderas.FirstOrDefault(b => b.Indice == i)?.Nombre;
}

/// <summary>Acceso interno a la separacion de nombres de eventos desde los modelos de vista.</summary>
internal static class TextoEventosPublico
{
    public static (string Legible, string? Clave) Partir(string nombre) => TextoEventos.Partir(nombre);
}
