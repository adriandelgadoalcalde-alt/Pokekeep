// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using static System.Buffers.Binary.BinaryPrimitives;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>
/// PK-SAV-038 (TAB_BF): el Frente Batalla de <c>SAV_Misc4</c>. Es el calco de <c>ReadBattleFrontier</c>, <c>ChangeStat1</c>, <c>StatAddrControl</c>, <c>ChangeStatVal</c> y el Hall y el Castillo: cada instalación (la Torre en Diamante/Perla; Torre, Fábrica, Sala,
/// Castillo y Ruleta en Platino/HG/SS) guarda sus valores en la misma tabla de direcciones del formulario (<c>BFF</c>: orden de los valores, tipo de combate, dirección, separación e indicador de «continuar») y los guarda al momento de cambiarlos.
/// <para>En el móvil las fotos son una pestaña (cinco desplegables, uno por instalación, en vez de cinco botones que dan la vuelta por los cinco estados) y las rachas una lista con un elemento por instalación y una sección por tipo de combate
/// (y por modo, Nv. 50 o Abierto, en la Fábrica). Quirks de PKHeX conservados a propósito para que los bytes sean idénticos: (1) en la Torre, «Actual» y «Continuar» van ligados (continuar = actual / 7, y al revés), (2) los rangos del Castillo
/// se escriben como enteros de 32 bits y pisan el rango siguiente, (3) los contadores por tipo de la Sala usan la dirección <c>+6 + ((i >> 1) &lt;&lt; 1)</c> con dos tipos por byte. Declarado en el ADR 0065.</para>
/// </summary>
internal static partial class VariosGen4
{
    private enum Instalacion { Torre, Fabrica, Sala, Castillo, Ruleta }

    // BFF de cada juego: [indice de BFV, indice de BFT, direccion, separacion por tipo de combate, direccion del indicador de continuar]. BFV: orden de los u16 -> cuadro (2 = récord, 0 = actual, 1/3 = intercambio o PC).
    private static readonly int[][] BffDp = [[0, 1, 0x5FCA, 0x04, 0x6601]];
    private static readonly int[][] BffPt = [[0, 1, 0x68E0, 0x04, 0x723D], [1, 0, 0x68F4, 0x10, 0x7EF8], [0, 0, 0x6924, 0x18, 0x7EFC], [2, 0, 0x696C, 0x10, 0x7F00], [0, 0, 0x699C, 0x04, 0x7F04]];
    private static readonly int[][] BffHgss = [[0, 1, 0x5264, 0x04, 0x5BC1], [1, 0, 0x5278, 0x10, 0x687C], [0, 0, 0x52A8, 0x18, 0x6880], [2, 0, 0x52F0, 0x10, 0x6884], [0, 0, 0x5320, 0x04, 0x6888]];
    private static readonly int[][] Bfv = [[2, 0], [2, 0, 3, 1], [2, 0, 1, -1, 3]];

    private static string[][] Combates => [[IdiomaInterfaz.T("VariosGen4Frente.Individual"), IdiomaInterfaz.T("VariosGen4Frente.Dobles"), IdiomaInterfaz.T("VariosGen4Frente.Multi")], [IdiomaInterfaz.T("VariosGen4Frente.Individual"), IdiomaInterfaz.T("VariosGen4Frente.Dobles"), IdiomaInterfaz.T("VariosGen4Frente.MultiEntrenador"), IdiomaInterfaz.T("VariosGen4Frente.MultiAmigo"), "Wi-Fi"]];

    // Gen4.Met0: Torre Batalla, Fábrica Batalla, Sala Batalla, Castillo Batalla y Ruleta Batalla (los nombres oficiales de Platino y HG/SS).
    private static readonly int[] LugaresDeLaInstalacion = [80, 113, 116, 114, 115];

    private static int[][] Bff(SAV4 s) => s switch { SAV4DP => BffDp, SAV4Pt => BffPt, _ => BffHgss };
    private static int InicioDeFotos(SAV4 s) => s is SAV4Pt ? 79 : 77;
    private static int Instalaciones(SAV4 s) => (int)s.MaxFacility + 1;
    private static string Nombre(ContextoP c, int f) => c.Textos.Gen4.Met0[LugaresDeLaInstalacion[f]];

    private static string IdF(int f, int t, int m, string campo) => $"f{f}.t{t}.m{m}.{campo}";

