// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen6y7;

/// <summary>
/// PK-SAV-117 a PK-SAV-119 · <c>SAV_SecretBase</c>, las bases secretas de Rubí Omega y Zafiro Alfa (solo ROZA): la propia («*») y las 30 de otros entrenadores, con sus datos, sus 28 objetos colocados y, en las de otros,
/// el equipo de 3 Pokémon. El original tiene una lista de bases a la izquierda, un cuadro de propiedades con los datos de la base (<c>PG_Base</c>, que escribe cada propiedad al cambiarla), el grupo «Objetos» (indice, tipo,
/// X, Y y rotacion), el panel «Entrenador Pokémon» (<c>f_PKM</c>) y los botones Importar/Exportar (<c>.sb6</c>), «X» (quitar), «Marcar decoraciones» y «Capturadas». En el movil son tres pestañas: «Inicio» (<c>f_MAIN</c>: una
/// coleccion de 31 bases con sus datos, importar/exportar y quitar), «Objetos» (las 28 posiciones de cada base) y «Entrenador Pokémon» (<c>f_PKM</c>: 30 bases x 3 Pokémon), mas «General» con «Capturadas» y «Marcar decoraciones».
/// Los datos de la base y los objetos se escriben tal cual. Del Pokémon, <c>SavePKM</c> escribe TODOS sus campos desde los controles al cambiar de participante o de base y al guardar, deduciendo el género (fijo si la especie
/// no tiene dos), la forma (la primera al cambiar de especie) y la habilidad (el mismo puesto de la lista de la especie y la forma): aqui las derivaciones son las mismas y especie, forma y habilidad forman un grupo de escritura.
/// «X» reordena la lista (las siguientes suben un puesto): accion inmediata. «Importar» sustituye la base elegida y el borrador de sus campos se vuelve a leer.
/// </summary>
internal static class BasesSecretas6
{
    public const string Form = "SAV_SecretBase";
    public static readonly string[] Censo = ["PK-SAV-117", "PK-SAV-118", "PK-SAV-119"];
    private const int Bases = 1 + SecretBase6Block.OtherSecretBaseCount, Equipo = SecretBase6Other.COUNT_TEAM, ListaGeneroSinGenero = 2;

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SecretBase6Block Bloque(SaveFile sav) => ((SAV6AO)sav).SecretBase;
    private static SecretBase6 Base(SaveFile sav, int i) => i == 0 ? Bloque(sav).GetSecretBaseSelf() : Bloque(sav).GetSecretBaseOther(i - 1);
    private static SecretBase6Other Otra(SaveFile sav, int i) => Bloque(sav).GetSecretBaseOther(i - 1);

    private static FilteredGameDataSource Filtro(ContextoP c) => FuentesGen6y7.Filtro(c);

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV6AO;

    private static string NombreBase(SaveFile sav, int i)
    {
        var nombre = Base(sav, i).TrainerName;
        return i == 0 ? $"* {nombre}" : $"{i:00} {(string.IsNullOrWhiteSpace(nombre) ? IdiomaInterfaz.T("Form.Fama3.Vacio") : nombre)}";
    }

    // ---------------- Inicio: los datos de la base (el cuadro de propiedades) ----------------
    /// <summary>Etiqueta de un dato de la base: PKHeX las enseña con el nombre INGLES de la propiedad (cuadro de propiedades), asi que son traduccion propia y viven en <see cref="RespaldoGen6y7.PropiedadesDeLaBase"/> (ADR 0064).</summary>
    private static string E(string id) => RespaldoGen6y7.PropiedadesDeLaBase[id];

