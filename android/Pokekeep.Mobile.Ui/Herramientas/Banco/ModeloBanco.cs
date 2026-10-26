// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Herramientas.Equipo;
using Pokekeep.Mobile.Ui.Modelo;

namespace Pokekeep.Mobile.Ui.Herramientas.Banco;

/// <summary>Lo último que se supo de la legalidad de un Pokémon del banco (el análisis del Core al guardarlo). «Sin analizar» no es un veredicto: no se filtra como legal ni como ilegal.</summary>
public enum EstadoLegalBanco { SinAnalizar = 0, Legal = 1, Ilegal = 2 }

/// <summary>
/// Una fila del índice del banco (X38): lo necesario para listar, filtrar y buscar sin abrir los archivos. El Pokémon vive en su propio archivo estándar (<see cref="Archivo"/>, <c>.pk*</c> sin cifrar) y el índice
/// se puede reconstruir desde los archivos: lo único que NO sale de ellos (favorito, etiquetas, papelera, fecha) se conserva en la copia <c>.bak</c> del índice.
/// </summary>
/// <param name="Id">Identificador estable (16 hexadecimales); va también en el nombre del archivo, entre corchetes.</param>
/// <param name="Archivo">Nombre del archivo en el depósito.</param>
/// <param name="Extension">Extensión del formato (<c>pk9</c>, <c>pk4</c>...); de ella sale cómo se lee, no del tamaño.</param>
/// <param name="Contexto">Contexto del Core (<see cref="EntityContext"/>).</param>
/// <param name="Generacion">Generación de ORIGEN del Pokémon (donde se capturó o creó).</param>
/// <param name="Juego">Versión de origen (<see cref="GameVersion"/>); 0 si el formato no la guarda (Gen. 1 y 2).</param>
/// <param name="Legalidad">Veredicto del Core al guardarlo.</param>
/// <param name="Huella">SHA-256 de lo guardado del Pokémon (parte de caja descifrada): dos iguales son el mismo Pokémon, copiado dos veces.</param>
/// <param name="Individuo">SHA-256 de lo que identifica a UN Pokémon y no cambia al subirlo de nivel, evolucionarlo o moverlo de formato.</param>
public sealed record EntradaBanco(
    string Id,
    string Archivo,
    string Extension,
    int Contexto,
    int Especie,
    int Forma,
    bool Variocolor,
    bool Huevo,
    int Nivel,
    string Apodo,
    int Generacion,
    int Juego,
    int Tipo1,
    int Tipo2,
    EstadoLegalBanco Legalidad,
    string Huella,
    string Individuo,
    int Bytes,
    DateTime Guardado,
    bool Favorito = false,
    IReadOnlyList<string>? Etiquetas = null,
    bool EnPapelera = false,
    DateTime? Eliminado = null)
{
    public IReadOnlyList<string> EtiquetasOVacio => Etiquetas ?? [];

    /// <summary>El nombre del Pokémon: su mote si lo tiene; si no, la especie (con su forma si es una forma con nombre).</summary>
    public string Titulo(GameStrings t)
        => Apodo.Length > 0 ? Apodo : NombreEspecie(t);

    public string NombreEspecie(GameStrings t) => Especie > 0 && Especie < t.Species.Count ? t.Species[Especie] : "#" + Especie;

    /// <summary>La forma con nombre («Alola», «Hembra»...) o vacío si es la forma normal o el Core no la nombra.</summary>
    public string NombreForma(GameStrings t)
    {
        if (Forma == 0) return string.Empty;
        try
        {
            var formas = FormConverter.GetFormList((ushort)Especie, t.Types, t.forms, GameInfo.GenderSymbolUnicode, (EntityContext)Contexto);
            return Forma < formas.Length ? formas[Forma] : string.Empty;
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException) { return string.Empty; }
    }

    public string NombreJuego => Juego > 0 && ((GameVersion)Juego).IsValidSavedVersion() ? NombresJuego.Oficial((GameVersion)Juego) : string.Empty;
}

