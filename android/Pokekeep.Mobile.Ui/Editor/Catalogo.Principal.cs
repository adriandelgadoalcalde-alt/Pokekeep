// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Editor;

/// <summary>
/// Catalogo de campos del editor de entidad: UNA definicion por fila del censo de paridad (<c>PK-ED-*</c>), con la visibilidad por formato de <c>PKMEditor.ToggleInterface</c> de PKHeX.
/// Esta parte: pestana Principal (<c>Tab_Main</c>).
/// </summary>
internal static partial class Catalogo
{
    // ---- Reglas de visibilidad compartidas (PKMEditor.ToggleInterface) ----
    internal static bool Desde(ContextoEditor c, int formato) => c.Formato >= formato;
    internal static bool Es<T>(ContextoEditor c) => c.Pk is T;
    private static string? SoloLecturaGb(ContextoEditor c, string motivo) => c.Formato <= 2 ? motivo : null;

    private static PKM P(ContextoEditor c) => c.Pk;

    /// <summary>
    /// ¿Hay que ocultar los datos «secretos» (PID, EC, rastreador HOME)? Ajuste <c>Privacy.HideSecretDetails</c> de PKHeX (<c>PKMEditor.HideSecretValues</c> → <c>ToggleSecrets</c>), leído en el momento (bloque 10-3).
    /// PKHeX esconde los cuadros del PID y de la EC y el rastreador; aquí igual: los campos y el botón «Regenerar PID» desaparecen mientras dure el ajuste.
    /// </summary>
    private static bool OcultarSecretos => Ajustes.Pkhex.AjustesVigentesPkhex.Actual.Privacy.HideSecretDetails;

    /// <summary>Ajustes del editor de PKHeX (<c>EntityEditor.*</c>), leídos en el momento como <c>Main.Settings.EntityEditor</c> en <c>PKMEditor</c> (bloque 10-3).</summary>
    internal static EntityEditorSettings AjustesEditor => Ajustes.Pkhex.AjustesVigentesPkhex.Actual.EntityEditor;

