// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen5;

internal static partial class GaleriaUnion
{
    // ---------------------------------------------------------------- General (JoinAvenueEntityGeneralEditor)
    private static IEnumerable<CampoP> General(string pre, Func<ContextoP, IJoinAvenueEntity5> e)
    {
        string Id(string x) => pre + x;
        var grupoIdioma = $"{pre}idioma";
        yield return P.Texto(Id("nombre"), L("L_Name"), c => e(c).Name, (c, v) => e(c).Name = v, 7);
        yield return P.Entero(Id("pais"), L("L_Country"), c => e(c).Country, (c, v) => e(c).Country = (byte)v, 0, 255);
        yield return P.Entero(Id("subregion"), L("L_Subregion"), c => e(c).Subregion, (c, v) => e(c).Subregion = (byte)v, 0, 255);
        yield return P.Texto(Id("exclamacion"), L("L_Shout"), c => e(c).Shout, (c, v) => e(c).Shout = v, 8);
        yield return P.Opcion(Id("version"), L("L_Version"), c => e(c).Version, (c, v) => e(c).Version = (byte)v, Juegos, titulo: L("L_Version"));
        yield return P.Opcion(Id("idioma"), L("L_Language"), c => e(c).Language, (c, v) => e(c).Language = (byte)v, Idiomas, titulo: L("L_Language"), grupo: grupoIdioma);
        yield return P.Entero(Id("desconocido22"), L("L_Unknown22"), c => e(c).Unknown22, (c, v) => e(c).Unknown22 = (byte)v, 0, 15);
        yield return P.Opcion(Id("genero"), L("L_Gender"), c => Math.Min(e(c).Gender, (byte)2), (c, v) => e(c).Gender = (byte)v, _ => P.Lista(IdiomaInterfaz.T("GaleriaUnion.Masculino"), IdiomaInterfaz.T("GaleriaUnion.Femenino"), "-"), titulo: L("L_Gender"));
        yield return P.Entero(Id("sinuso23"), L("L_Unused23"), c => e(c).Unused23, (c, v) => e(c).Unused23 = (byte)v, 0, 255);
        yield return P.Entero(Id("tid"), L("L_TID16").TrimEnd(':'), c => e(c).TID16, (c, v) => e(c).TID16 = (ushort)v, 0, ushort.MaxValue);
        yield return P.Entero(Id("desconocido26"), L("L_Unknown26"), c => e(c).Unknown26, (c, v) => e(c).Unknown26 = (byte)v, 0, 255);
        yield return P.Entero(Id("desconocido27"), L("L_Unknown27"), c => e(c).Unknown27, (c, v) => e(c).Unknown27 = (byte)v, 0, 255);
        yield return P.Entero(Id("horas"), L("L_PlayedHours"), c => e(c).PlayedHours, (c, v) => e(c).PlayedHours = (ushort)v, 0, 1023);
        yield return P.Entero(Id("minutos"), L("L_PlayedMinutes"), c => e(c).PlayedMinutes, (c, v) => e(c).PlayedMinutes = (byte)v, 0, 63);
        yield return P.Entero(Id("imagen"), L("L_Sprite"), c => e(c).Sprite, (c, v) => e(c).Sprite = (ushort)v, 0, ushort.MaxValue);
        yield return P.Texto(Id("saludo"), L("L_Greeting"), c => e(c).Greeting, (c, v) => e(c).Greeting = v, 8, grupo: grupoIdioma);
        yield return P.Texto(Id("despedida"), L("L_Farewell"), c => e(c).Farewell, (c, v) => e(c).Farewell = v, 8, grupo: grupoIdioma);
        yield return P.Entero(Id("encuentro-ano"), L("L_MetYear"), c => e(c).MetYear, (c, v) => e(c).MetYear = (byte)v, 0, 255);
        yield return P.Entero(Id("encuentro-mes"), L("L_MetMonth"), c => e(c).MetMonth, (c, v) => e(c).MetMonth = (byte)v, 0, 255);
        yield return P.Entero(Id("encuentro-dia"), L("L_MetDay"), c => e(c).MetDay, (c, v) => e(c).MetDay = (byte)v, 0, 255);
        yield return P.Entero(Id("semilla"), L("L_Seed"), c => e(c).Seed, (c, v) => e(c).Seed = (uint)v, 0, uint.MaxValue);
    }

