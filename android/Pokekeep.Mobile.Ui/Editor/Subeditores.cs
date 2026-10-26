// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Editor;

/// <summary>Un boton de la parte alta de un subeditor (marcar todas, quitar todas, aplicar lo legal...).</summary>
public sealed record AccionSubeditor(string Id, string Texto, Action<ContextoEditor> Ejecutar, string? Ayuda = null);

/// <summary>
/// Un subeditor (cintas, memorias, superentrenamiento, registros tecnicos, tienda de movimientos, registro Plus, bytes del texto, caracteres especiales): una lista de campos como las pestanas del editor,
/// que escribe en el MISMO borrador (el borrador es el area de trabajo; «Descartar» del editor lo tira entero) mas botones propios. Al abrirse guarda una copia para ofrecer «Deshacer los cambios de este subeditor».
/// </summary>
public sealed class SubeditorDef
{
    public required string Id { get; init; }
    public required string Titulo { get; init; }
    public required IReadOnlyList<SeccionDef> Secciones { get; init; }
    public IReadOnlyList<AccionSubeditor> Acciones { get; init; } = [];
    /// <summary>Filas del censo de paridad que este subeditor cubre.</summary>
    public IReadOnlyList<string> Filas { get; init; } = [];
    /// <summary>Caracteres especiales del juego para insertar en un texto (subeditor de glifos); vacio en los demas.</summary>
    public IReadOnlyList<string> Glifos { get; init; } = [];
    public string? Aviso { get; init; }
}

public static class Subeditores
{
    public static readonly IReadOnlyList<string> Ids = ["cintas", "memorias", "superentrenamiento", "tecnicas", "tienda", "plus", "basura", "glifos"];

    /// <summary>Crea el subeditor para el Pokemon del contexto, o null si ese Pokemon no lo tiene (formato o interfaz que no corresponde). <paramref name="objetivo"/> es el texto sobre el que actuan «basura» y «glifos» (mote, eo, ea).</summary>
    public static SubeditorDef? Crear(string id, ContextoEditor c, string objetivo = "mote") => id switch
    {
        "cintas" when c.Formato >= 3 => Cintas(c),
        "memorias" when c.Formato >= 6 && c.Pk is not PB7 => Memorias(c),
        "superentrenamiento" when c.Pk is ISuperTrainRegimen && c.Formato is 6 or 7 && c.Pk is not PB7 => Super(c),
        "tecnicas" when c.Pk is ITechRecord => Tecnicas(c),
        "tienda" when c.Pk is IMoveShop8Mastery => Tienda(c),
        "plus" when c.Pk is IPlusRecord && c.Pk.PersonalInfo is IPermitPlus => Plus(c),
        "basura" => Basura(c, objetivo),
        "glifos" => Glifos(c, objetivo),
        _ => null,
    };

    private static string Tipo(ContextoEditor c, ushort move)
    {
        try { var t = MoveInfo.GetType(move, c.Pk.Context); return t < c.F.Textos.types.Length ? c.F.Textos.types[t] : string.Empty; } catch { return string.Empty; }
    }

