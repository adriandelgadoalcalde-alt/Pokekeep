// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Herramientas.Equipo;

/// <summary>Lo que hace falta para dibujar el icono de caja de un Pokémon (X45): especie, forma y su argumento, sexo, variocolor, huevo y contexto. El modo y la versión de sprites los pone la vista.</summary>
public sealed record DatosSprite(int Especie, int Forma, uint ArgumentoForma, int Genero, bool Brillante, bool Huevo, int Contexto)
{
    public static DatosSprite De(PKM pk) => new(pk.Species, pk.Form, pk is IFormArgument fa ? fa.FormArgument : 0u, pk.Gender, pk.IsShiny, pk.IsEgg, (int)pk.Context);
}

/// <summary>Un movimiento de un Pokémon tal y como lo necesita el cálculo: nombre, tipo (numeración de <c>MoveType</c>) y si hace daño.</summary>
public sealed record MovimientoCobertura(int Id, string Nombre, int Tipo, bool HaceDano);

/// <summary>Un Pokémon del equipo para el cálculo: sus tipos (uno o dos), su habilidad y sus movimientos.</summary>
public sealed record MiembroCobertura(string Nombre, int Especie, IReadOnlyList<int> Tipos, int Habilidad, string NombreHabilidad, IReadOnlyList<MovimientoCobertura> Movimientos, DatosSprite? Sprite = null);

/// <summary>Cómo recibe un Pokémon un tipo atacante: el multiplicador final (con la habilidad si cuenta) y si la habilidad lo ha cambiado.</summary>
public sealed record EfectoEnMiembro(string Nombre, double Multiplicador, bool PorHabilidad, DatosSprite? Sprite = null);

/// <summary>Una fila de la defensa: un tipo atacante y cómo le va a cada Pokémon del equipo.</summary>
public sealed record FilaDefensa(int Tipo, string NombreTipo, IReadOnlyList<EfectoEnMiembro> Miembros)
{
    /// <summary>Reciben daño doble o más.</summary>
    public int Debiles => Miembros.Count(m => m.Multiplicador > 1);
    /// <summary>Lo resisten (daño reducido, no nulo).</summary>
    public int Resisten => Miembros.Count(m => m.Multiplicador is > 0 and < 1);
    /// <summary>No reciben daño.</summary>
    public int Inmunes => Miembros.Count(m => m.Multiplicador == 0);
    public int Neutros => Miembros.Count(m => m.Multiplicador == 1);
    /// <summary>Nadie lo resiste ni es inmune (un resistido o un inmune cubre al equipo).</summary>
    public bool NadieAguanta => Resisten + Inmunes == 0;
    /// <summary>«Punto débil del equipo»: golpea supereficaz a tres o más y nadie lo resiste.</summary>
    public bool EsPuntoDebil => Debiles >= ModeloCobertura.DebilesParaPuntoDebil && NadieAguanta;
}

/// <summary>Un movimiento con daño supereficaz y el Pokémon del equipo que lo tiene (X45: para enseñar su icono junto al movimiento).</summary>
public sealed record GolpeCobertura(string Movimiento, string Miembro, DatosSprite? Sprite);

/// <summary>Una fila de la ofensiva: un tipo defensor y los movimientos con daño del equipo que lo golpean supereficaz.</summary>
public sealed record FilaOfensiva(int Tipo, string NombreTipo, IReadOnlyList<string> Movimientos, IReadOnlyList<GolpeCobertura>? Golpes = null)
{
    public bool Cubierto => Movimientos.Count > 0;
}

/// <summary>El resultado completo del cálculo para un equipo.</summary>
public sealed record ResultadoCobertura(int Generacion, IReadOnlyList<MiembroCobertura> Miembros, IReadOnlyList<FilaDefensa> Defensa, IReadOnlyList<FilaOfensiva> Ofensiva)
{
    public IReadOnlyList<FilaDefensa> PuntosDebiles => [.. Defensa.Where(f => f.EsPuntoDebil)];
    public IReadOnlyList<FilaOfensiva> TiposSinCubrir => [.. Ofensiva.Where(f => !f.Cubierto)];
    /// <summary>Hay al menos un movimiento con daño en el equipo (sin ellos, «sin cubrir» no dice nada).</summary>
    public bool HayMovimientosConDano => Miembros.Any(m => m.Movimientos.Any(x => x.HaceDano));
    public bool TieneHabilidades => Generacion >= 3;
}

