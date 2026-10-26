// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida;

/// <summary>Constructores cortos de campos para los catalogos de editores de partida.</summary>
internal static class P
{
    public static CampoP Texto(string id, string etiqueta, Func<ContextoP, string> leer, Action<ContextoP, string>? escribir, int maxLongitud = 0, Func<ContextoP, int>? maxFn = null, Func<ContextoP, bool>? visible = null, string? ayuda = null, Func<ContextoP, string?>? motivo = null, int prioridad = 0, string? grupo = null, Func<string, string>? normaliza = null, bool lecturaAsimetrica = false, Func<ContextoP, IEnumerable<string>>? afectaA = null)
        => new() { AfectaA = afectaA, LecturaAsimetrica = lecturaAsimetrica, Id = id, Etiqueta = etiqueta, Tipo = TipoP.Texto, Prioridad = prioridad, GrupoEscritura = grupo, Normaliza = normaliza, Leer = c => leer(c), Escribir = escribir is null ? null : (c, v) => escribir(c, (string)v!), MaxLongitud = maxLongitud, MaxLongitudFn = maxFn, Visible = visible, Ayuda = ayuda, Motivo = motivo };

    public static CampoP Entero(string id, string etiqueta, Func<ContextoP, long> leer, Action<ContextoP, long>? escribir, long min = 0, long max = int.MaxValue, Func<ContextoP, long>? maxFn = null, Func<ContextoP, bool>? visible = null, string? ayuda = null, string? sufijo = null, Func<ContextoP, string?>? detalle = null, bool hex = false, Func<ContextoP, string?>? motivo = null, int prioridad = 0, string? grupo = null, Func<ContextoP, IEnumerable<string>>? afectaA = null, bool lecturaAsimetrica = false, Func<ContextoP, long, string?>? validarValor = null, Func<ContextoP, string>? etiquetaFn = null)
        => new() { EtiquetaFn = etiquetaFn, ValidarValor = validarValor, LecturaAsimetrica = lecturaAsimetrica, AfectaA = afectaA, Id = id, Etiqueta = etiqueta, Tipo = TipoP.Entero, GrupoEscritura = grupo, Leer = c => leer(c), Escribir = escribir is null ? null : (c, v) => escribir(c, (long)v!), Min = min, Max = max, MaxFn = maxFn, Visible = visible, Ayuda = ayuda, Sufijo = sufijo, Detalle = detalle, Hex = hex, Motivo = motivo, Prioridad = prioridad };

    public static CampoP Decimal(string id, string etiqueta, Func<ContextoP, double> leer, Action<ContextoP, double>? escribir, double min = -1e9, double max = 1e9, Func<ContextoP, bool>? visible = null, int decimales = 2, string? ayuda = null, string? grupo = null, Func<ContextoP, IEnumerable<string>>? afectaA = null, bool lecturaAsimetrica = false)
        => new() { LecturaAsimetrica = lecturaAsimetrica, AfectaA = afectaA, Id = id, Etiqueta = etiqueta, Tipo = TipoP.Decimal, GrupoEscritura = grupo, Leer = c => Math.Round(leer(c), decimales), Escribir = escribir is null ? null : (c, v) => escribir(c, (double)v!), Min = (long)min, Max = (long)max, Visible = visible, Decimales = decimales, Ayuda = ayuda };

    public static CampoP Opcion(string id, string etiqueta, Func<ContextoP, long> leer, Action<ContextoP, long>? escribir, Func<ContextoP, IReadOnlyList<OpcionP>> opciones, Func<ContextoP, bool>? visible = null, string[]? reinicia = null, string? ayuda = null, string? titulo = null, Func<ContextoP, string?>? motivo = null, int prioridad = 0, Func<ContextoP, IReadOnlyDictionary<string, object?>>? deriva = null, bool soloAccion = false, bool lecturaAsimetrica = false, Func<ContextoP, string?>? detalle = null, Func<ContextoP, IEnumerable<string>>? afectaA = null, string? grupo = null, bool especie = false)
        => new() { EsEspecie = especie, GrupoEscritura = grupo, AfectaA = afectaA, Id = id, Etiqueta = etiqueta, Tipo = TipoP.Opcion, Deriva = deriva, SoloAccion = soloAccion, LecturaAsimetrica = lecturaAsimetrica, Detalle = detalle, Leer = c => leer(c), Escribir = escribir is null ? null : (c, v) => escribir(c, (long)v!), Opciones = opciones, Visible = visible, Reinicia = reinicia ?? [], Ayuda = ayuda, TituloSelector = titulo ?? etiqueta, Motivo = motivo, Prioridad = prioridad };