/// <summary>El índice completo tal y como se guarda (<c>indice.json</c>).</summary>
public sealed record IndiceBanco(int Version, IReadOnlyList<EntradaBanco> Entradas)
{
    public const int VersionActual = 1;
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(IndiceBanco))]
[JsonSerializable(typeof(UbicacionGuardada))]
internal sealed partial class BancoJson : JsonSerializerContext;

/// <summary>La carpeta del usuario elegida para el banco (solo URI y nombre), en la carpeta privada de la app.</summary>
public sealed record UbicacionGuardada(string Uri, string Nombre);

/// <summary>Cómo se calcula lo que el banco sabe de un Pokémon al guardarlo: datos del índice, huellas y legalidad. Todo sale del Core; nada se inventa.</summary>
public static class FichaBanco
{
    /// <summary>Huella del Pokémon guardado: SHA-256 de su parte de caja descifrada (<c>DecryptedBoxData</c>). La parte de equipo (nivel actual, PS) no cuenta: el mismo Pokémon sacado de la caja o del equipo tiene la misma huella.</summary>
    public static string Huella(PKM pk)
    {
        var guardado = new byte[pk.SIZE_STORED];
        pk.WriteDecryptedDataStored(guardado);
        // El formato entra en la huella: los mismos bytes en dos formatos (un PK4 y un PK5 en blanco) no son el mismo Pokémon.
        return Convert.ToHexString(SHA256.HashData([.. Encoding.ASCII.GetBytes(pk.Extension + ":"), .. guardado]));
    }