    // ================= Cintas (RibbonEditor) =================
    private static SubeditorDef Cintas(ContextoEditor c)
    {
        var pk = c.Pk;
        var lista = RibbonInfo.GetRibbonInfo(pk);
        var resultados = new Dictionary<string, RibbonResult>();
        List<RibbonInfo> otras = [];
        try
        {
            lock (Pokekeep.Sprites.LegalityGate.Cerrojo)
            {
                var la = new LegalityAnalysis(pk);
                Span<RibbonResult> res = stackalloc RibbonResult[lista.Count];
                var args = new RibbonVerifierArguments(pk, la.EncounterOriginal, la.Info.EvoChainsAllGens);
                var n = RibbonVerifier.GetRibbonResults(args, res);
                foreach (var r in res[..n]) resultados[r.PropertyName] = r;
                var clon = pk.Clone();
                RibbonApplicator.SetAllValidRibbons(clon);
                otras = RibbonInfo.GetRibbonInfo(clon);
            }
        }
        catch { /* sin analisis: sin pistas */ }

        string Pista(RibbonInfo r)
        {
            if (r.Name.StartsWith("RibbonMark", StringComparison.Ordinal)) return IdiomaInterfaz.T("Subeditores.Marca");
            if (resultados.TryGetValue(r.Name, out var x)) return x.IsMissing ? IdiomaInterfaz.T("Subeditores.FaltaObligatoria") : IdiomaInterfaz.T("Subeditores.ValidaPokemon");
            var otra = otras.Find(z => z.Name == r.Name);
            return otra is { HasRibbon: true } || otra is { RibbonCount: > 0 } ? IdiomaInterfaz.T("Informe.Columna.Legal") : string.Empty;
        }

        int Orden(RibbonInfo r)
        {
            if (r.Name.StartsWith("RibbonMark", StringComparison.Ordinal)) return 99;
            var otra = otras.Find(z => z.Name == r.Name);
            if (otra is { HasRibbon: true }) return 0;
            if (resultados.TryGetValue(r.Name, out var x)) return x.IsMissing ? 1 : 2;
            return 3;
        }

        string Nombre(string n) => c.F.Textos.Ribbons.GetNameSafe(n, out var s) ? s : n;
        var campos = new List<CampoDef>();
        var idx = 0;
        foreach (var r in lista.OrderBy(Orden).ThenBy(z => Nombre(z.Name)))
        {
            var nombre = r.Name;
            // PKHeX 26.10.18: PK3.RibbonCountMemoryContest/Battle pasan a ser propiedades derivadas (solo lectura, suma de las cintas de Gen3); no hay nada que escribir.
            if (pk.GetType().GetProperty(nombre) is { CanWrite: false }) continue;
            var i = idx++;
            if (r.Type is RibbonValueType.Byte)
            {
                var max = r.MaxCount;
                if (max == 8 && nombre is nameof(IRibbonSetMemory6.RibbonCountMemoryBattle) && pk.Format >= 9) max = 7;
                var maxF = max;
                campos.Add(F.Entero("PK-PKME-009", Nombre(nombre), cc => Convert.ToInt64(ReflectUtil.GetValue(cc.Pk, nombre) ?? 0), (cc, v) => ReflectUtil.SetValue(cc.Pk, nombre, (byte)v), 0, maxF) with { Indice = i, Detalle = _ => Pista(r), Grupo = "cinta" });
            }
            else
            {
                campos.Add(F.Casilla("PK-PKME-009", Nombre(nombre), cc => ReflectUtil.GetValue(cc.Pk, nombre) is true, (cc, v) => { ReflectUtil.SetValue(cc.Pk, nombre, v); return null; }) with { Indice = i, Detalle = _ => Pista(r), Grupo = "cinta" });
            }
        }

        var secciones = new List<SeccionDef>();
        if (pk is IRibbonSetAffixed)
        {
            var total = AffixedRibbon.Max + 1;
            var opciones = new List<OpcionCampo> { new((int)AffixedRibbon.None, IdiomaInterfaz.T("Catalogo.Ninguna")) };
            opciones.AddRange(Enumerable.Range(0, total).Select(z => new OpcionCampo(z, Catalogo.NombreCinta(c.F, (RibbonIndex)z))).OrderBy(o => o.Texto, StringComparer.CurrentCultureIgnoreCase));
            secciones.Add(new(IdiomaInterfaz.T("Catalogo.CintaMarcaFijada"), [F.Opcion("PK-ED-073", IdiomaInterfaz.T("Catalogo.CintaMarcaFijada"), _ => opciones, cc => (int)((IRibbonSetAffixed)cc.Pk).AffixedRibbon, (cc, id) => { ((IRibbonSetAffixed)cc.Pk).AffixedRibbon = (sbyte)id; return null; },
                leer: cc => { var a = ((IRibbonSetAffixed)cc.Pk).AffixedRibbon; return a == AffixedRibbon.None ? IdiomaInterfaz.T("Catalogo.Ninguna") : Catalogo.NombreCinta(cc.F, (RibbonIndex)a); }, ayuda: IdiomaInterfaz.T("Subeditores.CintaMarcaMuestraJunto"))]));
        }
        secciones.Add(new(IdiomaInterfaz.T("Subeditores.CintasMarcas"), campos));
        return new SubeditorDef
        {
            Id = "cintas", Titulo = IdiomaInterfaz.T("Subeditores.CintasMarcas"), Secciones = secciones, Filas = ["PK-PKME-009", "PK-ED-073", "PK-ED-080"],
            Acciones =
            [
                new("todas", IdiomaInterfaz.T("Subeditores.MarcarTodas"), cc => { foreach (var r in RibbonInfo.GetRibbonInfo(cc.Pk)) ReflectUtil.SetValue(cc.Pk, r.Name, r.Type is RibbonValueType.Boolean ? true : (object)(byte)r.MaxCount); }, IdiomaInterfaz.T("Subeditores.MarcaTodasCintasTambien")),
                new("legales", IdiomaInterfaz.T("Subeditores.SoloLegales"), cc => { lock (Pokekeep.Sprites.LegalityGate.Cerrojo) { RibbonApplicator.RemoveAllValidRibbons(cc.Pk); RibbonApplicator.SetAllValidRibbons(cc.Pk); } }, IdiomaInterfaz.T("Subeditores.DejaExactamenteCintasCore")),
                new("quitar-legales", IdiomaInterfaz.T("Subeditores.QuitarLegales"), cc => { lock (Pokekeep.Sprites.LegalityGate.Cerrojo) RibbonApplicator.RemoveAllValidRibbons(cc.Pk); }, IdiomaInterfaz.T("Subeditores.QuitaCintasLegalesDeja")),
                new("ninguna", IdiomaInterfaz.T("Subeditores.QuitarTodas"), cc => { foreach (var r in RibbonInfo.GetRibbonInfo(cc.Pk)) ReflectUtil.SetValue(cc.Pk, r.Name, r.Type is RibbonValueType.Boolean ? false : (object)(byte)0); if (cc.Pk is IRibbonSetAffixed a) a.AffixedRibbon = AffixedRibbon.None; }, IdiomaInterfaz.T("Subeditores.QuitaTodasCintasFijada")),
            ],
        };
    }