    // ---------------------------------------------------------------- Específico del visitante / ocupante / propio (JoinAvenueVisitorSpecificEditor)
    // Nombres DESCRIPTIVOS, sin fuente oficial en el Core (JoinAvenueShopType5 solo trae Raffle/Salon/Market/Florist/Dojo/Nurse/Antique/Cafe y ningun .txt los traduce): «Nurse» es la tienda de cria/felicidad, no una clinica.
    private static string[] Tiendas => ["Rifa", IdiomaInterfaz.T("GaleriaUnion.SalonBelleza"), "Mercado", IdiomaInterfaz.T("GaleriaUnion.Floristeria"), IdiomaInterfaz.T("GaleriaUnion.Dojo"), IdiomaInterfaz.T("GaleriaUnion.GuarderiaFelicidad"), "Anticuario", IdiomaInterfaz.T("GaleriaUnion.Cafeteria")];

    private static IReadOnlyList<OpcionP> OpcionesDeTienda() => [new OpcionP(-1, IdiomaInterfaz.T("Catalogo.Ninguna")), .. Tiendas.Select((t, i) => new OpcionP(i, t))];

    private static string FormatoDeFecha(ushort raw)
    {
        if (raw == 0) return string.Empty;
        var fecha = new JoinAvenueDate5(raw);
        return fecha.Date is { } v ? v.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : $"0x{raw:X4}";
    }

    private static bool NumeroSinSigno(string t, out uint v)
    {
        t = t.Trim();
        return t.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? uint.TryParse(t[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v) : uint.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
    }

    /// <summary>Calco de <c>ParseDate</c> del formulario: vacío es 0; un número («0x»+hex o decimal) es el valor crudo; una fecha «aaaa-mm-dd» se codifica. Null si el texto no es nada de eso (el formulario lo convierte en 0 sin avisar; aquí se rechaza).</summary>
    private static ushort? LeerFecha(string texto)
    {
        texto = texto.Trim();
        if (texto.Length == 0) return 0;
        if (NumeroSinSigno(texto, out var raw)) return (ushort)Math.Min(raw, ushort.MaxValue);
        if (DateOnly.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)) { JoinAvenueDate5 v = default; v.Date = fecha; return v.RawValue; }
        return null;
    }

    private static CampoP Fecha(string id, string etiqueta, Func<ContextoP, ushort> leer, Action<ContextoP, ushort> escribir)
        => new()
        {
            Id = id, Etiqueta = etiqueta, Tipo = TipoP.Texto, MaxLongitud = 10, Ayuda = IdiomaInterfaz.T("GaleriaUnion.AaaaMmDdNumero"),
            Leer = c => FormatoDeFecha(leer(c)), Escribir = (c, v) => escribir(c, LeerFecha((string)v!) ?? 0),
            Validar = (_, t) => LeerFecha(t) is null ? IdiomaInterfaz.T("GaleriaUnion.CambiadoEscribeFechaAaaa") : null,
        };