    internal static PestanaDef Principal() => new("principal", IdiomaInterfaz.T("Catalogo.TituloPrincipal"),
    [
        new(IdiomaInterfaz.T("Nav.Pokemon"),
        [
            F.Opcion("PK-ED-012", IdiomaInterfaz.T("Cajas.Especie"), c => c.F.Especies, c => c.Pk.Species, (c, id) => { Reglas.CambiarEspecie(c.Pk, (ushort)id); return null; }, ayuda: IdiomaInterfaz.T("Catalogo.EspeciePokemon")) with { AfectaStats = true },
            F.Opcion("PK-ED-020", IdiomaInterfaz.T("Catalogo.Forma"), FormasDe, c => c.Pk.Form, (c, id) => { Reglas.CambiarForma(c.Pk, (byte)id); return null; },
                visible: TieneFormas, motivo: c => c.Formato < 3 ? IdiomaInterfaz.T("Catalogo.GeneracionFormaUnownSale") : null) with { AfectaStats = true },
            F.Entero("PK-ED-115", IdiomaInterfaz.T("Form.Trainer8.FormArgument"), c => ((IFormArgument)c.Pk).FormArgument, (c, v) => ((IFormArgument)c.Pk).FormArgument = (uint)v, 0, int.MaxValue,
                visible: c => TipoArgumento(c) == FormArgumentType.Raw, maxDinamico: c => (int)Math.Min(int.MaxValue, FormArgumentUtil.GetFormArgumentMaxEdge(c.Pk.Species, c.Pk.Form, c.Pk.Context)), ayuda: IdiomaInterfaz.T("Catalogo.ContadorForma")),
            F.Opcion("PK-ED-114", IdiomaInterfaz.T("Form.Trainer8.FormArgument"), c => ArgumentosNombrados(c), c => (int)Math.Clamp(((IFormArgument)c.Pk).FormArgument, 0, 255), (c, id) => { ((IFormArgument)c.Pk).FormArgument = (uint)id; return null; },
                visible: c => TipoArgumento(c) == FormArgumentType.Named, ayuda: IdiomaInterfaz.T("Catalogo.VarianteForma")),
            F.Entero("PK-ED-116", IdiomaInterfaz.T("Catalogo.ArgumentoFormaMaximo"), c => ((IFormArgument)c.Pk).FormArgumentMaximum, (c, v) => ((IFormArgument)c.Pk).FormArgumentMaximum = (byte)v, 0, 255, visible: c => TipoArgumento(c) == FormArgumentType.Triple),
            F.Entero("PK-ED-116", IdiomaInterfaz.T("Catalogo.ArgumentoFormaRestante"), c => ((IFormArgument)c.Pk).FormArgumentRemain, (c, v) => ((IFormArgument)c.Pk).FormArgumentRemain = (byte)v, 0, 255, visible: c => TipoArgumento(c) is FormArgumentType.Triple or FormArgumentType.TripleParty) with { Indice = 1 },
            F.Entero("PK-ED-116", IdiomaInterfaz.T("Catalogo.ArgumentoFormaTranscurrido"), c => ((IFormArgument)c.Pk).FormArgumentElapsed, (c, v) => ((IFormArgument)c.Pk).FormArgumentElapsed = (byte)v, 0, 255, visible: c => TipoArgumento(c) is FormArgumentType.Triple or FormArgumentType.TripleParty) with { Indice = 2 },
            F.Opcion("PK-ED-011", IdiomaInterfaz.T("Catalogo.Sexo"), Sexos, c => c.Pk.Gender, (c, id) => Reglas.PonerSexo(c.Pk, (byte)id),
                visible: c => c.Formato >= 2 || (c.Formato == 1 && AjustesEditor.ShowGenderGen1), motivo: MotivoSexo) with { AfectaStats = false },
            F.Opcion("PK-ED-010", IdiomaInterfaz.T("Catalogo.Variocolor"), OpcionesVariocolor, c => Reglas.IdVariocolor(c.Pk), (c, id) => Reglas.PonerEstadoVariocolor(c.Pk, id),
                leer: c => Reglas.EstadoVariocolor(c.Pk), ayuda: IdiomaInterfaz.T("Catalogo.VariocolorCoreEligePID")),
            F.Accion("PK-ED-010", IdiomaInterfaz.T("Catalogo.VariocolorAzar"), c => Reglas.PonerVariocolor(c.Pk, Reglas.ModoVariocolor.Aleatorio), ayuda: IdiomaInterfaz.T("Catalogo.HaceVariocolorPokemonPID")) with { Indice = 1 },
            F.Accion("PK-ED-010", IdiomaInterfaz.T("Catalogo.VariocolorCambiandoSID"), c => Reglas.PonerVariocolor(c.Pk, Reglas.ModoVariocolor.AleatorioPorSid), visible: c => c.Formato >= 3,
                ayuda: IdiomaInterfaz.T("Catalogo.HaceVariocolorPokemonCambiando")) with { Indice = 2 },
        ]),
        new(IdiomaInterfaz.T("Catalogo.NivelExperiencia"),
        [
            F.Entero("PK-ED-016", IdiomaInterfaz.T("Cajas.Nivel"), c => c.Pk.CurrentLevel, (c, v) => Reglas.PonerNivel(c.Pk, (int)v), 1, 100, ayuda: IdiomaInterfaz.T("Catalogo.Nivel")) with { AfectaStats = true },
            F.Entero("PK-ED-016", IdiomaInterfaz.T("Informe.Columna.EXP"), c => c.Pk.EXP, (c, v) => Reglas.PonerExperiencia(c.Pk, (uint)v, c.Avanzado), 0, int.MaxValue, maxDinamico: c => (int)Math.Min(int.MaxValue, Experience.GetEXP(Experience.MaxLevel, c.Pk.PersonalInfo.EXPGrowth)),
                ayuda: IdiomaInterfaz.T("Catalogo.PuntosExperiencia")) with { Indice = 1, AfectaStats = true },
            F.Barra("PK-ED-017", IdiomaInterfaz.T("Catalogo.BarraExperiencia"), c => c.Pk.EXP, (c, v) => Reglas.PonerExperiencia(c.Pk, (uint)v, c.Avanzado), c => (int)Math.Min(int.MaxValue, Experience.GetEXP(Experience.MaxLevel, c.Pk.PersonalInfo.EXPGrowth))) with { AfectaStats = true, Visible = _ => AjustesEditor.ShowExperienceBar },
        ]),
        new(IdiomaInterfaz.T("Catalogo.Mote"),
        [
            F.Casilla("PK-ED-013", IdiomaInterfaz.T("Catalogo.TieneMote"), c => c.Pk.IsNicknamed, (c, v) => { c.Pk.IsNicknamed = v; if (!v) Reglas.ActualizarMoteDeFabrica(c.Pk, true); return null; },
                motivo: c => c.Pk is G3PKM ? IdiomaInterfaz.T("Catalogo.GeneracionJuegoDeduceMote") : null),
            F.Texto("PK-ED-014", IdiomaInterfaz.T("Catalogo.Mote"), c => c.Pk.Nickname, (c, t) => { Reglas.PonerMote(c.Pk, t); return null; }, c => c.Pk.MaxStringLengthNickname, ayuda: IdiomaInterfaz.T("Catalogo.NombrePokemonVacioUsa")),
            F.Info("PK-ED-015", IdiomaInterfaz.T("Catalogo.AvisoTipografia"), c => IdiomaInterfaz.T("Catalogo.MoteTieneCaracteresJuego"), visible: c => AvisoFuente(c, c.Pk.Nickname)),
        ]),
        new(IdiomaInterfaz.T("Catalogo.NaturalezaHabilidad"),
        [
            F.Opcion("PK-ED-018", IdiomaInterfaz.T("Informe.Columna.Nature"), c => c.F.Naturalezas, c => (int)c.Pk.Nature, (c, id) => Reglas.PonerNaturaleza(c.Pk, (Nature)id),
                visible: c => c.Formato >= 3, ayuda: IdiomaInterfaz.T("Informe.Columna.Nature")) with { AfectaStats = true },
            F.Opcion("PK-ED-019", IdiomaInterfaz.T("Catalogo.NaturalezaEstadisticas"), c => c.F.Naturalezas, c => (int)c.Pk.StatAlignment, (c, id) => { c.Pk.StatAlignment = (Nature)id; return null; },
                visible: c => c.Formato >= 8, ayuda: IdiomaInterfaz.T("Catalogo.NaturalezaModificaEstadisticas")) with { AfectaStats = true },
            F.Opcion("PK-ED-023", IdiomaInterfaz.T("Informe.Columna.Ability"), HabilidadesDe, c => Math.Max(0, Reglas.IndiceHabilidad(c.Pk)), (c, id) => Reglas.PonerHabilidad(c.Pk, id),
                visible: c => c.Formato >= 3 && !(c.Pk is PA9) && !c.Avanzado, leer: LeerHabilidad, ayuda: IdiomaInterfaz.T("Catalogo.HabilidadEntreEspecie")),
            F.Opcion("PK-ED-024", IdiomaInterfaz.T("Catalogo.HabilidadLibre"), c => c.F.Habilidades, c => c.Pk.Ability, (c, id) => { c.Pk.Ability = id; return null; },
                visible: c => (c.Formato > 3 && c.Avanzado) || c.Pk is PA9, avanzado: false, ayuda: IdiomaInterfaz.T("Catalogo.ModoAvanzadoCualquierHabilidad")),
            F.Entero("PK-ED-024", IdiomaInterfaz.T("Informe.Columna.AbilityNum"), c => c.Pk.AbilityNumber, (c, v) => c.Pk.AbilityNumber = (int)v, 0, 4, visible: c => c.Formato >= 6 && c.Avanzado, avanzado: true, ayuda: IdiomaInterfaz.T("CatalogoPrincipal.O")) with { Indice = 1 },
        ]),
        new(IdiomaInterfaz.T("Catalogo.ObjetoEIdioma"),
        [
            F.Opcion("PK-ED-022", IdiomaInterfaz.T("Cajas.ObjetoEquipado"), c => c.F.Objetos, c => c.Pk.HeldItem, (c, id) => { c.Pk.HeldItem = id; return null; },
                visible: ObjetoVisible, leer: c => c.Pk.HeldItem == 0 ? IdiomaInterfaz.T("Form.Ninguno") : c.F.NombreObjeto(c.Pk), ayuda: IdiomaInterfaz.T("Catalogo.ObjetoLlevaEquipado")),
            F.Opcion("PK-ED-025", IdiomaInterfaz.T("Catalogo.Idioma"), c => c.F.Idiomas, c => c.Formato <= 2 ? Reglas.IdiomaGb(c.Pk) : c.Pk.Language,
                (c, id) => { c.Pk.Language = id; Reglas.ActualizarMoteDeFabrica(c.Pk, true); return null; },
                motivo: c => SoloLecturaGb(c, IdiomaInterfaz.T("Catalogo.GeneracionIdiomaDependeGuardado"))),
        ]),
        new(IdiomaInterfaz.T("Catalogo.HuevoPokerus"),
        [
            F.Casilla("PK-ED-026", IdiomaInterfaz.T("Informe.Columna.IsEgg"), c => c.Pk.IsEgg, (c, v) => { Reglas.PonerHuevo(c.Pk, v, c.Sav); return null; }, visible: c => c.Formato >= 2),
            F.Casilla("PK-ED-027", IdiomaInterfaz.T("CatalogoPrincipal.PokerusInfectado"), c => c.Pk.IsPokerusInfected, (c, v) => { Reglas.PonerInfectado(c.Pk, v); return null; }, visible: c => c.Formato >= 2),
            F.Casilla("PK-ED-027", IdiomaInterfaz.T("Catalogo.PokerusCurado"), c => c.Pk.IsPokerusCured, (c, v) => { Reglas.PonerCurado(c.Pk, v); return null; }, visible: c => c.Formato >= 2) with { Indice = 1 },
            F.Opcion("PK-ED-027", IdiomaInterfaz.T("Informe.Columna.PokerusStrain"), c => [.. Enumerable.Range(0, 16).Select(i => new OpcionCampo(i, i == 0 ? IdiomaInterfaz.T("Catalogo.Cepa") : IdiomaInterfaz.F("Catalogo.Cepa2", i)))], c => c.Pk.PokerusStrain,
                (c, id) => { Reglas.PonerCepaPokerus(c.Pk, id); return null; }, visible: c => c.Formato >= 2 && c.Pk.PokerusStrain != 0) with { Indice = 2 },
            F.Opcion("PK-ED-027", IdiomaInterfaz.T("Informe.Columna.PokerusDays"), c => [.. Enumerable.Range(0, Pokerus.GetMaxDuration(c.Pk.PokerusStrain) + 1).Select(i => new OpcionCampo(i, i.ToString()))], c => c.Pk.PokerusDays,
                (c, id) => { c.Pk.PokerusDays = id; return null; }, visible: c => c.Formato >= 2 && c.Pk.PokerusStrain != 0 && !c.Pk.IsPokerusCured) with { Indice = 3 },
        ]),
        new(IdiomaInterfaz.T("Catalogo.Identificadores"),
        [
            F.Hex("PK-ED-008", IdiomaInterfaz.T("Catalogo.PID"), 8, c => c.Pk.PID, (c, v) => c.Pk.PID = (uint)v, visible: c => c.Formato >= 3 && !OcultarSecretos, ayuda: IdiomaInterfaz.T("Catalogo.ValorPersonalidadHexadecimal")),
            F.Accion("PK-ED-009", IdiomaInterfaz.T("Catalogo.RegenerarPid"), c => Reglas.RegenerarPid(c.Pk), visible: c => c.Formato >= 3 && !OcultarSecretos, ayuda: IdiomaInterfaz.T("Catalogo.GeneraPIDValidoEspecie")),
        ]),
        new(IdiomaInterfaz.T("Catalogo.OtrosDatosFormato"),
        [
            F.Casilla("PK-ED-028", IdiomaInterfaz.T("CatalogoPrincipal.DestelloDeN"), c => ((PK5)c.Pk).NSparkle, (c, v) => { ((PK5)c.Pk).NSparkle = v; return null; }, visible: c => c.Pk is PK5),
            F.Entero("PK-ED-029", IdiomaInterfaz.T("CatalogoPrincipal.IdDeSombra"), c => ((IShadowCapture)c.Pk).ShadowID, (c, v) => ((IShadowCapture)c.Pk).ShadowID = (ushort)v, 0, ushort.MaxValue, visible: c => c.Pk is IShadowCapture),
            F.Entero("PK-ED-029", IdiomaInterfaz.T("Catalogo.EngranajePurificacion"), c => Math.Max(0, ((IShadowCapture)c.Pk).Purification), (c, v) => ((IShadowCapture)c.Pk).Purification = (int)v, 0, 100000,
                visible: c => c.Pk is IShadowCapture { ShadowID: > 0 }) with { Indice = 1 },
            F.Casilla("PK-ED-029", IdiomaInterfaz.T("CatalogoPrincipal.PokemonOscuro"), c => ((IShadowCapture)c.Pk).IsShadow, (c, v) => { var s = (IShadowCapture)c.Pk; s.Purification = v ? 1 : (c.Pk is CK3 && s.ShadowID != 0 ? CK3.Purified : 0); return null; },
                visible: c => c.Pk is IShadowCapture { ShadowID: > 0 }) with { Indice = 2 },
            F.Entero("PK-ED-030", IdiomaInterfaz.T("Catalogo.RatioCaptura"), c => ((PK1)c.Pk).CatchRate, (c, v) => ((PK1)c.Pk).CatchRate = (byte)v, 0, 255, visible: c => c.Pk is PK1, ayuda: IdiomaInterfaz.T("Catalogo.RatioCapturaGeneracion")),
            F.Accion("PK-ED-030", IdiomaInterfaz.T("Catalogo.RestablecerRatioCaptura"), c => ((PK1)c.Pk).CatchRate = (byte)CatchRateApplicator.GetSuggestedCatchRate((PK1)c.Pk, c.Sav), visible: c => c.Pk is PK1, ayuda: IdiomaInterfaz.T("Catalogo.PoneRatioSugiereCore")) with { Indice = 1 },
            F.Opcion("PK-ED-031", IdiomaInterfaz.T("Catalogo.EstadoAlterado"), OpcionesEstado, c => IdEstado(c.Pk), (c, id) => { PonerEstado(c.Pk, id); return null; },
                visible: c => c.Pk.PartyStatsPresent && AjustesEditor.ShowStatusCondition, motivo: c => c.Pk.PartyStatsPresent ? null : IdiomaInterfaz.T("Catalogo.PokemonGuardaDatosCombate")),
            F.Entero("PK-ED-031", IdiomaInterfaz.T("Catalogo.TurnosSueno"), c => Math.Max(1, c.Pk.Status_Condition & 7), (c, v) => c.Pk.Status_Condition = (c.Pk.Status_Condition & ~7) | (int)v, 1, 7,
                visible: c => c.Formato <= 4 && c.Pk.PartyStatsPresent && (c.Pk.Status_Condition & 7) != 0 && AjustesEditor.ShowStatusCondition) with { Indice = 1 },
        ]),
    ]);

