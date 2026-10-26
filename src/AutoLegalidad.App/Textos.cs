// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace AutoLegalidad.Ui;

/// <summary>Todos los textos de la interfaz de Pokekeep, en castellano de España (Hito 4). Un único sitio para revisar el idioma.</summary>
public static class Textos
{
    // ---- Acciones ----
    public static string LegalizarAutomaticamente => AppIdioma.T("Legalizar automáticamente", "Legalize automatically");
    public static string OptimizarStats => AppIdioma.T("Optimizar stats", "Optimize stats");
    public static string LegalizarTooltip => AppIdioma.T("Convierte este Pokémon en uno legal para la partida abierta, conservando su especie. Después optimiza los IV dentro de lo legal.", "Turns this Pokémon into a legal one for the open save, keeping its species. Then it optimizes the IVs within what is legal.");
    public static string OptimizarTooltip => AppIdioma.T("Mejora los stats (IV, entrenamiento) al máximo que permite su encuentro, sin cambiar ningún dato de origen. Solo para Pokémon ya legales.", "Improves the stats (IVs, training) to the maximum its encounter allows, without changing any origin data. Only for Pokémon that are already legal.");
    public const string MenuPokekeep = "Pokekeep";
    public static string CompletarElJuego => AppIdioma.T("Completar el juego", "Complete the game");
    public static string EdicionMasiva => AppIdioma.T("Edición masiva", "Bulk edit");
    public static string LibreriaPestana => AppIdioma.T("Librería", "Library");
    public static string LibreriaMenu => AppIdioma.T("Librería de Pokekeep", "Pokekeep Library");
    public static string LibreriaVentana => AppIdioma.T("Abrir en ventana", "Open in window");

    // ---- Barra de operación ----
    public static string Analizando => AppIdioma.T("Analizando", "Analyzing");
    public static string BuscandoOrigen => AppIdioma.T("Buscando un origen válido", "Searching for a valid origin");
    public static string Comprobando => AppIdioma.T("Comprobando", "Checking");
    public static string Aplicando => AppIdioma.T("Aplicando", "Applying");
    public static string Optimizando => AppIdioma.T("Optimizando stats", "Optimizing stats");
    public static string Cancelar => AppIdioma.T("Cancelar", "Cancel");
    public static string Cancelando => AppIdioma.T("Cancelando…", "Canceling…");
    public static string VerCambios => AppIdioma.T("Ver cambios", "View changes");
    public static string Deshacer => AppIdioma.T("Deshacer", "Undo");
    public static string Cerrar => AppIdioma.T("Cerrar", "Close");
    public static string CambiosTitulo => AppIdioma.T("Cambios de Pokekeep", "Pokekeep changes");

    // ---- Resultados cortos ----
    public static string Legalizado => AppIdioma.T("Legalizado", "Legalized");
    public static string StatsOptimizados => AppIdioma.T("Stats optimizados", "Stats optimized");
    public static string YaEsLegal => AppIdioma.T("Este Pokémon ya es legal: no se ha cambiado nada.", "This Pokémon is already legal: nothing was changed.");
    public static string SinMargen => AppIdioma.T("Sin margen de mejora: los stats ya son los mejores posibles para su encuentro.", "No room for improvement: the stats are already the best possible for its encounter.");
    public static string PrimeroLegalizar => AppIdioma.T("Primero hay que legalizarlo: usa «Legalizar automáticamente».", "It has to be legalized first: use “Legalize automatically”.");
    public static string OperacionCancelada => AppIdioma.T("Operación cancelada. No se ha cambiado nada.", "Operation canceled. Nothing was changed.");
    public static string AnadidoALaCaja => AppIdioma.T("Añadido a la caja", "Added to the box");
    public static string PokedexRegistrada => AppIdioma.T("Pokédex: registrado como visto y capturado (Deshacer devuelve el Pokémon, pero no borra este registro).", "Pokédex: registered as seen and caught (Undo returns the Pokémon but doesn’t erase this registration).");
    public static string DeshechoCorrectamente => AppIdioma.T("Cambio deshecho.", "Change undone.");
    public static string CerrarResumen => AppIdioma.T("Cerrar el resumen", "Close the summary");
    public static string CerrarResumenAyuda => AppIdioma.T("Cierra este resumen (lo hecho se mantiene; sigue pudiéndose deshacer desde Edición > Deshacer).", "Closes this summary (what was done is kept; it can still be undone from Edit > Undo).");

