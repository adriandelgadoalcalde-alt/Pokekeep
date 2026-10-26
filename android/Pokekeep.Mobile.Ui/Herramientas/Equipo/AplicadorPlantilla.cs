// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.Equipo;

public enum DestinoPlantilla { Equipo, Caja, NoSeColoca }

/// <summary>Qué pasará con un Pokémon de la plantilla (o del equipo actual): a dónde va (o por qué no se coloca), ya convertido al formato de la partida y con los avisos de compatibilidad del Core.</summary>
public sealed record ItemPlan(string Nombre, PKM? Pokemon, DestinoPlantilla Destino, CasillaRef? Casilla, string? Motivo, IReadOnlyList<string> Avisos);

/// <param name="Plantilla">La plantilla que se aplica.</param>
/// <param name="Items">Los Pokémon de la plantilla: al equipo, a una caja (los que no caben en el equipo) o sin colocar, en el orden de la plantilla.</param>
/// <param name="Desplazados">Los Pokémon del equipo actual, que pasan a las primeras casillas libres de las cajas (vacío si no hay equipo que sustituir).</param>
/// <param name="Bloqueo">Por qué no se puede aplicar nada (sin sitio en las cajas para el equipo actual, equipo solo de huevos), o null.</param>
/// <param name="CasillasLibres">Casillas libres de las cajas cuando se planificó (para decir cuántas faltan).</param>
public sealed record PlanAplicacion(PlantillaEquipo Plantilla, IReadOnlyList<ItemPlan> Items, IReadOnlyList<ItemPlan> Desplazados, string? Bloqueo = null, int CasillasLibres = 0)
{
    public int EnEquipo => Items.Count(i => i.Destino == DestinoPlantilla.Equipo);
    public int EnCaja => Items.Count(i => i.Destino == DestinoPlantilla.Caja);
    public int NoColocados => Items.Count(i => i.Destino == DestinoPlantilla.NoSeColoca);
    public int Colocables => EnEquipo + EnCaja;
    /// <summary>Cuántos Pokémon del equipo actual pasan a las cajas.</summary>
    public int AlaCaja => Desplazados.Count(i => i.Destino == DestinoPlantilla.Caja);
    /// <summary>¿Hay algo que escribir? Sin bloqueo y con al menos un Pokémon de la plantilla que colocar.</summary>
    public bool Aplicable => Bloqueo is null && Colocables > 0;
}

