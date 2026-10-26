// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen6y7;

/// <summary>
/// PK-SAV-107 a PK-SAV-110 · <c>SAV_Link6</c>, el Nexo Pokémon de X/Y y de Rubí Omega/Zafiro Alfa (el paquete <c>PL6</c> que el juego recibe de Pokémon Link). Las tres pestañas del original:
/// «Inicio» (disponibilidad, origen, PB y Poké Millas), «Pokémon» (los seis Pokémon recibidos) y «Objetos» (los seis objetos y sus cantidades).
/// En el formulario SOLO se pueden cambiar los PB (0 a 9999) y las Poké Millas (0 a 65.535), y solo si el paquete esta habilitado; la casilla de disponibilidad, el origen, los Pokémon y los objetos estan deshabilitados
/// (<c>Enabled = false</c> en el diseñador) y se enseñan como informacion. «Importar» sustituye el paquete entero por un archivo <c>.pl6</c> de 0xA47 bytes y «Exportar» (solo con el paquete habilitado) lo guarda.
/// «Guardar» vuelve a escribir todos los campos del paquete desde los controles (<c>SaveLinkData</c>) y recalcula el checksum (<c>RefreshChecksum</c>): aqui cada escritura recalcula el checksum y la importacion repite esa
/// normalizacion (origen, disponibilidad, cantidades de objeto como un byte) porque el formulario la hace despues de importar.
/// </summary>
internal static class NexoPokemon
{
    public const string Form = "SAV_Link6";
    public static readonly string[] Censo = ["PK-SAV-107", "PK-SAV-108", "PK-SAV-109", "PK-SAV-110"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static LinkBlock6 Enlace(ContextoP c) => ((ISaveBlock6Main)c.Sav).Link;
    private static PL6 Paquete(ContextoP c) => Enlace(c).Gifts;
    private static string NoHabilitado => IdiomaInterfaz.T("NexoPokemon.NexoPokemonHabilitadoPartida");

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is ISaveBlock6Main;

    private static string Especie(ContextoP c, ushort especie) => especie < c.Textos.Species.Count ? c.Textos.Species[especie] : especie.ToString();
    private static string Objeto(ContextoP c, ushort objeto) => objeto < c.Textos.itemlist.Length ? c.Textos.itemlist[objeto] : objeto.ToString();

    /// <summary>
    /// Lo que hace <c>SaveLinkData</c> con un paquete recien importado: reescribe origen, disponibilidad, PB, Poké Millas, objetos y cantidades (estas como un byte: se pierde el byte alto) y el checksum.
    /// </summary>
    internal static void Normalizar(ContextoP c)
    {
        var g = Paquete(c);
        g.Origin = g.Origin;
        g.Enabled = g.Enabled;
        g.BattlePoints = g.BattlePoints;
        g.Pokemiles = g.Pokemiles;
        g.Quantity1 = (byte)g.Quantity1; g.Quantity2 = (byte)g.Quantity2; g.Quantity3 = (byte)g.Quantity3;
        g.Quantity4 = (byte)g.Quantity4; g.Quantity5 = (byte)g.Quantity5; g.Quantity6 = (byte)g.Quantity6;
        Enlace(c).RefreshChecksum();
    }

    /// <summary>Los PB y las Poké Millas solo se editan con el paquete habilitado; tras elegir un archivo cuenta el del archivo (el formulario recarga sus controles al importar).</summary>
    private static bool Habilitado(ContextoP c) => c.Valor("paquete") is byte[] b && b.Length == PL6.Size ? new PL6(b.ToArray()).Enabled : Paquete(c).Enabled;

    /// <summary>
    /// «Importar»/«Exportar»: un campo de archivo a mano (no <c>P.Archivo</c>) porque al importar el formulario RECARGA sus controles (<c>LoadLinkData</c>): los PB y las Poké Millas del borrador pasan a ser los del archivo
    /// (<see cref="CampoP.Deriva"/>) y el archivo se escribe antes que ellos (<see cref="CampoP.Prioridad"/>), asi lo que se cambie despues de importar gana, como en el original. El control de PB admite hasta 9999: un archivo
    /// con mas hace lanzar una excepcion al formulario original; aqui se rechaza con su motivo.
    /// </summary>
    private static CampoP CampoPaquete() => new()
    {
        Id = "paquete", Etiqueta = L("B_Import"), Tipo = TipoP.Archivo, Leer = _ => null, Prioridad = -1,
        Escribir = (c, v) => { if (v is byte[] b) { b.CopyTo(Paquete(c).Data); Normalizar(c); } },
        ValidarArchivo = (_, b) => b.Length != PL6.Size ? IdiomaInterfaz.T("NexoPokemon.ArchivoPaqueteNexoPokemon") : new PL6(b.ToArray()).BattlePoints > 9999 ? IdiomaInterfaz.T("NexoPokemon.PBPaqueteSuperan") : null,
        Exporta = c => Paquete(c).Enabled ? ("nexo-pokemon.pl6", Paquete(c).Data.ToArray()) : null,
        Extension = "pl6", TextoBoton = L("B_Import"),
        Ayuda = IdiomaInterfaz.T("NexoPokemon.SustituyePaqueteEnteroArchivo"),
        Deriva = c => c.Valor("paquete") is byte[] b && b.Length == PL6.Size
            ? new Dictionary<string, object?> { ["pb"] = (long)new PL6(b.ToArray()).BattlePoints, ["pokemiles"] = (long)new PL6(b.ToArray()).Pokemiles }
            : new Dictionary<string, object?>(),
    };

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not ISaveBlock6Main) return null;
        string? SinPaquete(ContextoP c) => Habilitado(c) ? null : NoHabilitado;