    public static CampoP Marca(string id, string etiqueta, Func<ContextoP, bool> leer, Action<ContextoP, bool>? escribir, Func<ContextoP, bool>? visible = null, string? ayuda = null, Func<ContextoP, string?>? motivo = null, int prioridad = 0, Func<ContextoP, IEnumerable<string>>? afectaA = null, bool lecturaAsimetrica = false, bool sinEscritura = false, Func<ContextoP, IReadOnlyDictionary<string, object?>>? deriva = null, string? grupo = null)
        => new() { SinEscritura = sinEscritura, Deriva = deriva, GrupoEscritura = grupo, LecturaAsimetrica = lecturaAsimetrica, AfectaA = afectaA, Id = id, Etiqueta = etiqueta, Tipo = TipoP.Marca, Leer = c => leer(c), Escribir = escribir is null ? null : (c, v) => escribir(c, (bool)v!), Visible = visible, Ayuda = ayuda, Motivo = motivo, Prioridad = prioridad };

    public static CampoP FechaHora(string id, string etiqueta, Func<ContextoP, DateTime> leer, Action<ContextoP, DateTime>? escribir, Func<ContextoP, bool>? visible = null, string? ayuda = null)
        => new() { Id = id, Etiqueta = etiqueta, Tipo = TipoP.FechaHora, Leer = c => leer(c), Escribir = escribir is null ? null : (c, v) => escribir(c, (DateTime)v!), Visible = visible, Ayuda = ayuda };

    public static CampoP Info(string id, string etiqueta, Func<ContextoP, string> leer, Func<ContextoP, bool>? visible = null)
        => new() { Id = id, Etiqueta = etiqueta, Tipo = TipoP.Info, Leer = c => leer(c), Visible = visible };

    /// <summary>Lee una vez el ID; su texto se obtiene del idioma actual sin sustituir originales ni borrador.</summary>
    public static CampoP Info(string id, string etiqueta, Func<ContextoP, long> leer, Func<ContextoP, long, string> presentar, Func<ContextoP, bool>? visible = null)
        => new() { Id = id, Etiqueta = etiqueta, Tipo = TipoP.Info, Leer = c => leer(c), PresentarInfo = presentar, Visible = visible };

    /// <summary>
    /// Una accion de un solo sentido (boton del formulario de PKHeX): queda pendiente en el borrador y <paramref name="ejecutar"/> se llama al aplicar, dentro de la transaccion del historial.
    /// <paramref name="motivo"/> la deja fija con su explicacion cuando ya no se puede hacer («Ya está en el mapa»).
    /// </summary>
    public static CampoP Accion(string id, string etiqueta, Action<ContextoP> ejecutar, Func<ContextoP, string?>? motivo = null, Func<ContextoP, bool>? visible = null, string? ayuda = null, string? textoBoton = null, string? confirmacion = null, int prioridad = 0, Func<ContextoP, string?>? detalle = null, Func<ContextoP, IEnumerable<string>>? afectaA = null, bool inmediata = false, Func<ContextoP, string>? etiquetaFn = null)
        => new() { EtiquetaFn = etiquetaFn, Inmediata = inmediata, Id = id, Etiqueta = etiqueta, Tipo = TipoP.Accion, Leer = _ => false, Escribir = (c, v) => { if (v is true) ejecutar(c); }, Motivo = motivo, Visible = visible, Ayuda = ayuda, TextoBoton = textoBoton, Confirmacion = confirmacion, Prioridad = prioridad, Detalle = detalle, AfectaA = afectaA };

