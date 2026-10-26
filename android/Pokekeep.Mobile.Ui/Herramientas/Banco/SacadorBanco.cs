// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Modelo;

namespace Pokekeep.Mobile.Ui.Herramientas.Banco;

public enum DestinoSacar { Caja, NoSeColoca }

/// <summary>
/// Qué pasará con un Pokémon del banco al sacarlo a la partida abierta: ya convertido al formato de la partida, con el veredicto del Core, la casilla vacía que le toca (o por qué no se coloca) y los avisos de
/// compatibilidad. <see cref="Legalizado"/> es verdad si Pokekeep lo ha legalizado antes de colocarlo.
/// </summary>
public sealed record ItemSacar(EntradaBanco Entrada, string Nombre, PKM? Pokemon, EstadoLegal Veredicto, string InformeLegal, DestinoSacar Destino, CasillaRef? Casilla, string? Motivo, IReadOnlyList<string> Avisos, bool Legalizado = false)
{
    public bool Colocable => Destino == DestinoSacar.Caja && Pokemon is not null && Casilla is not null;
    public bool NecesitaLegalizar => Colocable && Veredicto == EstadoLegal.Ilegal;
}

/// <param name="Items">Los Pokémon elegidos, en el orden en que se eligieron.</param>
/// <param name="CasillasLibres">Casillas vacías y escribibles de las cajas cuando se planificó.</param>
/// <param name="Bloqueo">Por qué no se puede sacar nada (partida sin cajas), o null.</param>
public sealed record PlanSacar(IReadOnlyList<ItemSacar> Items, int CasillasLibres, string? Bloqueo = null)
{
    public int Colocables => Items.Count(i => i.Colocable);
    public int NoColocados => Items.Count(i => !i.Colocable);
    public int Ilegales => Items.Count(i => i.NecesitaLegalizar);
    public bool Aplicable => Bloqueo is null && Colocables > 0;
}

/// <summary>Lo que ocurrió al escribir: qué entradas del banco se colocaron y cuáles se saltaron en el último momento (con el motivo).</summary>
public sealed record ResultadoSacar(IReadOnlyList<EntradaBanco> Colocados, IReadOnlyList<(EntradaBanco Entrada, string Motivo)> Omitidos);

/// <summary>
/// «Sacar a la partida» (X38): un Pokémon del banco se convierte al formato de la partida abierta con el Core (la misma conversión y las mismas comprobaciones que «Importar»), pasa por el análisis de legalidad del
/// Core ANTES de colocarse (si es ilegal, la pantalla ofrece «Legalizar» con el motor de Pokekeep) y se coloca en casillas vacías de las cajas SIN sustituir nunca nada, en una sola entrada del historial (un Deshacer).
/// Planificar no escribe nada; Aplicar escribe el plan que se enseñó, comprobando otra vez que cada casilla sigue vacía.
/// </summary>
public static class SacadorBanco
{
    public static PlanSacar Planificar(Sesion sesion, IReadOnlyList<(EntradaBanco Entrada, PKM Pokemon)> elegidos, int cajaInicial, GameStrings textos)
    {
        var sav = sesion.Guardado;
        var libres = LibresEnCajas(sesion);
        if (!sav.HasBox || sav.BoxCount == 0)
            return new PlanSacar([.. elegidos.Select(e => SinColocar(e.Entrada, e.Entrada.NombreEspecie(textos), TextosBanco.PartidaSinCajas))], 0, TextosBanco.PartidaSinCajas);
        var reservadas = new HashSet<(int Caja, int Casilla)>();
        var items = new List<ItemSacar>();
        foreach (var (entrada, origen) in elegidos)
        {
            var nombre = entrada.Titulo(textos);
            var pk = Convertir(sav, origen, out var motivo);
            if (pk is null) { items.Add(SinColocar(entrada, nombre, motivo)); continue; }
            var avisos = sav.EvaluateCompatibility(pk, "es");
            var libre = PrimeraLibre(sesion, pk, cajaInicial, reservadas);
            var (veredicto, informe) = Veredicto(pk);
            if (libre is null) { items.Add(new ItemSacar(entrada, nombre, pk, veredicto, informe, DestinoSacar.NoSeColoca, null, TextosBanco.SinSitio, avisos)); continue; }
            if (libre.Info is SlotInfoBox b) reservadas.Add((b.Box, b.Slot));
            items.Add(new ItemSacar(entrada, nombre, pk, veredicto, informe, DestinoSacar.Caja, libre, null, avisos));
        }
        return new PlanSacar(items, libres);
    }

    private static ItemSacar SinColocar(EntradaBanco entrada, string nombre, string? motivo)
        => new(entrada, nombre, null, EstadoLegal.SinDatos, string.Empty, DestinoSacar.NoSeColoca, null, motivo, []);

    /// <summary>El veredicto del Core para un Pokémon ya en el formato de la partida, y las primeras líneas de su informe si no es legal.</summary>
    public static (EstadoLegal Estado, string Informe) Veredicto(PKM pk)
    {
        var estado = AnalizadorLegalidad.Evaluar(pk, StorageSlotType.Box);
        if (estado == EstadoLegal.Legal || estado == EstadoLegal.SinDatos) return (estado, string.Empty);
        var (_, texto) = Intercambio.InformeLegalidad(pk, StorageSlotType.Box);
        var lineas = texto.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(4);
        return (estado, string.Join("\n", lineas));
    }