    // ================= Memorias y Amie (MemoryAmie) =================
    private static SubeditorDef Memorias(ContextoEditor c)
    {
        var pk = c.Pk;
        var secciones = new List<SeccionDef>();
        var gen = (int)pk.Generation;
        if (gen == 0) gen = pk.Format;

        IReadOnlyList<OpcionCampo> Mem(ContextoEditor cc) => FuentesEditor.Convertir(cc.F.Memorias.Memory);
        IReadOnlyList<OpcionCampo> Args(ContextoEditor cc, byte memoria, int g) => FuentesEditor.Convertir(cc.F.Memorias.GetArgumentStrings(Memories.GetMemoryArgType(memoria, g), g));
        IReadOnlyList<OpcionCampo> Intens(ContextoEditor cc) => [new(0, cc.F.Textos.specieslist[0]), .. cc.F.Memorias.GetMemoryQualities()[1..].ToArray().Select((t, i) => new OpcionCampo(i + 1, t))];
        IReadOnlyList<OpcionCampo> Sent(ContextoEditor cc, int g) => [.. cc.F.Memorias.GetMemoryFeelings(g).ToArray().Select((t, i) => new OpcionCampo(i, t))];

        if (pk is ITrainerMemories m)
        {
            bool OtActivo(ContextoEditor cc) => !cc.Pk.IsEgg && cc.Pk.Generation >= 6;
            bool HtActivo(ContextoEditor cc) => !cc.Pk.IsEgg && cc.Pk.HandlingTrainerName.Length != 0;
            string? MotivoOt(ContextoEditor cc) => cc.Pk.IsEgg ? IdiomaInterfaz.T("Subeditores.HuevoTieneRecuerdos") : cc.Pk.Generation < 6 ? IdiomaInterfaz.T("Subeditores.PokemonGeneracionAnteriorEntrenador") : null;
            string? MotivoHt(ContextoEditor cc) => cc.Pk.IsEgg ? IdiomaInterfaz.T("Subeditores.HuevoTieneRecuerdos") : cc.Pk.HandlingTrainerName.Length == 0 ? IdiomaInterfaz.T("Subeditores.EntrenadorActualPokemonNunca") : null;
            var memGenOt = gen;
            var memGenHt = (int)pk.Format;

            secciones.Add(new(IdiomaInterfaz.T("Subeditores.RecuerdosEntrenadorOriginal"),
            [
                F.Opcion("PK-PKME-003", IdiomaInterfaz.T("Subeditores.Recuerdo"), Mem, cc => ((ITrainerMemories)cc.Pk).OriginalTrainerMemory, (cc, id) => { var t = (ITrainerMemories)cc.Pk; t.OriginalTrainerMemory = (byte)id; t.OriginalTrainerMemoryVariable = 0; return null; }, motivo: MotivoOt),
                F.Opcion("PK-PKME-003", IdiomaInterfaz.T("Subeditores.DetalleRecuerdo"), cc => Args(cc, ((ITrainerMemories)cc.Pk).OriginalTrainerMemory, memGenOt), cc => ((ITrainerMemories)cc.Pk).OriginalTrainerMemoryVariable, (cc, id) => { ((ITrainerMemories)cc.Pk).OriginalTrainerMemoryVariable = (ushort)id; return null; },
                    visible: cc => ((ITrainerMemories)cc.Pk).OriginalTrainerMemory != 0 && Args(cc, ((ITrainerMemories)cc.Pk).OriginalTrainerMemory, memGenOt).Count > 1, motivo: MotivoOt) with { Indice = 1 },
                F.Opcion("PK-PKME-003", IdiomaInterfaz.T("Subeditores.Intensidad"), Intens, cc => ((ITrainerMemories)cc.Pk).OriginalTrainerMemoryIntensity, (cc, id) => { ((ITrainerMemories)cc.Pk).OriginalTrainerMemoryIntensity = (byte)id; return null; },
                    visible: cc => ((ITrainerMemories)cc.Pk).OriginalTrainerMemory != 0, motivo: MotivoOt) with { Indice = 2 },
                F.Opcion("PK-PKME-003", IdiomaInterfaz.T("Subeditores.Sentimiento"), cc => Sent(cc, memGenOt), cc => ((ITrainerMemories)cc.Pk).OriginalTrainerMemoryFeeling, (cc, id) => { ((ITrainerMemories)cc.Pk).OriginalTrainerMemoryFeeling = (byte)id; return null; },
                    visible: cc => ((ITrainerMemories)cc.Pk).OriginalTrainerMemory != 0, motivo: MotivoOt) with { Indice = 3 },
                F.Info("PK-PKME-003", IdiomaInterfaz.T("Subeditores.AsiCuentaJuego"), cc => TextoRecuerdo(cc, true), visible: OtActivo) with { Indice = 4 },
            ]));
            secciones.Add(new(IdiomaInterfaz.T("Subeditores.RecuerdosEntrenadorActual"),
            [
                F.Opcion("PK-PKME-004", IdiomaInterfaz.T("Subeditores.Recuerdo"), Mem, cc => ((ITrainerMemories)cc.Pk).HandlingTrainerMemory, (cc, id) => { var t = (ITrainerMemories)cc.Pk; t.HandlingTrainerMemory = (byte)id; t.HandlingTrainerMemoryVariable = 0; return null; }, motivo: MotivoHt),
                F.Opcion("PK-PKME-004", IdiomaInterfaz.T("Subeditores.DetalleRecuerdo"), cc => Args(cc, ((ITrainerMemories)cc.Pk).HandlingTrainerMemory, memGenHt), cc => ((ITrainerMemories)cc.Pk).HandlingTrainerMemoryVariable, (cc, id) => { ((ITrainerMemories)cc.Pk).HandlingTrainerMemoryVariable = (ushort)id; return null; },
                    visible: cc => ((ITrainerMemories)cc.Pk).HandlingTrainerMemory != 0 && Args(cc, ((ITrainerMemories)cc.Pk).HandlingTrainerMemory, memGenHt).Count > 1, motivo: MotivoHt) with { Indice = 1 },
                F.Opcion("PK-PKME-004", IdiomaInterfaz.T("Subeditores.Intensidad"), Intens, cc => ((ITrainerMemories)cc.Pk).HandlingTrainerMemoryIntensity, (cc, id) => { ((ITrainerMemories)cc.Pk).HandlingTrainerMemoryIntensity = (byte)id; return null; },
                    visible: cc => ((ITrainerMemories)cc.Pk).HandlingTrainerMemory != 0, motivo: MotivoHt) with { Indice = 2 },
                F.Opcion("PK-PKME-004", IdiomaInterfaz.T("Subeditores.Sentimiento"), cc => Sent(cc, memGenHt), cc => ((ITrainerMemories)cc.Pk).HandlingTrainerMemoryFeeling, (cc, id) => { ((ITrainerMemories)cc.Pk).HandlingTrainerMemoryFeeling = (byte)id; return null; },
                    visible: cc => ((ITrainerMemories)cc.Pk).HandlingTrainerMemory != 0, motivo: MotivoHt) with { Indice = 3 },
                F.Info("PK-PKME-004", IdiomaInterfaz.T("Subeditores.AsiCuentaJuego"), cc => TextoRecuerdo(cc, false), visible: HtActivo) with { Indice = 4 },
            ]));
            _ = m;
        }

        if (pk is IGeoTrack)
        {
            var geo = new List<CampoDef>();
            for (var i = 0; i < 5; i++)
            {
                var k = i;
                geo.Add(F.Opcion("PK-PKME-005", IdiomaInterfaz.F("Subeditores.Pais", k + 1), cc => cc.F.Paises, cc => GeoPais(cc.Pk, k), (cc, id) => { PonerGeoPais(cc.Pk, k, (byte)id); return null; }, motivo: cc => cc.Pk.HandlingTrainerName.Length == 0 && k > 0 ? IdiomaInterfaz.T("Subeditores.EntrenadorActualSoloExiste") : null) with { Indice = k * 2 });
                geo.Add(F.Opcion("PK-PKME-005", IdiomaInterfaz.F("Subeditores.Region", k + 1), cc => cc.F.Subregiones(GeoPais(cc.Pk, k)), cc => GeoRegion(cc.Pk, k), (cc, id) => { PonerGeoRegion(cc.Pk, k, (byte)id); return null; },
                    motivo: cc => GeoPais(cc.Pk, k) == 0 ? IdiomaInterfaz.T("Catalogo.EligePrimeroPais") : null) with { Indice = k * 2 + 1 });
            }
            secciones.Add(new(IdiomaInterfaz.T("Subeditores.ResidenciaLugaresPaso"), geo));
        }

        var otros = new List<CampoDef>
        {
            F.Entero("PK-PKME-006", IdiomaInterfaz.T("Subeditores.AmistadEntrenadorOriginal"), cc => cc.Pk.OriginalTrainerFriendship, (cc, v) => cc.Pk.OriginalTrainerFriendship = (byte)v, 0, 255),
            F.Entero("PK-PKME-006", IdiomaInterfaz.T("Catalogo.AmistadEntrenadorActual"), cc => cc.Pk.HandlingTrainerFriendship, (cc, v) => cc.Pk.HandlingTrainerFriendship = (byte)v, 0, 255) with { Indice = 1 },
            F.Entero("PK-PKME-006", IdiomaInterfaz.T("Subeditores.AfectoEntrenadorOriginal"), cc => ((IAffection)cc.Pk).OriginalTrainerAffection, (cc, v) => ((IAffection)cc.Pk).OriginalTrainerAffection = (byte)v, 0, 255, visible: cc => cc.Pk is IAffection && cc.Formato <= 7) with { Indice = 2 },
            F.Entero("PK-PKME-006", IdiomaInterfaz.T("Subeditores.AfectoEntrenadorActual"), cc => ((IAffection)cc.Pk).HandlingTrainerAffection, (cc, v) => ((IAffection)cc.Pk).HandlingTrainerAffection = (byte)v, 0, 255, visible: cc => cc.Pk is IAffection && cc.Formato <= 7) with { Indice = 3 },
            F.Entero("PK-PKME-006", IdiomaInterfaz.T("Subeditores.Saciedad"), cc => ((IFullnessEnjoyment)cc.Pk).Fullness, (cc, v) => ((IFullnessEnjoyment)cc.Pk).Fullness = (byte)v, 0, 255, visible: cc => cc.Pk is IFullnessEnjoyment) with { Indice = 4 },
            F.Entero("PK-PKME-006", IdiomaInterfaz.T("Subeditores.Disfrute"), cc => ((IFullnessEnjoyment)cc.Pk).Enjoyment, (cc, v) => ((IFullnessEnjoyment)cc.Pk).Enjoyment = (byte)v, 0, 255, visible: cc => cc.Pk is IFullnessEnjoyment) with { Indice = 5 },
            F.Entero("PK-PKME-006", IdiomaInterfaz.T("Subeditores.Sociabilidad"), cc => ((ISociability)cc.Pk).Sociability, (cc, v) => ((ISociability)cc.Pk).Sociability = (uint)v, 0, 255, visible: cc => cc.Pk is ISociability) with { Indice = 6 },
        };
        secciones.Add(new(IdiomaInterfaz.T("Subeditores.AmistadAfecto"), otros));

        return new SubeditorDef
        {
            Id = "memorias", Titulo = IdiomaInterfaz.T("Catalogo.HistorialRecuerdos"), Secciones = secciones, Filas = ["PK-PKME-002", "PK-PKME-003", "PK-PKME-004", "PK-PKME-005", "PK-PKME-006", "PK-ED-082"],
            Acciones = pk is IGeoTrack ? [new("borrar-lugares", IdiomaInterfaz.T("Subeditores.BorrarLugares"), cc => { if (cc.Pk is IGeoTrack g) g.ClearGeoLocationData(); }, IdiomaInterfaz.T("Subeditores.DejaResidenciaNingunLugar"))] : [],
        };
    }