    /// <summary>
    /// Lo que identifica a UN Pokémon aunque cambie: personalidad (PID y constante de cifrado), entrenador original, juego de origen y los IV; en Gen. 1 y 2, que no tienen PID, los IV y el entrenador. No incluye especie
    /// ni nivel (evolucionar o subir de nivel no lo convierte en otro) ni movimientos.
    /// </summary>
    public static string Individuo(PKM pk)
    {
        var s = pk.Format < 3
            ? $"gb|{pk.TID16}|{pk.OriginalTrainerName}|{pk.GetIVs()}"
            : $"pid|{pk.EncryptionConstant}|{pk.PID}|{pk.TID16}|{pk.SID16}|{pk.OriginalTrainerName}|{(int)pk.Version}|{pk.GetIVs()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    }

    /// <summary>El tipo de la especie normalizado a la numeración de <see cref="MoveType"/> (también en Gen. 1 y 2); -1 si no hay segundo tipo.</summary>
    public static (int Tipo1, int Tipo2) Tipos(PKM pk)
    {
        if (pk.PersonalInfo is not IPersonalType pt) return (-1, -1);
        int T(byte crudo) => (int)((MoveType)crudo).GetMoveTypeGeneration((byte)pk.Format);
        var a = T(pt.Type1); var b = T(pt.Type2);
        return (a, b == a ? -1 : b);
    }

    public static EstadoLegalBanco Legalidad(PKM pk) => AnalizadorLegalidad.Evaluar(pk, StorageSlotType.Box) switch
    {
        EstadoLegal.Legal => EstadoLegalBanco.Legal,
        EstadoLegal.Ilegal or EstadoLegal.NecesitaHome => EstadoLegalBanco.Ilegal,
        _ => EstadoLegalBanco.SinAnalizar,
    };

    /// <summary>Nombre del archivo en el depósito: el de PKHeX de escritorio (<c>FileNameWithoutExtension</c>, sin caracteres no válidos) más la marca del banco <c>[pkb-id]</c> y la extensión.</summary>
    public static string NombreArchivo(PKM pk, string id)
    {
        // Sin caracteres de control ni sin asignar (un Pokémon sin mote en blanco trae basura U+FFFF en el nombre): un proveedor de SAF puede rechazarlos o escaparlos.
        var limpio = new string([.. pk.FileNameWithoutExtension.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) is not (UnicodeCategory.Control or UnicodeCategory.OtherNotAssigned or UnicodeCategory.Format or UnicodeCategory.PrivateUse))]);
        var baseNombre = PathUtil.CleanFileName(limpio).Trim();
        if (baseNombre.Length == 0) baseNombre = "Pokemon";
        if (baseNombre.Length > 80) baseNombre = baseNombre[..80].TrimEnd();
        return $"{baseNombre} [pkb-{id}].{pk.Extension}";
    }

    /// <summary>Los bytes del archivo: exactamente lo que escribe «Exportar archivo» y PKHeX de escritorio (<c>.pk*</c> sin cifrar, con la parte de equipo).</summary>
    public static byte[] Bytes(PKM pk) => Pokekeep.Mobile.Ui.Cajas.Intercambio.Bytes(pk);

    /// <summary>El mote si lo tiene y se puede leer: sin caracteres de control ni sin asignar (un formato en blanco guarda basura U+FFFF que se pintaba como un título vacío).</summary>
    public static string Mote(PKM pk)
    {
        if (!pk.IsNicknamed) return string.Empty;
        var m = new string([.. pk.Nickname.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) is not (UnicodeCategory.Control or UnicodeCategory.OtherNotAssigned or UnicodeCategory.Format or UnicodeCategory.PrivateUse))]).Trim();
        return m.Any(char.IsLetterOrDigit) ? m : string.Empty;
    }

    /// <summary>La fila del índice de un Pokémon recién leído o guardado.</summary>
    public static EntradaBanco Entrada(PKM pk, string id, string archivo, byte[] bytes, DateTime guardado, EstadoLegalBanco? legalidad = null)
    {
        var (t1, t2) = Tipos(pk);
        return new EntradaBanco(id, archivo, pk.Extension, (int)pk.Context, pk.Species, pk.Form, pk.IsShiny, pk.IsEgg, pk.CurrentLevel, Mote(pk),
            pk.Generation, (int)pk.Version, t1, t2, legalidad ?? Legalidad(pk), Huella(pk), Individuo(pk), bytes.Length, guardado);
    }

    /// <summary>
    /// Lee un Pokémon de un archivo del banco: primero por la extensión (el Core confunde PK5 con BK4 y PA9 con PK9 al adivinar por el tamaño); si no es de un formato descifrado conocido (un <c>.ek*</c> cifrado, por
    /// ejemplo), por el reconocimiento de archivos del Core. Null si los datos no valen.
    /// </summary>
    public static PKM? Leer(byte[] bytes, string extension)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        var pk = PlantillaEquipo.Leer(new MiembroPlantilla(ext, 0, Convert.ToBase64String(bytes)));
        if (pk is not null) return pk;
        try { return FileUtil.TryGetPKM(bytes, out var p, "." + ext) && p is not null && p.Species != 0 ? p : null; }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or InvalidOperationException) { return null; }
    }
}

public enum Tri3 { DaIgual, Si, No }

public enum OrdenBanco { Reciente, Pokedex, Nombre, Nivel }

