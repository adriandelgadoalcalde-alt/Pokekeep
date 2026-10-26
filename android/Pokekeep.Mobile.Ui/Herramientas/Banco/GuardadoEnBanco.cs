// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Modelo;

namespace Pokekeep.Mobile.Ui.Herramientas.Banco;

/// <summary>Qué pasó al guardar Pokémon de la partida en el banco.</summary>
/// <param name="Guardados">Copias nuevas en el banco.</param>
/// <param name="YaEstaban">Ya había una igual (misma huella): no se duplica.</param>
/// <param name="Recuperados">Estaban en la papelera del banco y se han sacado de ella.</param>
/// <param name="Fallos">No se pudieron guardar (motivo en <see cref="Motivo"/>).</param>
/// <param name="Quitados">Quitados de la partida (solo con «Mover al banco»).</param>
/// <param name="NoQuitados">Guardados, pero no se pudieron quitar de la partida (casilla bloqueada o de solo lectura).</param>
public sealed record InformeGuardadoBanco(int Guardados, int YaEstaban, int Recuperados, int Fallos, int Quitados, int NoQuitados, string? Motivo)
{
    public int EnElBanco => Guardados + YaEstaban + Recuperados;
    public bool Limpio => Fallos == 0 && NoQuitados == 0;
}

/// <summary>
/// «Guardar en el banco» y «Mover al banco» (X38): copia los Pokémon de las casillas indicadas al banco propio. Copiar NUNCA toca la partida. Mover quita de la partida (con un único Deshacer) SOLO los que ya están a
/// salvo en el banco: si uno no se pudo guardar, se queda donde estaba. Se hace en tres pasos para que la pantalla pueda guardar en segundo plano: <see cref="Leer"/> (hilo de la interfaz), <c>BancoPokemon.Guardar</c> (cualquier hilo)
/// y <see cref="Concluir"/> (hilo de la interfaz).
/// </summary>
public static class GuardadoEnBanco
{
    /// <summary>Las copias de lo que hay en las casillas (las vacías se saltan).</summary>
    public static IReadOnlyList<(CasillaRef Casilla, PKM Pokemon)> Leer(Sesion sesion, IReadOnlyList<CasillaRef> casillas)
        => [.. casillas.Where(c => !sesion.Casillas.EstaVacia(c.Info)).Select(c => (c, sesion.Casillas.Leer(c).Clone()))];

    /// <summary>Tras guardar: con <paramref name="mover"/>, quita de la partida (un Deshacer) lo que quedó a salvo en el banco, y resume lo ocurrido.</summary>
    public static InformeGuardadoBanco Concluir(Sesion sesion, IReadOnlyList<(CasillaRef Casilla, PKM Pokemon)> origen, IReadOnlyList<ResultadoGuardado> resultados, bool mover)
    {
        var motivo = resultados.FirstOrDefault(r => r.Tipo == TipoGuardado.Fallo)?.Motivo;
        int quitados = 0, noQuitados = 0;
        if (mover)
        {
            var aQuitar = new List<ISlotInfo>();
            for (var i = 0; i < origen.Count; i++)
            {
                if (!resultados[i].Correcto) continue;   // sin copia a salvo, no se quita
                var info = origen[i].Casilla.Info;
                if (!sesion.Casillas.EsEscribible(info) || sesion.Casillas.BloqueoAlQuitar(info) is not null) { noQuitados++; continue; }
                aQuitar.Add(info);
            }
            if (aQuitar.Count > 0)
            {
                var r = sesion.Casillas.Borrar(aQuitar);
                if (r.Correcto) quitados = r.Tocadas; else { noQuitados += aQuitar.Count; motivo ??= r.Mensaje; }
            }
        }
        return new InformeGuardadoBanco(resultados.Count(r => r.Tipo == TipoGuardado.Guardado), resultados.Count(r => r.Tipo == TipoGuardado.YaEstaba), resultados.Count(r => r.Tipo == TipoGuardado.Recuperado),
            resultados.Count(r => r.Tipo == TipoGuardado.Fallo), quitados, noQuitados, motivo);
    }

    /// <summary>Los tres pasos seguidos, en el mismo hilo (pruebas y usos sin pantalla).</summary>
    public static InformeGuardadoBanco Guardar(Sesion sesion, BancoPokemon banco, IReadOnlyList<CasillaRef> casillas, bool mover)
    {
        var origen = Leer(sesion, casillas);
        if (origen.Count == 0) return new InformeGuardadoBanco(0, 0, 0, 0, 0, 0, TextosBanco.NadaQueGuardar);
        return Concluir(sesion, origen, banco.Guardar([.. origen.Select(o => o.Pokemon)]), mover);
    }
}