    private static byte GeoPais(PKM pk, int i) => pk is IGeoTrack g ? i switch { 0 => g.Geo1_Country, 1 => g.Geo2_Country, 2 => g.Geo3_Country, 3 => g.Geo4_Country, _ => g.Geo5_Country } : (byte)0;
    private static byte GeoRegion(PKM pk, int i) => pk is IGeoTrack g ? i switch { 0 => g.Geo1_Region, 1 => g.Geo2_Region, 2 => g.Geo3_Region, 3 => g.Geo4_Region, _ => g.Geo5_Region } : (byte)0;

    private static void PonerGeoPais(PKM pk, int i, byte v)
    {
        if (pk is not IGeoTrack g) return;
        switch (i) { case 0: g.Geo1_Country = v; g.Geo1_Region = 0; break; case 1: g.Geo2_Country = v; g.Geo2_Region = 0; break; case 2: g.Geo3_Country = v; g.Geo3_Region = 0; break; case 3: g.Geo4_Country = v; g.Geo4_Region = 0; break; default: g.Geo5_Country = v; g.Geo5_Region = 0; break; }
    }

    private static void PonerGeoRegion(PKM pk, int i, byte v)
    {
        if (pk is not IGeoTrack g) return;
        switch (i) { case 0: g.Geo1_Region = v; break; case 1: g.Geo2_Region = v; break; case 2: g.Geo3_Region = v; break; case 3: g.Geo4_Region = v; break; default: g.Geo5_Region = v; break; }
    }

    /// <summary>La frase del recuerdo como la compone MemoryAmie.GetMemoryString: texto del juego con el mote, el entrenador, el detalle, el sentimiento y la intensidad.</summary>
    private static string TextoRecuerdo(ContextoEditor c, bool eo)
    {
        var m = (ITrainerMemories)c.Pk;
        var mensajes = c.F.Textos.memories;
        var memoria = eo ? m.OriginalTrainerMemory : m.HandlingTrainerMemory;
        var nombre = c.Pk.Nickname;
        if (memoria == 0) return mensajes.Length > 0 ? string.Format(CultureInfo.CurrentCulture, mensajes[0], nombre) : string.Empty;
        var tr = eo ? c.Pk.OriginalTrainerName : c.Pk.HandlingTrainerName;
        var gen = eo ? (c.Pk.Generation == 0 ? c.Pk.Format : c.Pk.Generation) : c.Pk.Format;
        var tipoArg = Memories.GetMemoryArgType(memoria, gen);
        var variable = eo ? m.OriginalTrainerMemoryVariable : m.HandlingTrainerMemoryVariable;
        var argumentos = c.F.Memorias.GetArgumentStrings(tipoArg, gen);
        var arg = argumentos.Find(z => z.Value == variable)?.Text ?? string.Empty;
        var sentimientos = c.F.Memorias.GetMemoryFeelings(gen);
        var calidades = c.F.Memorias.GetMemoryQualities();
        var feel = eo ? m.OriginalTrainerMemoryFeeling : m.HandlingTrainerMemoryFeeling;
        var qual = eo ? m.OriginalTrainerMemoryIntensity : m.HandlingTrainerMemoryIntensity;
        var f = feel < sentimientos.Length ? sentimientos[feel] : string.Empty;
        var q = qual < calidades.Length ? calidades[qual] : string.Empty;
        var msg = (uint)memoria < mensajes.Length ? mensajes[memoria] : memoria.ToString();
        try { return string.Format(CultureInfo.CurrentCulture, msg, nombre, tr, arg, f, q); } catch (FormatException) { return msg; }
    }