    /// <summary>Vuelve a evaluar un elemento tras legalizarlo (el motor modifica su Pokémon en el sitio).</summary>
    public static ItemSacar Reevaluar(ItemSacar item, bool legalizado)
    {
        if (item.Pokemon is null) return item;
        var (estado, informe) = Veredicto(item.Pokemon);
        return item with { Veredicto = estado, InformeLegal = informe, Legalizado = legalizado || item.Legalizado };
    }

    /// <summary>El Pokémon convertido al formato de la partida, o null con el motivo (no existe en el juego, o el Core no lo convierte).</summary>
    public static PKM? Convertir(SaveFile sav, PKM origen, out string? motivo)
    {
        motivo = null;
        Pokekeep.Mobile.Ui.Ajustes.Pkhex.TextosNucleo.Preparar();
        if (origen.Species > sav.MaxSpeciesID) { motivo = TextosBanco.NoExisteEnElJuego; return null; }
        var destino = sav.PKMType;
        var copia = origen.Clone();
        var pk = EntityConverter.ConvertToType(copia, destino, out var resultado);
        if (pk is null) { motivo = TextosBanco.NoSePuedeConvertir(resultado.GetDisplayString(origen, destino)); return null; }
        if (sav is ILangDeviantSave il && !EntityConverter.IsCompatibleGB(origen, il.Japanese, pk.Japanese)) { motivo = TextosBanco.NoSePuedeConvertir(EntityConverterResult.IncompatibleLanguageGB.GetIncompatibleGBMessage(pk, il.Japanese)); return null; }
        if (!sav.Personal.IsPresentInGame(pk.Species, pk.Form)) { motivo = TextosBanco.NoExisteEnElJuego; return null; }
        return pk;
    }

    private static int LibresEnCajas(Sesion sesion)
    {
        var sav = sesion.Guardado;
        if (!sav.HasBox || sav.BoxCount == 0) return 0;
        var n = 0;
        for (var b = 0; b < sav.BoxCount; b++)
            for (var s = 0; s < sav.BoxSlotCount; s++)
            {
                var info = new SlotInfoBox(b, s, sav);
                if (info.CanWriteTo(sav) && info.Read(sav).Species == 0) n++;
            }
        return n;
    }

    private static CasillaRef? PrimeraLibre(Sesion sesion, PKM pk, int cajaInicial, HashSet<(int Caja, int Casilla)> reservadas)
    {
        var sav = sesion.Guardado;
        for (var b = 0; b < sav.BoxCount; b++)
        {
            var caja = (cajaInicial + b) % sav.BoxCount;
            for (var s = 0; s < sav.BoxSlotCount; s++)
            {
                if (reservadas.Contains((caja, s))) continue;
                var info = new SlotInfoBox(caja, s, sav);
                if (info.CanWriteTo(sav) && info.Read(sav).Species == 0 && sesion.Casillas.Bloqueo(info, pk) is null) return new CasillaRef(info, caja);
            }
        }
        return null;
    }

    /// <summary>
    /// Escribe los elementos colocables del plan como UNA entrada del historial. Antes de cada escritura se comprueba otra vez que la casilla sigue vacía y se puede escribir (regla 7): si no, ese Pokémon se salta y se
    /// cuenta como omitido; NUNCA se sobrescribe nada. Se escribe con los ajustes de «Escritura de ranuras».
    /// </summary>
    public static ResultadoSacar Aplicar(Sesion sesion, PlanSacar plan, string descripcion)
    {
        var sav = sesion.Guardado;
        var colocados = new List<EntradaBanco>(); var omitidos = new List<(EntradaBanco, string)>();
        if (!plan.Aplicable) return new ResultadoSacar([], [.. plan.Items.Select(i => (i.Entrada, i.Motivo ?? TextosBanco.NoSeColoca))]);
        // Regla 7: antes de escribir se mira otra vez cada casilla; la que ya no está vacía (o no se puede escribir) se salta y NUNCA se sustituye.
        var validos = new List<ItemSacar>();
        foreach (var i in plan.Items)
        {
            if (!i.Colocable) { omitidos.Add((i.Entrada, i.Motivo ?? TextosBanco.NoSeColoca)); continue; }
            var info = i.Casilla!.Info;
            if (!info.CanWriteTo(sav) || info.Read(sav).Species != 0) omitidos.Add((i.Entrada, TextosBanco.CasillaYaOcupada)); else validos.Add(i);
        }
        if (validos.Count == 0) return new ResultadoSacar([], omitidos);
        var ok = sesion.Casillas.Historial.Batch([.. validos.Select(i => i.Casilla!.Info)], _ =>
        {
            foreach (var i in validos)
                if (i.Casilla!.Info.WriteTo(sav, i.Pokemon!, default)) colocados.Add(i.Entrada); else omitidos.Add((i.Entrada, TextosBanco.NoSeHaPodidoEscribir));
        });
        if (!ok || colocados.Count == 0) return new ResultadoSacar([], [.. omitidos.Concat(validos.Where(v => !omitidos.Any(o => o.Item1.Id == v.Entrada.Id)).Select(v => (v.Entrada, TextosBanco.NoSeHaPodidoEscribir)))]);
        sesion.Casillas.Notificar(EditorCasillas.TipoCambio.Escritura, [.. validos.Where(i => colocados.Contains(i.Entrada)).Select(i => i.Casilla!.Info)], descripcion);
        return new ResultadoSacar(colocados, omitidos);
    }
}