    /// <summary>
    /// Importar (y opcionalmente exportar) un archivo: el contenido elegido queda en el borrador y <paramref name="escribir"/> lo coloca en la partida al aplicar. <paramref name="validar"/> rechaza con su motivo lo que no sirve.
    /// <paramref name="exporta"/> devuelve el nombre y los bytes de lo que hay ahora en la partida (null = nada que exportar); exportar nunca toca la partida.
    /// </summary>
    public static CampoP Archivo(string id, string etiqueta, Action<ContextoP, byte[]>? escribir, Func<ContextoP, byte[], string?>? validar = null, Func<ContextoP, (string Nombre, byte[] Datos)?>? exporta = null, string? extension = null, string? textoBoton = null, Func<ContextoP, bool>? visible = null, string? ayuda = null, Func<ContextoP, string?>? motivo = null, int prioridad = 0, string? confirmacion = null, Func<ContextoP, IEnumerable<string>>? afectaA = null, Func<ContextoP, ImagenJugador?>? vista = null)
        => new() { SoloExporta = escribir is null, Vista = vista, Id = id, Etiqueta = etiqueta, Tipo = TipoP.Archivo, Leer = _ => null, Escribir = escribir is null ? null : (c, v) => { if (v is byte[] b) escribir(c, b); }, ValidarArchivo = validar, Exporta = exporta, Extension = extension, TextoBoton = textoBoton, Visible = visible, Ayuda = ayuda, Motivo = motivo, Prioridad = prioridad, Confirmacion = confirmacion, AfectaA = afectaA };

    public static IReadOnlyList<OpcionP> Lista(IEnumerable<string> textos) => [.. textos.Select((t, i) => new OpcionP(i, t))];
    public static IReadOnlyList<OpcionP> Lista(params string[] textos) => [.. textos.Select((t, i) => new OpcionP(i, t))];
    public static IReadOnlyList<OpcionP> Lista(IEnumerable<ComboItem> items) => [.. items.Select(i => new OpcionP(i.Value, i.Text))];

    /// <summary>Medallas: una casilla por bit de un entero de medallas (la escritura cambia solo su bit).</summary>
    public static IEnumerable<CampoP> Medallas(string prefijo, int cantidad, Func<ContextoP, int> leer, Action<ContextoP, int> escribir, Func<int, string>? nombre = null, Func<ContextoP, bool>? visible = null, int desde = 0)
    {
        for (var i = 0; i < cantidad; i++)
        {
            var bit = i + desde;
            yield return Marca($"{prefijo}{bit + 1}", nombre?.Invoke(bit) ?? IdiomaInterfaz.F("Form.Medalla", bit + 1), c => (leer(c) & (1 << bit)) != 0, (c, v) => escribir(c, v ? leer(c) | (1 << bit) : leer(c) & ~(1 << bit)), visible, IdiomaInterfaz.T("Form.MedallaAyuda"));
        }
    }
}

/// <summary>TID y SID como los muestra PKHeX (<c>TrainerIDManager</c>): 16 bits con 5 cifras hasta la 6.ª generacion y TID de 6 cifras + SID de 4 desde la 7.ª, con el TSV derivado.</summary>
internal static class IdsP
{
    public static bool SeisCifras(ContextoP c) => c.Sav.TrainerIDDisplayFormat == TrainerIDFormat.SixDigit;

    /// <summary>TSV de lo que hay en el borrador de TID y SID (el mismo calculo que <c>GetTSV</c>).</summary>
    public static string Tsv(ContextoP c, string idTid = "tid", string idSid = "sid")
    {
        var tid = (uint)c.Entero(idTid); var sid = (uint)c.Entero(idSid);
        uint id32 = SeisCifras(c) ? (sid * 1_000_000) + tid : ((sid & 0xFFFF) << 16) | (tid & 0xFFFF);
        var xor = (id32 >> 16) ^ (id32 & 0xFFFF);
        var tsv = c.Sav.Generation <= 5 ? xor >> 3 : xor >> 4;
        return $"TSV: {tsv:0000}";
    }

