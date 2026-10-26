// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>Tablas largas comunes de los editores de partida.</summary>
internal static class TablasP
{
    /// <summary>
    /// Estadisticas del entrenador (<c>TrainerStat</c>): todas las de <see cref="ITrainerStatRecord"/> con su nombre de PKHeX y su limite real (<c>GetRecordMax</c>, o el valor actual si lo supera, como el control original).
    /// Los nombres son los de PKHeX (en ingles: no hay una traduccion oficial al castellano).
    /// </summary>
    public static TablaDefP Registros(Dictionary<int, string> nombres) => new()
    {
        Id = "records",
        Marcador = IdiomaInterfaz.T("Form.Tabla.BuscarStat"),
        Ayuda = IdiomaInterfaz.T("Form.Tabla.StatAyuda"),
        Filas = c =>
        {
            var sav = (ITrainerStatRecord)c.Sav;
            var l = new List<FilaTablaP>();
            for (var i = 0; i < sav.RecordCount; i++)
            {
                var n = i;
                if (!nombres.TryGetValue(n, out var nombre)) nombre = IdiomaInterfaz.F("Form.Tabla.Stat", n);
                l.Add(new FilaTablaP { Id = n, Nombre = nombre, Min = 0, Max = Math.Max(sav.GetRecordMax(n), sav.GetRecord(n)), Leer = x => (long)((ITrainerStatRecord)x.Sav).GetRecord(n), Escribir = (x, v) => ((ITrainerStatRecord)x.Sav).SetRecord(n, (int)(long)v) });
            }
            return l;
        },
    };
}