    private static IEnumerable<CampoP> EspecificoDelVisitante(string pre, Func<ContextoP, JoinAvenueVisitor5> v)
    {
        string Id(string x) => pre + x;
        yield return P.Marca(Id("marca2c"), L("L_IsFlag2C"), c => v(c).IsFlag2C, (c, x) => v(c).IsFlag2C = x);
        yield return P.Entero(Id("nivel-galeria"), L("L_AvenueLevel").TrimEnd(':'), c => v(c).JoinAvenueLevel, (c, x) => v(c).JoinAvenueLevel = (byte)x, 0, 127);
        yield return P.Entero(Id("sinuso2d"), L("L_Unused2D"), c => v(c).Unused2D, (c, x) => v(c).Unused2D = (byte)x, 0, 255);
        string[] nombresTienda = ["sorteo", "salon", "mercado", "floristeria", "dojo", "clinica", "anticuario", "cafe"];
        for (var i = 0; i < 8; i++)
        {
            var n = i;
            yield return P.Entero(Id($"tiendas-{nombresTienda[n]}"), $"{L("L_ShopCounts").TrimEnd(':')} · {Tiendas[n]}", c => ConteoDeTienda(v(c), n), (c, x) => PonerConteoDeTienda(v(c), n, (byte)x), 0, 15);
        }
        yield return P.Entero(Id("dex"), L("L_DexSeen").TrimEnd(':'), c => v(c).DexSeen, (c, x) => v(c).DexSeen = (ushort)x, 0, 1023);
        yield return P.Opcion(Id("inicial"), L("L_FavoriteSpecies").TrimEnd(':'), c => v(c).FavoriteSpecies, (c, x) => v(c).FavoriteSpecies = (ushort)x, Especies, titulo: L("L_FavoriteSpecies").TrimEnd(':'));
        yield return P.Entero(Id("insignia-rango"), L("L_MedalRank").TrimEnd(':'), c => v(c).MedalRank, (c, x) => v(c).MedalRank = (byte)x, 0, 255);
        yield return P.Entero(Id("insignia-pista"), L("L_MedalHint").TrimEnd(':'), c => v(c).MedalHint, (c, x) => v(c).MedalHint = (byte)x, 0, 255);
        yield return P.Entero(Id("insignia-cantidad"), L("L_MedalCount").TrimEnd(':'), c => v(c).MedalCount, (c, x) => v(c).MedalCount = (byte)x, 0, 255);
        yield return Fecha(Id("fecha1"), L("L_Date1"), c => v(c).Date1.RawValue, (c, x) => v(c).Date1 = new JoinAvenueDate5(x));
        yield return Fecha(Id("fecha-inicio"), L("L_DateStart"), c => v(c).DateAdventureStart.RawValue, (c, x) => v(c).DateAdventureStart = new JoinAvenueDate5(x));
        yield return Fecha(Id("fecha-salon"), L("L_DateHall"), c => v(c).DateHallOfFame.RawValue, (c, x) => v(c).DateHallOfFame = new JoinAvenueDate5(x));
        string[] nombresRecord = ["intercambios", "motes", "clientes", "dinero", "viandantes", "combates", "capturas", "huevos"];
        for (var i = 0; i < (int)JoinAvenueRecordIndex5.COUNT_MAX; i++)
        {
            var n = i;
            yield return P.Entero(Id($"record-{nombresRecord[n]}"), $"{L("L_Records").TrimEnd(':')} · {n}", c => v(c).GetRecord((JoinAvenueRecordIndex5)n), (c, x) => v(c).SetRecord((JoinAvenueRecordIndex5)n, (uint)x), 0, uint.MaxValue);
        }
        for (var i = 0; i < JoinAvenueVisitor5.TriviaCount; i++)
        {
            var n = i;
            yield return P.Entero(Id($"curiosidad{n}"), $"{L("L_Trivia").TrimEnd(':')} · {n}", c => v(c).GetTrivia(n), (c, x) => v(c).SetTrivia(n, (byte)x), 0, 255);
        }
        for (var i = 0; i < JoinAvenueVisitor5.ActivityCount; i++)
        {
            var n = i;
            yield return P.Entero(Id($"actividad{n}"), $"{L("L_Activities").TrimEnd(':')} · {n}", c => v(c).GetActivity(n), (c, x) => v(c).SetActivity(n, (byte)x), 0, 255);
            yield return Fecha(Id($"actividad-fecha{n}"), $"{L("L_ActivityDates").TrimEnd(':')} · {n}", c => v(c).GetActivityDate(n).RawValue, (c, x) => v(c).SetActivityDate(n, new JoinAvenueDate5(x)));
        }
        yield return P.Opcion(Id("origen"), L("L_Origin").TrimEnd(':'), c => v(c).Origin, (c, x) => v(c).Origin = (ushort)x, _ => P.Lista("PNJ", IdiomaInterfaz.T("GaleriaUnion.JugadorHumano")), titulo: L("L_Origin").TrimEnd(':'));
        yield return P.Entero(Id("encuentro-hora"), L("L_MetHour"), c => v(c).MetHour, (c, x) => v(c).MetHour = (byte)x, 0, 255);
        yield return P.Entero(Id("encuentro-minuto"), L("L_MetMinute"), c => v(c).MetMinute, (c, x) => v(c).MetMinute = (byte)x, 0, 255);
        yield return P.Entero(Id("desconocidoa8"), L("L_UnknownA8"), c => v(c).UnknownA8, (c, x) => v(c).UnknownA8 = (byte)x, 0, 255);
        yield return P.Marca(Id("cambiar-tienda"), L("L_IsShopChangeAllowed").TrimEnd(':'), c => v(c).IsShopChangeAllowed, (c, x) => v(c).IsShopChangeAllowed = x);
        yield return P.Marca(Id("marca-a9-1"), L("L_IsFlagA9_1"), c => v(c).IsFlagA9_1, (c, x) => v(c).IsFlagA9_1 = x);
        yield return P.Marca(Id("marca-a9-2"), L("L_IsFlagA9_2"), c => v(c).IsFlagA9_2, (c, x) => v(c).IsFlagA9_2 = x);
        yield return P.Marca(Id("interactuo"), L("L_InteractedToday").TrimEnd(':'), c => v(c).IsInteractedToday, (c, x) => v(c).IsInteractedToday = x);
        yield return P.Marca(Id("marca-aa"), L("L_IsFlagAA"), c => v(c).IsFlagAA, (c, x) => v(c).IsFlagAA = x);
        yield return P.Entero(Id("rango-galeria"), L("L_JoinAvenueRank").TrimEnd(':'), c => v(c).JoinAvenueRank, (c, x) => v(c).JoinAvenueRank = (byte)x, 0, 255);
        yield return P.Entero(Id("desconocidoac"), L("L_UnknownAC"), c => v(c).UnknownAC, (c, x) => v(c).UnknownAC = (byte)x, 0, 255);
        yield return P.Entero(Id("tienda-nivel-a"), L("L_ShopRank").TrimEnd(':'), c => v(c).ShopRank, (c, x) => v(c).ShopRank = (byte)x, 0, 10);
        yield return P.Entero(Id("tienda-experiencia"), L("L_ShopExperience").TrimEnd(':'), c => v(c).ShopExperience, (c, x) => v(c).ShopExperience = (ushort)x, 0, ushort.MaxValue);
        yield return P.Entero(Id("inventario"), L("L_IsInventory").TrimEnd(':'), c => v(c).IsInventory, (c, x) => v(c).IsInventory = (uint)x, 0, uint.MaxValue);
        yield return P.Entero(Id("tienda-trabajo"), L("L_ShopWork").TrimEnd(':'), c => v(c).ShopWork, (c, x) => v(c).ShopWork = (ushort)x, 0, ushort.MaxValue);
        yield return P.Entero(Id("sinusob8"), L("L_UnusedB8"), c => v(c).UnusedB8, (c, x) => v(c).UnusedB8 = (uint)x, 0, uint.MaxValue);
        yield return P.Entero(Id("bits0-8"), L("L_UnknownBits0_8"), c => v(c).UnknownBits0_8, (c, x) => v(c).UnknownBits0_8 = (ushort)x, 0, 511);
        yield return P.Marca(Id("bit9"), L("L_UnknownBit9"), c => v(c).IsUnknownBits9, (c, x) => v(c).IsUnknownBits9 = x);
        yield return P.Entero(Id("bits10"), L("L_UnknownBits10"), c => v(c).UnknownBits10, (c, x) => v(c).UnknownBits10 = (byte)x, 0, 7);
        yield return P.Entero(Id("bits13-20"), L("L_UnknownBits13_20"), c => v(c).UnknownBits13_20, (c, x) => v(c).UnknownBits13_20 = (byte)x, 0, 255);
        yield return P.Entero(Id("bits21-27"), L("L_UnknownBits21_27"), c => v(c).UnknownBits21_27, (c, x) => v(c).UnknownBits21_27 = (byte)x, 0, 127);
        yield return P.Entero(Id("bits28-31"), L("L_UnknownBits28_31"), c => v(c).UnknownBits28_31, (c, x) => v(c).UnknownBits28_31 = (byte)x, 0, 15);
        foreach (var f in Tienda(pre + "deseada-", L("L_DesiredShopType").TrimEnd(':'), c => v(c).DesiredShopTypeTuple, (c, t) => v(c).DesiredShopTypeTuple = t)) yield return f;
        foreach (var f in Tienda(pre + "tienda-", L("L_ShopType").TrimEnd(':'), c => v(c).ShopTypeTuple, (c, t) => v(c).ShopTypeTuple = t)) yield return f;
    }