    // ================= Superentrenamiento (SuperTrainingEditor) =================
    private static string NombreRegimen(int i)
    {
        string[] stats = [IdiomaInterfaz.T("Informe.Columna.HP"), IdiomaInterfaz.T("Informe.Columna.ATK"), IdiomaInterfaz.T("Informe.Columna.DEF"), IdiomaInterfaz.T("Catalogo.AtEspecial"), IdiomaInterfaz.T("Catalogo.DefEspecial"), IdiomaInterfaz.T("Informe.Columna.SPE")];
        if (i < 18) return IdiomaInterfaz.F("Subeditores.Regimen", i / 6 + 1, stats[i % 6]);
        return i switch { 18 => IdiomaInterfaz.T("Subeditores.Regimen2"), 19 => IdiomaInterfaz.T("Subeditores.Regimen3"), 20 => IdiomaInterfaz.T("Subeditores.Regimen4"), 21 => IdiomaInterfaz.T("Subeditores.Regimen5"), 22 => IdiomaInterfaz.T("Subeditores.Regimen6"), 23 => IdiomaInterfaz.T("Subeditores.Regimen7"), 24 => IdiomaInterfaz.T("Subeditores.Regimen8"), 25 => IdiomaInterfaz.T("Subeditores.Regimen9"), 26 => IdiomaInterfaz.T("Subeditores.Regimen10"), 27 => IdiomaInterfaz.T("Subeditores.Regimen11"), 28 => IdiomaInterfaz.T("Subeditores.Regimen12"), _ => IdiomaInterfaz.T("Subeditores.Regimen13") };
    }

    private static SubeditorDef Super(ContextoEditor c)
    {
        var campos = new List<CampoDef>();
        for (var i = 0; i < SuperTrainRegimenExtensions.CountRegimen; i++)
        {
            var k = i;
            campos.Add(F.Casilla("PK-PKME-010", NombreRegimen(k), cc => ((ISuperTrainRegimen)cc.Pk).GetRegimenState(k), (cc, v) => { ((ISuperTrainRegimen)cc.Pk).SetRegimenState(k, v); return null; },
                motivo: cc => cc.Pk is PK6 p && k >= 18 && !p.SecretSuperTrainingUnlocked ? IdiomaInterfaz.T("Subeditores.DesbloqueaSuperentrenamientoSecreto") : null) with { Indice = k });
        }
        var dist = new List<CampoDef>();
        for (var i = 0; i < SuperTrainRegimenExtensions.CountRegimenDistribution; i++)
        {
            var k = i;
            dist.Add(F.Casilla("PK-PKME-010", IdiomaInterfaz.F("Subeditores.RegimenDistribucion", k + 1), cc => ((ISuperTrainRegimen)cc.Pk).GetRegimenStateDistribution(k), (cc, v) => { ((ISuperTrainRegimen)cc.Pk).SetRegimenStateDistribution(k, v); return null; }) with { Indice = 100 + k });
        }
        var secciones = new List<SeccionDef>();
        if (c.Pk is PK6)
        {
            var bolsas = c.F.Textos.trainingbags;
            secciones.Add(new(IdiomaInterfaz.T("Subeditores.SuperentrenamientoSecreto"),
            [
                F.Casilla("PK-PKME-010", IdiomaInterfaz.T("Subeditores.SuperentrenamientoSecretoDesbloqueado"), cc => cc.Pk is ISuperTrain s && s.SecretSuperTrainingUnlocked, (cc, v) => { var s = (ISuperTrain)cc.Pk; s.SecretSuperTrainingUnlocked = v; if (!v) s.SuperTrainSupremelyTrained = false; return null; }) with { Indice = 200 },
                F.Casilla("PK-PKME-010", IdiomaInterfaz.T("Subeditores.EntrenadoMaximo"), cc => cc.Pk is ISuperTrain s && s.SuperTrainSupremelyTrained, (cc, v) => { ((ISuperTrain)cc.Pk).SuperTrainSupremelyTrained = v; return null; },
                    motivo: cc => cc.Pk is ISuperTrain { SecretSuperTrainingUnlocked: true } ? null : IdiomaInterfaz.T("Subeditores.NecesitaSuperentrenamientoSecretoDesbloque")) with { Indice = 201 },
                F.Opcion("PK-PKME-010", IdiomaInterfaz.T("Subeditores.SacoEntrenamiento"), cc => [.. Enumerable.Range(0, bolsas.Length).Select(z => new OpcionCampo(z, z == 0 ? IdiomaInterfaz.T("Form.Ninguno") : bolsas[z]))], cc => ((PK6)cc.Pk).TrainingBag, (cc, id) => { ((PK6)cc.Pk).TrainingBag = (byte)id; return null; }) with { Indice = 202 },
                F.Entero("PK-PKME-010", IdiomaInterfaz.T("Subeditores.GolpesSaco"), cc => ((PK6)cc.Pk).TrainingBagHits, (cc, v) => ((PK6)cc.Pk).TrainingBagHits = (byte)v, 0, 255) with { Indice = 203 },
            ]));
        }
        secciones.Add(new(IdiomaInterfaz.T("Subeditores.Regimenes"), campos));
        secciones.Add(new(IdiomaInterfaz.T("Subeditores.RegimenesDistribucion"), dist));
        return new SubeditorDef
        {
            Id = "superentrenamiento", Titulo = IdiomaInterfaz.T("Partida.Especifico.supertrain.Titulo"), Secciones = secciones, Filas = ["PK-PKME-010", "PK-ED-081"],
            Acciones =
            [
                new("todos", IdiomaInterfaz.T("Subeditores.MarcarTodos"), cc => { var s = (ISuperTrainRegimen)cc.Pk; if (cc.Pk is PK6 p) { p.SecretSuperTrainingUnlocked = true; p.SuperTrainSupremelyTrained = true; } for (var i = 0; i < SuperTrainRegimenExtensions.CountRegimen; i++) s.SetRegimenState(i, true); if (cc.Pk is not PK6 || ((ISuperTrain)cc.Pk).SecretSuperTrainingUnlocked) for (var i = 0; i < SuperTrainRegimenExtensions.CountRegimenDistribution; i++) s.SetRegimenStateDistribution(i, true); }),
                new("ninguno", IdiomaInterfaz.T("Mochila.QuitarTodos"), cc => { var s = (ISuperTrainRegimen)cc.Pk; if (cc.Pk is ISuperTrain t) { t.SecretSuperTrainingUnlocked = false; t.SuperTrainSupremelyTrained = false; } for (var i = 0; i < SuperTrainRegimenExtensions.CountRegimen; i++) s.SetRegimenState(i, false); for (var i = 0; i < SuperTrainRegimenExtensions.CountRegimenDistribution; i++) s.SetRegimenStateDistribution(i, false); }),
            ],
        };
    }

