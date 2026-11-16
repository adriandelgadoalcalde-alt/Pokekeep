// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Desafios;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios;

/// <summary>
/// R-013 en Android: «Ir al jefe» y «Máximo» de cada instalación y modo, y «Todo al jefe» / «Todo al máximo» de cada formulario. La lógica y los números viven en <c>AutoLegalidad.Desafios</c> (ADR 0106, cada número con su fuente):
/// aquí solo se llama a la biblioteca, sin repetir ningún número ni filtro. Cada botón es una <see cref="TipoP.Accion"/> inmediata (como <c>Dar todo</c> del Pokéathlon): actúa sobre la partida de golpe, como UNA entrada del historial
/// (Deshacer la revierte entera), los campos del formulario se vuelven a leer y la pantalla enseña un aviso que cuenta qué ha cambiado (<see cref="ContextoP.Aviso"/>).
/// <para>Si la biblioteca dice que no se puede ir al jefe (<see cref="InstalacionDesafio.PuedeIrAlJefe"/>), el botón no se ofrece y en su lugar se lee el motivo de la biblioteca, el patrón de las acciones deshabilitadas de esta app (en el móvil no hay ayuda emergente).</para>
/// </summary>
internal static class DesafiosP
{
    /// <summary>Cómo se pinta el apartado de desafíos de un formulario.</summary>
    /// <param name="PorModo">Un par de botones por instalación y modo (Torre, Metro…). Con <c>false</c> solo los de «Todo» del formulario (Pokéathlon, PWT, PB del Metro: un único dato por modo).</param>
    /// <param name="ConCampos">Los datos de cada modo se editan en el mismo apartado (los formularios nuevos de lo que no tenía editor).</param>
    public sealed record Opciones(bool PorModo = true, bool ConCampos = false);

    /// <summary>El id de la lista de instalaciones (no puede ser «instalaciones»: la pestaña «Frente» de Gen. 3 y 4 ya tiene una colección con ese id).</summary>
    internal const string IdColeccion = "desafios_inst";

    private static readonly JefeDesafio[] Jefes = [JefeDesafio.Plata, JefeDesafio.Oro, JefeDesafio.Repetido];

    private static bool Espanol => IdiomaInterfaz.Actual.CodigoCore != "en";
    /// <summary>Un texto de la biblioteca en el idioma de la interfaz.</summary>
    internal static string T(TextoDoble t) => t.Para(Espanol);
    private static string Tx(string clave) => IdiomaInterfaz.T(clave);

    /// <summary>Instalaciones con un jefe al que ir. La Colina Entrenador, los PB del Metro, la PWT y el Pokéathlon son marcadores sin combate final: no ofrecen «Ir al jefe» y no se finge un botón sin sentido.</summary>
    internal static bool TieneJefe(InstalacionDesafio i) => i is not (ColinaEntrenador3 or PuntosMetro5 or Pwt5 or Pokeathlon4Maximo);

    /// <summary>La Frontera de Esmeralda y las instalaciones de Gen. 4 tienen jefe de plata, de oro y repetido; en el resto el jefe lo fija el modo (Metro normal = plata, Super = oro) y basta un botón.</summary>
    private static bool JefesExplicitos(InstalacionDesafio i) => i is Frente3Esmeralda or Frente4;