/// <summary>Lo que se pide al listar el banco: búsqueda libre y filtros por especie, tipo, juego de origen, variocolor y legalidad (M2). Todo vacío = todo el banco.</summary>
public sealed record FiltroBanco(
    string Texto = "",
    int Especie = 0,
    int Tipo = -1,
    int Juego = 0,
    Tri3 Variocolor = Tri3.DaIgual,
    Tri3 Legal = Tri3.DaIgual,
    bool SoloFavoritos = false,
    string Etiqueta = "",
    OrdenBanco Orden = OrdenBanco.Reciente)
{
    public bool HayFiltros => Texto.Length > 0 || Especie != 0 || Tipo >= 0 || Juego != 0 || Variocolor != Tri3.DaIgual || Legal != Tri3.DaIgual || SoloFavoritos || Etiqueta.Length > 0;

    /// <summary>Los que cumplen el filtro (sin los de la papelera; ésos se listan aparte), en el orden pedido.</summary>
    public IReadOnlyList<EntradaBanco> Aplicar(IEnumerable<EntradaBanco> entradas, GameStrings t)
    {
        var texto = Texto.Trim();
        var q = entradas.Where(e => !e.EnPapelera);
        if (Especie != 0) q = q.Where(e => e.Especie == Especie);
        if (Tipo >= 0) q = q.Where(e => e.Tipo1 == Tipo || e.Tipo2 == Tipo);
        if (Juego != 0) q = q.Where(e => e.Juego == Juego);
        if (Variocolor != Tri3.DaIgual) q = q.Where(e => e.Variocolor == (Variocolor == Tri3.Si));
        // «Legal» e «ilegal» no incluyen lo que aún no se ha analizado: no se asegura nada que no se sepa.
        if (Legal == Tri3.Si) q = q.Where(e => e.Legalidad == EstadoLegalBanco.Legal);
        else if (Legal == Tri3.No) q = q.Where(e => e.Legalidad == EstadoLegalBanco.Ilegal);
        if (SoloFavoritos) q = q.Where(e => e.Favorito);
        if (Etiqueta.Length > 0) q = q.Where(e => e.EtiquetasOVacio.Contains(Etiqueta, StringComparer.CurrentCultureIgnoreCase));
        if (texto.Length > 0)
            q = q.Where(e => Coincide(e.Titulo(t), texto) || Coincide(e.NombreEspecie(t), texto) || Coincide(e.NombreForma(t), texto) || e.EtiquetasOVacio.Any(x => Coincide(x, texto)) || Coincide(e.NombreJuego, texto));
        return Orden switch
        {
            OrdenBanco.Pokedex => [.. q.OrderBy(e => e.Especie).ThenBy(e => e.Forma).ThenByDescending(e => e.Guardado)],
            OrdenBanco.Nombre => [.. q.OrderBy(e => e.Titulo(t), StringComparer.CurrentCultureIgnoreCase).ThenByDescending(e => e.Guardado)],
            OrdenBanco.Nivel => [.. q.OrderByDescending(e => e.Nivel).ThenBy(e => e.Especie)],
            _ => [.. q.OrderByDescending(e => e.Guardado).ThenBy(e => e.Especie)],
        };
    }

    private static bool Coincide(string texto, string buscado) => texto.Contains(buscado, StringComparison.CurrentCultureIgnoreCase);
}

/// <summary>Un grupo de Pokémon repetidos: los idénticos (misma huella) o varias versiones del mismo Pokémon (mismo individuo, distinta huella).</summary>
public sealed record GrupoDuplicados(bool Identicos, IReadOnlyList<EntradaBanco> Entradas);

public static class ModeloDuplicados
{
    /// <summary>
    /// Agrupa los de la papelera fuera. Primero los idénticos (misma huella); después, de lo que queda, las versiones del mismo Pokémon (mismo individuo con huellas distintas: se guardó antes y después de subir de nivel, por
    /// ejemplo). Un grupo siempre tiene al menos dos.
    /// </summary>
    public static IReadOnlyList<GrupoDuplicados> Buscar(IEnumerable<EntradaBanco> entradas)
    {
        var activas = entradas.Where(e => !e.EnPapelera).ToList();
        var grupos = new List<GrupoDuplicados>();
        foreach (var g in activas.GroupBy(e => e.Huella).Where(g => g.Count() > 1))
            grupos.Add(new GrupoDuplicados(true, [.. g.OrderBy(e => e.Guardado)]));
        // Un representante por huella: así «tres copias idénticas» no cuenta además como «versiones» de sí mismas.
        var representantes = activas.GroupBy(e => e.Huella).Select(g => g.OrderBy(e => e.Guardado).First());
        foreach (var g in representantes.GroupBy(e => e.Individuo).Where(g => g.Count() > 1))
            grupos.Add(new GrupoDuplicados(false, [.. g.OrderBy(e => e.Guardado)]));
        return grupos;
    }

    /// <summary>Qué copias sobran de un grupo de idénticos: todas menos la favorita o, si ninguna lo es, la más antigua.</summary>
    public static IReadOnlyList<EntradaBanco> Sobrantes(GrupoDuplicados grupo)
    {
        var conservar = grupo.Entradas.FirstOrDefault(e => e.Favorito) ?? grupo.Entradas.OrderBy(e => e.Guardado).First();
        return [.. grupo.Entradas.Where(e => e.Id != conservar.Id)];
    }
}

