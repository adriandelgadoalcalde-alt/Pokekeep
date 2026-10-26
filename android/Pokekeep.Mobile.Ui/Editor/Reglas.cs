// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Editor;

/// <summary>
/// Las relaciones entre campos del editor, transcritas de <c>PKMEditor</c> de PKHeX (cada metodo cita el suyo). Aqui no hay formulas propias: todo se resuelve con funciones del Core
/// (<c>CommonEdits</c>, <c>EncounterSuggestion</c>, <c>EggStateLegality</c>, <c>SpeciesName</c>...). Las pantallas y las pruebas llaman a estos metodos; el modelo de campos los usa para escribir.
/// </summary>
public static class Reglas
{
    private static readonly DateOnly Fecha2000 = new(2000, 1, 1);

    // ---- Mote (PKMEditor.UpdateNickname / IsPossibleNotNicknamed) ----
    /// <summary>¿Puede ser un mote «de fabrica» (el nombre de la especie en algun idioma)? Si no, hay que marcar el indicador de mote.</summary>
    public static bool PuedeNoTenerMote(PKM pk, ReadOnlySpan<char> actual)
    {
        var especie = pk.Species;
        if (pk.IsEgg) especie = 0;
        var contexto = pk.Context;
        if (!SpeciesName.IsNicknamedAnyLanguage(especie, actual, contexto)) return true;
        // La mayuscula/minuscula automatica no existio hasta la Gen. VI: un Pokemon de Gen. III-IV pasado a la V puede venir de cualquiera de las dos formas.
        if (pk.IsEgg) return false;
        if (contexto != EntityContext.Gen5 || especie > 493 || pk.Gen5) return false;
        return !SpeciesName.IsNicknamedAnyLanguage(especie, actual, EntityContext.Gen4);
    }

    /// <summary>Escribe un mote: si no es el de fabrica se marca «tiene mote» (como el editor de PKHeX al escribir).</summary>
    public static void PonerMote(PKM pk, string texto)
    {
        pk.Nickname = texto;
        if (pk.IsNicknamed) return;
        if (pk.Species == 0 || pk.Species > pk.MaxSpeciesID) return;
        if (!PuedeNoTenerMote(pk, texto)) pk.IsNicknamed = true;
    }

    /// <summary>Pone el nombre de la especie (o «Huevo») en el idioma del Pokemon cuando NO tiene mote propio (<c>UpdateNickname</c>).</summary>
    public static void ActualizarMoteDeFabrica(PKM pk, bool cambioDeIdioma = false)
    {
        if (pk.IsNicknamed) return;
        var especie = pk.Species;
        if (especie == 0 || especie > pk.MaxSpeciesID) { pk.Nickname = string.Empty; return; }
        var actual = pk.Nickname;
        if (PuedeNoTenerMote(pk, actual) && !cambioDeIdioma) return;
        string nombre;
        var idioma = pk.Format <= 2 ? IdiomaGb(pk) : pk.Language;
        if (pk.IsEgg) nombre = SpeciesName.GetEggName(idioma, pk.Format);
        else
        {
            if (!cambioDeIdioma && !SpeciesName.IsNicknamedAnyLanguage(especie, actual, pk.Context)) return;
            nombre = SpeciesName.GetSpeciesNameGeneration(especie, idioma, pk.Format);
        }
        pk.Nickname = nombre;
        if (pk is GBPKM gb) gb.SetNotNicknamed(idioma);
    }

    /// <summary>Idioma de una entidad de Gen. I-II: japones o el del guardado (el nombre se detecta como hace el editor de PKHeX).</summary>
    public static int IdiomaGb(PKM pk) => pk is GBPKM gb ? (gb.IsSpeciesNameMatch((int)LanguageID.English) ? (int)LanguageID.English : gb.GuessedLanguage((int)LanguageID.English)) : pk.Language;