    // ---------------------------------------------------------------------------------------------- pestañas
    /// <summary>
    /// Las pestañas «Desafíos» de un formulario para las instalaciones que cumplan <paramref name="filtro"/> en esta partida: una plana con «Todo» (y, con una sola instalación, sus modos) y, si hay varias instalaciones, otra con
    /// una lista de instalaciones. Vacío si la partida no tiene ninguna.
    /// </summary>
    public static IReadOnlyList<PestanaP> Pestanas(SaveFile sav, Func<InstalacionDesafio, bool> filtro, Opciones? opciones = null)
    {
        var o = opciones ?? new Opciones();
        var insts = CatalogoDesafios.Para(sav).Where(filtro).ToList();
        if (insts.Count == 0) return [];
        var conJefe = insts.Any(TieneJefe);
        var secciones = new List<SeccionP> { new(string.Empty, [.. TodoDelFormulario(insts, conJefe)]) };
        var pestanas = new List<PestanaP>();
        if (o.PorModo && insts.Count == 1)
            foreach (var modo in insts[0].Modos(sav)) secciones.AddRange(SeccionesDeModo(insts[0], sav, modo, clave => $"{insts[0].Clave}.{modo.Clave}.{clave}", o, soloUnModo: insts[0].Modos(sav).Count == 1));
        else if (o.ConCampos)
            foreach (var inst in insts) foreach (var modo in inst.Modos(sav)) secciones.AddRange(SeccionesDeModo(inst, sav, modo, clave => $"{inst.Clave}.{modo.Clave}.{clave}", o, soloUnModo: inst.Modos(sav).Count == 1));
        pestanas.Add(new PestanaP("desafios", Tx("Desafios.Pestana"), secciones));
        if (o.PorModo && insts.Count > 1)
        {
            ColeccionDefP? col = null;
            col = new ColeccionDefP
            {
                Id = IdColeccion, Elemento = Tx("Desafios.Instalacion"), Cantidad = _ => insts.Count, Titulo = (_, i) => T(insts[i].Nombre),
                Resumen = (c, i) => T(insts[i].Juego),
                Secciones = (c, i) => [.. insts[i].Modos(c.Sav).SelectMany(m => SeccionesDeModo(insts[i], c.Sav, m, clave => col!.IdCampo(i, $"{m.Clave}.{clave}"), o, soloUnModo: false))],
            };
            pestanas.Add(new PestanaP("desafios_instalaciones", Tx("Desafios.Instalaciones"), [], Coleccion: col));
        }
        return pestanas;
    }

    /// <summary>El editor con las pestañas de desafíos añadidas al final (sin tocar nada de lo que ya tenía).</summary>
    public static EditorDefP? ConDesafios(EditorDefP? def, SaveFile sav, Func<InstalacionDesafio, bool> filtro, Opciones? opciones = null)
    {
        if (def is null) return null;
        var nuevas = Pestanas(sav, filtro, opciones);
        return nuevas.Count == 0 ? def : def with { Pestanas = [.. def.Pestanas, .. nuevas] };
    }

    // ---------------------------------------------------------------------------------------------- «Todo» del formulario
    private static IEnumerable<CampoP> TodoDelFormulario(IReadOnlyList<InstalacionDesafio> insts, bool conJefe)
    {
        if (conJefe)
            yield return P.Accion("todo_jefe", Tx("Desafios.TodoAlJefe"),
                c => c.Aviso = ResumenTodo(Tx("Desafios.TodoAlJefe"), insts.SelectMany(i => i.IrAlJefeTodo(c.Sav)).ToList()),
                motivo: c => insts.Where(TieneJefe).Any(i => i.Modos(c.Sav).Any(m => Jefes.Any(j => i.PuedeIrAlJefe(c.Sav, m, j).Puede))) ? null : Tx("Desafios.NingunJefeComprobado"),
                ayuda: Tx("Desafios.AyudaTodoAlJefe"), textoBoton: Tx("Desafios.TodoAlJefe"), confirmacion: Tx("Desafios.ConfirmaTodoAlJefe"), inmediata: true,
                textoHistorial: c => $"{Tx("Desafios.TodoAlJefe")} · {Nombres(insts)}");
        yield return P.Accion("todo_maximo", Tx("Desafios.TodoAlMaximo"),
            c => c.Aviso = ResumenTodo(Tx("Desafios.TodoAlMaximo"), insts.SelectMany(i => i.MaximoTodo(c.Sav)).ToList()),
            ayuda: Tx("Desafios.AyudaTodoAlMaximo"), textoBoton: Tx("Desafios.TodoAlMaximo"), confirmacion: Tx("Desafios.ConfirmaTodoAlMaximo"), inmediata: true,
            textoHistorial: c => $"{Tx("Desafios.TodoAlMaximo")} · {Nombres(insts)}");
    }

