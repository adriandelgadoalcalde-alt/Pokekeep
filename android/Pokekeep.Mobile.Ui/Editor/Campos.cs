// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Editor;

/// <summary>Como se pinta y se edita un campo del editor.</summary>
public enum TipoCampo
{
    /// <summary>Texto libre (mote, nombre del entrenador).</summary>
    Texto,
    /// <summary>Numero entero con limites; teclado numerico.</summary>
    Entero,
    /// <summary>Valor hexadecimal de N digitos (PID, EC, rastreador HOME).</summary>
    Hex,
    /// <summary>Se elige de una lista con buscador (hoja inferior).</summary>
    Opcion,
    /// <summary>Interruptor.</summary>
    Casilla,
    /// <summary>Fecha dd/mm/aaaa.</summary>
    Fecha,
    /// <summary>Fecha y hora dd/mm/aaaa hh:mm:ss.</summary>
    FechaHora,
    /// <summary>Numero con decimales (altura y peso absolutos).</summary>
    Decimal,
    /// <summary>Solo lectura.</summary>
    Info,
    /// <summary>Boton que ejecuta una accion (abrir un subeditor, aleatorizar...).</summary>
    Accion,
    /// <summary>Deslizador entero (barra de experiencia).</summary>
    Barra,
}

/// <summary>Todo lo que un campo necesita para leerse y escribirse: el Pokemon del borrador, la partida, las listas y si esta el modo avanzado.</summary>
public sealed class ContextoEditor(PKM pk, SaveFile sav, FuentesEditor fuentes)
{
    public PKM Pk { get; } = pk;
    public SaveFile Sav { get; } = sav;
    public FuentesEditor F { get; } = fuentes;
    public bool Avanzado => F.Avanzado;
    public byte Formato => Pk.Format;
    /// <summary>Modo avanzado «estadisticas editadas» (CHK_HackedStats): las estadisticas finales se escriben a mano y el modelo no las recalcula.</summary>
    public bool StatsManuales { get; set; }
    /// <summary>Byte extra elegido en la lista de bytes sin nombre del formato (modo avanzado).</summary>
    public int IndiceByteExtra { get; set; }
    /// <summary>Acciones que un campo pide a la pantalla (abrir un subeditor, una hoja de seleccion...). Las rellena el ViewModel; las pruebas del host las dejan nulas.</summary>
    public IAnfitrionEditor? Anfitrion { get; set; }
}

/// <summary>Lo que el modelo del editor pide a la pantalla: abrir un subeditor, avisar de algo.</summary>
public interface IAnfitrionEditor
{
    void AbrirSubeditor(string id);
    void Aviso(string texto);
    void IrAPestana(string id);
}

/// <summary>
/// Un campo del editor de entidad. Cada uno nace de UNA fila del censo de paridad (<see cref="Id"/>, p. ej. <c>PK-ED-008</c>) y dice, para el Pokemon que se edita, si es visible (la regla de
/// <c>PKMEditor.ToggleInterface</c>), que valor tiene, si se puede editar (y si no, POR QUE) y como se escribe. Los valores se escriben siempre con las funciones del Core
/// (<c>SetPIDGender</c>, <c>SetShiny</c>, <c>RefreshAbility</c>...): la interfaz nunca calcula una relacion entre campos por su cuenta.
/// </summary>
public sealed record CampoDef
{
    public required string Id { get; init; }
    public required string Etiqueta { get; init; }
    /// <summary>Etiqueta que depende del Pokemon (los nombres de las marcas cambian en la 3.ª generacion).</summary>
    public Func<ContextoEditor, string>? EtiquetaFn { get; init; }
    public string TextoEtiqueta(ContextoEditor c) => EtiquetaFn?.Invoke(c) ?? Etiqueta;
    public required TipoCampo Tipo { get; init; }
    /// <summary>Etiqueta que lee el lector de pantalla si difiere de la visible.</summary>
    public string? Ayuda { get; init; }
    /// <summary>Texto secundario bajo la etiqueta (el tipo de un movimiento, «legal» o «posible»...).</summary>
    public Func<ContextoEditor, string?>? Detalle { get; init; }
    public Func<ContextoEditor, bool>? Visible { get; init; }