    // ---- Especie y forma (UpdateSpecies / UpdateForm) ----
    public static void CambiarEspecie(PKM pk, ushort especie)
    {
        var indiceHabilidad = IndiceHabilidad(pk);
        pk.Species = especie;
        var pi = pk.PersonalInfo;
        // Una especie sin formas elegibles queda en la forma 0 (SetForms).
        if (!FormInfo.HasFormSelection(pi, especie, pk.Format) && pk.Format >= 3) pk.Form = 0;
        else if (pk.Form >= FormCount(pk)) pk.Form = 0;
        pk.EXP = Experience.GetEXP(pk.CurrentLevel, pk.PersonalInfo.EXPGrowth);
        if (pk.Format >= 3 && indiceHabilidad >= 0) RestaurarIndiceHabilidad(pk, indiceHabilidad);
        pk.Gender = pk.GetSaneGender();
        if (pk is IFormArgument f) ReiniciarArgumentoDeForma(pk, f);
        ActualizarMoteDeFabrica(pk);
    }

    public static int FormCount(PKM pk)
    {
        var s = Ajustes.Pkhex.ProgramaPkhex.Textos;
        return FormConverter.GetFormList(pk.Species, s.types, s.forms, Ajustes.Pkhex.PresentacionPkhex.SimbolosGenero, pk.Context).Length;
    }

    public static void CambiarForma(PKM pk, byte forma)
    {
        var anterior = pk.Form;
        pk.Form = forma;
        pk.EXP = Experience.GetEXP(pk.CurrentLevel, pk.PersonalInfo.EXPGrowth);
        if (pk.Species == (int)Species.Unown)
        {
            if (pk.Format == 3) pk.SetPIDUnown3(forma);
            else if (pk.Format == 2)
            {
                // En Gen. II la forma de Unown sale de los DV: se sortean DV (con las reglas del Core) hasta que coincida; con un tope para no colgarse.
                var intentos = 0;
                while (pk.Form != forma && intentos++ < 20000) pk.SetRandomIVs();
            }
        }
        else if (EntityGender.GetFromString(NombreForma(pk)) < 2 && FormCount(pk) == 2) pk.Gender = (byte)Math.Min(forma, (byte)1);   // formas M/F de verdad (no «S, M, L, XL» de Pumpkaboo)
        else pk.Gender = pk.GetSaneGender();
        if (pk.Species == (int)Species.Basculin && pk.Context == EntityContext.Gen5) { /* la lista de habilidades cambia con la forma: se conserva el indice */ }
        _ = anterior;
        if (pk is IFormArgument f) ReiniciarArgumentoDeForma(pk, f);
    }

    public static string NombreForma(PKM pk)
    {
        var s = Ajustes.Pkhex.ProgramaPkhex.Textos;
        var l = FormConverter.GetFormList(pk.Species, s.types, s.forms, Ajustes.Pkhex.PresentacionPkhex.SimbolosGenero, pk.Context);
        return pk.Form < l.Length ? l[pk.Form] : string.Empty;
    }

    private static void ReiniciarArgumentoDeForma(PKM pk, IFormArgument f)
    {
        var tipo = FormArgumentUtil.GetType(pk.Species, pk.Form, pk.Context);
        if (tipo == FormArgumentType.None) f.FormArgument = 0;
    }

    // ---- Habilidad ----
    /// <summary>Indice (0 a 2) de la habilidad actual entre las de la especie, como lo lee PKHeX al cargar el Pokemon (<c>LoadMisc4</c>, <c>LoadMisc6</c>, <c>PopulateFieldsPK3/5</c>).</summary>
    public static int IndiceHabilidad(PKM pk)
    {
        if (pk.Format < 3) return -1;
        var pi = pk.PersonalInfo;
        if (pk is G3PKM g3) return g3.AbilityBit && pi.AbilityCount > 1 ? 1 : 0;
        if (pk is PK5 { HiddenAbility: true }) return 2;
        if (pk.Format >= 6) return AbilityVerifier.IsValidAbilityBits(pk.AbilityNumber) ? pk.AbilityNumber >> 1 : 0;
        var indice = pi.GetIndexOfAbility(pk.Ability);
        if (indice >= 2) return 2;
        if (indice < 0)
        {
            if (pk.Ability == (int)Ability.Reckless && pk is { Context: EntityContext.Gen5, Species: (ushort)Species.Basculin, Form: 1 }) return 3;
            return 0;
        }
        if (pi is IPersonalAbility12 a && a.IsAbility12Same) return pk.PIDAbility;
        return indice;
    }