    // ================= Registros tecnicos (TechRecordEditor) =================
    private static SubeditorDef Tecnicas(ContextoEditor c)
    {
        var rec = (ITechRecord)c.Pk;
        var indices = rec.Permit.RecordPermitIndexes.ToArray();
        var @base = c.Pk.Context == EntityContext.Gen9a ? 1 : 0;   // en Leyendas Z-A la MT001 es el bit 0 (PKHeX numera igual la lista y el indice del indicador)
        IReadOnlyList<EvoCriteria> evos = [];
        try { lock (Pokekeep.Sprites.LegalityGate.Cerrojo) evos = [.. new LegalityAnalysis(c.Pk).Info.EvoChainsAllGens.Get(c.Pk.Context)]; } catch { /* sin pistas */ }
        var actuales = new HashSet<ushort>(c.Pk.Moves);
        var campos = new List<CampoDef>();
        for (var i = 0; i < indices.Length; i++)
        {
            var k = i;
            var mov = indices[k];
            campos.Add(F.Casilla("PK-PKME-011", $"{(k + @base):000} · {c.F.NombreMovimiento(mov)}", cc => ((ITechRecord)cc.Pk).GetMoveRecordFlag(k + @base), (cc, v) => { ((ITechRecord)cc.Pk).SetMoveRecordFlag(k + @base, v); return null; }) with
            {
                Indice = k,
                Detalle = cc =>
                {
                    var r = (ITechRecord)cc.Pk;
                    var estado = r.Permit.IsRecordPermitted(k) ? IdiomaInterfaz.T("Informe.Columna.Legal") : evos.Count > 0 && r.IsRecordPermitted(evos.ToArray(), k) ? IdiomaInterfaz.T("Subeditores.Posible") : IdiomaInterfaz.T("Subeditores.NoPermitida");
                    return $"{Tipo(cc, mov)} · {estado}{(actuales.Contains(mov) ? IdiomaInterfaz.T("Subeditores.ConoceAhora") : string.Empty)}";
                },
            });
        }
        return new SubeditorDef
        {
            Id = "tecnicas", Titulo = IdiomaInterfaz.T("Catalogo.RegistrosTecnicos"), Secciones = [new("MT / DT / TR", campos)], Filas = ["PK-PKME-011", "PK-ED-065"],
            Acciones =
            [
                new("legales-todos", IdiomaInterfaz.T("Form.Trainer7.TodosLegales"), cc => Bloq(() => ((ITechRecord)cc.Pk).SetRecordFlags(cc.Pk, TechnicalRecordApplicatorOption.LegalAll)), IdiomaInterfaz.T("Subeditores.MarcaTodosRegistrosCore")),
                new("legales-actuales", IdiomaInterfaz.T("Subeditores.LegalesActuales"), cc => Bloq(() => ((ITechRecord)cc.Pk).SetRecordFlags(cc.Pk, TechnicalRecordApplicatorOption.LegalCurrent)), IdiomaInterfaz.T("Subeditores.MarcaEnsenanMovimientosActuales")),
                new("forzar", IdiomaInterfaz.T("Subeditores.ForzarTodos"), cc => Bloq(() => ((ITechRecord)cc.Pk).SetRecordFlags(cc.Pk, TechnicalRecordApplicatorOption.ForceAll)), IdiomaInterfaz.T("Subeditores.MarcaTodosTambienPermitidos")),
                new("ninguno", IdiomaInterfaz.T("Mochila.QuitarTodos"), cc => ((ITechRecord)cc.Pk).ClearRecordFlags()),
            ],
        };
    }

    private static void Bloq(Action a) { lock (Pokekeep.Sprites.LegalityGate.Cerrojo) a(); }

    // ================= Tienda de movimientos y maestria (MoveShopEditor) =================
    private static SubeditorDef Tienda(ContextoEditor c)
    {
        var shop = (IMoveShop8)c.Pk;
        var indices = shop.Permit.RecordPermitIndexes.ToArray();
        var campos = new List<CampoDef>();
        for (var i = 0; i < indices.Length; i++)
        {
            var k = i;
            var mov = indices[k];
            campos.Add(F.Casilla("PK-PKME-007", IdiomaInterfaz.F("Subeditores.Comprado", k + 1, c.F.NombreMovimiento(mov)), cc => ((IMoveShop8)cc.Pk).GetPurchasedRecordFlag(k), (cc, v) => { ((IMoveShop8)cc.Pk).SetPurchasedRecordFlag(k, v); return null; }) with { Indice = k * 2, Detalle = cc => $"{Tipo(cc, mov)} · {(((IMoveShop8)cc.Pk).Permit.IsRecordPermitted(k) ? IdiomaInterfaz.T("Informe.Columna.Legal") : IdiomaInterfaz.T("Subeditores.NoPermitido"))}" });
            campos.Add(F.Casilla("PK-PKME-007", IdiomaInterfaz.F("Subeditores.Dominado", k + 1, c.F.NombreMovimiento(mov)), cc => ((IMoveShop8Mastery)cc.Pk).GetMasteredRecordFlag(k), (cc, v) => { ((IMoveShop8Mastery)cc.Pk).SetMasteredRecordFlag(k, v); return null; }) with { Indice = k * 2 + 1 });
        }
        return new SubeditorDef
        {
            Id = "tienda", Titulo = IdiomaInterfaz.T("Subeditores.TiendaMovimientos"), Secciones = [new(IdiomaInterfaz.T("Subeditores.MovimientosLeyendasArceus"), campos)], Filas = ["PK-PKME-007", "PK-ED-066"],
            Acciones =
            [
                new("legal", IdiomaInterfaz.T("Catalogo.MaestriaLegal"), cc => Bloq(() => ((IMoveShop8Mastery)cc.Pk).SetMoveShopFlags(cc.Pk)), IdiomaInterfaz.T("Subeditores.MarcaDominioCoreConsidera")),
                new("reiniciar", IdiomaInterfaz.T("Subeditores.ReiniciarMaestriaLegal"), cc => Bloq(() => { var m = (IMoveShop8Mastery)cc.Pk; m.ClearMoveShopFlags(); m.SetMoveShopFlags(cc.Pk); }), IdiomaInterfaz.T("Subeditores.QuitaTodoDejaSolo")),
                new("todos", IdiomaInterfaz.T("Subeditores.TodosCompradosDominados"), cc => { var m = (IMoveShop8Mastery)cc.Pk; m.SetPurchasedFlagsAll(cc.Pk); m.SetMoveShopFlagsAll(cc.Pk); }, IdiomaInterfaz.T("Subeditores.MarcaTodosTambienPermitidos")),
                new("ninguno", IdiomaInterfaz.T("Mochila.QuitarTodos"), cc => ((IMoveShop8)cc.Pk).ClearMoveShopFlags()),
            ],
        };
    }