/// <summary>
/// «Aplicar a la partida» (X37, X44): la plantilla PASA A SER el equipo y los Pokémon que había en el equipo van a las primeras casillas libres de las cajas («se debería de comportar que cambie los Pokémon que ya están
/// en tu equipo, y tu equipo que ya tenías equipado que lo envíe a la caja»). Si la plantilla trae más Pokémon de los que caben en el equipo, el resto va a cajas detrás del equipo antiguo. En una partida sin equipo
/// (sin zona de equipo) todo va a las cajas, como antes, y no se desplaza nada. Cada Pokémon pasa por la MISMA conversión que «Importar» (<c>EntityConverter.ConvertToType</c> y <c>EvaluateCompatibility</c>, como
/// <c>Intercambio.Analizar</c>) y las mismas comprobaciones de destino que el resto de escrituras (<c>EditorCasillas.Bloqueo</c>): el que no existe en el juego de destino o no se puede convertir se avisa y no se coloca.
/// Nada se fuerza (reglas 1 y 13): se escribe con los ajustes de «Escritura de ranuras» y todo va en UN lote del historial (un Deshacer). Si en las cajas no hay sitio para el equipo actual no se escribe NADA a medias.
/// Planificar no escribe nada; Aplicar escribe el plan que se enseñó.
/// </summary>
public static class AplicadorPlantilla
{
    public static PlanAplicacion Planificar(Sesion sesion, PlantillaEquipo plantilla, int cajaInicial, GameStrings textos)
    {
        var sav = sesion.Guardado;
        var tieneEquipo = sesion.Zonas.TieneEquipo;
        var capacidadEquipo = tieneEquipo ? Math.Max(0, Math.Min(6, sesion.Zonas.CasillasEquipo)) : 0;

        // 1.º Cada miembro: convertido (o el motivo de no colocarlo) y, si hay equipo, los primeros que quepan forman el equipo nuevo.
        var miembros = new List<(string Nombre, PKM? Pk, string? Motivo, IReadOnlyList<string> Avisos, bool Equipo)>();
        var equipoNuevo = 0; var soloHuevos = true;
        foreach (var miembro in plantilla.Miembros)
        {
            var origen = PlantillaEquipo.Leer(miembro);
            if (origen is null) { miembros.Add(("#", null, TextosEquipo.PlantillaDanada, [], false)); continue; }
            var nombre = origen.Species < textos.Species.Count ? textos.Species[origen.Species] : "#" + origen.Species;
            var pk = Convertir(sav, origen, out var motivo);
            if (pk is null) { miembros.Add((nombre, null, motivo, [], false)); continue; }
            var avisos = sav.EvaluateCompatibility(pk, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);
            var alEquipo = equipoNuevo < capacidadEquipo && BloqueoEquipo(sav, pk) is null;
            if (alEquipo) { equipoNuevo++; if (!pk.IsEgg) soloHuevos = false; }
            miembros.Add((nombre, pk, null, avisos, alEquipo));
        }

        // 2.º Qué hay en el equipo actual (solo se desplaza si la plantilla va a ocupar el equipo).
        var actuales = new List<(string Nombre, PKM Pk)>();
        if (equipoNuevo > 0)
            for (var i = 0; i < Math.Min(6, sav.PartyCount); i++)
            {
                var pk = sav.GetPartySlotAtIndex(i);
                if (pk.Species == 0) continue;
                actuales.Add((pk.Species < textos.Species.Count ? textos.Species[pk.Species] : "#" + pk.Species, pk.Clone()));
            }

        // 3.º Las casillas libres de las cajas: primero para el equipo actual (tiene que caber entero), después para lo que la plantilla no mete en el equipo.
        var reservadas = new HashSet<(int Caja, int Casilla)>();
        var desplazados = new List<ItemPlan>();
        var faltaSitio = false;
        foreach (var (nombre, pk) in actuales)
        {
            var libre = PrimeraLibre(sesion, pk, cajaInicial, reservadas);
            if (libre is null) { faltaSitio = true; desplazados.Add(new ItemPlan(nombre, pk, DestinoPlantilla.NoSeColoca, null, TextosEquipo.AplicarSinSitio, [])); continue; }
            if (libre.Info is SlotInfoBox b) reservadas.Add((b.Box, b.Slot));
            desplazados.Add(new ItemPlan(nombre, pk, DestinoPlantilla.Caja, libre, null, []));
        }
        var items = new List<ItemPlan>();
        var k = 0;
        foreach (var (nombre, pk, motivo, avisos, alEquipo) in miembros)
        {
            if (pk is null) { items.Add(new ItemPlan(nombre, null, DestinoPlantilla.NoSeColoca, null, motivo, avisos)); continue; }
            if (alEquipo) { items.Add(new ItemPlan(nombre, pk, DestinoPlantilla.Equipo, CasillaRef.Equipo(k++), null, avisos)); continue; }   // la casilla k+1 del equipo nuevo
            var libre = PrimeraLibre(sesion, pk, cajaInicial, reservadas);
            if (libre is null) { items.Add(new ItemPlan(nombre, pk, DestinoPlantilla.NoSeColoca, null, TextosEquipo.AplicarSinSitio, avisos)); continue; }
            if (libre.Info is SlotInfoBox b) reservadas.Add((b.Box, b.Slot));
            items.Add(new ItemPlan(nombre, pk, DestinoPlantilla.Caja, libre, null, avisos));
        }

        var libres = LibresEnCajas(sesion);
        string? bloqueo = null;
        if (faltaSitio) bloqueo = TextosEquipo.AplicarSinSitioEnCajas(actuales.Count, libres);
        else if (equipoNuevo > 0 && soloHuevos) bloqueo = TextosEquipo.AplicarSoloHuevos;
        return new PlanAplicacion(plantilla, items, desplazados, bloqueo, libres);
    }

    /// <summary>Por qué un Pokémon no puede ir al equipo (null si puede). Como <see cref="EditorCasillas.Bloqueo"/> sin la regla de «equipo solo de huevos», que aquí se mira sobre el equipo NUEVO entero.</summary>
    private static string? BloqueoEquipo(SaveFile sav, PKM pk)
        => !new SlotInfoParty(0).CanWriteTo(sav) ? MessageStrings.MsgSaveSlotLocked : pk.Species == 0 ? MessageStrings.MsgSaveSlotEmpty : null;

    /// <summary>Cuántas casillas de caja hay vacías y escribibles.</summary>
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