    private static byte ConteoDeTienda(JoinAvenueVisitor5 v, int i) => i switch
    {
        0 => v.ShopCountRaffle, 1 => v.ShopCountSalon, 2 => v.ShopCountMarket, 3 => v.ShopCountFlorist, 4 => v.ShopCountDojo, 5 => v.ShopCountNurse, 6 => v.ShopCountAntique, _ => v.ShopCountCafe,
    };

    private static void PonerConteoDeTienda(JoinAvenueVisitor5 v, int i, byte x)
    {
        switch (i)
        {
            case 0: v.ShopCountRaffle = x; break; case 1: v.ShopCountSalon = x; break; case 2: v.ShopCountMarket = x; break; case 3: v.ShopCountFlorist = x; break;
            case 4: v.ShopCountDojo = x; break; case 5: v.ShopCountNurse = x; break; case 6: v.ShopCountAntique = x; break; default: v.ShopCountCafe = x; break;
        }
    }

    /// <summary>La tienda (o la deseada): tipo, nivel y versión se guardan como UNA tupla (<c>SetShopTuple</c>); sin tipo, el valor es 0.</summary>
    private static IEnumerable<CampoP> Tienda(string pre, string nombre, Func<ContextoP, (byte Version, JoinAvenueShopType5 Type, byte Rank)?> leer, Action<ContextoP, (byte Version, JoinAvenueShopType5 Type, byte Rank)?> escribir)
    {
        var idTipo = pre + "tipo"; var idNivel = pre + "nivel"; var idVersion = pre + "version"; var grupo = pre + "tupla";
        void Guardar(ContextoP c)
        {
            var t = (int)c.Entero(idTipo);
            escribir(c, t < 0 ? null : ((byte)c.Entero(idVersion), (JoinAvenueShopType5)t, (byte)c.Entero(idNivel)));
        }
        yield return P.Opcion(idTipo, nombre, c => leer(c) is { } x ? (int)x.Type : -1, (c, _) => Guardar(c), _ => OpcionesDeTienda(), titulo: nombre, grupo: grupo);
        yield return P.Entero(idNivel, IdiomaInterfaz.F("GaleriaUnion.Nivel", nombre), c => leer(c) is { } x ? x.Rank : 0, (c, _) => Guardar(c), 0, 9, grupo: grupo);
        yield return P.Entero(idVersion, $"{nombre} · {L("L_Version").TrimEnd(':').ToLowerInvariant()}", c => leer(c) is { } x ? x.Version : 0, (c, _) => Guardar(c), 0, 3, grupo: grupo);
    }