    // ================= Registro Plus (PlusRecordEditor) =================
    private static SubeditorDef Plus(ContextoEditor c)
    {
        var permiso = (IPermitPlus)c.Pk.PersonalInfo;
        var indices = permiso.PlusMoveIndexes.ToArray();
        var aprendibles = new bool[c.Pk.MaxMoveID + 1];
        try { lock (Pokekeep.Sprites.LegalityGate.Cerrojo) { var la = new LegalityAnalysis(c.Pk); LearnPossible.Get(c.Pk, la.EncounterMatch, la.Info.EvoChainsAllGens, aprendibles); } } catch { /* sin pistas */ }
        var actuales = new HashSet<ushort>(c.Pk.Moves);
        var campos = new List<CampoDef>();
        for (var i = 0; i < indices.Length; i++)
        {
            var k = i;
            var mov = indices[k];
            campos.Add(F.Casilla("PK-PKME-008", $"{k:000} · {c.F.NombreMovimiento(mov)}", cc => ((IPlusRecord)cc.Pk).GetMovePlusFlag(k), (cc, v) => { ((IPlusRecord)cc.Pk).SetMovePlusFlag(k, v); return null; }) with
            {
                Indice = k,
                Detalle = cc => $"{Tipo(cc, mov)} · {(actuales.Contains(mov) ? IdiomaInterfaz.T("Subeditores.ConoceAhora2") : mov < aprendibles.Length && aprendibles[mov] ? IdiomaInterfaz.T("Subeditores.Aprendible") : IdiomaInterfaz.T("Subeditores.NoAprendible"))}",
            });
        }
        return new SubeditorDef
        {
            Id = "plus", Titulo = IdiomaInterfaz.T("Catalogo.RegistroPlus"), Secciones = [new(IdiomaInterfaz.T("Catalogo.MovimientosPlusLeyendasZ"), campos)], Filas = ["PK-PKME-008", "PK-ED-067"],
            Acciones =
            [
                new("legal", IdiomaInterfaz.T("Subeditores.Legales"), cc => Bloq(() => ((IPlusRecord)cc.Pk).SetPlusFlags(cc.Pk, (IPermitPlus)cc.Pk.PersonalInfo, PlusRecordApplicatorOption.LegalCurrent)), IdiomaInterfaz.T("Catalogo.MarcaPlusLegalesAhora")),
                new("legal-tm", IdiomaInterfaz.T("Subeditores.LegalesMTActuales"), cc => Bloq(() => ((IPlusRecord)cc.Pk).SetPlusFlags(cc.Pk, (IPermitPlus)cc.Pk.PersonalInfo, PlusRecordApplicatorOption.LegalCurrentTM))),
                new("semilla-tm", IdiomaInterfaz.T("Subeditores.LegalesSemillaMT"), cc => Bloq(() => ((IPlusRecord)cc.Pk).SetPlusFlags(cc.Pk, (IPermitPlus)cc.Pk.PersonalInfo, PlusRecordApplicatorOption.LegalSeedTM))),
                new("ninguno", IdiomaInterfaz.T("Mochila.QuitarTodos"), cc => ((IPlusRecord)cc.Pk).ClearPlusFlags(((IPermitPlus)cc.Pk.PersonalInfo).PlusCountTotal)),
            ],
        };
    }

    // ================= Bytes del texto (TrashEditor) =================
    private static (string Nombre, Func<PKM, byte[]> Leer, Action<PKM, int, byte> Escribir, Func<PKM, string> Texto, int Longitud)? Objetivo(PKM pk, string objetivo) => objetivo switch
    {
        "eo" => (IdiomaInterfaz.T("Catalogo.NombreEO"), p => p.OriginalTrainerTrash.ToArray(), (p, i, v) => p.OriginalTrainerTrash[i] = v, p => p.OriginalTrainerName, pk.OriginalTrainerTrash.Length),
        "ea" when pk.HandlingTrainerTrash.Length != 0 => (IdiomaInterfaz.T("Catalogo.NombreEntrenadorActual"), p => p.HandlingTrainerTrash.ToArray(), (p, i, v) => p.HandlingTrainerTrash[i] = v, p => p.HandlingTrainerName, pk.HandlingTrainerTrash.Length),
        "mote" => (IdiomaInterfaz.T("Catalogo.Mote"), p => p.NicknameTrash.ToArray(), (p, i, v) => p.NicknameTrash[i] = v, p => p.Nickname, pk.NicknameTrash.Length),
        _ => null,
    };

