// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Security.Cryptography;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida;

/// <summary>Acceso a una zona de memoria de trabajo del guardado (un delegado, porque un <see cref="Span{T}"/> no puede guardarse en una lista).</summary>
internal delegate Span<byte> AccesoRegion(SaveFile sav);

/// <summary>
/// Foto de TODA la memoria de trabajo en la que los editores de partida escriben: los datos del guardado (<c>Data</c>), los tres bufferes de la 3.ª generacion, el bloque general de la 4.ª y, en Gen. 8-9 con
/// bloques cifrados (SwishCrypto), el tipo y los bytes de cada bloque. Sirve para que Deshacer/Rehacer de un cambio de partida sea EXACTO (los mismos bytes que habia, incluida la basura que el Core no
/// vuelve a escribir igual) sin que cada editor tenga que saber invertirse (ADR 0061).
/// </summary>
internal sealed class InstantaneaGuardado
{
    private readonly byte[][] _regiones;
    private readonly (SCTypeCode Tipo, byte[] Datos)[] _bloques;

    private InstantaneaGuardado(byte[][] regiones, (SCTypeCode, byte[])[] bloques) { _regiones = regiones; _bloques = bloques; }

    /// <summary>Las zonas de memoria de este guardado, en un orden estable.</summary>
    public static IReadOnlyList<AccesoRegion> Regiones(SaveFile sav)
    {
        var l = new List<AccesoRegion> { s => s.Data };
        switch (sav)
        {
            case SAV3: l.Add(s => ((SAV3)s).Small); l.Add(s => ((SAV3)s).Large); l.Add(s => ((SAV3)s).Storage); break;
            case SAV4: l.Add(s => ((SAV4)s).General); break;
            // Gen. 1 y 2 guardan el equipo, las cajas y la guarderia desempaquetados en una memoria PRIVADA (Reserved) que no es Data: sin ella, un cambio del equipo no entraria en el historial ni se desharia.
            case SAV1 or SAV2 when ReservadaGen12(sav) is { } campo: l.Add(s => ((Memory<byte>)campo.GetValue(s)!).Span); break;
        }
        return l;
    }

    private static System.Reflection.FieldInfo? ReservadaGen12(SaveFile sav)
        => sav.GetType().GetField("Reserved", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) is { } f && f.FieldType == typeof(Memory<byte>) ? f : null;

    public static InstantaneaGuardado Tomar(SaveFile sav)
    {
        var acc = Regiones(sav);
        var reg = new byte[acc.Count][];
        for (var i = 0; i < acc.Count; i++) reg[i] = acc[i](sav).ToArray();
        (SCTypeCode, byte[])[] bl = [];
        if (sav is ISCBlockArray sc)
        {
            var todos = sc.AllBlocks;
            bl = new (SCTypeCode, byte[])[todos.Count];
            for (var i = 0; i < todos.Count; i++) bl[i] = (todos[i].Type, todos[i].Data.ToArray());
        }
        return new InstantaneaGuardado(reg, bl);
    }

    /// <summary>Lo que cambio entre dos fotos; null si no cambio ni un byte.</summary>
    public static DeltaGuardado? Diferencia(InstantaneaGuardado antes, InstantaneaGuardado despues)
    {
        var trozos = new List<Trozo>();
        for (var r = 0; r < antes._regiones.Length; r++)
            AñadirDiferencias(trozos, r, false, antes._regiones[r], despues._regiones[r], default, default);
        for (var b = 0; b < antes._bloques.Length; b++)
        {
            var (ta, da) = antes._bloques[b]; var (td, dd) = despues._bloques[b];
            if (ta == td && da.AsSpan().SequenceEqual(dd)) continue;
            AñadirDiferencias(trozos, b, true, da, dd, ta, td, forzarTipo: ta != td);
        }
        return trozos.Count == 0 ? null : new DeltaGuardado(trozos);
    }

    private static void AñadirDiferencias(List<Trozo> trozos, int origen, bool esBloque, byte[] a, byte[] d, SCTypeCode ta, SCTypeCode td, bool forzarTipo = false)
    {
        if (a.Length != d.Length) throw new InvalidOperationException(IdiomaInterfaz.F("HistorialPartida.LaZonaCambioDe", (esBloque ? IdiomaInterfaz.T("HistorialPartida.Bloque") : ""), origen, a.Length, d.Length));
        var n = a.Length;
        var i = 0; var añadido = false;
        while (i < n)
        {
            if (a[i] == d[i]) { i++; continue; }
            var ini = i; var fin = i + 1; var huecos = 0;
            for (var j = i + 1; j < n; j++)
            {
                if (a[j] != d[j]) { fin = j + 1; huecos = 0; }
                else if (++huecos > 24) break;
            }
            trozos.Add(new Trozo(origen, esBloque, ini, a[ini..fin], d[ini..fin], ta, td));
            añadido = true;
            i = fin;
        }
        if (!añadido && forzarTipo) trozos.Add(new Trozo(origen, esBloque, 0, [], [], ta, td));
    }

    internal sealed record Trozo(int Origen, bool EsBloque, int Desplazamiento, byte[] Antes, byte[] Despues, SCTypeCode TipoAntes, SCTypeCode TipoDespues);
}

/// <summary>Los bytes que un editor cambio en el guardado, con lo que habia antes y lo que hay despues; se aplica en cualquiera de los dos sentidos.</summary>
internal sealed class DeltaGuardado(IReadOnlyList<InstantaneaGuardado.Trozo> trozos)
{
    public IReadOnlyList<InstantaneaGuardado.Trozo> Trozos { get; } = trozos;
    public int Bytes => Trozos.Sum(t => t.Antes.Length);