    // Lectura
    public Func<ContextoEditor, string>? Leer { get; init; }
    public Func<ContextoEditor, int>? IdActual { get; init; }
    public Func<ContextoEditor, bool>? Marcado { get; init; }
    public Func<ContextoEditor, IReadOnlyList<OpcionCampo>>? Opciones { get; init; }

    // Escritura: devuelven null si se acepto o el motivo del rechazo
    public Func<ContextoEditor, string, string?>? Escribir { get; init; }
    public Func<ContextoEditor, int, string?>? EscribirId { get; init; }
    public Func<ContextoEditor, bool, string?>? EscribirMarca { get; init; }
    public Action<ContextoEditor>? Ejecutar { get; init; }

    /// <summary>Si devuelve un texto, el campo se muestra en solo lectura y ese texto dice por que.</summary>
    public Func<ContextoEditor, string?>? Motivo { get; init; }

    public int Min { get; init; }
    public int Max { get; init; } = int.MaxValue;
    public Func<ContextoEditor, int>? MaxDinamico { get; init; }
    public Func<ContextoEditor, int>? MaxLongitud { get; init; }
    public int Digitos { get; init; }
    public bool Avanzado { get; init; }
    public string? Sufijo { get; init; }
    /// <summary>Agrupa campos que la pantalla pinta juntos (la tabla de estadisticas: «iv», «ev», «av», «gv»; los movimientos: «mov», «pp», «ppups», «rec»).</summary>
    public string? Grupo { get; init; }
    public int Indice { get; init; } = -1;
    /// <summary>Es una lista cuya eleccion EJECUTA algo (cambiar la naturaleza por estadistica): no guarda un valor que se pueda releer.</summary>
    public bool SoloAccion { get; init; }
    /// <summary>Escribirlo cambia las estadisticas de combate guardadas (nivel, IV, EV, naturaleza, especie...): el modelo las recalcula con el Core.</summary>
    public bool AfectaStats { get; init; }
    /// <summary>Clave unica del campo en la pantalla: el id del censo mas el indice (las seis estadisticas comparten id).</summary>
    public string Clave => Indice >= 0 ? $"{Id}:{Indice}" : Id;

    public bool EsVisible(ContextoEditor c) => (Visible?.Invoke(c) ?? true) && (!Avanzado || c.Avanzado);

    public int MaximoEn(ContextoEditor c) => MaxDinamico?.Invoke(c) ?? Max;

    public string Valor(ContextoEditor c)
    {
        try { return Leer?.Invoke(c) ?? string.Empty; }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException or InvalidOperationException or NullReferenceException) { return "—"; }
    }

    public string? Rechazo(ContextoEditor c) => Motivo?.Invoke(c);
}

/// <summary>Una seccion de la pestana (una tarjeta con titulo).</summary>
public sealed record SeccionDef(string Titulo, IReadOnlyList<CampoDef> Campos, Func<ContextoEditor, bool>? Visible = null)
{
    public bool EsVisible(ContextoEditor c) => Visible?.Invoke(c) ?? true;
}

/// <summary>Una pestana del editor (Principal, Encuentro, Estadisticas, Movimientos, Cosmetico, EO/Varios).</summary>
public sealed record PestanaDef(string Id, string Titulo, IReadOnlyList<SeccionDef> Secciones, Func<ContextoEditor, bool>? Visible = null)
{
    public bool EsVisible(ContextoEditor c) => Visible?.Invoke(c) ?? true;
}