    // ---- Pokémon que necesitan pasar por HOME (R-009) ----
    /// <summary>Nombre EXACTO con el que el ajuste sale en Opciones &gt; Ajustes &gt; Pokekeep (0.6.6, B1: <see cref="PokekeepSettings.AllowHomeTrackerless"/> usa esta misma constante como <c>DisplayName</c>, así los textos nunca mandan a un ajuste que no existe).</summary>
    public const string AjusteHome = "Sin HOME (con riesgo)";
    /// <summary>El nombre del ajuste tal como se ve en cada idioma (la constante es el nombre en castellano: la usan atributos).</summary>
    public static string AjusteHomeTexto => AppIdioma.T(AjusteHome, "Without HOME (risky)");
    public static string NecesitaHome => AppIdioma.T("Necesita pasar por HOME", "Needs to pass through HOME");
    /// <summary>0.6.6 (M4): el motor devolvió el mismo Pokémon (ya era legal salvo la matrícula de HOME): no se escribe nada.</summary>
    /// <summary>0.6.6 (B7): la vista previa propuso una versión de evento que necesita pasar por HOME, pero el ajuste se desactivó antes de aplicar.</summary>
    public static string EventoNoAplicadaAjusteHome => AppIdioma.F("esa versión de evento necesita pasar por HOME y el ajuste «{0}» está desactivado ahora (estaba activado al preparar la vista previa). Se queda como estaba.",
        "that event version needs to pass through HOME and the “{0}” setting is now off (it was on when the preview was prepared). It stays as it was.", AjusteHomeTexto);
    public static string NecesitaHomeSinCambios => AppIdioma.T("Necesita pasar por HOME · no se ha cambiado nada", "Needs to pass through HOME · nothing was changed");
    /// <summary>0.6.6 (M6): la partida no tiene nombre de entrenador. Pokekeep no pone «PKHeX» ni ningún otro nombre inventado.</summary>
    public static string SinNombreDeEntrenador => AppIdioma.T("La partida no tiene nombre de entrenador. Ponlo en PKHeX (editor del entrenador) antes de usar Pokekeep: sin él PKHeX da por inválido cualquier Pokémon cuyo entrenador original eres tú, y Pokekeep no se inventa un nombre.", "The save has no trainer name. Set it in PKHeX (trainer editor) before using Pokekeep: without it PKHeX considers any Pokémon whose original trainer is you invalid, and Pokekeep doesn’t make up a name.");
    public static string NecesitaHomeAviso => AppIdioma.T("Necesita pasar por HOME: este Pokémon lleva la matrícula (rastreador) en blanco y PKHeX lo marcará como inválido hasta que pase por HOME, que se la asigna. Llévalo a HOME desde este juego. Pokekeep nunca se inventa esa matrícula y no puede comprobar qué harán con él los servidores de Nintendo.", "Needs to pass through HOME: this Pokémon has a blank tracker and PKHeX will mark it invalid until it passes through HOME, which assigns one. Take it to HOME from this game. Pokekeep never makes up that tracker and can’t check what Nintendo’s servers will do with it.");
    public static string NecesitaHomeSinAjuste => AppIdioma.F("Necesita pasar por HOME: solo existe con la matrícula que asigna Pokémon HOME, que Pokekeep nunca se inventa. Para generarlo en blanco (con riesgo) activa «{0}» en Opciones > Ajustes > Pokekeep.",
        "Needs to pass through HOME: it only exists with the tracker that Pokémon HOME assigns, which Pokekeep never makes up. To generate it with a blank tracker (risky) turn on “{0}” in Options > Settings > Pokekeep.", AjusteHomeTexto);
    public static string NecesitaHomeConAjuste => AppIdioma.T("Necesita pasar por HOME: se generará con la matrícula en blanco y PKHeX lo marcará como inválido hasta que lo pases por HOME.", "Needs to pass through HOME: it will be generated with a blank tracker and PKHeX will mark it invalid until you pass it through HOME.");

    // ---- Motivos por los que una acción está desactivada ----
    public static string SinPartida => AppIdioma.T("No hay una partida abierta con un juego conocido: abre un guardado primero.", "There is no open save with a known game: open a save first.");
    public static string CasillaVacia => AppIdioma.T("La casilla está vacía.", "The slot is empty.");
    public static string CasillaBloqueada => AppIdioma.T("La casilla está bloqueada y no se puede modificar.", "The slot is locked and can’t be modified.");
    public static string OperacionEnCurso => AppIdioma.T("Hay otra operación de Pokekeep en curso.", "Another Pokekeep operation is in progress.");
    public static string SinEspecie => AppIdioma.T("El editor no tiene ningún Pokémon.", "The editor has no Pokémon.");
    public static string EditorIncompleto => AppIdioma.T("Termina de editar los campos del Pokémon antes de usar Pokekeep.", "Finish editing the Pokémon’s fields before using Pokekeep.");
    public static string MotorNoEncontrado => AppIdioma.T("No se encuentra el motor de Pokekeep (pokekeep-worker.exe). Revisa Opciones > Ajustes > Pokekeep.", "The Pokekeep engine (pokekeep-worker.exe) was not found. Check Options > Settings > Pokekeep.");
    public static string CatalogoNoEncontrado => AppIdioma.T("No se encuentra la base de datos de Pokekeep (autolegalidad.sqlite). Revisa Opciones > Ajustes > Pokekeep.", "The Pokekeep database (autolegalidad.sqlite) was not found. Check Options > Settings > Pokekeep.");