    private static bool Cabe(uint sid7, uint tid7) => ((ulong)sid7 * 1_000_000) + tid7 <= uint.MaxValue;   // = ITrainerID32.IsValidTrainerID7 de PKHeX

    public static IEnumerable<CampoP> Campos(SaveFile sav, string idTid = "tid", string idSid = "sid", bool visibleSid = true)
    {
        var seis = sav.TrainerIDDisplayFormat == TrainerIDFormat.SixDigit;
        // TID y SID de 6+4 cifras comparten un ID de 32 bits: la combinacion tiene que caber (IsValidTrainerID7 de PKHeX; fuera de rango el Core da la vuelta y deja SID 0 y un TID cualquiera).
        static long Actual(ContextoP c, string id, Func<ContextoP, long> leer) => c.Borrador.TryGetValue(id, out var v) && v is long l ? l : leer(c);
        string? ValidarTid(ContextoP c, long tid) { var sid = Actual(c, idSid, x => x.Sav.DisplaySID); return Cabe((uint)sid, (uint)tid) ? null : IdiomaInterfaz.F("Form.IdTidLimite", sid, uint.MaxValue - (uint)sid * 1_000_000UL); }
        string? ValidarSid(ContextoP c, long sid) { var tid = Actual(c, idTid, x => x.Sav.DisplayTID); return Cabe((uint)sid, (uint)tid) ? null : IdiomaInterfaz.F("Form.IdSidLimite", tid, (uint.MaxValue - (uint)tid) / 1_000_000); }
        yield return P.Entero(idTid, "TID", c => c.Sav.DisplayTID, (c, v) => c.Sav.DisplayTID = (uint)v, 0, seis ? 999_999 : 65_535, detalle: c => c.Sav.Generation > 2 ? Tsv(c, idTid, idSid) : null, validarValor: seis ? ValidarTid : null, ayuda: seis ? IdiomaInterfaz.T("Form.Tid6Ayuda") : IdiomaInterfaz.T("Form.Tid16Ayuda"));
        yield return P.Entero(idSid, "SID", c => c.Sav.DisplaySID, (c, v) => c.Sav.DisplaySID = (uint)v, 0, seis ? 4_294 : 65_535, visible: c => visibleSid && c.Sav.Generation > 2, detalle: c => Tsv(c, idTid, idSid), validarValor: seis ? ValidarSid : null, ayuda: seis ? IdiomaInterfaz.T("Form.Sid4Ayuda") : IdiomaInterfaz.T("Form.Sid16Ayuda"));
    }
}

/// <summary>Campos comunes de todos los editores de entrenador: tiempo de juego y datos del mapa en forma de vector.</summary>
internal static class ComunP
{
    /// <summary>Horas, minutos y segundos de juego (el Core las trata como un solo valor: se escriben juntas).</summary>
    public static IEnumerable<CampoP> Tiempo(int maxHoras = 65535)
    {
        yield return P.Entero("horas", IdiomaInterfaz.T("Form.HorasJugadas"), c => c.Sav.PlayedHours, (c, v) => c.Sav.PlayedHours = (ushort)v, 0, maxHoras, grupo: "tiempo");
        yield return P.Entero("minutos", IdiomaInterfaz.T("Form.Minutos"), c => c.Sav.PlayedMinutes, (c, v) => c.Sav.PlayedMinutes = (ushort)v % 60, 0, 59, grupo: "tiempo");
        yield return P.Entero("segundos", IdiomaInterfaz.T("Form.Segundos"), c => c.Sav.PlayedSeconds, (c, v) => c.Sav.PlayedSeconds = (ushort)v % 60, 0, 59, grupo: "tiempo");
    }

    public static string DatosBloque(ContextoP c, string k) => k;
}