    // ---- Auxiliares de la pestana ----
    private static IReadOnlyList<OpcionCampo> FormasDe(ContextoEditor c)
    {
        var s = c.F.Textos;
        var formas = FormConverter.GetFormList(c.Pk.Species, s.types, s.forms, Ajustes.Pkhex.PresentacionPkhex.SimbolosGenero, c.Pk.Context);
        return [.. formas.Select((t, i) => new OpcionCampo(i, string.IsNullOrEmpty(t) ? IdiomaInterfaz.F("Libreria.NumeroForma", i) : t))];
    }

    private static bool TieneFormas(ContextoEditor c)
    {
        var pi = c.Sav.Personal[c.Pk.Species];
        if (!FormInfo.HasFormSelection(pi, c.Pk.Species, c.Pk.Format)) return c.Avanzado && c.Formato >= 4;
        return FormasDe(c).Count > 1;
    }

    private static FormArgumentType TipoArgumento(ContextoEditor c)
        => c.Pk is IFormArgument && c.Formato >= 6 ? FormArgumentUtil.GetType(c.Pk.Species, c.Pk.Form, c.Pk.Context) : FormArgumentType.None;

    private static IReadOnlyList<OpcionCampo> ArgumentosNombrados(ContextoEditor c)
        => [.. FormConverter.GetFormArgumentStrings(c.Pk.Species).Select((t, i) => new OpcionCampo(i, t))];