/// <summary>Una fila de la comparación: lo de uno y lo del otro, y si difieren.</summary>
public sealed record FilaComparacion(string Grupo, string Etiqueta, string A, string B, bool Difiere)
{
    public string Accesible => Difiere ? TextosBanco.ComparacionDistinta(Etiqueta, A, B) : TextosBanco.ComparacionIgual(Etiqueta, A);
}

public static class ModeloComparacion
{
    private static string Lista(IReadOnlyList<string> l, int i) => i >= 0 && i < l.Count ? l[i] : "#" + i;

    /// <summary>Compara dos Pokémon: datos generales, estadísticas, IV, EV, movimientos y origen. Los nombres son los del Core en el idioma de los datos; ninguna cifra se calcula a mano.</summary>
    public static IReadOnlyList<FilaComparacion> Comparar(PKM a, PKM b, GameStrings t)
    {
        var filas = new List<FilaComparacion>();
        void F(string grupo, string etiqueta, string x, string y) => filas.Add(new FilaComparacion(grupo, etiqueta, x, y, x != y));
        string Esp(PKM p) => p.Species == 0 ? "-" : Lista(t.Species, p.Species) + (p.Form > 0 ? " (" + NombreForma(p, t) + ")" : string.Empty);
        string Hab(PKM p) => p.Format < 3 ? TextosBanco.NoAplica : Lista(t.Ability, p.Ability);
        string Nat(PKM p) => p.Format < 3 ? TextosBanco.NoAplica : Lista(t.Natures, (int)p.Nature);
        string Obj(PKM p) => p.HeldItem == 0 ? TextosBanco.Ninguno : Lista(t.Item, p.HeldItem);

        var G = TextosBanco.GrupoGeneral;
        F(G, TextosBanco.Especie, Esp(a), Esp(b));
        F(G, TextosBanco.Mote, FichaBanco.Mote(a) is { Length: > 0 } ma ? ma : TextosBanco.Ninguno, FichaBanco.Mote(b) is { Length: > 0 } mb ? mb : TextosBanco.Ninguno);
        F(G, TextosBanco.Nivel, a.CurrentLevel.ToString(), b.CurrentLevel.ToString());
        F(G, TextosBanco.Naturaleza, Nat(a), Nat(b));
        F(G, TextosBanco.Habilidad, Hab(a), Hab(b));
        F(G, TextosBanco.Objeto, Obj(a), Obj(b));
        F(G, TextosBanco.Variocolor, a.IsShiny ? TextosBanco.Si : TextosBanco.No, b.IsShiny ? TextosBanco.Si : TextosBanco.No);

        var S = TextosBanco.GrupoEstadisticas;
        (string, int, int)[] stats =
        [
            (TextosBanco.PS, a.Stat_HPMax, b.Stat_HPMax), (TextosBanco.Ataque, a.Stat_ATK, b.Stat_ATK), (TextosBanco.Defensa, a.Stat_DEF, b.Stat_DEF),
            (TextosBanco.AtaqueEspecial, a.Stat_SPA, b.Stat_SPA), (TextosBanco.DefensaEspecial, a.Stat_SPD, b.Stat_SPD), (TextosBanco.Velocidad, a.Stat_SPE, b.Stat_SPE),
        ];
        foreach (var (n, x, y) in stats) F(S, n, x.ToString(), y.ToString());

        var I = TextosBanco.GrupoIv;
        (string, int, int)[] ivs =
        [
            (TextosBanco.PS, a.IV_HP, b.IV_HP), (TextosBanco.Ataque, a.IV_ATK, b.IV_ATK), (TextosBanco.Defensa, a.IV_DEF, b.IV_DEF),
            (TextosBanco.AtaqueEspecial, a.IV_SPA, b.IV_SPA), (TextosBanco.DefensaEspecial, a.IV_SPD, b.IV_SPD), (TextosBanco.Velocidad, a.IV_SPE, b.IV_SPE),
        ];
        foreach (var (n, x, y) in ivs) F(I, n, x.ToString(), y.ToString());

        var E = TextosBanco.GrupoEv;
        (string, int, int)[] evs =
        [
            (TextosBanco.PS, a.EV_HP, b.EV_HP), (TextosBanco.Ataque, a.EV_ATK, b.EV_ATK), (TextosBanco.Defensa, a.EV_DEF, b.EV_DEF),
            (TextosBanco.AtaqueEspecial, a.EV_SPA, b.EV_SPA), (TextosBanco.DefensaEspecial, a.EV_SPD, b.EV_SPD), (TextosBanco.Velocidad, a.EV_SPE, b.EV_SPE),
        ];
        foreach (var (n, x, y) in evs) F(E, n, x.ToString(), y.ToString());

        var M = TextosBanco.GrupoMovimientos;
        for (var i = 0; i < 4; i++)
        {
            string Mov(PKM p) => p.GetMove(i) == 0 ? TextosBanco.Ninguno : Lista(t.Move, p.GetMove(i));
            F(M, TextosBanco.MovimientoNumero(i + 1), Mov(a), Mov(b));
        }

        var O = TextosBanco.GrupoOrigen;
        string Juego(PKM p) => p.Version.IsValidSavedVersion() ? NombresJuego.Oficial(p.Version) : TextosBanco.NoConsta;
        F(O, TextosBanco.JuegoDeOrigen, Juego(a), Juego(b));
        F(O, TextosBanco.GeneracionDeOrigen, a.Generation.ToString(), b.Generation.ToString());
        F(O, TextosBanco.EntrenadorOriginal, a.OriginalTrainerName.Length > 0 ? a.OriginalTrainerName : TextosBanco.NoConsta, b.OriginalTrainerName.Length > 0 ? b.OriginalTrainerName : TextosBanco.NoConsta);
        F(O, TextosBanco.NivelDeEncuentro, a.MetLevel > 0 ? a.MetLevel.ToString() : TextosBanco.NoConsta, b.MetLevel > 0 ? b.MetLevel.ToString() : TextosBanco.NoConsta);
        F(O, TextosBanco.Formato, a.Extension.ToUpperInvariant(), b.Extension.ToUpperInvariant());
        return filas;
    }