    private static void RestaurarIndiceHabilidad(PKM pk, int indice)
    {
        var n = pk.PersonalInfo.AbilityCount;
        if (n <= 0) return;
        indice = Math.Clamp(indice, 0, n - 1);
        if (pk is G3PKM g3) g3.AbilityBit = indice != 0;
        else pk.RefreshAbility(indice);
    }

    /// <summary>Restaura los bytes del Pokemon (cuando una escritura no pudo hacerse del todo no se deja a medias).</summary>
    private static void Restaurar(PKM pk, byte[] copia) => copia.CopyTo(pk.Data);

    /// <summary>Elige la habilidad por su posicion en la lista de la especie. Hasta la 5.ª generacion el Core vuelve a sortear el PID para que case (como PKHeX al salir del desplegable). Devuelve el motivo si no se pudo.</summary>
    public static string? PonerHabilidad(PKM pk, int indice)
    {
        var copia = pk.Data.ToArray();
        pk.SetAbilityIndex(indice);
        if (pk is G3PKM g3) g3.AbilityBit = indice != 0;
        if (IndiceHabilidad(pk) == indice) return null;
        Restaurar(pk, copia);
        return IdiomaInterfaz.T("Reglas.CorePuedeDejarHabilidad");
    }

    // ---- Sexo (ClickGender) ----
    public static string? PonerSexo(PKM pk, byte sexo)
    {
        var pi = pk.PersonalInfo;
        if (!pi.IsDualGender) return IdiomaInterfaz.T("Catalogo.EspecieTieneSexoFijo");
        var copia = pk.Data.ToArray();
        if (pk.Format <= 2) pk.SetAttackIVFromGender(sexo);
        else if (pk.Format <= 4) pk.SetPIDGender(sexo);
        pk.Gender = sexo;
        // Formas ligadas al sexo (Meowstic, Indeedee...): la forma sigue al sexo.
        if (FormCount(pk) == 2 && EntityGender.GetFromString(NombreForma(pk)) < 2) pk.Form = (byte)Math.Min(sexo, (byte)1);
        if (pk.Gender == sexo) return null;
        Restaurar(pk, copia);
        return IdiomaInterfaz.T("Reglas.CorePuedeDejarSexo");
    }

    // ---- Naturaleza (ValidateComboBox2 / UpdateRandomPID) ----
    public static string? PonerNaturaleza(PKM pk, Nature n)
    {
        var copia = pk.Data.ToArray();
        if (pk.Format <= 4 && pk.Nature != n) pk.SetPIDNature(n);
        pk.Nature = n;          // desde la 4.ª generacion la naturaleza se guarda ademas del PID (PKHeX escribe las dos)
        if (pk.Nature == n) return null;
        Restaurar(pk, copia);
        return IdiomaInterfaz.T("Reglas.CorePuedeDejarNaturaleza");
    }

    // ---- Variocolor (UpdateShiny) ----
    public enum ModoVariocolor { Quitar, Aleatorio, Estrella, Cuadrado, AleatorioPorSid }