        var inicio = new SeccionP(string.Empty,
        [
            P.Info("disponible", L("CHK_LinkAvailable"), c => Paquete(c).Enabled ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No")),
            P.Info("origen", L("RTB_LinkSource"), c => Paquete(c).Origin),
            P.Entero("pb", L("L_BP"), c => Paquete(c).BattlePoints, (c, v) => { Paquete(c).BattlePoints = (ushort)v; Enlace(c).RefreshChecksum(); }, 0, 9999, motivo: SinPaquete),
            P.Entero("pokemiles", L("L_Pokemiles"), c => Paquete(c).Pokemiles, (c, v) => { Paquete(c).Pokemiles = (ushort)v; Enlace(c).RefreshChecksum(); }, 0, ushort.MaxValue, motivo: SinPaquete),
            CampoPaquete(),
        ]);
        var pokemon = new SeccionP(string.Empty, [.. Enumerable.Range(0, 6).Select(i => P.Info($"pkm{i + 1}", L($"L_PKM{i + 1}"), c => Especie(c, i switch
        {
            0 => Paquete(c).Entity1.Species, 1 => Paquete(c).Entity2.Species, 2 => Paquete(c).Entity3.Species,
            3 => Paquete(c).Entity4.Species, 4 => Paquete(c).Entity5.Species, _ => Paquete(c).Entity6.Species,
        })))]);
        var objetos = new SeccionP(string.Empty, [.. Enumerable.Range(0, 6).Select(i => P.Info($"objeto{i + 1}", L($"L_Item{i + 1}"), c =>
        {
            var g = Paquete(c);
            var (objeto, cantidad) = i switch
            {
                0 => (g.Item1, g.Quantity1), 1 => (g.Item2, g.Quantity2), 2 => (g.Item3, g.Quantity3),
                3 => (g.Item4, g.Quantity4), 4 => (g.Item5, g.Quantity5), _ => (g.Item6, g.Quantity6),
            };
            return $"{Objeto(c, objeto)} × {cantidad}";
        }))]);
        return new EditorDefP("link6", IdiomaInterfaz.T("Partida.Especifico.link6.Titulo"),
            [new PestanaP("inicio", L("TAB_Main"), [inicio]), new PestanaP("pokemon", L("TAB_PKM"), [pokemon]), new PestanaP("objetos", L("TAB_Items"), [objetos])],
            Censo, IdiomaInterfaz.T("Partida.Especifico.link6.Descripcion"));
    }
}