    private static string NombreForma(PKM p, GameStrings t)
    {
        try
        {
            var formas = FormConverter.GetFormList(p.Species, t.Types, t.forms, GameInfo.GenderSymbolUnicode, p.Context);
            return p.Form < formas.Length && formas[p.Form].Length > 0 ? formas[p.Form] : TextosBanco.FormaNumero(p.Form);
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException) { return TextosBanco.FormaNumero(p.Form); }
    }
}

/// <summary>El progreso de una generación: especies de la Pokédex nacional de esa generación y cuántas hay en el banco, normales y variocolor.</summary>
public sealed record FilaProgresoGeneracion(int Generacion, int Total, int Normales, int Variocolor, IReadOnlyList<int> Faltan, IReadOnlyList<int> FaltanVariocolor)
{
    public double FraccionNormales => Total == 0 ? 0 : (double)Normales / Total;
    public double FraccionVariocolor => Total == 0 ? 0 : (double)Variocolor / Total;
}

/// <summary>El progreso del banco frente a la Pokédex nacional (M4): especies y formas, normales y variocolor, y el desglose por generación.</summary>
public sealed record ProgresoBanco(int TotalEspecies, int Especies, int EspeciesVariocolor, int TotalFormas, int Formas, int FormasVariocolor, IReadOnlyList<FilaProgresoGeneracion> Generaciones)
{
    public double FraccionEspecies => TotalEspecies == 0 ? 0 : (double)Especies / TotalEspecies;
    public double FraccionVariocolor => TotalEspecies == 0 ? 0 : (double)EspeciesVariocolor / TotalEspecies;
    public double FraccionFormas => TotalFormas == 0 ? 0 : (double)Formas / TotalFormas;
    public double FraccionFormasVariocolor => TotalFormas == 0 ? 0 : (double)FormasVariocolor / TotalFormas;
}