    private static IReadOnlyList<OpcionCampo> Sexos(ContextoEditor c) => [new(0, IdiomaInterfaz.T("Catalogo.Macho") + Ajustes.Pkhex.PresentacionPkhex.SimbolosGenero[0]), new(1, IdiomaInterfaz.T("Catalogo.Hembra") + Ajustes.Pkhex.PresentacionPkhex.SimbolosGenero[1]), new(2, IdiomaInterfaz.T("Form.Trainer8.Genderless"))];

    private static string? MotivoSexo(ContextoEditor c)
    {
        var pi = c.Pk.PersonalInfo;
        if (!pi.IsDualGender) return IdiomaInterfaz.T("Catalogo.EspecieTieneSexoFijo");
        return null;
    }

    private static IReadOnlyList<OpcionCampo> OpcionesVariocolor(ContextoEditor c)
        => c.Formato <= 5 ? [new(0, IdiomaInterfaz.T("Comun.No")), new(1, IdiomaInterfaz.T("Comun.Si"))] : [new(0, IdiomaInterfaz.T("Comun.No")), new(1, IdiomaInterfaz.T("Catalogo.Estrella")), new(2, IdiomaInterfaz.T("Catalogo.Cuadrado"))];

    private static IReadOnlyList<OpcionCampo> HabilidadesDe(ContextoEditor c)
    {
        var pi = c.Pk.PersonalInfo;
        var lista = c.F.Filtro.GetAbilityList(pi);
        var r = new List<OpcionCampo>(lista.Count + 1);
        for (var i = 0; i < lista.Count; i++) r.Add(new OpcionCampo(i, lista[i].Text));
        if (c.Pk is { Context: EntityContext.Gen5, Species: (ushort)Species.Basculin, Form: 1 })
            r.Add(new OpcionCampo(r.Count, FilteredGameDataSource.GetAbilityItem(c.F.Textos.abilitylist, (int)Ability.Reckless, '*').Text));
        return r;
    }