    private static IEnumerable<CampoP> DatosDeLaBase(ColeccionDefP col, int i)
    {
        string Id(string campo) => col.IdCampo(i, campo);
        yield return P.Marca(Id("nueva"), E("nueva"), c => Base(c.Sav, i).IsNew, (c, v) => Base(c.Sav, i).IsNew = v);
        yield return P.Entero(Id("lugar"), E("lugar"), c => Base(c.Sav, i).BaseLocation, (c, v) => Base(c.Sav, i).BaseLocation = (int)v, SecretBase6.MinLocationID, SecretBase6.MaxLocationID,
            ayuda: IdiomaInterfaz.T("BasesSecretas6.CrearValoresCoreDeja"));
        yield return P.Entero(Id("boppoyama"), E("boppoyama"), c => Base(c.Sav, i).BoppoyamaScore, (c, v) => Base(c.Sav, i).BoppoyamaScore = (byte)v, 0, byte.MaxValue);
        yield return P.Texto(Id("entrenador"), E("entrenador"), c => Base(c.Sav, i).TrainerName, (c, v) => Base(c.Sav, i).TrainerName = v, 12);
        yield return P.Texto(Id("equipo"), E("equipo"), c => Base(c.Sav, i).TeamName, (c, v) => Base(c.Sav, i).TeamName = v, 16);
        yield return P.Texto(Id("lema"), E("lema"), c => Base(c.Sav, i).TeamSlogan, (c, v) => Base(c.Sav, i).TeamSlogan = v, 16);
        yield return P.Texto(Id("alegre"), E("alegre"), c => Base(c.Sav, i).SayHappy, (c, v) => Base(c.Sav, i).SayHappy = v, 16);
        yield return P.Texto(Id("animo"), E("animo"), c => Base(c.Sav, i).SayEncourage, (c, v) => Base(c.Sav, i).SayEncourage = v, 16);
        yield return P.Texto(Id("pizarra"), E("pizarra"), c => Base(c.Sav, i).SayBlackboard, (c, v) => Base(c.Sav, i).SayBlackboard = v, 16);
        yield return P.Texto(Id("confeti"), E("confeti"), c => Base(c.Sav, i).SayConfettiBall, (c, v) => Base(c.Sav, i).SayConfettiBall = v, 16);
        yield return P.Opcion(Id("rango"), E("rango"), c => (long)Base(c.Sav, i).Rank, (c, v) => Base(c.Sav, i).Rank = (SecretBase6Rank)v,
            _ => P.Lista(IdiomaInterfaz.T("BasesSecretas6.Rango"), "Bronce", IdiomaInterfaz.T("Form.Varios3.Plata"), IdiomaInterfaz.T("Form.Varios3.Oro"), "Platino"), titulo: E("rango"));
        yield return P.Entero(Id("banderas-amigos"), E("banderas-amigos"), c => Base(c.Sav, i).TotalFlagsFromFriends, (c, v) => Base(c.Sav, i).TotalFlagsFromFriends = (uint)v, 0, uint.MaxValue);
        yield return P.Entero(Id("banderas-otros"), E("banderas-otros"), c => Base(c.Sav, i).TotalFlagsFromOther, (c, v) => Base(c.Sav, i).TotalFlagsFromOther = (uint)v, 0, uint.MaxValue);
        yield return P.Entero(Id("banderas-hoy"), E("banderas-hoy"), c => Base(c.Sav, i).CollectedFlagsToday, (c, v) => Base(c.Sav, i).CollectedFlagsToday = (byte)v, 0, byte.MaxValue);
        yield return P.Entero(Id("banderas-ayer"), E("banderas-ayer"), c => Base(c.Sav, i).CollectedFlagsYesterday, (c, v) => Base(c.Sav, i).CollectedFlagsYesterday = (byte)v, 0, byte.MaxValue);
        if (i > 0)
        {
            yield return P.Entero(Id("idioma"), E("idioma"), c => Otra(c.Sav, i).Language, (c, v) => Otra(c.Sav, i).Language = (byte)v, 0, byte.MaxValue);
            yield return P.Entero(Id("genero"), E("genero"), c => Otra(c.Sav, i).Gender, (c, v) => Otra(c.Sav, i).Gender = (byte)v, 0, byte.MaxValue);
        }
        yield return P.Info(Id("vacia"), E("vacia"), c => Base(c.Sav, i).IsEmpty ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No"));
        yield return P.Info(Id("anulada"), E("anulada"), c => Base(c.Sav, i).IsDummiedBaseLocation ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No"));
    }

    private static IEnumerable<string> TodosLosIds(ColeccionDefP col, ColeccionDefP objetos, ColeccionDefP? equipo, int i)
    {
        foreach (var c in DatosDeLaBase(col, i)) yield return c.Id;
        for (var o = 0; o < SecretBase6.COUNT_GOODS; o++) foreach (var k in new[] { "tipo", "x", "y", "rot" }) yield return objetos.IdCampo(i, $"o{o}.{k}");
        if (i > 0 && equipo is not null)
            for (var j = 0; j < Equipo; j++) foreach (var id in CamposDelPokemon(equipo, ((i - 1) * Equipo) + j, i - 1, j).Select(x => x.Id)) yield return id;
    }

    // ---------------- Entrenador Pokémon (f_PKM) ----------------
    private static SecretBase6PKM Pk(SaveFile sav, int b, int j) => Otra(sav, b + 1).GetParticipant(j);
    private static void Cambiar(SaveFile sav, int b, int j, Action<SecretBase6PKM> f) { var o = Otra(sav, b + 1); var pk = o.GetParticipant(j); f(pk); o.SetParticipant(j, pk); }

    private static IPersonalAbility Entrada(ushort especie, byte forma) => PersonalTable.AO.GetFormEntry(especie, forma);
    private static bool TieneFormas(ushort especie) => FormInfo.HasFormSelection(PersonalTable.AO[especie], especie, 6);

    private static IEnumerable<CampoP> CamposDelPokemon(ColeccionDefP col, int e, int b, int j)
    {
        string Id(string campo) => col.IdCampo(e, campo);
        var idEspecie = Id("especie"); var idForma = Id("forma"); var idHab = Id("habilidad"); var idGenero = Id("genero");
        var grupoPk = $"pk{e}";   // un grupo por Pokémon: no mezcla los de otras bases
        ushort Especie(ContextoP c) => (ushort)c.Entero(idEspecie);
        byte Forma(ContextoP c) => (byte)Math.Max(0, c.Entero(idForma));

        // El valor de la opcion es el PUESTO (0 a 2), no el identificador de la habilidad: lo que escribe el formulario es el puesto y, de la lista, el identificador.
        IReadOnlyList<OpcionP> Habilidades(ContextoP c) => [.. Filtro(c).GetAbilityList(Entrada(Especie(c), Forma(c))).Select((x, k) => new OpcionP(k, x.Text))];
        // Como Label_Gender_Click de SAV_SecretBase: solo se alterna entre macho y hembra si la especie tiene los dos; si no, el genero es el fijo (Magnemite no puede ser hembra). Mismo patron que SalonFama6.
        IReadOnlyList<OpcionP> Generos(ContextoP c)
        {
            var pi = c.Sav.Personal[Especie(c)];
            return pi.IsDualGender ? [new(0, IdiomaInterfaz.T("Form.Trainer8.Male")), new(1, IdiomaInterfaz.T("Form.Trainer8.Female"))] : [new(pi.FixedGender(), pi.FixedGender() switch { 0 => IdiomaInterfaz.T("Form.Trainer8.Male"), 1 => IdiomaInterfaz.T("Form.Trainer8.Female"), _ => IdiomaInterfaz.T("GoPark.Genero") })];
        }
        IReadOnlyList<OpcionP> Formas(ContextoP c) => P.Lista(FormConverter.GetFormList(Especie(c), c.Textos.types, c.Textos.forms, GameInfo.GenderSymbolUnicode, c.Sav.Context));

        yield return P.Entero(Id("ec"), L("L_EncryptionConstant"), c => Pk(c.Sav, b, j).EncryptionConstant, (c, v) => Cambiar(c.Sav, b, j, p => p.EncryptionConstant = (uint)v), 0, uint.MaxValue, hex: true);
        // Cambiar la especie: la forma vuelve a la primera, el género es el fijo de la especie si no tiene dos y la habilidad conserva su puesto (UpdateSpecies / SetForms / SetAbilityList).
        yield return P.Opcion(idEspecie, IdiomaInterfaz.T("Cajas.Especie"), c => Pk(c.Sav, b, j).Species, (c, v) => Cambiar(c.Sav, b, j, p => p.Species = (ushort)v),
            c => P.Lista(Filtro(c).Species), titulo: IdiomaInterfaz.T("Cajas.Especie"), prioridad: 1, grupo: grupoPk,
            deriva: c =>
            {
                var d = new Dictionary<string, object?> { [idForma] = 0L };
                var pi = c.Sav.Personal[Especie(c)];
                if (!pi.IsDualGender) d[idGenero] = (long)pi.FixedGender();
                return d;
            }, especie: true);
        yield return P.Opcion(idForma, IdiomaInterfaz.T("Catalogo.Forma"), c => Pk(c.Sav, b, j).Form, (c, v) => Cambiar(c.Sav, b, j, p => p.Form = (byte)v), Formas, visible: c => TieneFormas(Especie(c)), titulo: IdiomaInterfaz.T("Catalogo.Forma"), prioridad: 2, grupo: grupoPk,
            deriva: c =>
            {
                // Una forma de un solo género fija el género (UpdateForm).
                var formas = FormConverter.GetFormList(Especie(c), c.Textos.types, c.Textos.forms, GameInfo.GenderSymbolUnicode, c.Sav.Context);
                var f = (int)c.Entero(idForma);
                return f >= 0 && f < formas.Length && EntityGender.GetFromString(formas[f]) < 2 ? new Dictionary<string, object?> { [idGenero] = (long)Math.Min(f, ListaGeneroSinGenero) } : new Dictionary<string, object?>();
            });
        yield return P.Opcion(idHab, IdiomaInterfaz.T("Informe.Columna.Ability"), c => { var n = Pk(c.Sav, b, j).AbilityNumber >> 1; return n < 3 ? n : 0; },
            (c, v) => Cambiar(c.Sav, b, j, p =>
            {
                // Como SavePKM: el identificador de la habilidad elegida y su puesto (puesto << 1).
                var lista = Filtro(c).GetAbilityList(Entrada(p.Species, p.Form));
                p.Ability = v < lista.Count ? lista[(int)v].Value : 0;
                p.AbilityNumber = (int)v << 1;
            }), Habilidades, titulo: IdiomaInterfaz.T("Informe.Columna.Ability"), prioridad: 3, grupo: grupoPk);
        yield return P.Opcion(Id("objeto"), IdiomaInterfaz.T("VariosGen4Frente.Objeto"), c => Pk(c.Sav, b, j).HeldItem, (c, v) => Cambiar(c.Sav, b, j, p => p.HeldItem = (int)v), c => P.Lista(Filtro(c).Items), titulo: IdiomaInterfaz.T("VariosGen4Frente.Objeto"));
        yield return P.Opcion(Id("naturaleza"), IdiomaInterfaz.T("Informe.Columna.Nature"), c => (long)Pk(c.Sav, b, j).Nature, (c, v) => Cambiar(c.Sav, b, j, p => p.Nature = (Nature)v), c => P.Lista(Filtro(c).Natures), titulo: IdiomaInterfaz.T("Informe.Columna.Nature"));
        yield return P.Opcion(idGenero, IdiomaInterfaz.T("Form.Genero"), c => Pk(c.Sav, b, j).Gender, (c, v) => Cambiar(c.Sav, b, j, p => p.Gender = (byte)v), Generos, titulo: IdiomaInterfaz.T("Form.Genero"));
        yield return P.Marca(Id("brillante"), IdiomaInterfaz.T("Catalogo.Variocolor"), c => Pk(c.Sav, b, j).IsShiny, (c, v) => Cambiar(c.Sav, b, j, p => p.IsShiny = v));
        yield return P.Opcion(Id("bola"), IdiomaInterfaz.T("BasesSecretas6.Bola"), c => Pk(c.Sav, b, j).Ball, (c, v) => Cambiar(c.Sav, b, j, p => p.Ball = (byte)v), c => P.Lista(Filtro(c).Balls), titulo: IdiomaInterfaz.T("BasesSecretas6.Bola"));
        yield return P.Entero(Id("nivel"), IdiomaInterfaz.T("Cajas.Nivel"), c => Pk(c.Sav, b, j).CurrentLevel, (c, v) => Cambiar(c.Sav, b, j, p => p.CurrentLevel = (byte)v), 0, byte.MaxValue);
        yield return P.Entero(Id("amistad"), L("L_PKFriendship").TrimEnd(':'), c => Pk(c.Sav, b, j).CurrentFriendship, (c, v) => Cambiar(c.Sav, b, j, p => p.CurrentFriendship = (int)v), 0, byte.MaxValue);

        // Esfuerzo (0 a 252: el formulario recorta con Clamp) e individuales (0 a 31: el formulario aplica & 0x1F).
        (string Clave, string Texto, Func<SecretBase6PKM, int> Ev, Action<SecretBase6PKM, int> SetEv, Func<SecretBase6PKM, int> Iv, Action<SecretBase6PKM, int> SetIv)[] stats =
        [
            ("hp", L("L_HP"), p => p.EV_HP, (p, v) => p.EV_HP = v, p => p.IV_HP, (p, v) => p.IV_HP = v),
            ("atq", L("L_ATK"), p => p.EV_ATK, (p, v) => p.EV_ATK = v, p => p.IV_ATK, (p, v) => p.IV_ATK = v),
            ("def", L("L_DEF"), p => p.EV_DEF, (p, v) => p.EV_DEF = v, p => p.IV_DEF, (p, v) => p.IV_DEF = v),
            ("ae", L("L_SpA"), p => p.EV_SPA, (p, v) => p.EV_SPA = v, p => p.IV_SPA, (p, v) => p.IV_SPA = v),
            ("de", L("L_SpD"), p => p.EV_SPD, (p, v) => p.EV_SPD = v, p => p.IV_SPD, (p, v) => p.IV_SPD = v),
            ("vel", L("L_SPE"), p => p.EV_SPE, (p, v) => p.EV_SPE = v, p => p.IV_SPE, (p, v) => p.IV_SPE = v),
        ];
        foreach (var s in stats)
        {
            var st = s;
            yield return P.Entero(Id($"ev-{st.Clave}"), $"{L("L_EVs")} · {st.Texto.TrimEnd('.')}", c => st.Ev(Pk(c.Sav, b, j)), (c, v) => Cambiar(c.Sav, b, j, p => st.SetEv(p, (int)v)), 0, EffortValues.Max252);
            yield return P.Entero(Id($"iv-{st.Clave}"), $"{L("L_IVs")} · {st.Texto.TrimEnd('.')}", c => st.Iv(Pk(c.Sav, b, j)), (c, v) => Cambiar(c.Sav, b, j, p => st.SetIv(p, (int)v)), 0, 31);
        }
        for (var m = 1; m <= 4; m++)
        {
            var k = m;
            Func<SecretBase6PKM, int> leer = k switch { 1 => p => p.Move1, 2 => p => p.Move2, 3 => p => p.Move3, _ => p => p.Move4 };
            Action<SecretBase6PKM, ushort> escribir = k switch { 1 => (p, v) => p.Move1 = v, 2 => (p, v) => p.Move2 = v, 3 => (p, v) => p.Move3 = v, _ => (p, v) => p.Move4 = v };
            Func<SecretBase6PKM, int> leerPp = k switch { 1 => p => p.Move1_PPUps, 2 => p => p.Move2_PPUps, 3 => p => p.Move3_PPUps, _ => p => p.Move4_PPUps };
            Action<SecretBase6PKM, int> escribirPp = k switch { 1 => (p, v) => p.Move1_PPUps = v, 2 => (p, v) => p.Move2_PPUps = v, 3 => (p, v) => p.Move3_PPUps = v, _ => (p, v) => p.Move4_PPUps = v };
            yield return P.Opcion(Id($"mov{k}"), IdiomaInterfaz.F("Catalogo.Movimiento", k), c => leer(Pk(c.Sav, b, j)), (c, v) => Cambiar(c.Sav, b, j, p => escribir(p, (ushort)v)), c => P.Lista(Filtro(c).Moves), titulo: IdiomaInterfaz.F("Catalogo.Movimiento", k));
            yield return P.Opcion(Id($"pp{k}"), $"{L("L_PPups")} {k}", c => leerPp(Pk(c.Sav, b, j)), (c, v) => Cambiar(c.Sav, b, j, p => escribirPp(p, (int)v)), _ => P.Lista("0", "1", "2", "3"), titulo: $"{L("L_PPups")} {k}");
        }
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV6AO) return null;
        ColeccionDefP? bases = null, objetos = null, equipo = null;
        bases = new ColeccionDefP
        {
            Id = "bases", Elemento = L("L_Favorite").TrimEnd(':'), Cantidad = _ => Bases, MostrarPosicion = false,
            Titulo = (c, i) => NombreBase(c.Sav, i),
            Resumen = (c, i) => Base(c.Sav, i).IsEmpty ? IdiomaInterfaz.T("Form.Fama3.Vacio") : IdiomaInterfaz.F("BasesSecretas6.Ubicacion", Base(c.Sav, i).BaseLocation),
            Secciones = (_, i) =>
            {
                var acciones = new List<CampoP>
                {
                    new()
                    {
                        Id = bases!.IdCampo(i, "importar"), Etiqueta = L("B_Import"), Tipo = TipoP.Archivo, Leer = _ => null, Prioridad = -1, TextoBoton = L("B_Import"), Extension = "sb6",
                        Ayuda = i == 0 ? IdiomaInterfaz.T("BasesSecretas6.SustituyeBasePropiaArchivo") : IdiomaInterfaz.T("BasesSecretas6.SustituyeBaseEquipoArchivo"),
                        Escribir = (c, v) => { if (v is byte[] d && SecretBase6.Read(d) is { } obj) Base(c.Sav, i).Load(obj); },
                        ValidarArchivo = (_, d) => d.Length is SecretBase6.SIZE or SecretBase6Other.SIZE ? null : IdiomaInterfaz.T("BasesSecretas6.ArchivoBaseSecretaDebe"),
                        Exporta = c => ($"{Base(c.Sav, i).BaseLocation:D2} - {(string.IsNullOrWhiteSpace(Base(c.Sav, i).TrainerName) ? IdiomaInterfaz.T("EntrenadorSwitch.Entrenador") : Base(c.Sav, i).TrainerName)}.sb6", Base(c.Sav, i).Data.ToArray()),
                        AfectaA = _ => TodosLosIds(bases!, objetos!, equipo, i),
                    },
                };
                if (i > 0)
                    acciones.Add(P.Accion(bases!.IdCampo(i, "quitar"), L("B_FDelete"), c => Bloque(c.Sav).DeleteOther(i - 1), inmediata: true, textoBoton: IdiomaInterfaz.T("PasesBr4.Quitar"),
                        confirmacion: IdiomaInterfaz.T("BasesSecretas6.QuitaBaseSiguientesSuben"), ayuda: IdiomaInterfaz.T("BasesSecretas6.QuitaBaseSiguientesSuben2")));
                else
                    acciones.Add(P.Accion(bases!.IdCampo(i, "quitar"), L("B_FDelete"), _ => { }, motivo: _ => IdiomaInterfaz.T("BasesSecretas6.BasePropiaPuedeQuitar"), inmediata: true, textoBoton: IdiomaInterfaz.T("PasesBr4.Quitar")));
                return [new SeccionP(IdiomaInterfaz.T("Form.Bases3.Base"), [.. DatosDeLaBase(bases!, i)]), new SeccionP(string.Empty, acciones)];
            },
        };
        objetos = new ColeccionDefP
        {
            Id = "objetos", Elemento = L("L_Favorite").TrimEnd(':'), Cantidad = _ => Bases, MostrarPosicion = false,
            Titulo = (c, i) => NombreBase(c.Sav, i),
            Secciones = (_, i) =>
            [
                .. Enumerable.Range(0, SecretBase6.COUNT_GOODS).Select(o =>
                {
                    SecretBase6GoodPlacement P0(ContextoP c) => Base(c.Sav, i).GetPlacement(o);
                    return new SeccionP($"{L("GB_Object")} · {o + 1}",
                    [
                        P.Entero(objetos!.IdCampo(i, $"o{o}.tipo"), L("L_Decoration").TrimEnd(':'), c => P0(c).Good, (c, v) => P0(c).Good = (ushort)v, 0, ushort.MaxValue),
                        P.Entero(objetos.IdCampo(i, $"o{o}.x"), L("L_X").TrimEnd(':'), c => P0(c).X, (c, v) => P0(c).X = (ushort)v, 0, ushort.MaxValue),
                        P.Entero(objetos.IdCampo(i, $"o{o}.y"), L("L_Y").TrimEnd(':'), c => P0(c).Y, (c, v) => P0(c).Y = (ushort)v, 0, ushort.MaxValue),
                        P.Entero(objetos.IdCampo(i, $"o{o}.rot"), L("L_Rotation").TrimEnd(':'), c => P0(c).Rotation, (c, v) => P0(c).Rotation = (byte)v, 0, byte.MaxValue),
                    ]);
                }),
            ],
        };
        equipo = new ColeccionDefP
        {
            Id = "equipo", Elemento = L("L_Participant").TrimEnd(':'), Cantidad = _ => (Bases - 1) * Equipo, MostrarPosicion = false,
            Titulo = (c, e) => $"{NombreBase(c.Sav, (e / Equipo) + 1)} · {L("L_Participant").TrimEnd(':')} {(e % Equipo) + 1}",
            Resumen = (c, e) => Pk(c.Sav, e / Equipo, e % Equipo) is { Species: > 0 } p ? $"{c.Textos.specieslist[Math.Min(p.Species, c.Textos.specieslist.Length - 1)]} · Nv. {p.CurrentLevel}" : IdiomaInterfaz.T("Form.Fama1.Vacio"),
            Secciones = (_, e) => [new SeccionP(string.Empty, [.. CamposDelPokemon(equipo!, e, e / Equipo, e % Equipo)])],
        };
        var general = new SeccionP(string.Empty,
        [
            P.Entero("capturadas", L("L_FlagsCaptured").TrimEnd(':'), c => ((SAV6AO)c.Sav).Records.GetRecord(80), (c, v) => ((SAV6AO)c.Sav).Records.SetRecord(80, (int)(uint)v), 0, uint.MaxValue, ayuda: IdiomaInterfaz.T("BasesSecretas6.EstadisticaEntrenador")),
            P.Accion("decoraciones", L("B_GiveDecor"), c => Bloque(c.Sav).GiveAllGoods(), textoBoton: IdiomaInterfaz.T("BasesSecretas6.Marcar"), confirmacion: IdiomaInterfaz.T("BasesSecretas6.MarcanObtenidasDecoracionesCada"),
                ayuda: IdiomaInterfaz.T("BasesSecretas6.DaCadaDecoracionesUsadas")),
        ]);
        return new EditorDefP("basessecretas", IdiomaInterfaz.T("BasesSecretas6.BasesSecretas"),
            [new PestanaP("general", IdiomaInterfaz.T("Cajas.General"), [general]), new PestanaP("inicio", L("f_MAIN"), [], Coleccion: bases), new PestanaP("objetos", L("GB_Object"), [], Coleccion: objetos), new PestanaP("equipo", L("f_PKM"), [], Coleccion: equipo)],
            Censo, IdiomaInterfaz.T("Partida.Especifico.basessecretas.Descripcion"));
    }
}