/// <summary>
/// Cobertura de tipos de un equipo (X37): defensa por tipo atacante y ofensiva con los movimientos con daño, con la tabla de tipos de la generación del juego. Puro: no toca la partida ni lee nada que no reciba.
/// Habilidades: solo las que el Core expone de forma fiable en <c>pk.Ability</c> (la habilidad real del Pokémon) y cuyo efecto sobre el daño por tipo es fijo (lista en <see cref="AjustePorHabilidad"/>).
/// No cuenta objetos (Globo Helio), clima, Teratipo ni movimientos de tipo variable (Bola Clima, Juicio, Ataque Natural...): usan su tipo base.
/// </summary>
public static class ModeloCobertura
{
    public const int DebilesParaPuntoDebil = 3;

    /// <param name="nombreTipo">Nombre en castellano de un tipo (numeración de <c>MoveType</c>).</param>
    /// <param name="conHabilidades">Contar las habilidades que cambian el daño por tipo (desde la 3.ª generación).</param>
    public static ResultadoCobertura Calcular(int generacion, IReadOnlyList<MiembroCobertura> miembros, Func<int, string> nombreTipo, bool conHabilidades = true)
    {
        var tipos = TablaTipos.Tipos(generacion);
        var usarHabilidades = conHabilidades && generacion >= 3;
        var defensa = tipos.Select(atacante => new FilaDefensa(atacante, nombreTipo(atacante),
            [.. miembros.Select(m => EfectoSobre(generacion, atacante, m, usarHabilidades))])).ToList();
        var ofensiva = tipos.Select(defensor =>
        {
            var golpes = new List<string>(); var conMiembro = new List<GolpeCobertura>();
            foreach (var m in miembros)
                foreach (var mov in m.Movimientos)
                    if (mov.HaceDano && TablaTipos.Existe(generacion, mov.Tipo) && TablaTipos.EsSupereficaz(generacion, mov.Tipo, defensor))
                    {
                        if (!golpes.Contains(mov.Nombre)) golpes.Add(mov.Nombre);
                        conMiembro.Add(new GolpeCobertura(mov.Nombre, m.Nombre, m.Sprite));
                    }
            return new FilaOfensiva(defensor, nombreTipo(defensor), golpes, conMiembro);
        }).ToList();
        return new ResultadoCobertura(generacion, miembros, defensa, ofensiva);
    }

    private static EfectoEnMiembro EfectoSobre(int generacion, int atacante, MiembroCobertura m, bool conHabilidades)
    {
        var tipo1 = m.Tipos.Count > 0 ? m.Tipos[0] : 0;
        var tipo2 = m.Tipos.Count > 1 ? m.Tipos[1] : -1;
        var baseM = TablaTipos.Contra(generacion, atacante, tipo1, tipo2);
        if (!conHabilidades) return new EfectoEnMiembro(m.Nombre, baseM, false, m.Sprite);
        var final = AjustePorHabilidad(m.Habilidad, atacante, baseM);
        return new EfectoEnMiembro(m.Nombre, final, final != baseM, m.Sprite);
    }

