// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using PKHeX.Core.Searching;

namespace Pokekeep.Mobile.Ui.Cajas;

/// <summary>Un criterio de tres valores («da igual», «si», «no») como las casillas de tres estados de PKHeX (variocolor, huevo, legal).</summary>
public enum Tri { DaIgual, Si, No }

/// <summary>
/// Criterios de «Buscar en cajas» (<c>EntitySearchSetup</c> de PKHeX, pestanas General y Avanzado): todos los filtros del Core (<see cref="SearchSettings"/>) mas instrucciones de edicion por lotes
/// (<c>Propiedad=valor</c>, una por linea) en la parte avanzada. Los valores «cualquiera» son los de PKHeX (especie 0, -1, <c>Nature.Random</c>...).
/// </summary>
public sealed class CriteriosBusqueda
{
    public ushort Especie { get; set; }
    public string Mote { get; set; } = string.Empty;
    public int Habilidad { get; set; } = -1;
    public Nature Naturaleza { get; set; } = Nature.Random;
    public int Objeto { get; set; } = -1;
    public GameVersion Version { get; set; }
    public byte Generacion { get; set; }
    public int TipoPotencialOculto { get; set; } = -1;
    public ushort[] Movimientos { get; } = new ushort[4];
    public Tri Variocolor { get; set; }
    public Tri Huevo { get; set; }
    public Tri Legal { get; set; }
    public SearchComparison ComparaNivel { get; set; }
    public byte Nivel { get; set; }
    /// <summary>0 = cualquiera, 1 = &lt;= 90, 2 = 91-120, 3 = 121-150, 4 = 151-179, 5 = 180+, 6 = 186 (los seis IV a 31).</summary>
    public int Ivs { get; set; }
    /// <summary>0 = cualquiera, 1 = ninguno, 2 = algunos (1-127), 3 = la mitad (128-507), 4 = todos (508+).</summary>
    public int Evs { get; set; }
    public CloneDetectionMethod Clones { get; set; }
    /// <summary>Instrucciones de la parte avanzada (una por linea: <c>.Nivel&gt;50</c>, <c>=Nombre=...</c>...). Se validan con el motor de ediciones por lotes del Core.</summary>
    public string Instrucciones { get; set; } = string.Empty;

    public bool EstaVacio => Especie == 0 && string.IsNullOrWhiteSpace(Mote) && Habilidad < 0 && !Naturaleza.IsFixed && Objeto < 0 && !Version.IsValidSavedVersion() && Generacion == 0 && TipoPotencialOculto < 0
        && Movimientos.All(m => m == 0) && Variocolor == Tri.DaIgual && Huevo == Tri.DaIgual && Legal == Tri.DaIgual && Nivel == 0 && Ivs == 0 && Evs == 0 && Clones == CloneDetectionMethod.None && string.IsNullOrWhiteSpace(Instrucciones);

    public void Reiniciar()
    {
        Especie = 0; Mote = string.Empty; Habilidad = -1; Naturaleza = Nature.Random; Objeto = -1; Version = default; Generacion = 0; TipoPotencialOculto = -1;
        Array.Clear(Movimientos); Variocolor = Huevo = Legal = Tri.DaIgual; ComparaNivel = SearchComparison.None; Nivel = 0; Ivs = 0; Evs = 0; Clones = CloneDetectionMethod.None; Instrucciones = string.Empty;
    }

    private static bool? Valor(Tri t) => t switch { Tri.Si => true, Tri.No => false, _ => null };

    /// <summary>Instrucciones que el motor por lotes del Core no entiende (una por linea con motivo); vacio si todas son validas. El filtro avanzado nunca se aplica a medias.</summary>
    public IReadOnlyList<string> InstruccionesInvalidas()
    {
        var malas = new List<string>();
        if (string.IsNullOrWhiteSpace(Instrucciones)) return malas;
        foreach (var linea in Instrucciones.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var filtros = StringInstruction.GetFilters(linea);
            if (filtros.Count == 0) malas.Add(linea);
        }
        return malas;
    }

    public SearchSettings Crear(SaveFile sav)
    {
        var s = new SearchSettings
        {
            Context = sav.Generation >= 8 ? sav.Context : EntityContext.None,
            SearchContext = sav.Generation >= 8 ? SearchComparison.Equals : SearchComparison.LessThanEquals,
            Generation = Generacion,
            Version = Version,
            HiddenPowerType = TipoPotencialOculto,
            Species = Especie,
            Ability = Habilidad,
            Nature = Naturaleza,
            Item = Objeto,
            Nickname = Mote.Trim(),
            BatchInstructions = Instrucciones,
            Level = Nivel == 0 ? null : Nivel,
            SearchLevel = ComparaNivel,
            EVType = Evs,
            IVType = Ivs,
            SearchClones = Clones,
        };
        foreach (var m in Movimientos) s.AddMove(m);
        s.SearchShiny = Valor(Variocolor);
        s.SearchLegal = Valor(Legal);
        s.SearchEgg = Valor(Huevo);
        return s;
    }

    /// <summary>El filtro como funcion sobre un Pokemon. La legalidad (si se pide) se analiza bajo el cerrojo del Core.</summary>
    public Func<PKM, bool> Predicado(SaveFile sav)
    {
        var s = Crear(sav);
        var p = s.CreateSearchPredicate();
        if (Legal == Tri.DaIgual) return p;
        return pk => { lock (Pokekeep.Sprites.LegalityGate.Cerrojo) return p(pk); };
    }
}

/// <summary>Una coincidencia de la busqueda: donde esta y que es.</summary>
public sealed record Coincidencia(CasillaRef Casilla, ushort Especie);

/// <summary>Recorre las cajas (y el equipo) buscando coincidencias, para marcarlas y saltar de una a otra (<c>Siguiente</c>/<c>Anterior</c> de PKHeX).</summary>
public static class BuscadorCajas
{
    public static IReadOnlyList<Coincidencia> Buscar(SaveFile sav, Func<PKM, bool> filtro, bool incluirEquipo = true)
    {
        var lista = new List<Coincidencia>();
        if (sav.HasBox)
        {
            for (var b = 0; b < sav.BoxCount; b++)
            {
                var datos = sav.GetBoxData(b);
                for (var i = 0; i < datos.Length; i++)
                {
                    var pk = datos[i];
                    if (pk.Species == 0 || !filtro(pk)) continue;
                    lista.Add(new Coincidencia(CasillaRef.Caja_(sav, b, i), pk.Species));
                }
            }
        }
        if (incluirEquipo && sav.HasParty)
        {
            for (var i = 0; i < Math.Min(6, sav.PartyCount); i++)
            {
                var pk = sav.GetPartySlotAtIndex(i);
                if (pk.Species != 0 && filtro(pk)) lista.Add(new Coincidencia(CasillaRef.Equipo(i), pk.Species));
            }
        }
        return lista;
    }
}