    public static void PonerVariocolor(PKM pk, ModoVariocolor modo)
    {
        if (modo == ModoVariocolor.Quitar) { pk.SetUnshiny(); return; }
        if (pk.Format <= 2) { pk.SetShiny(); return; }
        var tipo = modo switch { ModoVariocolor.Estrella => Shiny.AlwaysStar, ModoVariocolor.Cuadrado => Shiny.AlwaysSquare, _ => Shiny.Random };
        if (modo == ModoVariocolor.AleatorioPorSid) { pk.SetShinySID(tipo); return; }
        pk.SetShiny(tipo);
        if (pk.Format >= 6 && pk.Generation is 3 or 4 or 5) pk.EncryptionConstant = pk.PID;
    }

    /// <summary>Identificador del estado de variocolor para la lista del editor: 0 no, 1 si (o estrella desde la 3.ª generacion), 2 cuadrado.</summary>
    public static int IdVariocolor(PKM pk) => !pk.IsShiny ? 0 : pk.Format <= 5 ? 1 : pk.ShinyXor == 0 ? 2 : 1;

    /// <summary>Elige el estado de variocolor exacto (no, estrella o cuadrado). Si el Core no puede dejarlo asi (encuentros predestinados, GO...), no cambia nada y dice por que.</summary>
    public static string? PonerEstadoVariocolor(PKM pk, int id)
    {
        if (IdVariocolor(pk) == id) return null;
        var copia = pk.Data.ToArray();
        PonerVariocolor(pk, id switch { 0 => ModoVariocolor.Quitar, 1 => pk.Format <= 5 ? ModoVariocolor.Aleatorio : ModoVariocolor.Estrella, _ => ModoVariocolor.Cuadrado });
        if (IdVariocolor(pk) == id) return null;
        Restaurar(pk, copia);
        return id == 0 ? IdiomaInterfaz.T("Reglas.CorePuedeQuitarVariocolor") : IdiomaInterfaz.T("Reglas.CorePuedeDejarTipo");
    }

    public static string EstadoVariocolor(PKM pk)
    {
        if (!pk.IsShiny) return IdiomaInterfaz.T("Comun.No");
        return pk.Format <= 5 ? IdiomaInterfaz.T("Comun.Si") : pk.ShinyXor == 0 ? IdiomaInterfaz.T("Catalogo.Cuadrado") : IdiomaInterfaz.T("Catalogo.Estrella");
    }

    // ---- PID y EC (UpdateRandomPID / UpdateRandomEC) ----
    public static void RegenerarPid(PKM pk)
    {
        if (pk.Format < 3) return;
        pk.SetPIDGender(pk.Gender);
        if (pk.Format >= 6 && (pk.Gen3 || pk.Gen4 || pk.Gen5)) pk.EncryptionConstant = pk.PID;
    }

    public static void RegenerarEc(PKM pk)
    {
        if (pk.Format < 6) return;
        pk.SetRandomEC();
    }

    // ---- Nivel y experiencia (UpdateEXPLevel) ----
    public static void PonerNivel(PKM pk, int nivel)
    {
        var n = (byte)Math.Clamp(nivel, Experience.MinLevel, Experience.MaxLevel);
        pk.CurrentLevel = n;
        pk.Stat_Level = n;
    }

    public static void PonerExperiencia(PKM pk, uint exp, bool avanzado)
    {
        var crecimiento = pk.PersonalInfo.EXPGrowth;
        var nivel = Experience.GetLevel(exp, crecimiento);
        if (nivel == Experience.MaxLevel) exp = Math.Min(exp, Experience.GetEXP(Experience.MaxLevel, crecimiento)) is var tope && !avanzado ? tope : exp;
        pk.EXP = exp;
        pk.Stat_Level = Experience.GetLevel(pk.EXP, crecimiento);
    }