    // ---------------------------------------------------------------- Específico del fan y del asistente
    private static IEnumerable<CampoP> EspecificoDelFan(string pre, Func<ContextoP, JoinAvenueFan5> f)
    {
        string Id(string x) => pre + x;
        yield return P.Entero(Id("desconocido4c"), L("L_Unknown4C"), c => f(c).Unknown4C, (c, v) => f(c).Unknown4C = (byte)v, 0, 255);
        yield return P.Entero(Id("desconocido4d"), L("L_Unknown4D"), c => f(c).Unknown4D, (c, v) => f(c).Unknown4D = (byte)v, 0, 255);
        yield return P.Entero(Id("desconocido4e"), L("L_Unknown4E"), c => f(c).Unknown4F, (c, v) => f(c).Unknown4F = (byte)v, 0, 255);   // NUD_Unknown4E lee y escribe la propiedad Unknown4F (Data[0x4E])
        yield return P.Marca(Id("interactuo"), L("L_InteractedToday").TrimEnd(':'), c => f(c).IsInteractedToday, (c, v) => f(c).IsInteractedToday = v);
        yield return P.Opcion(Id("especie"), L("L_Species").TrimEnd(':'), c => f(c).Species, (c, v) => f(c).Species = (ushort)v, Especies, titulo: L("L_Species").TrimEnd(':'), especie: true);
        yield return P.Entero(Id("desconocido52"), L("L_Unknown52"), c => f(c).Unknown52, (c, v) => f(c).Unknown52 = (ushort)v, 0, ushort.MaxValue);
        yield return P.Entero(Id("desconocido54"), L("L_Unknown54"), c => f(c).Unknown54, (c, v) => f(c).Unknown54 = (byte)v, 0, 255);
        yield return P.Entero(Id("bocadillo"), L("L_BubbleTarget").TrimEnd(':'), c => f(c).BubbleTarget, (c, v) => f(c).BubbleTarget = (byte)v, 0, 255);
        yield return P.Entero(Id("desconocido56"), L("L_Unknown56"), c => f(c).Unknown56, (c, v) => f(c).Unknown56 = (byte)v, 0, 255);
        yield return P.Entero(Id("desconocido5a"), L("L_Unknown5A"), c => f(c).Unknown5A, (c, v) => f(c).Unknown5A = (ushort)v, 0, ushort.MaxValue);
    }