    private static string LeerHabilidad(ContextoEditor c)
    {
        var l = HabilidadesDe(c);
        var i = Math.Max(0, Reglas.IndiceHabilidad(c.Pk));
        return i < l.Count ? l[i].Texto : c.F.Textos.abilitylist[c.Pk.Ability];
    }

    private static bool ObjetoVisible(ContextoEditor c)
    {
        if (c.Formato < 2) return false;
        if (!c.Avanzado && (c.Sav is SAV7b || c.Sav is SAV8LA)) return false;
        return true;
    }

    private static bool AvisoFuente(ContextoEditor c, string texto)
    {
        try
        {
            if (c.Pk.Generation < 5 && c.Pk is not (CK3 or XK3)) return false;
            var idiomaPk = (LanguageID)(c.Formato <= 2 ? Reglas.IdiomaGb(c.Pk) : c.Pk.Language);
            return StringFontUtil.HasUndefinedCharacters(texto, c.Pk.Context, idiomaPk, (LanguageID)c.Sav.Language);
        }
        catch { return false; }
    }

    // Estado alterado (StatusBrowser): ids propios 0 a 6
    private static IReadOnlyList<OpcionCampo> OpcionesEstado(ContextoEditor c)
    {
        List<OpcionCampo> l = [new(0, IdiomaInterfaz.T("Form.Ninguno")), new(1, IdiomaInterfaz.T("Catalogo.Estado.Dormido")), new(2, IdiomaInterfaz.T("Catalogo.Estado.Envenenado")), new(3, IdiomaInterfaz.T("Catalogo.Estado.Quemado")), new(4, IdiomaInterfaz.T("Catalogo.Estado.Paralizado")), new(5, IdiomaInterfaz.T("Catalogo.Estado.Congelado"))];
        if (c.Formato is 3 or 4) l.Add(new(6, IdiomaInterfaz.T("Catalogo.Estado.GravementeEnvenenado")));
        return l;
    }