public static class ModeloProgreso
{
    /// <summary>Primera especie de cada generación en la Pokédex nacional (la 1 empieza en Bulbasaur; Meltan y Melmetal cuentan en la 7.ª, como los de Let's Go).</summary>
    private static readonly int[] InicioGeneracion = [1, 152, 252, 387, 494, 650, 722, 810, 906];

    private static readonly Lazy<(int MaxEspecie, IReadOnlySet<(int Especie, int Forma)> Formas)> Universo = new(Calcular);

    /// <summary>Especies de la Pokédex nacional que conoce el Core más reciente.</summary>
    public static int TotalEspecies => Universo.Value.MaxEspecie;
    /// <summary>Formas que existen fuera del combate (sin Megaevoluciones ni formas solo de batalla), contando cada especie con al menos su forma normal.</summary>
    public static int TotalFormas => Universo.Value.Formas.Count;

    private static (int, IReadOnlySet<(int, int)>) Calcular()
    {
        // Las formas se miran en las tablas de los juegos donde el Core las guarda; la mayor cuenta de formas de cada especie manda. Los índices de forma son los mismos entre juegos para la misma especie.
        var tablas = new IPersonalTable[] { PersonalTable.ZA, PersonalTable.SV, PersonalTable.SWSH, PersonalTable.USUM, PersonalTable.AO, PersonalTable.B2W2, PersonalTable.HGSS };
        var max = tablas.Max(t => (int)t.MaxSpeciesID);   // ZA solo trae las especies de su región; la nacional llega con la mayor de todas
        var formas = new HashSet<(int, int)>();
        for (var s = 1; s <= max; s++)
        {
            var n = 1;
            foreach (var tabla in tablas)
            {
                if (s > tabla.MaxSpeciesID) continue;
                var pi = tabla[(ushort)s];
                if (pi is IPersonalFormInfo fi) n = Math.Max(n, (int)fi.FormCount);
            }
            for (var f = 0; f < n; f++)
                if (f == 0 || !FormInfo.IsBattleOnlyForm((ushort)s, (byte)f, 9)) formas.Add((s, f));
        }
        return (max, formas);
    }

    public static int GeneracionDe(int especie)
    {
        for (var g = InicioGeneracion.Length - 1; g >= 0; g--) if (especie >= InicioGeneracion[g]) return g + 1;
        return 1;
    }

    /// <summary>Calcula el progreso con las entradas del banco (sin las de la papelera). Los huevos no cuentan: todavía no son esa especie.</summary>
    public static ProgresoBanco Calcular(IEnumerable<EntradaBanco> entradas)
    {
        var (max, formasUniverso) = Universo.Value;
        var activas = entradas.Where(e => !e.EnPapelera && !e.Huevo && e.Especie is > 0 && e.Especie <= max).ToList();
        var normales = activas.Select(e => e.Especie).ToHashSet();
        var brillos = activas.Where(e => e.Variocolor).Select(e => e.Especie).ToHashSet();
        var formas = activas.Select(e => (e.Especie, e.Forma)).Where(formasUniverso.Contains).ToHashSet();
        var formasBrillo = activas.Where(e => e.Variocolor).Select(e => (e.Especie, e.Forma)).Where(formasUniverso.Contains).ToHashSet();
        var filas = new List<FilaProgresoGeneracion>();
        for (var g = 0; g < InicioGeneracion.Length; g++)
        {
            var desde = InicioGeneracion[g]; var hasta = g + 1 < InicioGeneracion.Length ? InicioGeneracion[g + 1] - 1 : max;
            hasta = Math.Min(hasta, max);
            var todas = Enumerable.Range(desde, hasta - desde + 1).ToList();
            filas.Add(new FilaProgresoGeneracion(g + 1, todas.Count, todas.Count(normales.Contains), todas.Count(brillos.Contains),
                [.. todas.Where(s => !normales.Contains(s))], [.. todas.Where(s => !brillos.Contains(s))]));
        }
        return new ProgresoBanco(max, normales.Count, brillos.Count, formasUniverso.Count, formas.Count, formasBrillo.Count, filas);
    }
}
