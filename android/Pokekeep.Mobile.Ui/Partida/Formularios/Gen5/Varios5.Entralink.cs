// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Partida.Formularios.Gen1a3;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen5;

internal static partial class Varios5
{
    // ---------------------------------------------------------------- Zona Nexo (Entralink)
    private static string Detalle(int nivel, int exp)
    {
        var total = FestaBlock5.GetTotalEntreeExp(nivel) + exp;
        var siguiente = nivel == 999 ? -1 : FestaBlock5.GetExpNeededForLevelUp(nivel) - exp;
        return IdiomaInterfaz.F("Varios5.PuntosExperienciaSiguienteNivel", total, siguiente);
    }

    private static IEnumerable<CampoP> NivelDelNexarbol(bool b2w2, string prefijo, string nombre, Func<ContextoP, int> leerNivel, Action<ContextoP, int> escribirNivel, Func<ContextoP, int> leerExp, Action<ContextoP, int> escribirExp)
    {
        var idNivel = $"{prefijo}-nivel"; var idExp = $"{prefijo}-exp";
        string Etiqueta(string que) => $"{nombre} · {que}";
        yield return new CampoP
        {
            Id = idNivel, Etiqueta = Etiqueta(IdiomaInterfaz.T("Varios5.EtiquetaNivel")), Tipo = TipoP.Entero, Min = 0, Max = 999, Leer = c => (long)leerNivel(c), Escribir = (c, v) => escribirNivel(c, (int)(long)v!),
            Detalle = b2w2 ? c => Detalle((int)c.Entero(idNivel), (int)c.Entero(idExp)) : null,
            // Subir o bajar el nivel recorta la experiencia al maximo del nivel nuevo (SetNudMax).
            Deriva = c =>
            {
                if (!c.Tiene(idExp)) return new Dictionary<string, object?>();
                var max = FestaBlock5.GetExpNeededForLevelUp((int)c.Entero(idNivel)) - 1;
                return c.Entero(idExp) > max ? new Dictionary<string, object?> { [idExp] = (long)max } : new Dictionary<string, object?>();
            },
        };
        if (b2w2)
            yield return P.Entero(idExp, Etiqueta(IdiomaInterfaz.T("Varios5.EtiquetaExperiencia")), c => leerExp(c), (c, v) => escribirExp(c, (int)v), 0, 49, maxFn: c => FestaBlock5.GetExpNeededForLevelUp((int)c.Entero(idNivel)) - 1,
                detalle: c => Detalle((int)c.Entero(idNivel), (int)c.Entero(idExp)));
    }

    private static IEnumerable<SeccionP> Entralink(SaveFile sav)
    {
        var b2w2 = sav is SAV5B2W2;
        var blanco = NivelDelNexarbol(b2w2, "blanco", IdiomaInterfaz.T("Varios5.BosqueBlanco"), c => S(c).Entralink.WhiteForestLevel, (c, v) => S(c).Entralink.WhiteForestLevel = (ushort)v, c => B2(c).Festa.WhiteEXP, (c, v) => B2(c).Festa.WhiteEXP = (byte)v);
        var negro = NivelDelNexarbol(b2w2, "negro", IdiomaInterfaz.T("Varios5.CiudadNegra"), c => S(c).Entralink.BlackCityLevel, (c, v) => S(c).Entralink.BlackCityLevel = (ushort)v, c => B2(c).Festa.BlackEXP, (c, v) => B2(c).Festa.BlackEXP = (byte)v);
        yield return new SeccionP(L("GB_EntreeLevel"), [.. blanco, .. negro]);
        if (!b2w2) yield break;
        yield return new SeccionP(L("GB_PassPowers"),
        [
            PoderRegalo("poder1", IdiomaInterfaz.T("Varios5.PoderRegalo"), c => ((Entralink5B2W2)S(c).Entralink).PassPower1, (c, v) => ((Entralink5B2W2)S(c).Entralink).PassPower1 = (byte)v),
            PoderRegalo("poder2", IdiomaInterfaz.T("Varios5.PoderRegalo2"), c => ((Entralink5B2W2)S(c).Entralink).PassPower2, (c, v) => ((Entralink5B2W2)S(c).Entralink).PassPower2 = (byte)v),
            PoderRegalo("poder3", IdiomaInterfaz.T("Varios5.PoderRegalo3"), c => ((Entralink5B2W2)S(c).Entralink).PassPower3, (c, v) => ((Entralink5B2W2)S(c).Entralink).PassPower3 = (byte)v),
        ]);
        yield return new SeccionP(L("TAB_Entralink"),
        [
            P.Entero("fm-iniciadas", L("L_FMHosted"), c => B2(c).Festa.Hosted, (c, v) => B2(c).Festa.Hosted = (ushort)v, 0, 9999),
            P.Entero("fm-participado", L("L_FMParticipated"), c => B2(c).Festa.Participated, (c, v) => B2(c).Festa.Participated = (ushort)v, 0, 9999),
            P.Entero("fm-completadas", L("L_FMCompleted"), c => B2(c).Festa.Completed, (c, v) => B2(c).Festa.Completed = (ushort)v, 0, 9999),
            P.Entero("fm-maximos", L("L_FMTopScore"), c => B2(c).Festa.TopScores, (c, v) => B2(c).Festa.TopScores = (ushort)v, 0, 9999),
            P.Entero("fm-participantes", L("L_FMParticipants"), c => B2(c).Festa.Participants, (c, v) => B2(c).Festa.Participants = (byte)v, 0, byte.MaxValue),
        ]);
        yield return new SeccionP(L("GB_FunfestMissions"),
        [
            P.Accion("desbloquear-misiones", L("B_FunfestMissions"), c => B2(c).Festa.UnlockAllFunfestMissions(), textoBoton: L("B_FunfestMissions"), prioridad: -1,
                confirmacion: IdiomaInterfaz.T("Varios5.DesbloqueanTodasMisionesFestival"), ayuda: IdiomaInterfaz.T("Varios5.ActivaMarcasEventoNecesita")),
        ]);
    }

