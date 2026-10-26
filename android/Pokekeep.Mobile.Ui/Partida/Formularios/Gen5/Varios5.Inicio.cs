// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Buffers.Binary;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen5;

internal static partial class Varios5
{
    // ---------------------------------------------------------------- Inicio (solo Negro/Blanco): Ticket Libertad y errantes
    private static IReadOnlyList<OpcionP> EstadosDelErrante(int actual)
    {
        // GetStates(): los cuatro estados; solo se ofrecen los de «derrotado» y «capturado» y el que ya tiene (no se puede volver a «sin aparecer» ni a «vagando»); un valor desconocido se añade tal cual.
        var l = new List<OpcionP> { new(0, IdiomaInterfaz.T("Varios5.Aparecer")), new(1, "Vagando"), new(2, IdiomaInterfaz.T("Varios5Inicio.Derrotado")), new(3, IdiomaInterfaz.T("Pokedex8.Capturado")) };
        if (l.All(z => z.Id != actual)) l.Add(new OpcionP(actual, IdiomaInterfaz.F("Varios5.Desconocido0xX2", actual)));
        return [.. l.Where(v => v.Id >= 2 || v.Id == actual)];
    }

    private static IReadOnlyList<OpcionP> EstadosDelEventoErrante(ContextoP c, int actual)
    {
        var l = new List<OpcionP> { new(0, IdiomaInterfaz.T("Varios5Inicio.NoHaOcurrido")), new(1, IdiomaInterfaz.F("Varios5Inicio.IrALa", RutaSiete(c))), new(3, IdiomaInterfaz.T("Varios5.EventoTerminado")) };
        if (l.All(z => z.Id != actual)) l.Add(new OpcionP(actual, IdiomaInterfaz.F("Varios5.Desconocido0xX2", actual)));
        return l;
    }

    /// <summary>«Ruta 7» tal como la llama la lista de lugares del Core (la entrada 20 de <c>bw2_00000</c>).</summary>
    private static string RutaSiete(ContextoP c) => Lugar(c, 20);

    private static IEnumerable<SeccionP> Inicio()
    {
        yield return new SeccionP(string.Empty,
        [
            P.Marca("liberty", L("CHK_LibertyPass"), c => BW(c).Misc.IsLibertyTicketActivated, (c, v) => { if (v != BW(c).Misc.IsLibertyTicketActivated) BW(c).Misc.IsLibertyTicketActivated = v; }),
        ]);
        yield return new SeccionP(L("GB_Roamer"),
        [
            Errante(1, "errante-tornadus", L("L_Roamer641")),    // CB_Roamer641 es el segundo de cbr: índice 1
            Errante(0, "errante-thundurus", L("L_Roamer642")),   // CB_Roamer642 es el primero: índice 0
            P.Opcion("estado-errante", L("L_RoamStatus"), c => BW(c).EventWork.GetWorkRoamer(), (c, v) => BW(c).EventWork.SetWorkRoamer((ushort)v),
                c => EstadosDelEventoErrante(c, BW(c).EventWork.GetWorkRoamer()), titulo: L("L_RoamStatus"),
                ayuda: IdiomaInterfaz.T("Varios5.VolverCapturarTornadusThundurus")),
        ]);
    }

    private static CampoP Errante(int indice, string id, string etiqueta)
        => P.Opcion(id, etiqueta, c => BW(c).Encount.GetRoamerState(indice),
            (c, v) =>
            {
                var bw = BW(c);
                var encount = bw.Encount;
                int antes = encount.GetRoamerState(indice);
                var nuevo = (byte)v;
                if (antes == nuevo) return;
                encount.SetRoamerState(indice, nuevo);
                if (antes != 1) return;   // si vagaba, el errante y su segundo estado se vacían
                var errante = indice == 0 ? encount.Roamer1 : encount.Roamer2;
                errante.Clear();
                encount.SetRoamerState2C(indice, 0);
            },
            c => EstadosDelErrante(BW(c).Encount.GetRoamerState(indice)), titulo: etiqueta);

    // ---------------------------------------------------------------- Récords (Record5): cifrados en la partida
    private static T ConRecords<T>(ContextoP c, Func<Record5, T> f)
    {
        // Record5 se descifra en el sitio al leer o escribir y hay que volver a cifrarlo (EndAccess) o la partida quedaria descifrada.
        var r = S(c).Records;
        try { return f(r); }
        finally { r.EndAccess(); }
    }