    private static string Nombres(IReadOnlyList<InstalacionDesafio> insts) => insts.Count == 1 ? T(insts[0].Nombre) : T(insts[0].Juego);

    // ---------------------------------------------------------------------------------------------- un modo
    /// <summary>La sección de un modo: lo que hay ahora (o sus campos, si el formulario los edita), «Ir al jefe» y «Máximo».</summary>
    internal static IReadOnlyList<SeccionP> SeccionesDeModo(InstalacionDesafio inst, SaveFile sav, ModoDesafio modo, Func<string, string> id, Opciones o, bool soloUnModo)
    {
        var campos = new List<CampoP>();
        if (o.ConCampos)
            campos.AddRange(modo.Campos.Select((campo, k) => Campo(inst, modo, campo, id(campo.Clave), k)));
        else if (modo.Campos.Any(c => c.Rol is RolCampo.RachaActual or RolCampo.Record))
            campos.Add(P.Info(id("ahora"), Tx("Desafios.Ahora"), c => Ahora(inst, c.Sav, modo)));
        if (!o.PorModo) return campos.Count == 0 ? [] : [new SeccionP(soloUnModo ? string.Empty : T(modo.Nombre), campos)];
        if (TieneJefe(inst))
        {
            if (JefesExplicitos(inst)) foreach (var j in Jefes) campos.Add(BotonJefe(inst, modo, id, j));
            else campos.Add(BotonJefe(inst, modo, id, null));
        }
        var maximo = Tx("Desafios.Maximo");
        campos.Add(P.Accion(id("maximo"), maximo, c => c.Aviso = Resumen(inst, modo, inst.Maximo(c.Sav, modo), maximo),
            ayuda: Tx("Desafios.AyudaMaximo"), textoBoton: maximo, inmediata: true, prioridad: 90,
            textoHistorial: c => $"{maximo} · {T(inst.Nombre)}{(soloUnModo ? string.Empty : ", " + T(modo.Nombre))}"));
        return [new SeccionP(soloUnModo ? string.Empty : T(modo.Nombre), campos)];
    }

    private static CampoP BotonJefe(InstalacionDesafio inst, ModoDesafio modo, Func<string, string> id, JefeDesafio? jefe)
    {
        var etiqueta = jefe is { } j ? Tx("Desafios.IrAlJefe." + j.ToString().ToLowerInvariant()) : Tx("Desafios.IrAlJefe");
        return P.Accion(id(jefe is { } k ? "jefe_" + k.ToString().ToLowerInvariant() : "jefe"), etiqueta,
            c => c.Aviso = Resumen(inst, modo, inst.IrAlJefe(c.Sav, modo, jefe ?? PrimerJefe(inst, c.Sav, modo)), etiqueta),
            motivo: c => MotivoJefe(inst, c.Sav, modo, jefe), ayuda: Tx("Desafios.AyudaIrAlJefe"), textoBoton: Tx("Desafios.IrAlJefe"), inmediata: true, prioridad: 80 + (jefe is { } x ? (int)x : 0),
            textoHistorial: c => $"{etiqueta} · {T(inst.Nombre)}, {T(modo.Nombre)}");
    }

    /// <summary>El primer jefe que se puede (plata, oro, repetido), como en <c>IrAlJefeTodo</c>; si ninguno, el de plata (para que el motivo sea el suyo).</summary>
    internal static JefeDesafio PrimerJefe(InstalacionDesafio inst, SaveFile sav, ModoDesafio modo)
        => Jefes.FirstOrDefault(j => inst.PuedeIrAlJefe(sav, modo, j).Puede, JefeDesafio.Plata);

    /// <summary>El motivo de la biblioteca por el que no se puede ir al jefe (en el idioma de la interfaz), o null si se puede.</summary>
    internal static string? MotivoJefe(InstalacionDesafio inst, SaveFile sav, ModoDesafio modo, JefeDesafio? jefe)
    {
        var d = inst.PuedeIrAlJefe(sav, modo, jefe ?? PrimerJefe(inst, sav, modo));
        return d.Puede ? null : d.Motivo is { } m ? T(m) : Tx("Desafios.SinComprobar");
    }

