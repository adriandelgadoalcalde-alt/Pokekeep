// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen8y9;

/// <summary>
/// PK-SAV-169 · <c>SAV_Raid8</c>, las guaridas Dinamax de Espada y Escudo: de Galar (100 guaridas), de la Isla de la Armadura (DLC 1, 90) y de las Nevadas de la Corona (DLC 2, 86). El formulario elige una guarida
/// («Den 001») y la enseña en un cuadro de propiedades (<c>PropertyGrid</c> sin ordenar: el orden es el de la clase <c>RaidSpawnDetail</c>) que escribe en cuanto se cambia una propiedad, sobre una copia del guardado que
/// «Guardar» copia al original. Aquí cada guarida es un elemento de una colección con un campo por propiedad, en el mismo orden. Varias propiedades se tocan entre sí en el Core (cambiar el tipo de guarida cambia las marcas
/// «de evento», la rareza o la Pieza Deseo derivan del tipo...): al aplicar, los cambios se escriben en el orden de la lista y solo los de los campos tocados, lo que coincide con el formulario para un cambio suelto; con varios
/// cambios que se pisan entre sí el resultado depende del orden en que se hicieran en el cuadro de propiedades, que aquí no existe (ADR 0066).
/// </summary>
internal static class IncursionesGalar
{
    public const string Form = "SAV_Raid8";
    public static readonly string[] Censo = ["PK-SAV-169"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);

    /// <summary>La lista de guaridas que corresponde a cada origen, o null si el guardado no la tiene (la Isla de la Armadura y las Nevadas de la Corona piden el DLC).</summary>
    private static RaidSpawnList8? Lista(SaveFile sav, MaxRaidOrigin origen) => sav is SAV8SWSH s
        ? origen switch { MaxRaidOrigin.Galar => s.RaidGalar, MaxRaidOrigin.IsleOfArmor when s.SaveRevision >= 1 => s.RaidArmor, MaxRaidOrigin.CrownTundra when s.SaveRevision >= 2 => s.RaidCrown, _ => null }
        : null;