    // ---- Huevo (UpdateIsEgg) ----
    public static void PonerHuevo(PKM pk, bool huevo, SaveFile sav)
    {
        if (pk.IsEgg == huevo) return;
        pk.IsEgg = huevo;
        if (huevo)
        {
            pk.OriginalTrainerFriendship = (byte)EggStateLegality.GetMinimumEggHatchCycles(pk);
            if (pk.Format >= 4)
            {
                if (!EncounterStateUtil.IsMetAsEgg(pk) || pk.EggMetDate is null)
                {
                    pk.EggMetDate = FechaHoy();
                    pk.EggLocation = EncounterSuggestion.GetSuggestedEncounterEggLocationEgg(pk, pk.Version != sav.Version);
                }
                var cambiado = sav.OT != pk.OriginalTrainerName || sav.TID16 != pk.TID16 || sav.SID16 != pk.SID16;
                pk.MetLocation = cambiado ? Locations.TradedEggLocation(sav.Generation, sav.Version) : LocationEdits.GetNoneLocation(pk);
                pk.MetDate = pk.MetLocation == LocationEdits.GetNoneLocation(pk) ? null : Fecha2000;
            }
            pk.IsNicknamed = EggStateLegality.IsNicknameFlagSet(pk);
            pk.Nickname = SpeciesName.GetEggName(pk.Format <= 2 ? IdiomaGb(pk) : pk.Language, pk.Format);
            if (pk.Format >= 6) pk.ClearMemories();
            if (pk is PK9) pk.Version = 0;   // los huevos de EP no tienen version hasta que eclosionan
        }
        else
        {
            if (!pk.IsNicknamed) ActualizarMoteDeFabrica(pk);
            pk.OriginalTrainerFriendship = pk.PersonalInfo.BaseFriendship;
            if (pk.Format >= 4)
            {
                if (pk.EggLocation == LocationEdits.GetNoneLocation(pk))
                {
                    pk.MetDate = FechaHoy();
                    pk.EggMetDate = null;
                }
                else
                {
                    pk.MetDate = pk.EggMetDate;
                    pk.MetLocation = EncounterSuggestion.GetSuggestedEggMetLocation(pk);
                }
            }
            var nombreHuevo = SpeciesName.GetEggName(pk.Format <= 2 ? IdiomaGb(pk) : pk.Language, pk.Format);
            if (pk.Nickname == nombreHuevo) pk.IsNicknamed = false;
            ActualizarMoteDeFabrica(pk);
        }
    }

    public static DateOnly FechaHoy() => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>«Encontrado como huevo» (UpdateMetAsEgg): al marcarlo hay fecha y lugar de huevo; al quitarlo se borran y deja de ser huevo.</summary>
    public static void PonerComoHuevo(PKM pk, bool comoHuevo, SaveFile sav)
    {
        if (pk.Format < 4) return;
        if (comoHuevo)
        {
            pk.EggMetDate = FechaHoy();
            var viajado = pk.IsEgg && pk.Version != sav.Version;
            pk.EggLocation = EncounterSuggestion.GetSuggestedEncounterEggLocationEgg(pk, viajado);
            return;
        }
        if (pk.IsEgg) PonerHuevo(pk, false, sav);
        pk.EggMetDate = null;
        pk.EggLocation = LocationEdits.GetNoneLocation(pk);
    }

    // ---- Pokerus (UpdatePKRS*) ----
    public static void PonerInfectado(PKM pk, bool infectado)
    {
        if (!infectado) { pk.PokerusStrain = 0; pk.PokerusDays = 0; return; }
        if (pk.PokerusStrain == 0) { pk.PokerusStrain = 1; pk.PokerusDays = 1; }
        else if (pk.PokerusDays == 0 && !pk.IsPokerusCured) pk.PokerusDays = 1;
    }

    public static void PonerCurado(PKM pk, bool curado)
    {
        if (curado) { if (pk.PokerusStrain == 0) pk.PokerusStrain = 1; pk.PokerusDays = 0; }
        else { if (pk.PokerusStrain != 0 && pk.PokerusDays == 0) pk.PokerusDays = 1; }
    }

