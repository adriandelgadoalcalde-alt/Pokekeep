// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using SesionM = Pokekeep.Mobile.Ui.Modelo.Sesion;

namespace Pokekeep.Mobile.Ui.Partida;

/// <summary>
/// Modelo sin pantalla de un editor de partida (entrenador, estadisticas, eventos...): lee cada campo de la partida a un BORRADOR, valida lo que se escribe con los mismos limites que el formulario de PKHeX
/// y, al aplicar, escribe SOLO los campos que cambiaron, con la llamada del Core del formulario y en su orden, como UNA entrada del historial (un Deshacer).
/// </summary>
public sealed class ModeloEditorPartida
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public const string FormatoFechaHora = "dd/MM/yyyy HH:mm:ss";

    private readonly Dictionary<string, object?> _originales = new();
    private readonly Dictionary<string, FilaTablaP> _filas = new();
    private readonly Dictionary<string, IReadOnlyList<FilaTablaP>> _tablas = new();
    private readonly Dictionary<string, TablaDefP> _defTablas = new();
    // Colecciones: los campos de cada elemento se crean y se leen la primera vez que se abre (nunca antes).
    private readonly List<CampoP> _dinamicos = [];
    private readonly HashSet<string> _auxiliares = [];
    private readonly Dictionary<(string Coleccion, int Indice), IReadOnlyList<SeccionP>> _elementos = new();

    public ModeloEditorPartida(SesionM sesion, EditorDefP def)
    {
        Sesion = sesion; Def = def; Ctx = new ContextoP(sesion);
        Cargar();
    }

    public SesionM Sesion { get; }
    public EditorDefP Def { get; private set; }
    public ContextoP Ctx { get; }
    /// <summary>Campos que no se pudieron leer (la clave y por que): nunca se escriben.</summary>
    public Dictionary<string, string> NoLegibles { get; } = new();

    /// <summary>Los campos del editor, mas los de los elementos de colecciones que ya se han abierto.</summary>
    public IEnumerable<CampoP> Campos => _dinamicos.Count == 0 ? Def.Campos : Def.Campos.Concat(_dinamicos);
    public IReadOnlyList<PestanaP> PestanasVisibles => [.. Def.Pestanas.Where(p => p.EsVisible(Ctx))];

    /// <summary>Renueva solo definiciones. No lee valores, no descarta el borrador ni crea otro contexto.</summary>
    public bool ActualizarMetadatos(EditorDefP nueva)
    {
        if (nueva.Id != Def.Id || !nueva.Pestanas.Select(p => p.Id).SequenceEqual(Def.Pestanas.Select(p => p.Id))) return false;
        static bool MismasSecciones(IReadOnlyList<SeccionP> a, IReadOnlyList<SeccionP> b)
            => a.Count == b.Count && a.Zip(b).All(p => p.First.Campos.Select(c => c.Id).SequenceEqual(p.Second.Campos.Select(c => c.Id)));
        if (Def.Pestanas.Zip(nueva.Pestanas).Any(p => !MismasSecciones(p.First.Secciones, p.Second.Secciones) || p.First.Tabla?.Id != p.Second.Tabla?.Id || p.First.Coleccion?.Id != p.Second.Coleccion?.Id)) return false;
        var elementos = new Dictionary<(string Coleccion, int Indice), IReadOnlyList<SeccionP>>();
        foreach (var clave in _elementos.Keys)
        {
            var coleccion = nueva.Pestanas.Select(p => p.Coleccion).FirstOrDefault(c => c?.Id == clave.Coleccion);
            if (coleccion is null || clave.Indice >= coleccion.Cantidad(Ctx)) return false;
            var secciones = coleccion.Secciones(Ctx, clave.Indice);
            if (!MismasSecciones(_elementos[clave], secciones)) return false;
            elementos.Add(clave, secciones);
        }
        var dinamicos = elementos.Values.SelectMany(s => s).SelectMany(s => s.Campos).ToList();
        var antes = Campos.GroupBy(c => c.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        var despues = nueva.Campos.Concat(dinamicos).GroupBy(c => c.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        if (antes.Count != despues.Count || antes.Any(c => !despues.TryGetValue(c.Key, out var otro) || (c.Value.Tipo, c.Value.SinEscritura, c.Value.EsEspecie) != (otro.Tipo, otro.SinEscritura, otro.EsEspecie))) return false;
        var tablas = new Dictionary<string, IReadOnlyList<FilaTablaP>>();
        var definiciones = new Dictionary<string, TablaDefP>();
        foreach (var id in _tablas.Keys)
        {
            var tabla = nueva.Pestanas.Select(p => p.Tabla).FirstOrDefault(t => t?.Id == id);
            if (tabla is null || !_defTablas[id].Acciones.Select(a => a.Id).SequenceEqual(tabla.Acciones.Select(a => a.Id))) return false;
            var filas = tabla.Filas(Ctx);
            if (!filas.Select(f => (f.Id, f.EsMarca, f.SinEscritura, f.SoloLectura)).SequenceEqual(_tablas[id].Select(f => (f.Id, f.EsMarca, f.SinEscritura, f.SoloLectura)))) return false;
            tablas.Add(id, filas); definiciones.Add(id, tabla);
        }
        // Commit atomico de metadatos despues de comprobar identidades; los valores y originales siguen intactos.
        Def = nueva;
        foreach (var clave in NoLegibles.Keys.ToArray()) NoLegibles[clave] = IdiomaInterfaz.T("Form.NoLegible");
        _dinamicos.Clear(); _dinamicos.AddRange(dinamicos);
        _elementos.Clear(); foreach (var e in elementos) _elementos.Add(e.Key, e.Value);
        _tablas.Clear(); _defTablas.Clear(); _filas.Clear();
        foreach (var t in tablas)
        {
            _tablas.Add(t.Key, t.Value); _defTablas.Add(t.Key, definiciones[t.Key]);
            foreach (var fila in t.Value) _filas.Add(fila.Clave(t.Key), fila);
        }
        return true;
    }

    private void Cargar()
    {
        _originales.Clear(); Ctx.Borrador.Clear(); Ctx.NombresDeArchivo.Clear(); NoLegibles.Clear(); _filas.Clear(); _tablas.Clear(); _defTablas.Clear(); _dinamicos.Clear(); _elementos.Clear(); _auxiliares.Clear();
        foreach (var c in Campos) LeerCampo(c);
        foreach (var p in Def.Pestanas.Where(p => p.Tabla is not null && p.EsVisible(Ctx)))
        {
            var filas = p.Tabla!.Filas(Ctx);
            _tablas[p.Tabla.Id] = filas; _defTablas[p.Tabla.Id] = p.Tabla;
            foreach (var f in filas)
            {
                var k = f.Clave(p.Tabla.Id);
                var v = f.Leer(Ctx);
                _filas[k] = f; _originales[k] = v; Ctx.Borrador[k] = v; if (f.SinEscritura) _auxiliares.Add(k);
            }
        }
    }

    private void LeerCampo(CampoP c)
    {
        if (c.SinEscritura) _auxiliares.Add(c.Id);
        try { var v = c.Leer(Ctx); _originales[c.Id] = v; Ctx.Borrador[c.Id] = v; }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException or InvalidOperationException or NullReferenceException or InvalidCastException or KeyNotFoundException)
        {
            NoLegibles[c.Id] = IdiomaInterfaz.T("Form.NoLegible");
            _originales[c.Id] = null; Ctx.Borrador[c.Id] = null;
        }
    }

    /// <summary>Definicion de la coleccion de una pestana por id (null si no existe).</summary>
    public ColeccionDefP? Coleccion(string id) => Def.Pestanas.Select(p => p.Coleccion).FirstOrDefault(c => c?.Id == id);

    public int CantidadElementos(string coleccion) => Coleccion(coleccion) is { } c ? c.Cantidad(Ctx) : 0;

    /// <summary>
    /// Abre el elemento <paramref name="indice"/> de una coleccion: crea y lee sus campos la primera vez (el borrador parte de lo que hay en la partida) y devuelve sus secciones. Las siguientes veces devuelve
    /// las mismas, con lo editado hasta ahora. Un indice fuera de rango da una lista vacia (nunca una excepcion).
    /// </summary>
    public IReadOnlyList<SeccionP> AbrirElemento(string coleccion, int indice)
    {
        if (Coleccion(coleccion) is not { } def || indice < 0 || indice >= def.Cantidad(Ctx)) return [];
        if (_elementos.TryGetValue((coleccion, indice), out var ya)) return ya;
        var secciones = def.Secciones(Ctx, indice);
        _elementos[(coleccion, indice)] = secciones;
        foreach (var c in secciones.SelectMany(s => s.Campos)) { _dinamicos.Add(c); LeerCampo(c); }
        return secciones;
    }

    /// <summary>Elementos de una coleccion que tienen algun campo cambiado en el borrador.</summary>
    public IReadOnlyList<int> ElementosCambiados(string coleccion)
        => [.. _elementos.Where(kv => kv.Key.Coleccion == coleccion && kv.Value.SelectMany(s => s.Campos).Any(c => CuentaComoCambio(c.Id))).Select(kv => kv.Key.Indice).OrderBy(i => i)];

    public IReadOnlyList<FilaTablaP> Filas(string tabla) => _tablas.TryGetValue(tabla, out var f) ? f : [];

    /// <summary>Lo que hay ahora en la partida para ese campo (el borrador parte de aqui).</summary>
    public object? Original(string clave) => _originales.TryGetValue(clave, out var v) ? v : null;
    public object? Borrador(string clave) => Ctx.Borrador.TryGetValue(clave, out var v) ? v : null;
    public bool Cambiado(string clave) => !SonIguales(Original(clave), Borrador(clave));

    private static bool SonIguales(object? a, object? b) => a is byte[] x && b is byte[] y ? x.AsSpan().SequenceEqual(y) : Equals(a, b);

    public IReadOnlyList<string> ClavesCambiadas => [.. Ctx.Borrador.Keys.Where(CuentaComoCambio)];
    public bool HayCambios => Ctx.Borrador.Keys.Any(CuentaComoCambio);
    public int NumCambios => Ctx.Borrador.Keys.Count(CuentaComoCambio);
    /// <summary>Cambiado Y se escribira: los campos de ayuda (<see cref="CampoP.SinEscritura"/>) no cuentan.</summary>
    public bool CuentaComoCambio(string clave) => Cambiado(clave) && !_auxiliares.Contains(clave);

    // ---- Valores como texto ----
    public string Formatear(CampoP c, object? valor) => valor switch
    {
        null => string.Empty,
        string s => s,
        bool b => c.Tipo == TipoP.Accion ? (b ? IdiomaInterfaz.T("Form.SeHara") : string.Empty) : b ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No"),
        byte[] bytes => $"{bytes.Length} bytes",
        DateTime d => d.ToString(c.SoloFecha ? "dd/MM/yyyy" : c.SinSegundosEn(Ctx) ? "dd/MM/yyyy HH:mm" : FormatoFechaHora, Inv),
        double dd => dd.ToString($"0.{new string('#', Math.Max(1, c.Decimales))}", Inv),
        long l when c.Tipo == TipoP.Info && c.PresentarInfo is { } presentar => presentar(Ctx, l),
        long l when c.Tipo == TipoP.Opcion => Opciones(c).FirstOrDefault(o => o.Id == l)?.Texto ?? (l == 0 ? IdiomaInterfaz.T("Form.Ninguno") : IdiomaInterfaz.F("Form.SinNombre", l.ToString(Inv))),
        long l when c.Hex => l.ToString("X", Inv),
        long l => l.ToString(Inv),
        _ => Convert.ToString(valor, Inv) ?? string.Empty,
    };

    public IReadOnlyList<OpcionP> Opciones(CampoP c) => c.Opciones?.Invoke(Ctx) ?? [];

    // ---- Escritura al borrador ----
    /// <summary>Pone un valor en el borrador desde lo que el usuario escribio. Devuelve el motivo del rechazo (y no cambia nada) o null.</summary>
    public string? PonerTexto(CampoP c, string texto) => ProcesarTexto(c, texto, escribir: true);
    /// <summary>La misma validacion de entrada, sin normalizar/escribir valores ni disparar derivaciones.</summary>
    public string? ValidarTexto(CampoP c, string texto) => ProcesarTexto(c, texto, escribir: false);
    private string? ProcesarTexto(CampoP c, string texto, bool escribir)
    {
        string? Aceptar(object valor) => escribir ? Poner(c, valor) : Validar(c, valor);
        switch (c.Tipo)
        {
            case TipoP.Texto:
            {
                var max = c.LongitudEn(Ctx);
                if (max > 0 && texto.Length > max) return IdiomaInterfaz.F("Form.MaxCaracteres", max);
                if (c.Validar?.Invoke(Ctx, texto) is { } regla) return regla;
                return Aceptar(texto);
            }
            case TipoP.Entero:
            {
                var t = texto.Trim();
                if (t.Length == 0) return IdiomaInterfaz.F("Form.EscribirNumero", c.Min, c.MaximoEn(Ctx));
                if (!(c.Hex ? long.TryParse(t, NumberStyles.AllowHexSpecifier, Inv, out var n) : long.TryParse(t, NumberStyles.Integer, Inv, out n)))
                    return c.Hex ? IdiomaInterfaz.T("Form.EscribirHex") : IdiomaInterfaz.F("Form.EscribirNumero", c.Min, c.MaximoEn(Ctx));
                if (n < c.Min || n > c.MaximoEn(Ctx)) return IdiomaInterfaz.F("Form.FueraRango", c.Min, c.MaximoEn(Ctx));
                return Aceptar(n);
            }
            case TipoP.Decimal:
            {
                var t = texto.Trim().Replace(',', '.');
                if (!double.TryParse(t, NumberStyles.Float, Inv, out var d) || double.IsNaN(d) || double.IsInfinity(d)) return IdiomaInterfaz.T("Form.EscribirDecimal");
                if (d < c.Min || d > c.MaximoEn(Ctx)) return IdiomaInterfaz.F("Form.FueraRango", c.Min, c.MaximoEn(Ctx));
                return Aceptar(d);
            }
            case TipoP.FechaHora:
            {
                if (!DateTime.TryParseExact(texto.Trim(), [FormatoFechaHora, "dd/MM/yyyy HH:mm", "dd/MM/yyyy"], Inv, DateTimeStyles.None, out var f)) return IdiomaInterfaz.T("Form.EscribirFecha");
                return Aceptar(f);
            }
            default: return null;
        }
    }

    private string? Validar(CampoP c, object? valor)
    {
        if (c.Motivo?.Invoke(Ctx) is { } m) return m;
        if (valor is long lv && c.ValidarValor?.Invoke(Ctx, lv) is { } rechazo) return rechazo;
        if (valor is byte[] archivo && c.ValidarArchivo?.Invoke(Ctx, archivo) is { } malo) return malo;
        if (valor is DateTime fuera && (fuera < c.FechaMin || fuera > c.FechaMax))
            return IdiomaInterfaz.F("Form.FechaFueraRango", c.FechaMin ?? DateTime.MinValue, c.FechaMax ?? DateTime.MaxValue);
        return null;
    }

    public string? Poner(CampoP c, object? valor)
    {
        if (Validar(c, valor) is { } rechazo) return rechazo;
        if (valor is string str && c.Normaliza is { } norm) valor = norm(str);
        if (valor is double dbl) valor = Math.Round(dbl, c.Decimales);
        if (valor is DateTime dd2 && c.SoloFecha) valor = dd2.Date;
        if (valor is DateTime dt && c.SinSegundosEn(Ctx)) valor = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0);
        if (SonIguales(Ctx.Borrador.GetValueOrDefault(c.Id), valor)) return null;
        Ctx.Borrador[c.Id] = valor;
        if (c.Deriva is { } deriva) foreach (var kv in deriva(Ctx)) Ctx.Borrador[kv.Key] = kv.Value;
        foreach (var id in c.Reinicia)
        {
            var otro = Campos.FirstOrDefault(x => x.Id == id);
            if (otro is null) continue;
            var ops = Opciones(otro);
            // La lista dependiente vuelve a su primera opcion (el pais cambia las regiones posibles).
            Ctx.Borrador[id] = ops.Count > 0 ? (object)(long)ops[0].Id : otro.Tipo == TipoP.Entero ? (object)Math.Max(0L, otro.Min) : _originales.GetValueOrDefault(id);
        }
        return null;
    }

    public string? PonerFila(string tabla, FilaTablaP f, object valor)
    {
        if (f.SoloLectura) return IdiomaInterfaz.T("Form.FilaFija");
        if (valor is long l && (l < f.Min || l > f.Max)) return IdiomaInterfaz.F("Form.FueraRango", f.Min, f.Max);
        Ctx.Borrador[f.Clave(tabla)] = valor;
        if (_defTablas.TryGetValue(tabla, out var def) && def.Coherencia is { } coherencia)
            foreach (var (clave, nuevo) in coherencia(Ctx, f, valor)) if (_filas.ContainsKey(clave)) Ctx.Borrador[clave] = nuevo;
        return null;
    }

    /// <summary>Ejecuta una accion masiva de una tabla sobre el BORRADOR de sus filas (las de solo lectura no se tocan). Devuelve el motivo del primer rechazo (o null).</summary>
    public string? AplicarAccionTabla(string tabla, AccionTablaP accion)
    {
        string? rechazo = null;
        foreach (var (fila, valor) in accion.Cambios(Ctx, Filas(tabla)))
        {
            if (fila.SoloLectura) continue;
            var r = PonerFila(tabla, fila, valor);
            rechazo ??= r;
        }
        return rechazo;
    }

    // ---- Aplicar ----
    /// <summary>Escribe en la partida SOLO lo que cambio, en el orden del formulario, como una entrada del historial.</summary>
    public InformePartida Aplicar()
    {
        if (!HayCambios) return new InformePartida(ResultadoPartida.SinCambios, string.Empty, 0);
        var cambiadosSolos = Campos.Where(c => c.Escribir is not null && Cambiado(c.Id)).ToList();
        var grupos = cambiadosSolos.Where(c => c.GrupoEscritura is not null).Select(c => c.GrupoEscritura).ToHashSet();
        var cambiados = Campos.Where(c => c.Escribir is not null && (Cambiado(c.Id) || (c.GrupoEscritura is not null && grupos.Contains(c.GrupoEscritura)))).OrderBy(c => c.Prioridad).ToList();
        var tablaCambiada = _filas.Where(kv => CuentaComoCambio(kv.Key)).Select(kv => kv.Value).ToList();
        var etiquetas = cambiados.Select(c => c.TextoEtiqueta(Ctx)).Concat(tablaCambiada.Take(3).Select(f => f.Nombre)).Take(3).ToList();
        var mas = cambiados.Count + tablaCambiada.Count - etiquetas.Count;
        var desc = $"{Def.Titulo}: {string.Join(", ", etiquetas)}{(mas > 0 ? IdiomaInterfaz.F("Form.Mas", mas) : "")}";
        var inf = EscrituraPartida.Aplicar(Sesion, desc, () =>
        {
            var despues = Def.PrepararAplicar?.Invoke(Ctx);
            foreach (var c in cambiados) c.Escribir!(Ctx, Ctx.Borrador[c.Id]);
            foreach (var kv in _filas.Where(kv => CuentaComoCambio(kv.Key))) kv.Value.Escribir(Ctx, Ctx.Borrador[kv.Key]!);
            despues?.Invoke(Ctx);
        });
        Cargar();
        return inf;
    }

    /// <summary>
    /// Ejecuta una accion <see cref="CampoP.Inmediata"/> sobre la partida real como UNA entrada del historial y vuelve a leer todo. Con cambios pendientes en el borrador NO hace nada y devuelve
    /// <see cref="ResultadoPartida.SinCambios"/> con el motivo en <see cref="InformePartida.Descripcion"/> (<see cref="MotivoPendientes"/>): releer descartaria el borrador sin avisar, asi que ningun llamador
    /// (la pantalla, una derivacion, una prueba) puede perderlo por olvidarse de comprobarlo.
    /// </summary>
    public InformePartida EjecutarInmediata(CampoP accion)
    {
        if (HayCambios) return new InformePartida(ResultadoPartida.SinCambios, MotivoPendientes, 0);
        if (accion.Escribir is null || accion.Motivo?.Invoke(Ctx) is not null) return new InformePartida(ResultadoPartida.SinCambios, string.Empty, 0);
        var inf = EscrituraPartida.Aplicar(Sesion, $"{Def.Titulo}: {accion.TextoEtiqueta(Ctx)}", () => accion.Escribir(Ctx, true));
        Cargar();
        return inf;
    }

    /// <summary>Por que una accion inmediata no se ejecuta mientras haya cambios sin aplicar.</summary>
    public static string MotivoPendientes => IdiomaInterfaz.T("Form.Pendientes");

    /// <summary>Descarta el borrador y vuelve a leer la partida.</summary>
    public void Recargar() => Cargar();

    /// <summary>Tras aplicar o deshacer, el Core puede cambiar el esquema (p. ej., apariencia masculina/femenina).</summary>
    public void Recargar(EditorDefP nueva)
    {
        Def = nueva;
        Cargar();
    }
}