    private static IEnumerable<CampoP> EspecificoDelAsistente(string pre, Func<ContextoP, JoinAvenueAssistant5> a)
    {
        string Id(string x) => pre + x;
        yield return P.Entero(Id("posicion0"), L("L_Position0"), c => a(c).Position0, (c, v) => a(c).Position0 = (byte)v, 0, 255);
        yield return P.Entero(Id("posicion1"), L("L_Position1"), c => a(c).Position1, (c, v) => a(c).Position1 = (byte)v, 0, 255);
        yield return P.Entero(Id("posicion2"), L("L_Position2"), c => a(c).Position2, (c, v) => a(c).Position2 = (byte)v, 0, 255);
        yield return P.Entero(Id("posicion-sin-uso"), L("L_UnusedPosition"), c => a(c).PositionUnused, (c, v) => a(c).PositionUnused = (byte)v, 0, 255);
        yield return P.Marca(Id("interactuo"), L("L_InteractedToday").TrimEnd(':'), c => a(c).IsInteractedToday, (c, v) => a(c).IsInteractedToday = v);
    }

    // ---------------------------------------------------------------- Las colecciones de entidades y «Propio»
    private static string EtiquetaDeLaEntidad(ContextoP c, int i, IJoinAvenueEntity5 e)
    {
        var nombre = e.Name.Trim();
        if (string.IsNullOrWhiteSpace(nombre)) nombre = c.Textos.specieslist.Length > 0 && c.Textos.specieslist[0].Trim().Length > 0 ? c.Textos.specieslist[0] : "—";
        return $"{i + 1:00} - {nombre}";
    }

