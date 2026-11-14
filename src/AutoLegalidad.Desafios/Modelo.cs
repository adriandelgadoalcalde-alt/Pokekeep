// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Desafios;

/// <summary>Jefe al que se lleva la racha. «Plata» es la primera aparición, «Oro» la segunda y «Repetido» las siguientes (Esmeralda las repite cada cierto número de combates cuando ya se tienen los dos símbolos).</summary>
public enum JefeDesafio { Plata, Oro, Repetido }

/// <summary>Qué representa un campo de una instalación (para que la interfaz sepa cuál es la racha y cuál el récord).</summary>
public enum RolCampo { RachaActual, Record, Contador, Bandera, Otro }

/// <summary>Nivel de combate del modo, donde el juego distingue entre Nv. 50 y Abierto.</summary>
public enum NivelDesafio { NoAplica, Nv50, Abierto }

/// <summary>Estado de una acción sobre un modo.</summary>
public enum EstadoResultado
{
    /// <summary>Se escribió algo distinto de lo que había.</summary>
    Aplicado,
    /// <summary>La partida ya tenía esos valores.</summary>
    SinCambios,
    /// <summary>No se pudo (o no hay base verificada): <see cref="ResultadoDesafio.Motivo"/> dice por qué y no se tocó nada.</summary>
    NoDisponible,
}

/// <summary>Texto en español de España y en inglés. La interfaz elige el que toque.</summary>
public readonly record struct TextoDoble(string Es, string En)
{
    public string Para(bool espanol) => espanol ? Es : En;
    public override string ToString() => Es;
}

/// <summary>Un dato guardado de un modo. <see cref="Maximo"/> es el valor que escribe «Máximo»; <c>null</c> = «Máximo» no lo toca (sin tope conocido).</summary>
public sealed class CampoDesafio(string clave, TextoDoble nombre, RolCampo rol, long? maximo, Func<SaveFile, long> leer, Action<SaveFile, long> escribir)
{
    public string Clave { get; } = clave;
    public TextoDoble Nombre { get; } = nombre;
    public RolCampo Rol { get; } = rol;
    public long? Maximo { get; } = maximo;
    public long Leer(SaveFile sav) => leer(sav);
    public void Escribir(SaveFile sav, long valor) => escribir(sav, valor);
}

/// <summary>Una combinación de la instalación (por ejemplo «Individual, Nv. 50» de la Torre). Tiene sus propios campos.</summary>
public sealed class ModoDesafio(string clave, TextoDoble nombre, NivelDesafio nivel, IReadOnlyList<CampoDesafio> campos)
{
    public string Clave { get; } = clave;
    public TextoDoble Nombre { get; } = nombre;
    public NivelDesafio Nivel { get; } = nivel;
    public IReadOnlyList<CampoDesafio> Campos { get; } = campos;
    internal CampoDesafio? Rol(RolCampo rol) => Campos.FirstOrDefault(c => c.Rol == rol);
    internal CampoDesafio Campo(string clave) => Campos.First(c => c.Clave == clave);
    public override string ToString() => $"{Clave}: {Nombre.Es}";
}

public readonly record struct ValorCampo(string Clave, TextoDoble Nombre, RolCampo Rol, long Valor);

/// <summary>Lo que hay guardado ahora en un modo.</summary>
public sealed class LecturaDesafio(IReadOnlyList<ValorCampo> valores)
{
    public IReadOnlyList<ValorCampo> Valores { get; } = valores;
    public long? RachaActual => Valores.Where(v => v.Rol == RolCampo.RachaActual).Select(v => (long?)v.Valor).FirstOrDefault();
    public long? Record => Valores.Where(v => v.Rol == RolCampo.Record).Select(v => (long?)v.Valor).FirstOrDefault();
    public long? Valor(string clave) => Valores.Where(v => v.Clave == clave).Select(v => (long?)v.Valor).FirstOrDefault();
}

/// <summary>Si se puede ir al jefe en un modo, y si no, por qué.</summary>
public readonly record struct Disponibilidad(bool Puede, TextoDoble? Motivo)
{
    public static Disponibilidad Si { get; } = new(true, null);
    public static Disponibilidad No(string claveMotivo) => new(false, Textos.Doble(claveMotivo));
}

public readonly record struct CambioDesafio(string Campo, TextoDoble Nombre, long Antes, long Despues);

public sealed record ResultadoDesafio(
    string Instalacion, string Modo, EstadoResultado Estado, TextoDoble? Motivo, TextoDoble? Explicacion, JefeDesafio? JefeAplicado, IReadOnlyList<CambioDesafio> Cambios);

/// <summary>Lo que escribe «Ir al jefe» en un modo: valores por clave de campo (las claves que no estén no se tocan) y qué jefe es en realidad el elegido.</summary>
internal sealed record PlanJefe(bool Puede, TextoDoble? Motivo, IReadOnlyDictionary<string, long>? Valores, JefeDesafio? Efectivo, TextoDoble? Explicacion)
{
    public static PlanJefe No(string clave) => new(false, Textos.Doble(clave), null, null, null);
    public static PlanJefe Si(JefeDesafio efectivo, IReadOnlyDictionary<string, long> valores, string? claveExplicacion = null)
        => new(true, null, valores, efectivo, claveExplicacion is null ? null : Textos.Doble(claveExplicacion));
}