    // ---------------------------------------------------------------- Fotos (GB_Prints, no en D/P)
    private static IReadOnlyList<OpcionP> EstadosFoto => P.Lista(IdiomaInterfaz.T("VariosGen4Frente.Foto"), IdiomaInterfaz.T("VariosGen4Frente.FotoPlataLista"), IdiomaInterfaz.T("VariosGen4Frente.FotoPlataRecibida"), IdiomaInterfaz.T("VariosGen4Frente.FotoOroLista"), IdiomaInterfaz.T("VariosGen4Frente.FotoOroRecibida"));

    private static PestanaP Fotos(SAV4 s4)
    {
        var inicio = InicioDeFotos(s4);
        // BTN_Print_Click: el estado da la vuelta 0 -> 4 -> 0 (SetWork al momento). Las cinco fotos van en el orden de los botones: Torre, Fábrica, Sala, Castillo y Ruleta.
        var campos = Enumerable.Range(0, 5).Select(i => (CampoP)P.Opcion($"foto{i}", $"{L("GB_Prints")} · {Nombre0(i)}", c => S(c).GetWork(inicio + i), (c, v) => S(c).SetWork(inicio + i, (ushort)v), _ => EstadosFoto,
            titulo: $"{L("GB_Prints")} · {Nombre0(i)}", prioridad: i)).ToList();
        return new PestanaP("fotos", L("GB_Prints"), [new SeccionP(string.Empty, campos)]);

        string Nombre0(int i) => new[] { L("BTN_PrintTower"), L("BTN_PrintFactory"), L("BTN_PrintHall"), L("BTN_PrintCastle"), L("BTN_PrintArcade") }[i];
    }