    /// <summary>
    /// El multiplicador de un ataque de tipo <paramref name="atacante"/> contra un Pokémon con esa habilidad, partiendo del <paramref name="multiplicador"/> de la tabla. Solo habilidades de efecto fijo por tipo:
    /// Levitación y Come Tierra (Tierra), Absorbe Elec, Pararrayos y Electricidad Pasiva (Eléctrico), Absorbe Agua, Colector y Piel Seca (Agua), Absorbe Fuego y Cuerpo Horneado (Fuego), Herbívoro (Planta),
    /// Sebo y Termoaislante y Burbuja (Fuego/Hielo reducidos), Sal Purificadora (Fantasma reducido) y Superguarda (solo pasa lo supereficaz).
    /// </summary>
    public static double AjustePorHabilidad(int habilidad, int atacante, double multiplicador)
    {
        const int Tierra = 4, Fantasma = 7, Fuego = 9, Agua = 10, Planta = 11, Electrico = 12, Hielo = 14;
        if (habilidad == (int)Ability.WonderGuard) return multiplicador > 1 ? multiplicador : 0;
        var inmune = habilidad switch
        {
            (int)Ability.Levitate or (int)Ability.EarthEater => Tierra,
            (int)Ability.VoltAbsorb or (int)Ability.LightningRod or (int)Ability.MotorDrive => Electrico,
            (int)Ability.WaterAbsorb or (int)Ability.StormDrain or (int)Ability.DrySkin => Agua,
            (int)Ability.FlashFire or (int)Ability.WellBakedBody => Fuego,
            (int)Ability.SapSipper => Planta,
            _ => -1,
        };
        if (inmune >= 0) return atacante == inmune ? 0 : multiplicador;
        var mitad = habilidad switch
        {
            (int)Ability.ThickFat => atacante is Fuego or Hielo,
            (int)Ability.Heatproof or (int)Ability.WaterBubble => atacante == Fuego,
            (int)Ability.PurifyingSalt => atacante == Fantasma,
            _ => false,
        };
        return mitad ? multiplicador / 2 : multiplicador;
    }

    // ---------------------------------------------------------------------------------------------------------------- desde el Core
    /// <summary>Convierte un Pokémon del Core en un miembro del cálculo. Sin especie (casilla vacía) devuelve null.</summary>
    public static MiembroCobertura? DeEntidad(PKM pk, GameStrings textos)
    {
        if (pk.Species == 0) return null;
        var generacion = pk.Format;
        var tipos = new List<int>();
        if (pk.PersonalInfo is IPersonalType pt)
        {
            void Anadir(byte crudo)
            {
                var t = (int)((MoveType)crudo).GetMoveTypeGeneration((byte)generacion);
                if (t >= 0 && t < TablaTipos.Cantidad && !tipos.Contains(t)) tipos.Add(t);
            }
            Anadir(pt.Type1);
            Anadir(pt.Type2);
        }
        if (tipos.Count == 0) tipos.Add(0);
        var movimientos = new List<MovimientoCobertura>();
        for (var i = 0; i < 4; i++)
        {
            var id = pk.GetMove(i);
            if (id == 0) continue;
            var tipo = id == (ushort)Move.HiddenPower ? Math.Clamp(pk.HPType + 1, 1, 16) : MoveInfo.GetType(id, pk.Context);
            var nombre = id < textos.Move.Count ? textos.Move[id] : "#" + id;
            movimientos.Add(new MovimientoCobertura(id, nombre, tipo, TieneDano(id)));
        }
        var habilidad = generacion >= 3 ? pk.Ability : 0;
        var nombreHabilidad = habilidad > 0 && habilidad < textos.Ability.Count ? textos.Ability[habilidad] : string.Empty;
        var nombreEspecie = pk.Species < textos.Species.Count ? textos.Species[pk.Species] : "#" + pk.Species;
        return new MiembroCobertura(nombreEspecie, pk.Species, tipos, habilidad, nombreHabilidad, movimientos, DatosSprite.De(pk));
    }

    /// <summary>¿El movimiento hace daño (físico o especial)? Los datos vienen de Showdown (<c>DatosCobertura.MovimientosConDano</c>): el Core no trae la categoría de los movimientos.</summary>
    public static bool TieneDano(int movimiento)
    {
        var bits = DatosCobertura.MovimientosConDano;
        return movimiento > 0 && (movimiento >> 3) < bits.Length && (bits[movimiento >> 3] & (1 << (movimiento & 7))) != 0;
    }

    /// <summary>Los miembros de una lista de Pokémon (los huecos se saltan), con los nombres de <paramref name="textos"/>.</summary>
    public static IReadOnlyList<MiembroCobertura> Miembros(IEnumerable<PKM> pokemon, GameStrings textos) => [.. pokemon.Select(p => DeEntidad(p, textos)).OfType<MiembroCobertura>()];

    /// <summary>Calcula con los nombres de tipo de <paramref name="textos"/>.</summary>
    public static ResultadoCobertura Calcular(int generacion, IEnumerable<PKM> pokemon, GameStrings textos, bool conHabilidades = true)
        => Calcular(generacion, Miembros(pokemon, textos), t => t < textos.Types.Count ? textos.Types[t] : "#" + t, conHabilidades);
}