    private static CampoP PoderRegalo(string id, string etiqueta, Func<ContextoP, long> leer, Action<ContextoP, long> escribir)
        => P.Opcion(id, etiqueta, leer, escribir, _ => TextosPkhex.EnumLista<PassPower5>(), titulo: etiqueta);

    // ---------------------------------------------------------------- Misiones del Festival (solo Negro 2 / Blanco 2)
    private static string[] NivelesDelFestival(ContextoP c) => ["Nv. 1", "Nv. 2 +", "Nv. 3 ++", "Nv. 3 +++"];

    private static IReadOnlyList<OpcionP> OpcionesDeNivel(ContextoP c)
    {
        var ninguno = c.Textos.specieslist.Length > 0 && c.Textos.specieslist[0].Trim().Length > 0 ? c.Textos.specieslist[0] : "—";
        var n = NivelesDelFestival(c);
        return [new(0, n[0]), new(1, n[1]), new(2, n[2]), new(3, n[3]), new(7, ninguno)];
    }

    private static string NombreDeLaMision(int i) => TextosPkhex.Enum((Funfest5Mission)i);

    private static ColeccionDefP Misiones()
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "misiones", Elemento = IdiomaInterfaz.T("Varios5.Mision"), Cantidad = _ => FestaBlock5.MaxMissionIndex + 1, Titulo = (_, i) => NombreDeLaMision(i),
            Resumen = (c, i) => B2(c).Festa.GetMissionRecord(i) is var r && r.RawValue == 0 ? null : $"{r.Score} pts · total {r.Total}",
            Secciones = (_, i) =>
            {
                var grupo = $"mision{i}";
                string Id(string campo) => col!.IdCampo(i, campo);
                void Guardar(ContextoP c)
                {
                    var score = new Funfest5Score((int)c.Entero(Id("total")), (int)c.Entero(Id("puntos")), (int)c.Entero(Id("nivel")), c.Marca(Id("nueva")));
                    B2(c).Festa.SetMissionRecord(i, score);
                }
                return
                [
                    new SeccionP(string.Empty,
                    [
                        P.Info(Id("estado"), IdiomaInterfaz.T("CartasP.Estado"), c => B2(c).Festa.IsFunfestMissionUnlocked(i) ? L("L_FMUnlocked") : L("L_FMLocked")),
                        P.Marca(Id("nueva"), L("CHK_FMNew"), c => B2(c).Festa.GetMissionRecord(i).IsNew, (c, _) => Guardar(c), grupo: grupo),
                        P.Opcion(Id("nivel"), IdiomaInterfaz.T("Cajas.Nivel"), c => B2(c).Festa.GetMissionRecord(i).Level, (c, _) => Guardar(c), OpcionesDeNivel, titulo: IdiomaInterfaz.T("Cajas.Nivel"), grupo: grupo),
                        P.Entero(Id("puntos"), L("L_FMBestScore"), c => B2(c).Festa.GetMissionRecord(i).Score, (c, _) => Guardar(c), 0, 9999, grupo: grupo),
                        P.Entero(Id("total"), L("L_FMBestTotal"), c => B2(c).Festa.GetMissionRecord(i).Total, (c, _) => Guardar(c), 0, 9999, grupo: grupo),
                    ]),
                ];
            },
        };
        return col;
    }

    // ---------------------------------------------------------------- Bosque (Nexárbol): zonas desbloqueadas, aleatorizar y las 530 ranuras
    private const int TotalDeRanuras = 530;

    /// <summary>El bosque está cifrado en la partida: se descifra para leer o escribir y se vuelve a cifrar siempre (como <c>EnsureDecrypted(false)</c> del formulario).</summary>
    private static T ConBosque<T>(ContextoP c, Func<EntreeForest, T> f)
    {
        var b = S(c).EntreeForest;
        b.EnsureDecrypted();
        try { return f(b); }
        finally { b.EnsureDecrypted(false); }
    }

    private static IEnumerable<SeccionP> AjustesDelBosque()
    {
        yield return new SeccionP(string.Empty,
        [
            P.Entero("zonas", L("L_Area18"), c => Math.Clamp(ConBosque(c, b => b.Unlock38Areas) + 2, 2, 8), (c, v) => ConBosque(c, b => { b.Unlock38Areas = (int)v - 2; return 0; }), 2, 8, prioridad: 1),
            P.Marca("zona9", L("CHK_Area9"), c => ConBosque(c, b => b.Unlock9thArea), (c, v) => ConBosque(c, b => { b.Unlock9thArea = v; return 0; }), prioridad: 1),
        ]);
        yield return new SeccionP(string.Empty,
        [
            new CampoP
            {
                Id = "aleatorizar", Etiqueta = L("B_RandForest"), Tipo = TipoP.Accion, TextoBoton = L("B_RandForest"), Prioridad = -2, Leer = _ => false,
                Confirmacion = IdiomaInterfaz.T("Varios5.SustituyenPokemonBosqueOtros"),
                Ayuda = IdiomaInterfaz.T("Varios5.RellenaTodasRanurasPokemon"),
                Escribir = (c, v) => { if (v is true) AleatorizarBosque(c); },
                // El formulario deja ademas las zonas al maximo (NUD_Unlocked = 8, CHK_Area9 marcada): aqui se ve en el borrador y se escribe con sus campos.
                Deriva = c => c.Marca("aleatorizar") ? new Dictionary<string, object?> { ["zonas"] = 8L, ["zona9"] = true } : new Dictionary<string, object?>(),
            },
        ]);
    }

    private static void AleatorizarBosque(ContextoP c)
    {
        var origen = (S(c) is SAV5BW ? Encounters5BW.DreamWorld_BW : Encounters5B2W2.DreamWorld_B2W2).Concat(Encounters5DR.DreamWorld_Common).ToList();
        var rnd = Util.Rand;
        ConBosque(c, b =>
        {
            foreach (var s in b.Slots)
            {
                var indice = rnd.Next(origen.Count);
                var slot = origen[indice];
                origen.Remove(slot);
                s.Species = slot.Species;
                s.Form = slot.Form;
                s.Gender = !((IFixedGender)slot).IsFixedGender ? PersonalTable.B2W2[slot.Species].RandomGender() : slot.Gender;
                ReadOnlySpan<ushort> movs = slot.Moves;
                var cuenta = movs.Length - movs.Count<ushort>(0);
                s.Move = cuenta == 0 ? (ushort)0 : movs[rnd.Next(cuenta)];
            }
            return 0;
        });
    }

    // Zona de cada ranura (EntreeForest.GetSlotArea es privado): se lee una vez de los datos del Core.
    private static EntreeForestArea[]? _zonas;

    private static EntreeForestArea[] ZonasDe(ContextoP c) => _zonas ??= ConBosque(c, b => b.Slots.Select(s => s.Area).ToArray());

    private static string NombreDeLaZona(EntreeForestArea a, int indice)
    {
        string posicion = a.HasFlag(EntreeForestArea.Center) ? "centro" : a.HasFlag(EntreeForestArea.Left) ? "izquierda" : a.HasFlag(EntreeForestArea.Right) ? "derecha" : string.Empty;
        string zona;
        if (a.HasFlag(EntreeForestArea.Deepest)) zona = IdiomaInterfaz.T("Varios5.ZonaProfunda");
        else if (a.HasFlag(EntreeForestArea.Ninth)) zona = IdiomaInterfaz.T("Varios5.Zona");
        else
        {
            var n = 0;
            for (var f = (int)EntreeForestArea.First; f <= (int)EntreeForestArea.Eighth; f <<= 1) { n++; if (((int)a & f) != 0) break; }
            zona = IdiomaInterfaz.F("Varios5.Zona2", n);
        }
        return posicion.Length == 0 ? zona : $"{zona}, {posicion}";
    }

    private static int NumeroEnLaZona(int indice) => indice < 20 ? indice + 1 : indice < 50 ? ((indice - 20) % 10) + 1 : ((indice - 50) % 20) + 1;

    private static string NombreDeLaEspecie(ContextoP c, ushort especie) => especie < c.Textos.Species.Count ? c.Textos.Species[especie] : IdiomaInterfaz.F("Varios5.Valida", especie);

    private static string[] NombresDeLaAnimacion =>     [
        IdiomaInterfaz.T("Varios5.MiraAzar"), "Pasea en un radio de 3×3", IdiomaInterfaz.T("Varios5Entralink.PaseaEnUnRadio"), IdiomaInterfaz.T("Varios5.MueveArribaAbajo"), IdiomaInterfaz.T("Varios5.MueveIzquierdaDerecha"), IdiomaInterfaz.T("Varios5.MueveIzquierdaDerechaMirando"), IdiomaInterfaz.T("Varios5Entralink.GiraEnSentidoHorario"), IdiomaInterfaz.T("Varios5Entralink.GiraEnSentidoAntihorario"),
    ];

    private static IReadOnlyList<OpcionP> Generos(ushort especie)
    {
        if (especie == 0) return [new OpcionP(0, "-")];
        var pi = PersonalTable.B2W2[especie];
        if (pi.Genderless) return [new OpcionP(2, IdiomaInterfaz.T("GoPark.Genero"))];
        var l = new List<OpcionP>();
        if (!pi.OnlyFemale) l.Add(new OpcionP(0, IdiomaInterfaz.T("Varios5.Macho")));
        if (!pi.OnlyMale) l.Add(new OpcionP(1, IdiomaInterfaz.T("Varios5.Hembra")));
        return l;
    }

    private static bool TieneFormas(ushort especie) => PersonalTable.B2W2[especie].HasForms || especie == (int)Species.Mothim;

    private static IReadOnlyList<OpcionP> Formas(ContextoP c, ushort especie)
        => P.Lista(FormConverter.GetFormList(especie, c.Textos.types, c.Textos.forms, GameInfo.GenderSymbolUnicode, S(c).Context));

    private static ColeccionDefP Ranuras()
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "ranuras", Elemento = IdiomaInterfaz.T("Varios5.Ranura"), Cantidad = _ => TotalDeRanuras,
            Titulo = (c, i) => $"{NombreDeLaZona(ZonasDe(c)[i], i)} · {NumeroEnLaZona(i)}",
            Resumen = (c, i) => NombreDeLaEspecie(c, ConBosque(c, b => b.Slots[i].Species)),
            Secciones = (_, i) =>
            {
                string Id(string campo) => col!.IdCampo(i, campo);
                ushort Especie(ContextoP c) => (ushort)c.Entero(Id("especie"));
                return
                [
                    new SeccionP(string.Empty,
                    [
                        P.Opcion(Id("especie"), L("L_SlotSpecies"), c => ConBosque(c, b => b.Slots[i].Species), (c, v) => ConBosque(c, b => { b.Slots[i].Species = (ushort)v; return 0; }), AyudasGen3.Especies, titulo: L("L_SlotSpecies"),
                            deriva: c =>
                            {
                                // Cambiar la especie vuelve a enlazar las listas de forma y género y las deja en su primera opción (el formulario rehace el DataSource de ambos).
                                var generos = Generos(Especie(c));
                                return new Dictionary<string, object?> { [Id("forma")] = 0L, [Id("genero")] = (long)(generos.Count > 0 ? generos[0].Id : 0) };
                            }, especie: true),
                        P.Opcion(Id("movimiento"), L("L_Move").TrimEnd(':'), c => ConBosque(c, b => b.Slots[i].Move), (c, v) => ConBosque(c, b => { b.Slots[i].Move = (ushort)v; return 0; }), AyudasGen3.Movimientos, titulo: L("L_Move").TrimEnd(':')),
                        P.Opcion(Id("genero"), IdiomaInterfaz.T("Form.Genero"), c => ConBosque(c, b => b.Slots[i].Gender), (c, v) => ConBosque(c, b => { b.Slots[i].Gender = (byte)v; return 0; }), c => Generos(Especie(c)), titulo: IdiomaInterfaz.T("Form.Genero")),
                        P.Opcion(Id("forma"), L("L_Form").TrimEnd(':'), c => ConBosque(c, b => b.Slots[i].Form) is var f && f < Formas(c, ConBosque(c, b => b.Slots[i].Species)).Count ? (long)f : 0L,
                            (c, v) => ConBosque(c, b => { b.Slots[i].Form = (byte)v; return 0; }), c => Formas(c, Especie(c)), visible: c => TieneFormas(Especie(c)), titulo: L("L_Form").TrimEnd(':')),
                        P.Entero(Id("animacion"), L("L_Animation").TrimEnd(':'), c => (long)ConBosque(c, b => b.Slots[i].Animation), (c, v) => ConBosque(c, b => { b.Slots[i].Animation = (EntreeForestAnimation)(int)v; return 0; }), 0, 7,
                            detalle: c => c.Entero(Id("animacion")) is var a && a is >= 0 and < 8 ? NombresDeLaAnimacion[a] : null),
                    ]),
                ];
            },
        };
        return col;
    }
}