    /// <summary>El Pokémon convertido al formato de la partida, o null con el motivo (no existe en el juego, o el Core no lo convierte).</summary>
    private static PKM? Convertir(SaveFile sav, PKM origen, out string? motivo)
    {
        motivo = null;
        if (origen.Species > sav.MaxSpeciesID) { motivo = TextosEquipo.NoExisteEnElJuego; return null; }
        var destino = sav.PKMType;
        var pk = EntityConverter.ConvertToType(origen, destino, out var resultado);
        if (pk is null) { motivo = TextosEquipo.NoSePuedeConvertir(resultado.GetDisplayString(origen, destino)); return null; }
        if (sav is ILangDeviantSave il && !EntityConverter.IsCompatibleGB(origen, il.Japanese, pk.Japanese)) { motivo = TextosEquipo.NoSePuedeConvertir(EntityConverterResult.IncompatibleLanguageGB.GetIncompatibleGBMessage(pk, il.Japanese)); return null; }
        if (!sav.Personal.IsPresentInGame(pk.Species, pk.Form)) { motivo = TextosEquipo.NoExisteEnElJuego; return null; }
        return pk;
    }

    /// <summary>La primera casilla de caja vacía y escribible desde <paramref name="cajaInicial"/> (como «Importar»), sin contar las ya reservadas por este plan.</summary>
    private static CasillaRef? PrimeraLibre(Sesion sesion, PKM pk, int cajaInicial, HashSet<(int Caja, int Casilla)> reservadas)
    {
        var sav = sesion.Guardado;
        if (!sav.HasBox || sav.BoxCount == 0) return null;
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
    /// Escribe el plan en la partida como UNA entrada del historial. Devuelve cuántos Pokémon de la plantilla se escribieron (0 si el plan no es aplicable: nada a medias). Primero salen a las cajas el equipo actual y lo que la
    /// plantilla no mete en el equipo; luego se vacía el equipo y se escribe el nuevo en orden.
    /// </summary>
    public static int Aplicar(Sesion sesion, PlanAplicacion plan)
    {
        if (!plan.Aplicable) return 0;
        var sav = sesion.Guardado;
        var alEquipo = plan.Items.Where(i => i.Destino == DestinoPlantilla.Equipo && i.Pokemon is not null).ToList();
        var desplazados = plan.Desplazados.Where(i => i.Destino == DestinoPlantilla.Caja && i.Pokemon is not null && i.Casilla is not null).ToList();
        var aCajas = plan.Items.Where(i => i.Destino == DestinoPlantilla.Caja && i.Pokemon is not null && i.Casilla is not null).ToList();
        var casillas = new List<ISlotInfo>();
        if (alEquipo.Count > 0) casillas.Add(new SlotInfoParty(0));   // el historial del Core guarda el equipo ENTERO con cualquier casilla del equipo
        casillas.AddRange(desplazados.Concat(aCajas).Select(i => i.Casilla!.Info));
        var n = 0;
        // Historial.Batch (no EditorCasillas.Lote, que solo sabe revertir cajas): guarda el equipo entero y las casillas de caja tocadas, así que un Deshacer lo devuelve todo.
        var ok = sesion.Casillas.Historial.Batch(casillas, _ =>
        {
            // El equipo antiguo se MUEVE (no se importa): sin ajustes de importación. Lo que viene de la plantilla sí se escribe con los de «Escritura de ranuras».
            foreach (var i in desplazados) i.Casilla!.Info.WriteTo(sav, i.Pokemon!.Clone(), EntityImportSettings.None);
            foreach (var i in aCajas) if (i.Casilla!.Info.WriteTo(sav, i.Pokemon!, default)) n++;
            if (alEquipo.Count == 0) return;
            // Se vacía el equipo desde el final (cada borrado desliza los siguientes) y se escribe el nuevo en orden: la casilla k+1 se realinea con PartyCount.
            for (var p = Math.Min(6, sav.PartyCount) - 1; p >= 0; p--) new SlotInfoParty(p).WriteTo(sav, sav.BlankPKM, EntityImportSettings.None);
            for (var p = 0; p < alEquipo.Count; p++)
                if (new SlotInfoParty(p).WriteTo(sav, alEquipo[p].Pokemon!, default)) n++;
        });
        var tocadas = ok ? n : 0;
        if (tocadas > 0)
        {
            ISlotInfo[] tocadasInfo = [.. Enumerable.Range(0, alEquipo.Count).Select(p => (ISlotInfo)new SlotInfoParty(p)), .. desplazados.Concat(aCajas).Select(i => i.Casilla!.Info)];
            sesion.Casillas.Notificar(EditorCasillas.TipoCambio.Escritura, tocadasInfo, IdiomaInterfaz.F("AplicadorPlantilla.Plantilla", plan.Plantilla.Nombre));
        }
        return tocadas;
    }
}