    private static int IdEstado(PKM pk)
    {
        if (pk.Format <= 4)
        {
            var s = (StatusCondition)(pk.Status_Condition & 0xFF);
            if (s == StatusCondition.None) return 0;
            if (s <= StatusCondition.Sleep7) return 1;
            if ((s & StatusCondition.PoisonBad) != 0) return 6;
            if ((s & StatusCondition.Poison) != 0) return 2;
            if ((s & StatusCondition.Burn) != 0) return 3;
            if ((s & StatusCondition.Paralysis) != 0) return 4;
            if ((s & StatusCondition.Freeze) != 0) return 5;
            return 0;
        }
        return ((StatusType)(pk.Status_Condition & 7)) switch { StatusType.Sleep => 1, StatusType.Poison => 2, StatusType.Burn => 3, StatusType.Paralysis => 4, StatusType.Freeze => 5, _ => 0 };
    }

    private static void PonerEstado(PKM pk, int id)
    {
        var actual = pk.Status_Condition & ~0xFF;
        if (pk.Format <= 4)
        {
            var v = id switch { 1 => StatusCondition.Sleep1, 2 => StatusCondition.Poison, 3 => StatusCondition.Burn, 4 => StatusCondition.Paralysis, 5 => StatusCondition.Freeze, 6 => StatusCondition.PoisonBad, _ => StatusCondition.None };
            pk.Status_Condition = actual | (int)v;
        }
        else
        {
            var v = id switch { 1 => StatusType.Sleep, 2 => StatusType.Poison, 3 => StatusType.Burn, 4 => StatusType.Paralysis, 5 => StatusType.Freeze, _ => StatusType.None };
            pk.Status_Condition = actual | (int)v;
        }
    }
}