    private static RaidSpawnList8 Guaridas(ContextoP c, MaxRaidOrigin origen) => Lista(c.Sav, origen)!;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav, MaxRaidOrigin origen) => Lista(sav, origen) is not null;

    private static string TipoTexto(RaidType t) => t switch
    {
        RaidType.None => IdiomaInterfaz.T("Catalogo.Ninguna"), RaidType.Common => IdiomaInterfaz.T("IncursionesGalar.Comun"), RaidType.Rare => IdiomaInterfaz.T("IncursionesGalar.Rara"), RaidType.CommonWish => IdiomaInterfaz.T("IncursionesGalar.ComunTrozoDeseo"), RaidType.RareWish => IdiomaInterfaz.T("IncursionesGalar.RaraTrozoDeseo"), RaidType.Event => IdiomaInterfaz.T("IncursionesGalar.Evento"), RaidType.DynamaxCrystal => IdiomaInterfaz.T("IncursionesGalar.CristalDinamax"),
        _ => IdiomaInterfaz.F("Form.SinNombre", (byte)t),
    };

    public static string Titulo(MaxRaidOrigin origen) => origen switch { MaxRaidOrigin.Galar => IdiomaInterfaz.T("Partida.Especifico.incursiones.Titulo"), MaxRaidOrigin.IsleOfArmor => IdiomaInterfaz.T("Partida.Especifico.incursiones-dlc1.Titulo"), _ => IdiomaInterfaz.T("Partida.Especifico.incursiones-dlc2.Titulo") };
    private static string Id(MaxRaidOrigin origen) => origen switch { MaxRaidOrigin.Galar => "incursiones", MaxRaidOrigin.IsleOfArmor => "incursiones-dlc1", _ => "incursiones-dlc2" };

    public static EditorDefP? Crear(SaveFile sav, MaxRaidOrigin origen)
    {
        if (Lista(sav, origen) is null) return null;
        // Las propiedades que el Core liga entre si: al escribir una, las demas se releen.
        string[] ligadas(int i) => ["tipo", "marcas", "activa", "rara", "deseo", "vatios", "evento"];
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "guaridas", Elemento = IdiomaInterfaz.T("IncursionesGalar.Guarida"), Cantidad = c => Guaridas(c, origen).CountUsed,
            Titulo = (c, i) => { var d = Guaridas(c, origen).GetRaid(i); return $"{i + 1:000} · {(d.IsActive ? TipoTexto(d.DenType) : IdiomaInterfaz.T("IncursionesGalar.Inactiva"))}"; },
            Resumen = (c, i) => { var d = Guaridas(c, origen).GetRaid(i); return d.IsActive ? $"{d.Stars + 1}★ · tirada {d.RandRoll}" : null; },
            Secciones = (_, i) =>
            {
                IEnumerable<string> afecta(ContextoP c) => ligadas(i).Select(x => col!.IdCampo(i, x));
                RaidSpawnDetail D(ContextoP c) => Guaridas(c, origen).GetRaid(i);
                return
                [
                    new SeccionP(string.Empty,
                    [
                        CamposGen8y9.Hex(col!.IdCampo(i, "hash"), L("Prop_Hash"), c => D(c).Hash, (c, v) => D(c).Hash = v, ayuda: IdiomaInterfaz.T("IncursionesGalar.HashFNVBuscaTabla")),
                        CamposGen8y9.Hex(col.IdCampo(i, "semilla"), L("Prop_Seed"), c => D(c).Seed, (c, v) => D(c).Seed = v, ayuda: IdiomaInterfaz.T("IncursionesGalar.SemillaGeneradorCreaContenido")),
                        P.Entero(col.IdCampo(i, "estrellas"), L("Prop_Stars"), c => D(c).Stars, (c, v) => D(c).Stars = (byte)v, 0, 255, ayuda: IdiomaInterfaz.T("IncursionesGalar.EstrellasContenidoGuarida")),
                        P.Entero(col.IdCampo(i, "tirada"), L("Prop_RandRoll"), c => D(c).RandRoll, (c, v) => D(c).RandRoll = (byte)v, 0, 255, ayuda: IdiomaInterfaz.T("IncursionesGalar.ValorAleatorioEligeEncuentro")),
                        P.Opcion(col.IdCampo(i, "tipo"), L("Prop_DenType"), c => (byte)D(c).DenType, (c, v) => D(c).DenType = (RaidType)(byte)v, _ => [.. Enum.GetValues<RaidType>().Select(t => new OpcionP((byte)t, TipoTexto(t)))], titulo: L("Prop_DenType"), afectaA: afecta, ayuda: IdiomaInterfaz.T("IncursionesGalar.PrimerGrupoMarcasGuarida")),
                        P.Entero(col.IdCampo(i, "marcas"), L("Prop_Flags"), c => D(c).Flags, (c, v) => D(c).Flags = (byte)v, 0, 255, afectaA: afecta, ayuda: IdiomaInterfaz.T("IncursionesGalar.SegundoGrupoMarcasGuarida")),
                        P.Info(col.IdCampo(i, "activa"), L("Prop_IsActive"), c => D(c).IsActive ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No")),
                        P.Marca(col.IdCampo(i, "rara"), L("Prop_IsRare"), c => D(c).IsRare, (c, v) => D(c).IsRare = v, afectaA: afecta, ayuda: IdiomaInterfaz.T("IncursionesGalar.UsanDatosEncuentroRaro")),
                        P.Marca(col.IdCampo(i, "deseo"), L("Prop_IsWishingPiece"), c => D(c).IsWishingPiece, (c, v) => D(c).IsWishingPiece = v, afectaA: afecta, ayuda: IdiomaInterfaz.T("IncursionesGalar.UsoTrozoDeseoGuarida")),
                        P.Marca(col.IdCampo(i, "vatios"), L("Prop_WattsHarvested"), c => D(c).WattsHarvested, (c, v) => D(c).WattsHarvested = v, afectaA: afecta, ayuda: IdiomaInterfaz.T("IncursionesGalar.RecogidoVatiosGuarida")),
                        P.Marca(col.IdCampo(i, "evento"), L("Prop_IsEvent"), c => D(c).IsEvent, (c, v) => D(c).IsEvent = v, afectaA: afecta, ayuda: IdiomaInterfaz.T("IncursionesGalar.UsanDatosDistribucionEvento")),
                    ]),
                ];
            },
        };
        return new EditorDefP(Id(origen), Titulo(origen), [new PestanaP("guaridas", EtiquetasLang.RequerirFormulario(Form), [], Coleccion: col)], Censo, origen switch { MaxRaidOrigin.Galar => IdiomaInterfaz.T("IncursionesGalar.GuaridasDinamaxGalar"), MaxRaidOrigin.IsleOfArmor => IdiomaInterfaz.T("IncursionesGalar.GuaridasDinamaxIslaArmadura"), _ => IdiomaInterfaz.T("IncursionesGalar.GuaridasDinamaxNevadasCorona") });
    }
}