    public static void PonerCepaPokerus(PKM pk, int cepa)
    {
        var dias = pk.PokerusDays;
        pk.PokerusStrain = cepa;
        var max = Pokerus.GetMaxDuration(cepa);
        pk.PokerusDays = Math.Min(max, dias);
        if (cepa != 0 && pk.PokerusDays == 0 && !pk.IsPokerusCured) pk.PokerusDays = 1;
    }

    // ---- Juego de origen (UpdateOriginGame / CheckMetLocationChange) ----
    /// <summary>Cambia la version de origen; si pasa a otro grupo de juegos, el lugar de encuentro y el del huevo se ponen como sugiere el Core (la lista de lugares es otra).</summary>
    public static void CambiarVersion(PKM pk, GameVersion version, SaveFile sav)
    {
        if (version is not 0 && !version.IsValidSavedVersion()) return;
        var antes = GameUtil.GetMetLocationVersionGroup(pk.Version);
        pk.Version = version;
        var despues = GameUtil.GetMetLocationVersionGroup(pk.Version);
        if (antes != despues && pk.Format >= 3)
        {
            var lugar = EncounterSuggestion.TryGetSuggestedTransferLocation(pk);
            pk.MetLocation = (ushort)Math.Max((ushort)0, lugar);
            if (pk.Format >= 4)
                pk.EggLocation = EncounterStateUtil.IsMetAsEgg(pk) ? EncounterSuggestion.GetSuggestedEncounterEggLocationEgg(pk, true) : LocationEdits.GetNoneLocation(pk);
        }
        if (pk is IGroundTile g && !pk.Gen4) g.GroundTile = GroundTileType.None;
    }

    // ---- PreparePKM (lo que PKHeX hace al leer los campos) ----
    /// <summary>Movimientos sin huecos, movimientos recordados y recuerdos coherentes (<c>FixRelearn</c>, <c>FixMemories</c>) y el checksum recalculado, segun el formato.</summary>
    public static void Preparar(PKM pk)
    {
        pk.FixMoves();
        switch (pk)
        {
            case PK6 a: a.FixRelearn(); a.FixMemories(); break;
            case PK7 a: a.FixRelearn(); a.FixMemories(); break;
            case PB7 a: a.FixRelearn(); a.FixMemories(); break;
            case PK8 a: a.FixRelearn(); a.FixMemories(); break;
            case PB8 a: a.FixRelearn(); a.FixMemories(); break;
            case PA8 a: a.FixRelearn(); a.FixMemories(); break;
            case PK9 a: a.FixRelearn(); a.FixMemories(); break;
            case PA9 a: a.FixRelearn(); a.FixMemories(); break;
        }
        pk.RefreshChecksum();
    }

    // ---- Lugar de encuentro sugerido (SetSuggestedMetLocation) ----
    /// <summary>Devuelve el texto del cambio sugerido (para mostrarlo en el aviso) o null si el Core no sugiere nada o ya esta como sugiere.</summary>
    public static string? AplicarLugarSugerido(PKM pk)
    {
        var encuentro = EncounterSuggestion.GetSuggestedMetInfo(pk);
        if (encuentro is null || (pk.Format >= 3 && encuentro.Location == 0)) return null;
        var nivel = encuentro.LevelMin;
        int minimo = EncounterSuggestion.GetLowestLevel(pk, nivel);
        if (minimo == 0) minimo = nivel;
        ushort lugar = encuentro.Location;
        if (pk.Format < 3 && encuentro.Encounter is { } x && !x.Version.Contains(GameVersion.C)) lugar = 0;
        if (pk.CurrentLevel >= minimo && pk.MetLevel == nivel && pk.MetLocation == lugar)
        {
            if (!encuentro.HasGroundTile(pk.Format) || (pk is IGroundTile g && g.GroundTile == encuentro.GetSuggestedGroundTile())) return null;
        }
        if (minimo < nivel) minimo = nivel;
        pk.MetLocation = lugar;
        pk.MetLevel = encuentro.GetSuggestedMetLevel(pk);
        if (pk.Format >= 3)
        {
            if (encuentro.HasGroundTile(pk.Format) && pk is IGroundTile gt) gt.GroundTile = encuentro.GetSuggestedGroundTile();
            if (pk is { Gen6: true, WasEgg: true }) pk.SetHatchMemory6();
        }
        else if (pk is ICaughtData2 c2) c2.MetTimeOfDay = lugar == 0 ? 0 : encuentro.GetSuggestedMetTimeOfDay();
        if (pk.CurrentLevel < minimo) PonerNivel(pk, minimo);
        return IdiomaInterfaz.T("Reglas.LugarNivelEncuentroSugeridos");
    }

