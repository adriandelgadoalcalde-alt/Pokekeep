// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Desafios;

/// <summary>
/// Una instalación con racha, récord o puntuación que se puede llevar al jefe o al máximo (Torre Batalla, Metro Batalla, Pokéathlon…).
/// Cada subclase declara sus modos con sus campos y, si hay base verificada, el plan de «Ir al jefe». Todo se escribe con la API del Core sobre la partida abierta:
/// quien llama guarda después con el flujo normal de PKHeX (la biblioteca no exporta bytes: regla 13 del proyecto).
/// </summary>
public abstract class InstalacionDesafio(string clave, TextoDoble nombre, TextoDoble juego)
{
    public string Clave { get; } = clave;
    public TextoDoble Nombre { get; } = nombre;
    /// <summary>Juego o juegos a los que pertenece, para los títulos de la interfaz (por ejemplo «Esmeralda»).</summary>
    public TextoDoble Juego { get; } = juego;

    /// <summary>Si esta partida tiene la instalación.</summary>
    public abstract bool Aplica(SaveFile sav);

    /// <summary>Modos de la instalación en esa partida (algunos juegos cambian el número de modos).</summary>
    public abstract IReadOnlyList<ModoDesafio> Modos(SaveFile sav);

    public ModoDesafio Modo(SaveFile sav, string clave) =>
        Modos(sav).FirstOrDefault(m => m.Clave == clave) ?? throw new ArgumentException($"{Clave} no tiene el modo «{clave}».", nameof(clave));

    public LecturaDesafio Leer(SaveFile sav, ModoDesafio modo) =>
        new([.. modo.Campos.Select(c => new ValorCampo(c.Clave, c.Nombre, c.Rol, c.Leer(sav)))]);

    /// <summary>Plan de «Ir al jefe». Por defecto, sin base verificada en el juego.</summary>
    internal virtual PlanJefe Planificar(SaveFile sav, ModoDesafio modo, JefeDesafio jefe) => PlanJefe.No("motivo.sin_comprobar");

    /// <summary>Ajustes de coherencia tras «Máximo» (contadores derivados de la racha, banderas de los trenes Super…).</summary>
    protected virtual void Coherencia(SaveFile sav, ModoDesafio modo) { }

    public Disponibilidad PuedeIrAlJefe(SaveFile sav, ModoDesafio modo, JefeDesafio jefe = JefeDesafio.Plata)
    {
        var plan = Planificar(sav, modo, jefe);
        return plan.Puede ? Disponibilidad.Si : new Disponibilidad(false, plan.Motivo);
    }

    /// <summary>Deja la racha del modo justo antes del combate con el jefe (solo si hay base verificada; si no, no toca nada y dice por qué).</summary>
    public ResultadoDesafio IrAlJefe(SaveFile sav, ModoDesafio modo, JefeDesafio jefe = JefeDesafio.Plata)
    {
        var plan = Planificar(sav, modo, jefe);
        if (!plan.Puede)
            return new ResultadoDesafio(Clave, modo.Clave, EstadoResultado.NoDisponible, plan.Motivo, null, null, []);
        var antes = Instantanea(sav, modo);
        foreach (var (clave, valor) in plan.Valores!)
            modo.Campo(clave).Escribir(sav, valor);
        return Resultado(sav, modo, antes, plan.Efectivo, plan.Explicacion);
    }

    /// <summary>Lleva todos los campos del modo a su tope (el del juego si se conoce; si no, el del Core o el del formulario de PKHeX) y sube el récord si era menor.</summary>
    public ResultadoDesafio Maximo(SaveFile sav, ModoDesafio modo)
    {
        var antes = Instantanea(sav, modo);
        EscribirMaximos(sav, modo);
        Coherencia(sav, modo);
        return Resultado(sav, modo, antes, null, null);
    }

    /// <summary>Escribe el tope de cada campo que lo tenga. Se sobrescribe donde «mejor» no es «mayor» (el tiempo de la Colina Entrenador).</summary>
    protected virtual void EscribirMaximos(SaveFile sav, ModoDesafio modo)
    {
        foreach (var c in modo.Campos)
        {
            if (c.Maximo is { } m)
                c.Escribir(sav, m);
        }
    }

    /// <summary>
    /// «Ir al jefe» en todos los modos. Con <paramref name="jefe"/> = <c>null</c> se elige en cada modo el primero que se pueda (plata, oro, repetido).
    /// Los modos sin base verificada salen como <see cref="EstadoResultado.NoDisponible"/>.
    /// </summary>
    public IReadOnlyList<ResultadoDesafio> IrAlJefeTodo(SaveFile sav, JefeDesafio? jefe = null)
    {
        var lista = new List<ResultadoDesafio>();
        foreach (var modo in Modos(sav))
        {
            if (jefe is { } j)
            {
                lista.Add(IrAlJefe(sav, modo, j));
                continue;
            }
            JefeDesafio elegido = JefeDesafio.Plata;
            foreach (var candidato in (JefeDesafio[])[JefeDesafio.Plata, JefeDesafio.Oro, JefeDesafio.Repetido])
            {
                if (PuedeIrAlJefe(sav, modo, candidato).Puede)
                {
                    elegido = candidato;
                    break;
                }
            }
            lista.Add(IrAlJefe(sav, modo, elegido));
        }
        return lista;
    }

    public IReadOnlyList<ResultadoDesafio> MaximoTodo(SaveFile sav) => [.. Modos(sav).Select(m => Maximo(sav, m))];

    private static long[] Instantanea(SaveFile sav, ModoDesafio modo) => [.. modo.Campos.Select(c => c.Leer(sav))];

    private ResultadoDesafio Resultado(SaveFile sav, ModoDesafio modo, long[] antes, JefeDesafio? jefe, TextoDoble? explicacion)
    {
        var cambios = new List<CambioDesafio>();
        for (var i = 0; i < modo.Campos.Count; i++)
        {
            var despues = modo.Campos[i].Leer(sav);
            if (despues != antes[i])
                cambios.Add(new CambioDesafio(modo.Campos[i].Clave, modo.Campos[i].Nombre, antes[i], despues));
        }
        return new ResultadoDesafio(Clave, modo.Clave, cambios.Count == 0 ? EstadoResultado.SinCambios : EstadoResultado.Aplicado, null, explicacion, jefe, cambios);
    }

    protected static CampoDesafio Campo(string clave, TextoDoble nombre, RolCampo rol, long? max, Func<SaveFile, long> leer, Action<SaveFile, long> escribir, long? tope = null)
        => new(clave, nombre, rol, max, leer, escribir, tope);

    protected static CampoDesafio Campo(string clave, string claveTexto, RolCampo rol, long? max, Func<SaveFile, long> leer, Action<SaveFile, long> escribir, long? tope = null)
        => new(clave, Textos.Doble(claveTexto), rol, max, leer, escribir, tope);

    public override string ToString() => $"{Clave}: {Nombre.Es}";
}