    private static string Ahora(InstalacionDesafio inst, SaveFile sav, ModoDesafio modo)
        => string.Join(" · ", inst.Leer(sav, modo).Valores.Where(v => v.Rol is RolCampo.RachaActual or RolCampo.Record or RolCampo.Bandera).Select(v => $"{T(v.Nombre)}: {v.Valor}"));

    // ---------------------------------------------------------------------------------------------- campos editables (formularios nuevos)
    private static CampoP Campo(InstalacionDesafio inst, ModoDesafio modo, CampoDesafio campo, string id, int k)
    {
        var colina = inst is ColinaEntrenador3;
        // El tope de lo que se puede escribir a mano: el tope de la biblioteca cuando lo tiene (su «Máximo» o, en los campos que «Máximo» no toca, el declarado aparte: es el del juego o el del dato); la Colina admite hasta el valor de «sin récord» del juego; sin tope conocido, el de un entero.
        long max = colina ? ColinaEntrenador3.SinRecord : campo.Tope ?? uint.MaxValue;
        return P.Entero(id, T(campo.Nombre), c => campo.Leer(c.Sav), (c, v) => campo.Escribir(c.Sav, v), 0, max, prioridad: k,
            detalle: colina ? c => TiempoDeColina(campo.Leer(c.Sav)) : null,
            ayuda: colina ? Tx("Desafios.AyudaTiempoColina") : null);
    }

    /// <summary>Un tiempo de la Colina Entrenador (fotogramas de 1/60 s) como minutos, segundos y centésimas.</summary>
    internal static string TiempoDeColina(long fotogramas)
    {
        var f = Math.Max(0, fotogramas);
        var centesimas = f % 60 * 100 / 60;
        return $"{f / 3600}:{f / 60 % 60:00}.{centesimas:00}";
    }

    // ---------------------------------------------------------------------------------------------- avisos
    /// <summary>Lo que cuenta el aviso tras «Ir al jefe» o «Máximo» en un modo.</summary>
    internal static string Resumen(InstalacionDesafio inst, ModoDesafio modo, ResultadoDesafio r, string accion)
    {
        var donde = $"{T(inst.Nombre)} · {T(modo.Nombre)}";
        switch (r.Estado)
        {
            case EstadoResultado.NoDisponible:
                return IdiomaInterfaz.F("Desafios.Aviso.NoDisponible", donde, r.Motivo is { } m ? T(m) : Tx("Desafios.SinComprobar"));
            case EstadoResultado.SinCambios:
                return IdiomaInterfaz.F("Desafios.Aviso.SinCambios", donde, accion);
            default:
                var cambios = string.Join("; ", r.Cambios.Take(3).Select(x => $"{T(x.Nombre)} {x.Antes} → {x.Despues}"));
                if (r.Cambios.Count > 3) cambios += " " + IdiomaInterfaz.F("Desafios.Aviso.Mas", r.Cambios.Count - 3);
                var texto = IdiomaInterfaz.F("Desafios.Aviso.Hecho", accion, donde, cambios);
                if (r.JefeAplicado is { } j) texto += " " + IdiomaInterfaz.F("Desafios.Aviso.Jefe", T(AutoLegalidad.Desafios.Textos.Jefe(j)));
                if (r.Explicacion is { } e) texto += " " + T(e);
                return texto;
        }
    }

    /// <summary>Lo que cuenta el aviso tras «Todo al jefe» o «Todo al máximo»: cuántos modos cambian, cuántos ya estaban así y cuántos no tienen base verificada.</summary>
    internal static string ResumenTodo(string accion, IReadOnlyList<ResultadoDesafio> rs)
    {
        var aplicados = rs.Count(r => r.Estado == EstadoResultado.Aplicado);
        var iguales = rs.Count(r => r.Estado == EstadoResultado.SinCambios);
        var sin = rs.Count(r => r.Estado == EstadoResultado.NoDisponible);
        return IdiomaInterfaz.F("Desafios.Aviso.Todo", accion, aplicados, iguales, sin);
    }
}