    // ---- Movimientos ----
    public static void PonerMovimiento(PKM pk, int indice, ushort movimiento)
    {
        pk.SetMove(indice, movimiento);
        CurarPp(pk, indice);
    }

    public static void CurarPp(PKM pk, int indice)
    {
        var mov = pk.GetMove(indice);
        var ups = LeerPpUps(pk, indice);
        if (mov == 0) { EscribirPpUps(pk, indice, 0); ups = 0; EscribirPp(pk, indice, 0); return; }
        EscribirPp(pk, indice, pk.GetMovePP(mov, ups));
    }

    public static int LeerPp(PKM pk, int i) => i switch { 0 => pk.Move1_PP, 1 => pk.Move2_PP, 2 => pk.Move3_PP, _ => pk.Move4_PP };
    public static int LeerPpUps(PKM pk, int i) => i switch { 0 => pk.Move1_PPUps, 1 => pk.Move2_PPUps, 2 => pk.Move3_PPUps, _ => pk.Move4_PPUps };
    public static void EscribirPp(PKM pk, int i, int v) { switch (i) { case 0: pk.Move1_PP = v; break; case 1: pk.Move2_PP = v; break; case 2: pk.Move3_PP = v; break; default: pk.Move4_PP = v; break; } }
    public static void EscribirPpUps(PKM pk, int i, int v) { switch (i) { case 0: pk.Move1_PPUps = v; break; case 1: pk.Move2_PPUps = v; break; case 2: pk.Move3_PPUps = v; break; default: pk.Move4_PPUps = v; break; } }

    public static int MaxPpUps(PKM pk, int i) => Legal.IsPPUpAvailable(pk) && Legal.IsPPUpAvailable(pk.GetMove(i)) ? 3 : 0;

    /// <summary>«PP Ups» de la cabecera de PKHeX: 3 en cada movimiento que los admite (o 0 si ya estan al maximo en todos / el formato no los tiene).</summary>
    public static void PonerPpUpsTodos(PKM pk, bool minimo)
    {
        var quitar = minimo || !Legal.IsPPUpAvailable(pk);
        for (var i = 0; i < 4; i++)
        {
            EscribirPpUps(pk, i, quitar ? 0 : (Legal.IsPPUpAvailable(pk.GetMove(i)) ? 3 : 0));
            CurarPp(pk, i);
        }
    }

    public static void CurarTodosLosPp(PKM pk) { for (var i = 0; i < 4; i++) CurarPp(pk, i); }

    // ---- Estadisticas ----
    public static void PonerIv(PKM pk, int indice, int valor)
    {
        pk.SetIV(indice, Math.Clamp(valor, 0, pk.MaxIV));
        if (pk is IGanbaru g) { _ = g; }
    }

    public static void PonerEv(PKM pk, int indice, int valor) => pk.SetEV(indice, Math.Clamp(valor, 0, pk.MaxEV));

    /// <summary>Recalcula las estadisticas de combate guardadas (equipo) con las del Core, como <c>StatEditor.UpdateStats</c> (salvo que se editen a mano en el modo avanzado).</summary>
    public static void RecalcularStats(PKM pk)
    {
        var pi = pk.PersonalInfo;
        Span<ushort> stats = stackalloc ushort[6];
        pk.LoadStats(pi, stats);
        pk.SetStats(stats);
    }
}
