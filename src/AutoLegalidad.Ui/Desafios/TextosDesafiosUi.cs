// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Desafios;

namespace AutoLegalidad.Ui.Desafios;

/// <summary>
/// Textos de la interfaz de escritorio de R-013 (botones, títulos y mensajes), en español de España y en inglés. Los que dependen de la lógica (nombres de modos, campos, motivos, jefes) vienen ya traducidos de
/// <see cref="Textos"/> en AutoLegalidad.Desafios; aquí solo está lo que añade la interfaz. El idioma lo decide quien crea el control (el escritorio lo toma del idioma de PKHeX), no <c>AppIdioma</c>: el
/// parche de escritorio sigue el selector de idioma de PKHeX y <c>AppIdioma</c> se queda en castellano.
/// </summary>
public static class TextosDesafiosUi
{
    /// <summary>true si el idioma de PKHeX es el español (<c>es</c> o <c>es-419</c>).</summary>
    public static bool EsEspanol(string idioma) => idioma.StartsWith("es", StringComparison.OrdinalIgnoreCase);

    private static string T(bool es, string e, string i) => es ? e : i;

    public static string IrAlJefe(bool es) => T(es, "Ir al jefe", "Go to the boss");
    public static string Maximo(bool es) => T(es, "Máximo", "Maximum");
    public static string TodoAlJefe(bool es) => T(es, "Todo al jefe", "All to the boss");
    public static string TodoAlMaximo(bool es) => T(es, "Todo al máximo", "All to maximum");
    public static string TodosAlJefe(bool es) => T(es, "Todos al jefe", "All modes to the boss");
    public static string TodosAlMaximo(bool es) => T(es, "Todos al máximo", "All modes to maximum");
    public static string Jefe(bool es) => T(es, "Jefe:", "Boss:");
    public static string Desafios(bool es) => T(es, "Desafíos", "Challenges");
    public static string DesafiosPuntos(bool es) => T(es, "Desafíos…", "Challenges…");
    public static string TituloEditor(bool es) => T(es, "Desafíos y rachas", "Challenges and streaks");
    public static string Guardar(bool es) => T(es, "Guardar", "Save");
    public static string Cancelar(bool es) => T(es, "Cancelar", "Cancel");
    public static string Racha(bool es) => T(es, "Racha", "Streak");
    public static string Record(bool es) => T(es, "Récord", "Record");
    public static string YaEstabaAsi(bool es) => T(es, "ya estaba así, no hay nada que cambiar.", "already like that, nothing to change.");
    public static string SinInstalaciones(bool es) => T(es, "Esta partida no tiene desafíos de este tipo.", "This save has no challenges of this kind.");
    public static string EstadoInicial(bool es) => T(es, "Elegid «Ir al jefe» (deja la racha justo antes del combate con el jefe) o «Máximo» (el tope que admite el juego).", "Choose \"Go to the boss\" (leaves the streak right before the boss battle) or \"Maximum\" (the highest value the game accepts).");

    public static string Resumen(bool es, int aplicados, int sinCambios, int noDisponibles) =>
        es ? $"{Plural(aplicados, "modo cambiado", "modos cambiados")}, {Plural(sinCambios, "sin cambios", "sin cambios")}, {Plural(noDisponibles, "no disponible", "no disponibles")}."
           : $"{aplicados} {(aplicados == 1 ? "mode changed" : "modes changed")}, {sinCambios} unchanged, {noDisponibles} unavailable.";

    private static string Plural(int n, string singular, string plural) => $"{n} {(n == 1 ? singular : plural)}";

    // ---- Insignias del Pokéathlon ----
    public static string InsigniasTitulo(bool es) => T(es, "Insignias del Pokéathlon", "Pokéathlon medals");
    public static string DarLasCinco(bool es) => T(es, "Dar las cinco", "Give all five");
    public static string InsigniasSoloHgss(bool es) => T(es, "Solo en HeartGold y SoulSilver, para las especies 1 a 493.", "Only in HeartGold and SoulSilver, for species 1 to 493.");

    // ---- Selector de Pokémon ----
    public static string ElegirPokemon(bool es) => T(es, "Elegir Pokémon…", "Choose Pokémon…");
    public static string SelectorTitulo(bool es) => T(es, "Elegir un Pokémon de la partida", "Choose a Pokémon from the save");
    public static string Buscar(bool es) => T(es, "Buscar por nombre:", "Search by name:");
    public static string ColPokemon(bool es) => T(es, "Pokémon", "Pokémon");
    public static string ColUbicacion(bool es) => T(es, "Caja", "Box");
    public static string ColCasilla(bool es) => T(es, "Casilla", "Slot");
    public static string ColNivel(bool es) => T(es, "Nv.", "Lv.");
    public static string Equipo(bool es) => T(es, "Equipo", "Party");
    public static string Elegir(bool es) => T(es, "Elegir", "Choose");
    public static string NingunPokemon(bool es) => T(es, "No hay ningún Pokémon que coincida.", "No Pokémon matches.");
    public static string Cuenta(bool es, int mostrados, int total) => es ? $"{mostrados} de {total} Pokémon" : $"{mostrados} of {total} Pokémon";

    // ---- Pokéathlon ----
    public static string TodoAlMaximoPokeathlon(bool es) => T(es, "Todo al máximo", "All to maximum");

    /// <summary>Texto en la lengua pedida de un texto de la biblioteca.</summary>
    public static string De(bool es, TextoDoble t) => t.Para(es);
}