/// <summary>Constructores cortos de campos para el catalogo.</summary>
internal static class F
{
    public static CampoDef Texto(string id, string etiqueta, Func<ContextoEditor, string> leer, Func<ContextoEditor, string, string?> escribir, Func<ContextoEditor, int>? maxLongitud = null, Func<ContextoEditor, bool>? visible = null, Func<ContextoEditor, string?>? motivo = null, bool avanzado = false, string? ayuda = null)
        => new() { Id = id, Etiqueta = etiqueta, Tipo = TipoCampo.Texto, Leer = leer, Escribir = escribir, MaxLongitud = maxLongitud, Visible = visible, Motivo = motivo, Avanzado = avanzado, Ayuda = ayuda };

    public static CampoDef Entero(string id, string etiqueta, Func<ContextoEditor, long> leer, Action<ContextoEditor, long> escribir, int min, int max, Func<ContextoEditor, bool>? visible = null,
        Func<ContextoEditor, string?>? motivo = null, Func<ContextoEditor, int>? maxDinamico = null, bool avanzado = false, string? ayuda = null, string? sufijo = null)
        => new()
        {
            Id = id, Etiqueta = etiqueta, Tipo = TipoCampo.Entero, Min = min, Max = max, MaxDinamico = maxDinamico, Visible = visible, Motivo = motivo, Avanzado = avanzado, Ayuda = ayuda, Sufijo = sufijo,
            Leer = c => leer(c).ToString(CultureInfo.InvariantCulture),
            Escribir = (c, t) =>
            {
                if (!long.TryParse(t.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return IdiomaInterfaz.T("Campos.EscribeNumeroEntero");
                var tope = maxDinamico?.Invoke(c) ?? max;
                escribir(c, Math.Clamp(v, min, tope));
                return null;
            },
        };

    public static CampoDef Hex(string id, string etiqueta, int digitos, Func<ContextoEditor, ulong> leer, Action<ContextoEditor, ulong> escribir, Func<ContextoEditor, bool>? visible = null, Func<ContextoEditor, string?>? motivo = null, bool avanzado = false, string? ayuda = null)
        => new()
        {
            Id = id, Etiqueta = etiqueta, Tipo = TipoCampo.Hex, Digitos = digitos, Visible = visible, Motivo = motivo, Avanzado = avanzado, Ayuda = ayuda,
            Leer = c => leer(c).ToString("X" + digitos, CultureInfo.InvariantCulture),
            Escribir = (c, t) =>
            {
                var limpio = new string([.. t.Where(Uri.IsHexDigit)]);
                if (limpio.Length == 0) limpio = "0";
                if (limpio.Length > digitos) limpio = limpio[^digitos..];
                escribir(c, ulong.Parse(limpio, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                return null;
            },
        };

    public static CampoDef Opcion(string id, string etiqueta, Func<ContextoEditor, IReadOnlyList<OpcionCampo>> opciones, Func<ContextoEditor, int> idActual, Func<ContextoEditor, int, string?> escribir,
        Func<ContextoEditor, bool>? visible = null, Func<ContextoEditor, string?>? motivo = null, bool avanzado = false, string? ayuda = null, Func<ContextoEditor, string>? leer = null)
        => new()
        {
            Id = id, Etiqueta = etiqueta, Tipo = TipoCampo.Opcion, Opciones = opciones, IdActual = idActual, EscribirId = escribir, Visible = visible, Motivo = motivo, Avanzado = avanzado, Ayuda = ayuda,
            Leer = leer ?? (c =>
            {
                var actual = idActual(c);
                foreach (var o in opciones(c)) if (o.Id == actual) return o.Texto;
                return actual == 0 ? IdiomaInterfaz.T("Form.Ninguno") : IdiomaInterfaz.F("Campos.ValorFueraLista", actual);
            }),
        };

    public static CampoDef Casilla(string id, string etiqueta, Func<ContextoEditor, bool> leer, Func<ContextoEditor, bool, string?> escribir, Func<ContextoEditor, bool>? visible = null,
        Func<ContextoEditor, string?>? motivo = null, bool avanzado = false, string? ayuda = null)
        => new() { Id = id, Etiqueta = etiqueta, Tipo = TipoCampo.Casilla, Marcado = leer, EscribirMarca = escribir, Visible = visible, Motivo = motivo, Avanzado = avanzado, Ayuda = ayuda, Leer = c => leer(c) ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No") };

    public static CampoDef Fecha(string id, string etiqueta, Func<ContextoEditor, DateOnly?> leer, Func<ContextoEditor, DateOnly?, string?> escribir, Func<ContextoEditor, bool>? visible = null,
        Func<ContextoEditor, string?>? motivo = null, string? ayuda = null)
        => new()
        {
            Id = id, Etiqueta = etiqueta, Tipo = TipoCampo.Fecha, Visible = visible, Motivo = motivo, Ayuda = ayuda ?? IdiomaInterfaz.T("Campos.FechaFormatoDiaMes"),
            Leer = c => leer(c) is { } d ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty,
            Escribir = (c, t) =>
            {
                var s = t.Trim();
                if (s.Length == 0) return escribir(c, null);
                if (!DateOnly.TryParseExact(s, ["d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return IdiomaInterfaz.T("Campos.EscribeFechaDiaMes");
                return escribir(c, d);
            },
        };

    public static CampoDef Decimal(string id, string etiqueta, Func<ContextoEditor, float> leer, Action<ContextoEditor, float> escribir, Func<ContextoEditor, bool>? visible = null, string? ayuda = null)
        => new()
        {
            Id = id, Etiqueta = etiqueta, Tipo = TipoCampo.Decimal, Visible = visible, Ayuda = ayuda,
            Leer = c => leer(c).ToString("R", CultureInfo.InvariantCulture),
            Escribir = (c, t) =>
            {
                if (!float.TryParse(t.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || float.IsNaN(v) || float.IsInfinity(v) || v < 0) return IdiomaInterfaz.T("Campos.EscribeNumeroPositivoEjemplo");
                escribir(c, v);
                return null;
            },
        };

    public static CampoDef FechaHora(string id, string etiqueta, Func<ContextoEditor, string> leer, Func<ContextoEditor, string, string?> escribir, Func<ContextoEditor, bool>? visible = null, string? ayuda = null)
        => new() { Id = id, Etiqueta = etiqueta, Tipo = TipoCampo.FechaHora, Leer = leer, Escribir = escribir, Visible = visible, Ayuda = ayuda };

    public static CampoDef Info(string id, string etiqueta, Func<ContextoEditor, string> leer, Func<ContextoEditor, bool>? visible = null, string? ayuda = null)
        => new() { Id = id, Etiqueta = etiqueta, Tipo = TipoCampo.Info, Leer = leer, Visible = visible, Ayuda = ayuda };

    public static CampoDef Accion(string id, string etiqueta, Action<ContextoEditor> ejecutar, Func<ContextoEditor, bool>? visible = null, Func<ContextoEditor, string?>? motivo = null, string? ayuda = null, bool avanzado = false, Func<ContextoEditor, string>? leer = null)
        => new() { Id = id, Etiqueta = etiqueta, Tipo = TipoCampo.Accion, Ejecutar = ejecutar, Visible = visible, Motivo = motivo, Ayuda = ayuda, Avanzado = avanzado, Leer = leer };

    public static CampoDef Barra(string id, string etiqueta, Func<ContextoEditor, long> leer, Action<ContextoEditor, long> escribir, Func<ContextoEditor, int> max, Func<ContextoEditor, bool>? visible = null)
        => new()
        {
            Id = id, Etiqueta = etiqueta, Tipo = TipoCampo.Barra, Visible = visible, MaxDinamico = max,
            Leer = c => leer(c).ToString(CultureInfo.InvariantCulture),
            Escribir = (c, t) => { if (long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) escribir(c, Math.Clamp(v, 0, max(c))); return null; },
        };
}