    private static SubeditorDef? Basura(ContextoEditor c, string objetivo)
    {
        if (Objetivo(c.Pk, objetivo) is not { } o) return null;
        var campos = new List<CampoDef>();
        for (var i = 0; i < o.Longitud; i++)
        {
            var k = i;
            campos.Add(F.Hex("PK-PKME-012", $"Byte {k:X2}", 2, cc => Objetivo(cc.Pk, objetivo)!.Value.Leer(cc.Pk)[k], (cc, v) => Objetivo(cc.Pk, objetivo)!.Value.Escribir(cc.Pk, k, (byte)v)) with { Indice = k });
        }
        var estado = new Dictionary<string, int> { ["especie"] = Math.Max(1, (int)c.Pk.Species), ["idioma"] = c.Pk.Format <= 2 ? 2 : Math.Max(1, c.Pk.Language), ["gen"] = (int)c.Pk.Generation };
        var selector = new List<CampoDef>
        {
            F.Info("PK-PKME-012", IdiomaInterfaz.T("Subeditores.TextoActual"), cc => Objetivo(cc.Pk, objetivo)!.Value.Texto(cc.Pk)),
            F.Opcion("PK-PKME-012", IdiomaInterfaz.T("Subeditores.EspecieBasura"), cc => FuentesEditor.Convertir(cc.F.Datos.SpeciesDataSource), _ => estado["especie"], (cc, id) => { estado["especie"] = id; return null; }, leer: cc => cc.F.NombreEspecie(estado["especie"])) with { Indice = 1000 },
            F.Opcion("PK-PKME-012", IdiomaInterfaz.T("Subeditores.IdiomaBasura"), cc => FuentesEditor.Convertir(GameInfo.LanguageDataSource(cc.Pk.Generation == 0 ? cc.Pk.Format : cc.Pk.Generation, cc.Pk.Context)), _ => estado["idioma"], (cc, id) => { estado["idioma"] = id; return null; }) with { Indice = 1001 },
            F.Entero("PK-PKME-012", IdiomaInterfaz.T("Subeditores.GeneracionBasura"), _ => estado["gen"], (cc, v) => estado["gen"] = (int)v, 1, 9) with { Indice = 1002 },
        };
        return new SubeditorDef
        {
            Id = "basura", Titulo = IdiomaInterfaz.F("Subeditores.BytesTexto", o.Nombre), Secciones = [new(IdiomaInterfaz.T("Subeditores.Ajustes"), selector), new(IdiomaInterfaz.T("Subeditores.BytesTextoHexadecimal"), campos)], Filas = ["PK-PKME-012", "PK-ED-124"],
            Aviso = IdiomaInterfaz.T("Subeditores.FuncionAvanzadaBytesQuedan"),
            Acciones =
            [
                new("aplicar", IdiomaInterfaz.T("Subeditores.AplicarBasuraEspecie"), cc =>
                {
                    var obj = Objetivo(cc.Pk, objetivo)!.Value;
                    var maxChars = objetivo == "mote" ? cc.Pk.MaxStringLengthNickname : cc.Pk.MaxStringLengthTrainer;
                    var texto = SpeciesName.GetSpeciesNameGeneration((ushort)estado["especie"], estado["idioma"], (byte)estado["gen"]);
                    if (string.IsNullOrEmpty(texto)) texto = cc.F.NombreEspecie(estado["especie"]);
                    if (texto.Length > maxChars) texto = texto[..maxChars];
                    var actual = obj.Texto(cc.Pk);
                    Span<byte> temp = stackalloc byte[obj.Longitud];
                    var n = cc.Pk.SetString(temp, texto, texto.Length, StringConverterOption.None);
                    Span<byte> aqui = stackalloc byte[obj.Longitud];
                    var actualN = cc.Pk.SetString(aqui, actual, actual.Length, StringConverterOption.None);
                    for (var i = actualN; i < Math.Min(n, obj.Longitud); i++) obj.Escribir(cc.Pk, i, temp[i]);
                }, IdiomaInterfaz.T("Subeditores.EscribeTrasTextoBytes")),
                new("limpiar", IdiomaInterfaz.T("Subeditores.LimpiarBasura"), cc =>
                {
                    var obj = Objetivo(cc.Pk, objetivo)!.Value;
                    var actual = obj.Texto(cc.Pk);
                    Span<byte> aqui = stackalloc byte[obj.Longitud];
                    var actualN = cc.Pk.SetString(aqui, actual, actual.Length, StringConverterOption.None);
                    for (var i = actualN; i < obj.Longitud; i++) obj.Escribir(cc.Pk, i, 0);
                }, IdiomaInterfaz.T("Subeditores.PoneCeroBytesSobran")),
            ],
        };
    }

    // ================= Caracteres especiales (teclado de glifos) =================
    private static SubeditorDef? Glifos(ContextoEditor c, string objetivo)
    {
        var lista = GlifosDe(c.Pk.Context);
        if (lista.Count == 0) return null;
        return new SubeditorDef
        {
            Id = "glifos", Titulo = IdiomaInterfaz.T("Subeditores.CaracteresEspecialesJuego"), Secciones = [], Glifos = lista, Filas = ["PK-ED-014"],
            Aviso = IdiomaInterfaz.T("Subeditores.TocaCaracterAnadirloFinal"),
        };
    }

    /// <summary>Los caracteres propios del juego que un teclado normal no tiene (TrashEditor de PKHeX: simbolos de la 5.ª generacion en adelante).</summary>
    public static IReadOnlyList<string> GlifosDe(EntityContext contexto)
    {
        ushort[] g5 = [0x2460, 0x2461, 0x2462, 0x2463, 0x2464, 0x2465, 0x2466, 0x2467, 0x2468, 0x246C, 0x246D, 0x246E, 0x246F, 0x2470, 0x2471, 0x2472, 0x2473, 0x2474, 0x2475, 0x2476, 0x2477, 0x2478, 0x2479, 0x247A, 0x247B, 0x247C, 0x247D, 0x247E, 0x247F, 0x2480, 0x2481, 0x2482, 0x2483, 0x2484];
        ushort[] g67 = [0xE081, 0xE082, 0xE083, 0xE084, 0xE085, 0xE086, 0xE087, 0xE088, 0xE089, 0xE08D, 0xE08E, 0xE08F, 0xE090, 0xE091, 0xE092, 0xE093, 0xE094, 0xE095, 0xE096, 0xE097, 0xE098, 0xE099, 0xE09A, 0xE09B, 0xE09C, 0xE09D, 0xE09E, 0xE09F, 0xE0A0, 0xE0A1, 0xE0A2, 0xE0A3, 0xE0A4, 0xE0A5];
        ushort[] g8 = [0x2026, 0x2642, 0x2640, 0x2660, 0x2663, 0x2665, 0x2666, 0x2605, 0x25CE, 0x25CB, 0x25A1, 0x25B3, 0x25C7, 0x266A, 0x2600, 0x2601, 0x2602, 0x2603];
        var codigos = contexto switch
        {
            EntityContext.Gen5 => g5,
            EntityContext.Gen6 or EntityContext.Gen7 or EntityContext.Gen7b => g67,
            _ when !contexto.IsEraPreSwitch => g8,
            _ => [],
        };
        return [.. codigos.Select(x => ((char)x).ToString())];
    }
}