    private static TablaDefP Records()
    {
        const int Base16 = 1000;
        return new TablaDefP
        {
            Id = "records", Marcador = IdiomaInterfaz.T("VariosGen4.BuscarRecord"), SinNumero = true,
            Ayuda = IdiomaInterfaz.T("Varios5.ContadoresEntrenadorNombrePKHeX"),
            Filas = _ =>
            {
                var l = new List<FilaTablaP>();
                for (var i = 0; i < Record5.Record32; i++)
                {
                    var n = i;
                    l.Add(new FilaTablaP { Id = n, Nombre = $"{L("L_Record32").TrimEnd(':')} {n} (32 bits)", Min = 0, Max = Record5.Max32, Grupo = "32 bits",
                        Leer = c => (long)ConRecords(c, r => r.GetRecord32(n)), Escribir = (c, v) => ConRecords(c, r => { r.SetRecord32(n, (uint)(long)v); return 0; }) });
                }
                for (var i = 0; i < Record5.Record16; i++)
                {
                    var n = i;
                    l.Add(new FilaTablaP { Id = Base16 + n, Nombre = $"{L("L_Record16").TrimEnd(':')} {n} (16 bits)", Min = 0, Max = Record5.Max16, Grupo = "16 bits",
                        Leer = c => (long)ConRecords(c, r => r.GetRecord16(n)), Escribir = (c, v) => ConRecords(c, r => { r.SetRecord16(n, (ushort)(long)v); return 0; }) });
                }
                return l;
            },
        };
    }

    // ---------------------------------------------------------------- Vuelo y llaves
    private sealed record Destino(int Lugar, int Bit, string? Segundo = null, bool Parentesis = false, string? Sufijo = null);

    // Orden y bit de cada destino, calcados de ReadMain (FlyDestA y FlyDestC). El numero es la posicion del nombre en la lista de lugares del Core (bw2_00000; la prueba lo comprueba contra el original en ingles).
    private static readonly Destino[] DestinosBW =
    [
        new(4, 0), new(5, 1), new(6, 2), new(7, 3), new(8, 4), new(9, 5), new(10, 6), new(11, 7), new(12, 8), new(13, 9),
        new(40, 15, Sufijo: " (B/W)"), new(44, 11), new(41, 10), new(42, 13), new(51, 12, Segundo: "Black City"), new(52, 14, Parentesis: true),
    ];

    private static readonly Destino[] DestinosB2W2 =
    [
        new(117, 24), new(122, 27), new(118, 25), new(4, 8), new(5, 9), new(6, 10), new(7, 11), new(8, 12), new(9, 13), new(10, 14), new(11, 15), new(12, 16), new(13, 17),
        new(41, 18), new(42, 21), new(51, 20, Segundo: "Black City"), new(123, 28), new(119, 26), new(134, 66, Sufijo: " (B2/W2)"), new(44, 19), new(120, 5), new(121, 6), new(-1, 7), new(52, 22, Parentesis: true),
    ];

    /// <summary>Posiciones de la lista de lugares del Core con el nombre en ingles de cada destino, para la prueba que las contrasta con el original.</summary>
    internal static IReadOnlyList<(int Lugar, string Ingles)> LugaresDeLosDestinos { get; } =
    [
        (4, "Nuvema Town"), (5, "Accumula Town"), (6, "Striaton City"), (7, "Nacrene City"), (8, "Castelia City"), (9, "Nimbasa City"), (10, "Driftveil City"), (11, "Mistralton City"), (12, "Icirrus City"), (13, "Opelucid City"),
        (40, "Victory Road (B/W)"), (134, "Victory Road (B2/W2)"), (44, "Pokémon League"), (41, "Lacunosa Town"), (42, "Undella Town"), (51, "White Forest"), (50, "Black City"), (52, "Unity Tower"),
        (117, "Aspertia City"), (122, "Floccesy Town"), (118, "Virbank City"), (123, "Lentimas Town"), (119, "Humilau City"), (120, "Pokéstar Studios"), (121, "Join Avenue"),
    ];

    private static string NombreDelDestino(ContextoP c, Destino d)
    {
        // Lugar -1 es «PWT»: el Core mezcla su nombre con otro («Almacén Frío/PWT») para distinguirlo en el editor de Pokémon, y aquí hace falta el nombre de la sigla sola.
        var n = d.Lugar < 0 ? "PWT" : Lugar(c, d.Lugar);
        // El Core añade « (B/W)» o « (B2/W2)» a las dos Calles Victoria para distinguirlas; el destino de vuelo de cada juego es una sola.
        if (d.Sufijo is not null && n.EndsWith(d.Sufijo, StringComparison.Ordinal)) n = n[..^d.Sufijo.Length];
        if (d.Segundo is not null) n = $"{Lugar(c, 50)} / {n}";   // «Black City/White Forest»: la Ciudad Negra y el Bosque Blanco son un solo destino
        return d.Parentesis ? $"({n})" : n;
    }