    public void Aplicar(SaveFile sav, bool haciaAntes)
    {
        var acc = InstantaneaGuardado.Regiones(sav);
        var bloques = (sav as ISCBlockArray)?.AllBlocks;
        foreach (var t in Trozos)
        {
            var datos = haciaAntes ? t.Antes : t.Despues;
            if (t.EsBloque)
            {
                var b = bloques![t.Origen];
                if (t.Desplazamiento + datos.Length > b.Data.Length) throw new InvalidOperationException(IdiomaInterfaz.T("HistorialPartida.ElBloqueYaNo"));
                datos.CopyTo(b.Data[t.Desplazamiento..]);
                var tipo = haciaAntes ? t.TipoAntes : t.TipoDespues;
                if (b.Type != tipo && b.Type is SCTypeCode.Bool1 or SCTypeCode.Bool2) b.ChangeBooleanType(tipo);
            }
            else
            {
                var r = acc[t.Origen](sav);
                if (t.Desplazamiento + datos.Length > r.Length) throw new InvalidOperationException(IdiomaInterfaz.T("HistorialPartida.LaZonaDeMemoria"));
                datos.CopyTo(r[t.Desplazamiento..]);
            }
        }
    }
}

/// <summary>
/// Reversion de un cambio de PARTIDA para el historial del Core (<see cref="SlotChangelog.ISlotReversion"/>): aplica el <see cref="DeltaGuardado"/> hacia antes o hacia despues. Asi un cambio de partida
/// entra en el MISMO historial que las casillas (un solo Deshacer/Rehacer coherente) y se deshace byte a byte, sin tocar el Core.
/// </summary>
internal sealed class ReversionPartida(DeltaGuardado delta, bool haciaAntes, string descripcion) : SlotChangelog.ISlotReversion
{
    /// <summary>Que hizo este cambio, en castellano («Mochila: Objetos», «Entrenador»...): lo muestra la pantalla de historial.</summary>
    public string Descripcion { get; } = descripcion;
    public IReadOnlyList<ISlotInfo> Slots => [];
    public SlotChangelog.ISlotReversion CreateInverse(SaveFile parent) => new ReversionPartida(delta, !haciaAntes, Descripcion);
    public void Revert(SaveFile parent) => delta.Aplicar(parent, haciaAntes);
}

/// <summary>Resultado de aplicar un editor de partida.</summary>
public enum ResultadoPartida { Aplicado, SinCambios }

/// <summary>Lo que paso al aplicar un editor de partida: el resultado, la descripcion con la que queda en el historial y cuantos bytes del guardado cambiaron.</summary>
public readonly record struct InformePartida(ResultadoPartida Resultado, string Descripcion, int Bytes)
{
    public bool Aplicado => Resultado == ResultadoPartida.Aplicado;
    public bool SinCambios => Resultado == ResultadoPartida.SinCambios;
}

public static class EscrituraPartida
{
#if POKEKEEP_PRUEBAS
    /// <summary>Solo compilaciones de PRUEBA (Debug o <c>-p:PokekeepPruebas=true</c>; nunca el APK de uso): un fallo que se lanza dentro de <see cref="Aplicar"/> como si lo diera el Core (por hilo, para no afectar a otras pruebas); la partida se restaura y se relanza.</summary>
    [ThreadStatic] internal static Exception? FalloInyectado;
#endif

    /// <summary>
    /// Ejecuta <paramref name="accion"/> (que modifica el guardado con la API del Core, igual que el boton «Guardar» del formulario de PKHeX) como UNA entrada del historial de la sesion.
    /// Si no cambia ni un byte no queda entrada ni cambio pendiente; si la accion lanza, todo vuelve a como estaba y la excepcion sigue su camino.
    /// </summary>
    public static InformePartida Aplicar(Sesion sesion, string descripcion, Action accion)
    {
        var sav = sesion.Guardado;
        var antes = InstantaneaGuardado.Tomar(sav);
        try
        {
#if POKEKEEP_PRUEBAS
            if (FalloInyectado is { } fallo) throw fallo;
#endif
            accion();
        }
        catch
        {
            if (InstantaneaGuardado.Diferencia(antes, InstantaneaGuardado.Tomar(sav)) is { } d) d.Aplicar(sav, haciaAntes: true);
            throw;
        }
        var delta = InstantaneaGuardado.Diferencia(antes, InstantaneaGuardado.Tomar(sav));
        if (delta is null) return new InformePartida(ResultadoPartida.SinCambios, descripcion, 0);
        using var cambio = new SlotChangelog.Change(sesion.Casillas.Historial.Changelog, new ReversionPartida(delta, haciaAntes: true, descripcion));
        cambio.Commit();
        sesion.Casillas.Notificar(EditorCasillas.TipoCambio.Escritura, [], descripcion);
        return new InformePartida(ResultadoPartida.Aplicado, descripcion, delta.Bytes);
    }

    /// <summary>Huella del contenido del guardado tal como se escribiria (para las pruebas).</summary>
    public static string Huella(SaveFile sav)
    {
        try { return Convert.ToHexString(SHA256.HashData(sav.Write().Span)); }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or InvalidOperationException or NotSupportedException)
        {
            // Una partida en blanco del Core (solo en pruebas) no siempre se puede exportar: se compara su memoria de trabajo.
            var sb = new System.IO.MemoryStream();
            foreach (var r in InstantaneaGuardado.Regiones(sav)) sb.Write(r(sav));
            if (sav is ISCBlockArray sc) foreach (var b in sc.AllBlocks) sb.Write(b.Data);
            return Convert.ToHexString(SHA256.HashData(sb.ToArray()));
        }
    }
}