    // ---------------------------------------------------------------- Rachas (GB_Streaks)
    private static PestanaP FrenteBatalla(SAV4 s4)
    {
        var pestanas = new List<PestanaP>();
        var bff = Bff(s4);
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "instalaciones", Elemento = IdiomaInterfaz.T("VariosGen4Frente.Instalacion"), Cantidad = _ => Instalaciones(s4), Titulo = (c, f) => Nombre(c, f),
            Secciones = (c, f) => SeccionesDeInstalacion(c, col!, s4, bff, f),
        };
        return new PestanaP("frente", L("TAB_BF"), [], Coleccion: col);
    }

    private static IReadOnlyList<SeccionP> SeccionesDeInstalacion(ContextoP ctx, ColeccionDefP col, SAV4 s4, int[][] bff, int f)
    {
        var info = bff[f];
        var tipos = Combates[info[1]];
        var secciones = new List<SeccionP>();
        for (var t = 0; t < tipos.Length; t++)
        {
            for (var m = 0; m < (f == (int)Instalacion.Fabrica ? 2 : 1); m++)
            {
                var titulo = f == (int)Instalacion.Fabrica ? $"{tipos[t]} · {L(m == 0 ? "RB_Stats3_01" : "RB_Stats3_02")}" : tipos[t];
                var campos = CamposDeEstadisticas(col, s4, info, f, t, m);
                if (f == (int)Instalacion.Sala) campos.AddRange(CamposDeLaSala(ctx, col, s4, info, f, t));
                if (f == (int)Instalacion.Castillo) campos.AddRange(CamposDelCastillo(col, info, f, t));
                secciones.Add(new SeccionP(titulo, campos));
            }
        }
        return secciones;
    }

    /// <summary>La dirección de los u16 de una instalación, tipo de combate y modo: <c>addrVal</c> de <c>StatAddrControl</c>.</summary>
    private static int DireccionDeValores(int[] info, int t, int m) => info[2] + (info[3] * t) + (m << 3);

    private static List<CampoP> CamposDeEstadisticas(ColeccionDefP col, SAV4 s4, int[] info, int f, int t, int m)
    {
        var orden = Bfv[info[0]];
        var dir = DireccionDeValores(info, t, m);
        var flag = info[4];
        var mascara = (byte)(1 << (t + (m << 2)));
        var continuarTorre = flag + (s4 is SAV4DP ? 3 : 1) + (t << 1);
        var campos = new List<CampoP>();
        // Nombres de los cuadros: Actual (0), Récord (2), y los otros dos segun la instalacion (Intercambio en la Fábrica, puntos del Castillo en el Castillo).
        string Etiqueta(int cuadro) => cuadro switch
        {
            0 => L("L_Stat0"),
            2 => L("L_Stat2"),
            1 when f == (int)Instalacion.Fabrica => $"{L("L_Stat1")} ({L("L_Stat0")})",
            3 when f == (int)Instalacion.Fabrica => $"{L("L_Stat3")} ({L("L_Stat2")})",
            1 => IdiomaInterfaz.T("VariosGen4Frente.PuntosCastilloActual"),
            _ => IdiomaInterfaz.T("VariosGen4Frente.PuntosCastilloRecord"),
        };
        var idActual = IdF(f, t, m, "actual"); var idCuenta = IdF(f, t, m, "continuar_cuenta");
        for (var k = 0; k < orden.Length; k++)
        {
            var cuadro = orden[k];
            if (cuadro < 0) continue;
            var posicion = k;
            var id = cuadro switch { 0 => idActual, 2 => IdF(f, t, m, "record"), 1 => IdF(f, t, m, f == (int)Instalacion.Castillo ? "pc_actual" : "trueque_actual"), _ => IdF(f, t, m, f == (int)Instalacion.Castillo ? "pc_record" : "trueque_record") };
            // ChangeStat: el valor se lee del u16 y se recorta a 9999; StatAddrControl lo escribe recortado a 9999. En la Torre, «Actual» arrastra a «Continuar».
            campos.Add(P.Entero(Prefijo(col, f, id), Etiqueta(cuadro), c => Math.Min((ushort)9999, ReadUInt16LittleEndian(S(c).General[(dir + (posicion << 1))..])),
                (c, v) => WriteUInt16LittleEndian(S(c).General[(dir + (posicion << 1))..], (ushort)Math.Min(9999, v)), 0, 9999, prioridad: cuadro,
                afectaA: f == (int)Instalacion.Torre && cuadro == 0 ? _ => [Prefijo(col, f, idCuenta)] : null));
        }
        if (f == (int)Instalacion.Torre)
        {
            // NUD_Stat1 es el número de «continuar» de la Torre (0-65535): u16 en la dirección del indicador. ChangeStatVal liga los dos cuadros: continuar = actual / 7.
            campos.Add(P.Entero(Prefijo(col, f, idCuenta), L("CHK_Continue") + IdiomaInterfaz.T("VariosGen4Frente.Veces"), c => ReadUInt16LittleEndian(S(c).General[continuarTorre..]), (c, v) => WriteUInt16LittleEndian(S(c).General[continuarTorre..], (ushort)v), 0, 65535, prioridad: 1,
                afectaA: _ => [Prefijo(col, f, idActual)]));
            AtarTorre(campos, col, f, idActual, idCuenta);
        }
        campos.Add(P.Marca(Prefijo(col, f, IdF(f, t, m, "continuar")), L("CHK_Continue"), c => (S(c).General[flag] & mascara) != 0,
            (c, v) =>
            {
                var general = S(c).General;
                if (v) { general[flag] |= mascara; if (f == (int)Instalacion.Castillo) general[flag + 1] |= 0x01; }   // «no se sabe qué indica» (PKHeX): se pone junto con el del Castillo y nunca se quita
                else general[flag] &= (byte)~mascara;
            }, prioridad: 10));
        return campos;
    }

    /// <summary>El id de un campo de una instalación: el de la lista de elementos (<c>instalaciones[f].f0.t0.m0.actual</c>).</summary>
    private static string Prefijo(ColeccionDefP col, int f, string id) => col.IdCampo(f, id);

    /// <summary>
    /// ChangeStatVal de la Torre. Con «Actual» (n0) y «Continuar» (n1): si n0/7 no es n1, al cambiar n0 se pone n1 = n0/7 y al cambiar n1 se pone n0 = n1*7 (o 9999 si no cabe), lo que a su vez recalcula n1 = n0/7. Los cuadros solo disparan
    /// el evento si cambian. Aquí se resuelve la cadena entera y se dejan los dos en el borrador.
    /// </summary>
    internal static (long Actual, long Continuar) ResolverTorre(long n0, long n1, int cambiado)
    {
        for (var vueltas = 0; vueltas < 8 && (n0 / 7) != n1; vueltas++)
        {
            if (cambiado == 0) { n1 = n0 / 7; cambiado = 1; }
            else if (9999 > n1 * 7) { n0 = n1 * 7; cambiado = 0; }
            else if (n0 < 9999) { n0 = 9999; cambiado = 0; }
            else break;
        }
        return (n0, n1);
    }

    private static void AtarTorre(List<CampoP> campos, ColeccionDefP col, int f, string idActual, string idCuenta)
    {
        var a = Prefijo(col, f, idActual); var b = Prefijo(col, f, idCuenta);
        var iActual = campos.FindIndex(x => x.Id == a); var iCuenta = campos.FindIndex(x => x.Id == b);
        CampoP ConDeriva(CampoP x, int cambiado) => new()
        {
            Id = x.Id, Etiqueta = x.Etiqueta, Tipo = x.Tipo, Leer = x.Leer, Escribir = x.Escribir, Min = x.Min, Max = x.Max, Prioridad = x.Prioridad, AfectaA = x.AfectaA,
            Deriva = c => { var (n0, n1) = ResolverTorre(c.Entero(a), c.Entero(b), cambiado); return new Dictionary<string, object?> { [a] = n0, [b] = n1 }; },
        };
        campos[iActual] = ConDeriva(campos[iActual], 0);
        campos[iCuenta] = ConDeriva(campos[iCuenta], 1);
    }

    // ---------------------------------------------------------------- Sala Batalla (GB_Hall)
    private static readonly byte[] IndiceDeTipo = [0, 9, 10, 12, 11, 14, 1, 3, 4, 2, 13, 6, 5, 7, 15, 16, 8];

    private static int DireccionDeSala(int[] info, int t) => info[2] + (info[3] * t);
    private static int DireccionContador(int[] info, int t, int i) => DireccionDeSala(info, t) + 6 + ((i >> 1) << 1);

    private static ushort EspecieActual(ContextoP c, int[] info, int t) => ReadUInt16LittleEndian(S(c).General[(DireccionDeSala(info, t) + 4)..]);

    /// <summary>
    /// La Sala Batalla guarda, por tipo de combate, UNA especie «actual» (u16 en +4) con diecisiete contadores por tipo (nibbles) y, aparte, las rachas de cada especie (<c>Hall4</c>, solo Platino y HG/SS).
    /// En WinForms un desplegable de especie sirve para tres cosas: marcar «Actual» (escribe esa especie o 0), editar los contadores (solo si es la actual) y ver las rachas de ESA especie. Un campo auxiliar que cambia lo que se lee
    /// y una casilla cuyo valor depende de él no cuadran con «escribir solo lo que cambia» (marcar «Actual» para otra especie es el mismo «Sí» que había), así que en el móvil son los datos tal cual: «Especie actual» es una lista
    /// con la especie guardada (o ninguna), los contadores son los de esa especie y las rachas llevan su propio desplegable de especie. Se escribe lo mismo, en la misma dirección y con la misma llamada.
    /// </summary>
    private static IEnumerable<CampoP> CamposDeLaSala(ContextoP ctx, ColeccionDefP col, SAV4 s4, int[] info, int f, int t)
    {
        var idActual = col.IdCampo(f, $"sala.t{t}.actual");
        var idEspeciePropia = col.IdCampo(f, $"sala.t{t}.rachas_especie");
        long Nibble(ContextoP c, int cont) => Math.Min(10, (S(c).General[DireccionContador(info, t, cont)] >> ((cont & 1) << 2)) & 0x0F);
        // GetHallStat: los contadores solo se enseñan (y se editan) si hay especie actual; sin ella quedan a 0 y bloqueados.
        long Contador(ContextoP c, int cont) => EspecieActual(c, info, t) != 0 ? Nibble(c, cont) : 0;
        long Rachas(ContextoP c) => Math.Min((ushort)9999, S(c).GetHall()!.GetCount(t, (ushort)c.Entero(idEspeciePropia)));

        // CHK_HallCurrent_CheckedChanged: escribe la especie elegida (o 0 al desmarcar) en +4. Aqui la lista ES ese u16.
        var especies = new List<OpcionP> { new(0, Ajustes.Pkhex.ProgramaPkhex.Textos.specieslist[0]) };
        yield return P.Opcion(idActual, IdiomaInterfaz.F("Gen4.HallEspecie", L("CHK_HallCurrent").TrimEnd(':')), c => EspecieActual(c, info, t),
            (c, v) => WriteUInt16LittleEndian(S(c).General[(DireccionDeSala(info, t) + 4)..], (ushort)v),
            c => [.. especies, .. AyudasGen3Gen4.EspeciesSinHueco(c)], titulo: L("CHK_HallCurrent").TrimEnd(':'), prioridad: 20,
            ayuda: IdiomaInterfaz.T("VariosGen4Frente.EspecieSalaGuardaContadores"),
            afectaA: _ => Enumerable.Range(0, IndiceDeTipo.Length).Select(i => col.IdCampo(f, $"sala.t{t}.tipo{i}")));
        // NUD_HallType01..17: el contador de cada tipo (0-10) esta en un nibble.
        for (var i = 0; i < IndiceDeTipo.Length; i++)
        {
            var cont = i;
            yield return P.Entero(col.IdCampo(f, $"sala.t{t}.tipo{i}"), IdiomaInterfaz.F("VariosGen4Frente.Contador", ctx.Textos.types[IndiceDeTipo[i]]), c => Contador(c, cont),
                (c, v) =>
                {
                    var ofs = DireccionContador(info, t, cont); var general = S(c).General;
                    general[ofs] = (byte)((general[ofs] & ~(0xF << ((cont & 1) << 2))) | ((int)v << ((cont & 1) << 2)));
                }, 0, 10, prioridad: 30 + i,
                motivo: c => c.Entero(idActual) != 0 ? null : IdiomaInterfaz.T("VariosGen4Frente.EligeAntesEspecieActual"));
        }
        if (s4.GetHall() is not null)
        {
            // CB_Species + NUD_HallStreaks: las rachas de la especie elegida. El desplegable solo ayuda a elegir (no se escribe); cambiarlo relee las rachas, y solo se escribe una racha que se haya cambiado de verdad.
            yield return new CampoP
            {
                Id = idEspeciePropia, Etiqueta = IdiomaInterfaz.T("VariosGen4Frente.EspecieRachas"), Tipo = TipoP.Opcion, SinEscritura = true, TituloSelector = IdiomaInterfaz.T("VariosGen4Frente.EspecieRachas"), Leer = c => AyudasGen3Gen4.EspeciesSinHueco(c)[0].Id,
                Opciones = AyudasGen3Gen4.EspeciesSinHueco, Prioridad = 50,
                Deriva = c => new Dictionary<string, object?> { [col.IdCampo(f, $"sala.t{t}.rachas")] = Rachas(c) },
                Motivo = c => c.Tiene(col.IdCampo(f, $"sala.t{t}.rachas")) && c.Entero(col.IdCampo(f, $"sala.t{t}.rachas")) != Rachas(c) ? IdiomaInterfaz.T("VariosGen4Frente.AplicaDescartaAntesCambio") : null,
            };
            yield return P.Entero(col.IdCampo(f, $"sala.t{t}.rachas"), L("GB_Streaks"), Rachas,
                (c, v) => { if (v == Rachas(c)) return; var h = S(c).GetHall()!; h.SetCount(t, (ushort)c.Entero(idEspeciePropia), (ushort)v); h.RefreshChecksum(); }, 0, 9999, prioridad: 60);
        }
    }

    // ---------------------------------------------------------------- Castillo Batalla (GB_Castle)
    private static IEnumerable<CampoP> CamposDelCastillo(ColeccionDefP col, int[] info, int f, int t)
    {
        (string Id, string Nombre, int Min, int Max)[] rangos = [("recuperar", IdiomaInterfaz.T("Inicio.Recuperar"), 1, 3), ("objeto", IdiomaInterfaz.T("VariosGen4Frente.Objeto"), 1, 3), ("info", "Info.", 1, 2)];
        for (var i = 0; i < rangos.Length; i++)
        {
            var (id, nombre, min, max) = rangos[i];
            var ofs = DireccionDeSala(info, t) + 0x0A + (i << 1);
            // GetCastleStat recorta el i16 al rango del cuadro; NUD_CastleRank_ValueChanged escribe un ENTERO DE 32 BITS (WriteInt32LittleEndian) en la direccion del rango, asi que pisa tambien los dos bytes del rango siguiente.
            yield return P.Entero(col.IdCampo(f, $"castillo.t{t}.{id}"), $"{L("GB_Castle")} · {nombre}", c => Math.Clamp((int)ReadInt16LittleEndian(S(c).General[ofs..]), min, max),
                (c, v) => WriteInt32LittleEndian(S(c).General[ofs..], (int)v), min, max, prioridad: 40 + i,
                ayuda: i < 2 ? IdiomaInterfaz.T("VariosGen4Frente.PKHeXEscribeRangoBits") : null);
        }
    }
}

/// <summary>Listas de especies del bloque 8a-2 (las del juego abierto, sin la casilla vacía).</summary>
internal static class AyudasGen3Gen4
{
    public static IReadOnlyList<OpcionP> EspeciesSinHueco(ContextoP c) => [.. Gen1a3.AyudasGen3.Especies(c).Where(o => o.Id != 0)];
}