    private static string[] Llaves => [IdiomaInterfaz.T("Varios5.ModoFacil"), IdiomaInterfaz.T("Varios5.ModoDesafio"), IdiomaInterfaz.T("Varios5.CiudadNegraBosqueBlanco"), IdiomaInterfaz.T("Varios5.SalaHierro"), IdiomaInterfaz.T("Varios5.SalaIceberg")];

    private static TablaDefP DestinosYLlaves(SaveFile sav)
    {
        var bw = sav is SAV5BW;
        var destinos = bw ? DestinosBW : DestinosB2W2;
        var ofsFly = bw ? 0x204B2 : 0x20392;
        var acciones = new List<AccionTablaP> { new("todos-destinos", L("B_AllFlyDest"), (_, filas) => [.. filas.Where(f => f.Id is >= 100 and < 200).Select(f => (f, (object)true))], IdiomaInterfaz.T("VariosGen4.MarcaTodosDestinosVuelo")) };
        if (!bw) acciones.Add(new("todas-llaves", L("B_AllKeys"), (_, filas) => [.. filas.Where(f => f.Id >= 200).Select(f => (f, (object)true))], IdiomaInterfaz.T("Varios5.MarcaTodasLlavesObtenidas")));
        return new TablaDefP
        {
            Id = "destinos", Marcador = IdiomaInterfaz.T("Varios5.BuscarDestinoLlave"), SinNumero = true,
            Ayuda = IdiomaInterfaz.T("Varios5.DestinosPuedeVolar") + (bw ? "." : IdiomaInterfaz.T("Varios5.LlavesSistemaLlavesObtenida")),
            Filas = c =>
            {
                var l = new List<FilaTablaP>();
                for (var k = 0; k < destinos.Length; k++)
                {
                    var d = destinos[k]; var bit = d.Bit;
                    l.Add(new FilaTablaP
                    {
                        Id = 100 + k, EsMarca = true, Nombre = NombreDelDestino(c, d), Grupo = L("GB_FlyDest"),
                        Leer = x =>
                        {
                            var val = BinaryPrimitives.ReadUInt32LittleEndian(S(x).Data[ofsFly..]);
                            return bit < 32 ? (val & (1u << bit)) != 0 : (S(x).Data[ofsFly + (bit >> 3)] & (1 << (bit & 7))) != 0;
                        },
                        Escribir = (x, v) =>
                        {
                            var on = (bool)v;
                            if (bit < 32)
                            {
                                var val = BinaryPrimitives.ReadUInt32LittleEndian(S(x).Data[ofsFly..]);
                                val = on ? val | (1u << bit) : val & ~(1u << bit);
                                BinaryPrimitives.WriteUInt32LittleEndian(S(x).Data[ofsFly..], val);
                            }
                            else
                            {
                                var ofs = ofsFly + (bit >> 3);
                                S(x).Data[ofs] = (byte)((S(x).Data[ofs] & ~(1 << (bit & 7))) | ((on ? 1 : 0) << (bit & 7)));
                            }
                        },
                    });
                }
                if (!bw)
                {
                    for (var i = 0; i < 5; i++)
                    {
                        var tipo = (KeyType5)i;
                        l.Add(new FilaTablaP { Id = 200 + (i * 2), EsMarca = true, Nombre = IdiomaInterfaz.F("Varios5.LlaveObtenida", Llaves[i]), Grupo = L("GB_KeySystem"),
                            Leer = x => B2(x).Keys.GetIsKeyObtained(tipo), Escribir = (x, v) => { if ((bool)v != B2(x).Keys.GetIsKeyObtained(tipo)) B2(x).Keys.SetIsKeyObtained(tipo, (bool)v); } });
                        l.Add(new FilaTablaP { Id = 201 + (i * 2), EsMarca = true, Nombre = IdiomaInterfaz.F("Varios5.LlaveDesbloqueada", Llaves[i]), Grupo = L("GB_KeySystem"),
                            Leer = x => B2(x).Keys.GetIsKeyUnlocked(tipo), Escribir = (x, v) => { if ((bool)v != B2(x).Keys.GetIsKeyUnlocked(tipo)) B2(x).Keys.SetIsKeyUnlocked(tipo, (bool)v); } });
                    }
                }
                return l;
            },
            Acciones = acciones,
        };
    }
}