    /// <summary>El importar y exportar del <c>JoinAvenueListEditor</c>: el archivo puede ser de cualquiera de los tres tipos (por su tamaño) y <c>CopyFrom</c> copia lo común si el tipo no coincide.</summary>
    private static CampoP Archivo(string id, Func<ContextoP, IJoinAvenueEntity5> entidad, int indice)
        => P.Archivo(id, IdiomaInterfaz.T("GaleriaUnion.ArchivoEntidad"),
            (c, b) =>
            {
                var data = (byte[])b.Clone();
                IJoinAvenueEntity5 importada = data.Length switch
                {
                    JoinAvenueVisitor5.SIZE => new JoinAvenueVisitor5(data),
                    JoinAvenueFan5.SIZE => new JoinAvenueFan5(data),
                    _ => new JoinAvenueAssistant5(data),
                };
                entidad(c).CopyFrom(importada);
            },
            validar: (_, b) => b.Length is JoinAvenueVisitor5.SIZE or JoinAvenueFan5.SIZE or JoinAvenueAssistant5.SIZE ? null : IdiomaInterfaz.F("GaleriaUnion.PuedeImportarArchivoTiene", b.Length, JoinAvenueVisitor5.SIZE, JoinAvenueFan5.SIZE, JoinAvenueAssistant5.SIZE),
            exporta: c => { var e = entidad(c); return (AyudasGen5.NombreLimpio($"{indice + 1:00}_{e.Name}.{e.FileExtension}"), e.Write().ToArray()); },
            extension: "jav5", textoBoton: L("B_Import"), prioridad: -5,
            confirmacion: IdiomaInterfaz.T("GaleriaUnion.SustituyenDatosEntidadArchivo"),
            ayuda: IdiomaInterfaz.T("GaleriaUnion.ArchivoJav5VisitanteJah5"));

    private static ColeccionDefP Visitantes() => EntidadesTipadas("visitantes", IdiomaInterfaz.T("GaleriaUnionEntidades.Visitante"), JoinAvenue5.VisitorCount, (c, i) => Avenida(c).GetVisitor(i), (pre, v) => EspecificoDelVisitante(pre, v));
    private static ColeccionDefP Ocupantes() => EntidadesTipadas("ocupantes", "Ocupante", JoinAvenue5.OccupantCount, (c, i) => Avenida(c).GetOccupant(i), (pre, v) => EspecificoDelVisitante(pre, v));
    private static ColeccionDefP Fans() => EntidadesTipadas("fans", "Fan", JoinAvenue5.FanCount, (c, i) => Avenida(c).GetFan(i), (pre, f) => EspecificoDelFan(pre, f));
    private static ColeccionDefP Asistentes() => EntidadesTipadas("asistentes", IdiomaInterfaz.T("GaleriaUnionEntidades.Asistente"), JoinAvenue5.AssistantCount, (c, i) => Avenida(c).GetAssistant(i), (pre, a) => EspecificoDelAsistente(pre, a));

    private static ColeccionDefP EntidadesTipadas<T>(string id, string elemento, int cantidad, Func<ContextoP, int, T> obtener, Func<string, Func<ContextoP, T>, IEnumerable<CampoP>> especifico) where T : class, IJoinAvenueEntity5
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = id, Elemento = elemento, Cantidad = _ => cantidad,
            Titulo = (c, i) => EtiquetaDeLaEntidad(c, i, obtener(c, i)),
            Secciones = (_, i) =>
            {
                var pre = $"{id}[{i}].";
                T E(ContextoP c) => obtener(c, i);
                return
                [
                    new SeccionP(string.Empty, [Archivo(pre + "archivo", E, i)]),
                    new SeccionP(L("Tab_General"), [.. General(pre, E)]),
                    new SeccionP(L("Tab_Specific"), [.. especifico(pre, E)]),
                ];
            },
        };
        return col;
    }

    private static IEnumerable<SeccionP> Propio()
    {
        JoinAvenueVisitor5 Yo(ContextoP c) => Avenida(c).Self;
        yield return new SeccionP(L("Tab_SelfGeneral"), [.. General("propio.", Yo)]);
        yield return new SeccionP(L("Tab_SelfSpecific"), [.. EspecificoDelVisitante("propio.", Yo)]);
    }
}