    // ---- Errores de aplicación ----
    public static string CasillaCambio => AppIdioma.T("La casilla cambió mientras se trabajaba en ella: el resultado se ha descartado y no se ha modificado nada.", "The slot changed while it was being worked on: the result was discarded and nothing was modified.");
    public static string EditorCambio => AppIdioma.T("El Pokémon del editor cambió mientras se trabajaba en él: el resultado se ha descartado y no se ha modificado nada.", "The editor’s Pokémon changed while it was being worked on: the result was discarded and nothing was modified.");
    public static string PartidaCambio => AppIdioma.T("Se abrió o se cerró otra partida mientras se trabajaba: el resultado se ha descartado y no se ha modificado nada.", "Another save was opened or closed while working: the result was discarded and nothing was modified.");
    public static string CasillaOcupada => AppIdioma.T("La casilla ya no está libre: no se ha sobrescrito nada.", "The slot is no longer free: nothing was overwritten.");
    public static string ValidacionFallida => AppIdioma.T("El resultado no superó la comprobación final y se ha deshecho: tu Pokémon sigue como estaba.", "The result did not pass the final check and was undone: your Pokémon is as it was.");
    public static string NoHayHueco => AppIdioma.T("No quedan casillas libres en la caja activa. Cambia de caja o libera una casilla: Pokekeep nunca sobrescribe.", "There are no free slots in the active box. Change box or free a slot: Pokekeep never overwrites.");
    public static string UndoNoPosible => AppIdioma.T("No se puede deshacer: el Pokémon ya no está como lo dejó Pokekeep.", "It can’t be undone: the Pokémon is no longer as Pokekeep left it.");

    // ---- Operaciones masivas (Hito 4b) ----
    public static string CasillaOcupadaMientras => AppIdioma.T("La casilla se ocupó mientras se generaba y no quedó ninguna libre para colocarlo: no se ha sobrescrito nada.", "The slot was taken while generating and none was left free to place it: nothing was overwritten.");
    public static string CasillaCambioMientras => AppIdioma.T("Has cambiado esta casilla mientras se procesaba: no se ha aplicado nada en ella.", "You changed this slot while it was being processed: nothing was applied to it.");
    public static string NadaModificado => AppIdioma.T("No se ha modificado nada.", "Nothing was modified.");
    public static string Duracion(TimeSpan t) => t.TotalSeconds < 90 ? $"{Math.Max(1, (int)Math.Round(t.TotalSeconds))} s" : $"{Math.Max(1, (int)Math.Round(t.TotalMinutes))} min";
    public static string DeshacerLoteNegado(int cambiadas)
        => AppIdioma.F("Has cambiado {0} de las casillas después: no se puede deshacer todo el lote de una vez. Deshazlo con Edición > Deshacer de PKHeX o a mano.", "You changed {0} of the slots afterwards: the whole batch can’t be undone at once. Undo it with PKHeX’s Edit > Undo or by hand.", cambiadas);

    public static string OrigenDeLaLibreria(AutoLegalidad.Catalog.Library.LibraryRow row, string? encounterType)
        => AppIdioma.F("Origen: {0}", "Origin: {0}", Library.LibraryNames.Origin(row)) + (row.Kind == "Event" ? $" · {Library.LibraryNames.Provenance(row)}" : string.Empty) + (encounterType is { Length: > 0 } t ? $" ({t})" : string.Empty) + ".";

    public static string EspecieNoEnDestino(string especie, string juego) => AppIdioma.F("{0} no existe en {1}: no hay ninguna corrección que conserve la especie. El Pokémon queda intacto.", "{0} doesn’t exist in {1}: there is no correction that keeps the species. The Pokémon is left untouched.", especie, juego);
    public static string Porcentaje(double fraccion) => $"{Math.Clamp(fraccion * 100, 0, 100):0} %";
    public static string Segundos(TimeSpan t) => t.TotalSeconds < 10 ? $"{t.TotalSeconds:0.0} s" : $"{t.TotalSeconds:0} s";
    public static string CajaCasilla(int caja, int casilla) => AppIdioma.F("Caja {0}, casilla {1}", "Box {0}, slot {1}", caja + 1, casilla + 1);
}
