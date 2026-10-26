// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;

namespace Pokekeep.Mobile.Ui.Herramientas.Banco;

/// <summary>
/// El inglés de <see cref="TextosBanco"/> (X38): los mismos miembros, con los mismos nombres, tipos y argumentos. Una prueba (<c>TextosBancoX38Tests</c>) exige que no falte ni sobre ninguno y que ningún texto quede igual que el
/// español salvo los pocos que son iguales en los dos idiomas. Los términos del juego (especies, movimientos, habilidades, objetos, tipos, naturalezas y juegos) no están aquí: salen del Core en el idioma de los datos.
/// Cuando X36 (cambio de idioma en vivo) esté integrado, este fichero pasa a sus recursos de claves estables sin cambiar el significado de ningún texto.
/// </summary>
public static class TextosBancoEn
{
    // ---- Screen and "More" entry ----
    public const string Titulo = "Pokémon Bank";
    public const string Subtitulo = "Your own bank, outside any save: keep copies of your Pokémon, sort them and take them out to another save with their legality verdict. Everything stays on the device.";
    public const string EntradaMas = "Your Pokémon stored outside the saves: favorites, tags, recycle bin and progress";
    public const string Volver = "Back";
    public const string Cancelar = "Cancel";
    public const string Trabajando = "Working";
    public const string Si = "Yes";
    public const string No = "No";
    public const string Ninguno = "None";
    public const string NoAplica = "Not applicable";
    public const string NoConsta = "Unknown";
    public const string Huevo = "Egg";

    // ---- Where it is stored ----
    public const string EtiquetaDonde = "Where it is stored";
    public const string UbicacionPrivada = "The app's private folder";
    public const string AvisoCarpetaPrivada = "The bank is now in the app's private folder: if you uninstall the app, it is deleted. Choose a folder of your own so it survives.";
    public const string ElegirCarpeta = "Choose folder";
    public const string ElegirCarpetaAyuda = "Opens the system picker; whatever you already have stored moves to that folder and, if a bank was already there, it is recovered";
    public const string SinSelector = "This device does not allow choosing a folder.";
    public const string TrasladandoBanco = "Moving the bank…";
    public const string SinCambiosCarpeta = "No folder was chosen. The bank stays where it was.";
    public const string TrasladoConFallos = "Some Pokémon could not be moved.";
    public const string TrasladoNoComprobado = "Could not check that everything was in the new folder.";
    public static string DondeEsta(string descripcion, bool carpetaDelUsuario)
        => carpetaDelUsuario ? $"Chosen folder: “{descripcion}”." : $"{descripcion}.";
    public const string NotaFormato = "Each Pokémon is a standard .pk* file that desktop PKHeX can also open. If the bank index gets damaged, it is rebuilt from the files by itself.";
    public static string CarpetaPerdida(string nombre)
        => $"The folder “{nombre}” is no longer available (its permission was removed or it was deleted). Meanwhile the bank uses the app's private folder, which does not hold what you stored there. Choose the folder again to get it back.";
    public static string FalloCarpeta(string carpeta, string motivo) => $"The folder “{carpeta}” could not be used: {motivo} The bank stays where it was and nothing has been deleted.";
    public static string CarpetaElegida(string carpeta, int yaHabia, int trasladados)
    {
        var sb = new StringBuilder($"The bank is now in “{carpeta}”.");
        if (yaHabia > 0) sb.Append($" A bank with {Cantidad(yaHabia)} was found.");
        if (trasladados > 0) sb.Append($" {Cantidad(trasladados)} moved over.");
        return sb.ToString();
    }
    public static string ResumenCarga(InformeCarga i)
    {
        var p = new List<string>();
        if (i.ErrorDeposito is not null) p.Add($"The bank folder could not be read: {i.ErrorDeposito}");
        if (i.IndiceReconstruido) p.Add("The bank index could not be read and has been rebuilt from the files (favorites, tags and recycle bin could not be recovered).");
        else if (i.IndiceDeLaCopia) p.Add("The bank index could not be read and has been recovered from its previous copy.");
        if (i.Reconstruidas > 0 && !i.IndiceReconstruido) p.Add($"{Cantidad(i.Reconstruidas)} that were not in the index were added from their files.");
        if (i.Perdidas > 0) p.Add($"{Cantidad(i.Perdidas)} were removed from the list because their file is gone or cannot be read.");
        if (i.Actualizadas > 0) p.Add($"{Cantidad(i.Actualizadas)} were read again because their file had changed.");
        if (i.Ilegibles.Count > 0) p.Add($"{i.Ilegibles.Count} file(s) in the folder cannot be read as Pokémon and were left untouched: {string.Join(", ", i.Ilegibles.Take(3))}{(i.Ilegibles.Count > 3 ? "…" : string.Empty)}.");
        return string.Join(" ", p);
    }
    public const string CargandoBanco = "Loading the bank…";
    public const string CargaCancelada = "Loading cancelled. The bank was not touched.";
    public const string Reintentar = "Try loading again";
    public const string BancoNoLeido = "The bank could not be read; nothing was changed.";

    // ---- Tabs ----
    public static string PestanaPokemon(int n) => $"Pokémon ({n})";
    public static string PestanaPapelera(int n) => $"Recycle bin ({n})";
    public static string PestanaDuplicados(int n) => $"Duplicates ({n})";
    public const string PestanaProgreso = "Progress";

    // ---- List, search and filters ----
    public const string BancoVacioTexto = "The bank is empty. Open a save, long-press a Pokémon in the boxes and choose “Save to the bank” (or select several).";
    public const string BuscarMarca = "Name or tag";
    public const string BuscarAccesible = "Search the bank";
    public const string Buscar = "Search";
    public const string MostrarFiltros = "Filters";
    public const string OcultarFiltros = "Hide filters";
    public const string QuitarFiltros = "Clear filters";
    public const string SoloFavoritos = "Favorites only";
    public const string ElegirTodas = "Select all";
    public const string QuitarSeleccion = "Clear selection";
    public const string SinCoincidenciasTexto = "No Pokémon match those filters.";
    public const string CualquierTipo = "Any type";
    public const string CualquierJuego = "Any game";
    public const string CualquierEspecie = "Any species";
    public const string CualquierEtiqueta = "Any tag";
    public const string ElegirTipo = "Filter by type";
    public const string ElegirJuego = "Filter by origin game";
    public const string ElegirEspecie = "Filter by species";
    public const string ElegirEtiqueta = "Filter by tag";
    public const string OrdenarPor = "Sort by";
    public const string OrdenReciente = "Newest first";
    public const string OrdenPokedex = "Pokédex number";
    public const string OrdenNombre = "Name";
    public const string OrdenNivel = "Level, highest first";
    public static string Cantidad(int n) => n == 1 ? "1 Pokémon" : $"{n} Pokémon";
    public static string CantidadFiltrada(int mostrados, int total) => $"{mostrados} of {total} Pokémon";
    public static string MostrarMas(int n, int restantes) => $"Show {n} more ({restantes} not shown)";
    public static string FiltroTipo(string? t) => t is null ? "Type: any" : $"Type: {t}";
    public static string FiltroJuego(string? j) => j is null ? "Game: any" : $"Game: {j}";
    public static string FiltroEspecie(string? e) => e is null ? "Species: any" : $"Species: {e}";
    public static string FiltroEtiqueta(string? e) => e is null ? "Tag: any" : $"Tag: {e}";
    public static string FiltroVariocolor(Tri3 v) => v switch { Tri3.Si => "Shiny: yes", Tri3.No => "Shiny: no", _ => "Shiny: any" };
    public static string FiltroLegalidad(Tri3 v) => v switch { Tri3.Si => "Legality: legal", Tri3.No => "Legality: illegal", _ => "Legality: any" };
    public static string FiltroOrden(OrdenBanco o) => o switch { OrdenBanco.Pokedex => "Sort: Pokédex", OrdenBanco.Nombre => "Sort: name", OrdenBanco.Nivel => "Sort: level", _ => "Sort: newest" };

    // ---- Rows ----
    public const string Legal = "Legal";
    public const string RevisarLegalidad = "Check legality";
    public const string SinAnalizar = "Not analyzed";
    public const string Variocolor = "Shiny";
    public const string QuitarFavorito = "Remove from favorites";
    public const string PonerFavorito = "Mark as favorite";
    public static string DetalleFila(string especie, string forma, int nivel, int generacion, string juego)
        => string.Join(" · ", new[] { forma.Length > 0 ? (especie.Length > 0 ? $"{especie} ({forma})" : forma) : especie, $"Lv. {nivel}", generacion > 0 ? $"Gen. {generacion}" : string.Empty, juego }.Where(p => p.Length > 0));
    public static string DetalleHuevo(int generacion) => generacion > 0 ? $"Egg · Gen. {generacion}" : "Egg";
    public static string FavoritoAccesible(string titulo, bool favorito) => favorito ? $"{titulo}, favorite. Remove from favorites" : $"{titulo}. Mark as favorite";
    public static string FilaAccesible(string titulo, string subtitulo, string legal, bool variocolor, bool favorito, bool elegida, string etiquetas)
        => $"{titulo}, {subtitulo}. {legal}.{(variocolor ? " Shiny." : string.Empty)}{(favorito ? " Favorite." : string.Empty)}{(etiquetas.Length > 0 ? " Tags: " + etiquetas + "." : string.Empty)} {(elegida ? "Selected" : "Not selected")}";
    public static string EliminadoEl(DateTime d) => $"In the recycle bin since {d:yyyy-MM-dd}";
    public static string GuardadoEl(DateTime d) => $"Saved on {d:yyyy-MM-dd}";

    // ---- Actions on the selection ----
    public const string SacarALaPartida = "Take out to the save";
    public const string SacarAyuda = "Converts the selected ones to the open save's game, checks their legality and places them in empty slots; a plan is shown first";
    public const string SacarSinPartida = "Open a save to take Pokémon out of the bank.";
    public const string Comparar = "Compare";
    public const string CompararAyuda = "Compares the two selected Pokémon";
    public const string CompararNecesitaDos = "To compare, select exactly two Pokémon.";
    public const string EtiquetasTitulo = "Tags for the selection";
    public const string EtiquetaMarca = "New tag";
    public const string EtiquetaAccesible = "Tag text";
    public const string AnadirEtiqueta = "Add tag";
    public const string QuitarEtiquetaBoton = "Remove tag";
    public const string EscribeEtiqueta = "Type the tag first.";
    public const string PonerFavoritos = "Mark favorites";
    public const string QuitarFavoritos = "Remove favorites";
    public const string ElegirAntes = "Select some Pokémon first.";
    public const string AlaPapelera = "Send to the recycle bin";
    public const string AlaPapeleraAyuda = "Deletes nothing: it can be restored from the Recycle bin tab";
    public static string Elegidas(int n) => n == 0 ? "None selected" : n == 1 ? "1 selected" : $"{n} selected";
    public static string EtiquetaAnadida(string t, int n) => $"Tag “{t}” added to {Cantidad(n)}.";
    public static string EtiquetaQuitada(string t, int n) => $"Tag “{t}” removed from {Cantidad(n)}.";
    public static string PuestoFavorito(string t) => $"{t} is now a favorite.";
    public static string QuitadoFavorito(string t) => $"{t} is no longer a favorite.";
    public static string PuestosFavoritos(int n) => $"{Cantidad(n)} marked as favorites.";
    public static string QuitadosFavoritos(int n) => $"{Cantidad(n)} removed from favorites.";
    public static string EnviadosAPapelera(int n) => $"{Cantidad(n)} sent to the recycle bin. They can be restored from the Recycle bin tab.";
    public static string FalloAlGuardar(string motivo) => $"The change could not be saved in the bank: {motivo}";

    // ---- Saving from the save (slot and selection menus) ----
    public const string GrupoMenu = "Pokémon Bank";
    public const string GuardarEnElBanco = "Save to the bank";
    public const string MoverAlBanco = "Move to the bank";
    public const string NoDisponibleAqui = "Not available in this window.";
    public const string SoloLecturaNoSeQuita = "Read-only: it cannot be removed from here. Use “Save to the bank”.";
    public const string MoverTitulo = "Move to the bank";
    public const string MoverAyuda = "Only the ones that end up stored in the bank are removed from the save. You can undo it with “Undo”.";
    public const string MoverConfirmar = "Move";
    public const string NadaQueGuardar = "There is no Pokémon to save in those slots.";
    public const string AbrirElBanco = "Open the bank";
    public const string PokemonVacio = "That slot is empty.";
    public const string NoEstaEnElBanco = "That Pokémon is no longer in the bank.";
    public static string GuardarEnElBancoAyuda(int n) => n == 1 ? "Saves a copy of this Pokémon; the one in the save is not touched" : $"Saves a copy of {Cantidad(n)}; the ones in the save are not touched";
    public static string MoverAlBancoAyuda(int n) => n == 1 ? "Saves it in the bank and removes it from the save; can be undone" : $"Saves {Cantidad(n)} in the bank and removes them from the save; can be undone";
    public static string MoverTexto(int n) => $"A copy of {Cantidad(n)} will be saved in the bank and they will be removed from the save.";
    public static string ArchivoNoEncontrado(string nombre) => $"The bank file “{nombre}” cannot be found.";
    public static string ArchivoIlegible(string nombre) => $"The file “{nombre}” cannot be read as a Pokémon.";
    public static string ResumenGuardado(InformeGuardadoBanco i, bool mover)
    {
        var p = new List<string>();
        if (i.Guardados > 0) p.Add($"{Cantidad(i.Guardados)} saved in the bank.");
        if (i.YaEstaban > 0) p.Add($"{Cantidad(i.YaEstaban)} were already in the bank (not duplicated).");
        if (i.Recuperados > 0) p.Add($"{Cantidad(i.Recuperados)} were in the recycle bin and have been recovered.");
        if (i.Fallos > 0) p.Add($"{Cantidad(i.Fallos)} could not be saved{(i.Motivo is { Length: > 0 } ? ": " + i.Motivo : ".")}");
        if (mover && i.Quitados > 0) p.Add($"{Cantidad(i.Quitados)} removed from the save. You can undo it with “Undo”.");
        if (mover && i.NoQuitados > 0) p.Add($"{Cantidad(i.NoQuitados)} could not be removed from the save; their copy is in the bank.");
        return string.Join(" ", p);
    }

    // ---- Taking out to the save ----
    public const string SacarTitulo = "Take out to the save";
    public const string NoSeColoca = "Will not be placed";
    public const string LegalizadoYLegal = "Legalized and legal";
    public const string SigueIlegal = "Still illegal";
    public const string Legalizar = "Legalize";
    public const string LegalizarAyuda = "Uses the Pokekeep engine to make this Pokémon legal before placing it; nothing is written to the save";
    public const string LegalizarNoDisponible = "“Legalize” is not available in this window.";
    public const string LegalizacionCancelada = "Legalization cancelled. Nothing was changed.";
    public const string MoverAlSacar = "Move: remove them from the bank";
    public const string MoverAlSacarAyuda = "When on, the Pokémon that get placed go to the bank's recycle bin (they can be recovered). Otherwise the bank keeps its copy.";
    public const string ColocarEnLaPartida = "Place in the save";
    public const string ColocarAyuda = "Writes the Pokémon into the listed slots; can be undone with “Undo” and never replaces anything";
    public const string CancelarSacar = "Cancel";
    public const string ColocarIlegalesTitulo = "Place illegal Pokémon";
    public const string ColocarIlegalesAyuda = "You can go back and use “Legalize”. Pokekeep does not modify them on its own and nothing is forced.";
    public const string ColocarIgualmente = "Place anyway";
    public const string PartidaSinCajas = "This save has no boxes to place Pokémon in.";
    public const string NoExisteEnElJuego = "That species or form does not exist in the save's game.";
    public const string SinSitio = "There is no empty slot left in the boxes for this Pokémon.";
    public const string CasillaYaOcupada = "The slot was no longer empty; it was not replaced.";
    public const string NoSeHaPodidoEscribir = "Could not write to the save.";
    public static string VaA(string casilla) => $"Will go to: {casilla}";
    public static string NoSeColocaMotivo(string motivo) => $"Will not be placed: {motivo}";
    public static string NoSePuedeConvertir(string motivo) => $"Cannot be brought to the save's game: {motivo}";
    public static string ItemSacarAccesible(string nombre, string destino, string veredicto) => $"{nombre}. {destino}. {veredicto}.";
    public static string ResumenPlan(int colocables, int ilegales, int noColocados, int libres)
    {
        var sb = new StringBuilder(colocables == 0 ? "None can be placed." : colocables == 1 ? "1 Pokémon will go to an empty box slot." : $"{colocables} Pokémon will go to empty box slots.");
        if (ilegales > 0) sb.Append($" {ilegales} {(ilegales == 1 ? "is illegal" : "are illegal")}: you can legalize {(ilegales == 1 ? "it" : "them")} before placing.");
        if (noColocados > 0) sb.Append($" {noColocados} cannot be placed.");
        sb.Append($" Free slots: {libres}.");
        return sb.ToString();
    }
    public static string LegalizarTodos(int n) => n == 1 ? "Legalize the illegal one" : $"Legalize all {n} illegal ones";
    public static string Legalizado(string nombre, string titular) => $"{nombre}: {titular}.";
    public static string NoSeHaLegalizado(string nombre, string titular, string detalle) => $"{nombre}: {titular}. {detalle}".TrimEnd();
    public static string ColocarIlegalesTexto(int n) => n == 1 ? "1 Pokémon is still marked as illegal and will be placed as it is." : $"{n} Pokémon are still marked as illegal and will be placed as they are.";
    public static string HistorialSacar(int n) => $"Take {Cantidad(n)} out of the bank";
    public static string NadaColocado(string motivo) => $"Nothing was placed. {motivo}".TrimEnd();
    public static string Colocados(int colocados, int omitidos, int movidos)
    {
        var sb = new StringBuilder($"{Cantidad(colocados)} placed in the save. You can undo it with “Undo”.");
        if (omitidos > 0) sb.Append($" {omitidos} were not placed.");
        if (movidos > 0) sb.Append($" {Cantidad(movidos)} go to the bank's recycle bin.");
        return sb.ToString();
    }

    // ---- Recycle bin ----
    public const string PapeleraExplicacion = "What you send here is not deleted: it stays in the bank folder until you delete it for good.";
    public const string PapeleraVaciaTexto = "The recycle bin is empty.";
    public const string Restaurar = "Restore";
    public const string RestaurarAyuda = "Puts the selected ones back in the bank";
    public const string BorrarParaSiempre = "Delete for good";
    public const string BorrarParaSiempreAyuda = "Deletes the files of the selected ones; cannot be undone";
    public const string VaciarPapelera = "Empty the recycle bin";
    public const string VaciarPapeleraAyuda = "Deletes everything in the recycle bin for good";
    public const string BorrarTitulo = "Delete for good";
    public const string BorrarSinVuelta = "Their files are deleted from the bank folder. This cannot be undone.";
    public const string BorrarConfirmar = "Delete";
    public const string VaciarTitulo = "Empty the recycle bin";
    public const string VaciarConfirmar = "Empty";
    public const string PapeleraYaVacia = "The recycle bin is already empty.";
    public static string BorrarTexto(int n) => $"You are about to delete {Cantidad(n)}.";
    public static string Restaurados(int n) => $"{Cantidad(n)} restored to the bank.";
    public static string BorradosParaSiempre(int n) => $"{Cantidad(n)} deleted for good.";
    public static string BorradosConFallos(int borrados, int fallos) => $"{Cantidad(borrados)} deleted; {Cantidad(fallos)} could not be deleted and are still in the recycle bin.";

    // ---- Duplicates ----
    public const string DuplicadosExplicacion = "Identical ones are the same Pokémon stored twice. Versions are the same Pokémon stored at different moments (for example, before and after leveling up).";
    public const string SinDuplicados = "There are no duplicate Pokémon in the bank.";
    public const string ExplicacionIdenticos = "Same data, byte for byte. All but one copy are surplus: the favorite or, if there is none, the oldest.";
    public const string ExplicacionVersiones = "It is the same Pokémon (same personality, trainer and origin) but with different data. None is surplus: you can compare them.";
    public const string CompararVersiones = "Compare the first and the last";
    public const string EnviarTodosAyuda = "Sends the surplus identical copies of every group to the recycle bin; deletes nothing";
    public static string TituloIdenticos(string nombre, int n) => $"{nombre}: {n} identical copies";
    public static string TituloVersiones(string nombre, int n) => $"{nombre}: {n} versions of the same Pokémon";
    public static string EnviarSobrantes(int n) => n == 1 ? "Send the surplus copy to the recycle bin" : $"Send the {n} surplus copies to the recycle bin";
    public static string EnviarSobrantesAccesible(string nombre, int n) => $"{nombre}: {EnviarSobrantes(n)}";
    public static string CompararVersionesAccesible(string nombre) => $"{nombre}: compare the first and the last version";
    public static string SobrantesEnviados(int n) => $"{n} {(n == 1 ? "copy" : "copies")} sent to the recycle bin.";
    public const string SinSobrantes = "There are no surplus identical copies.";

    // ---- Comparison ----
    public const string Distinto = "Different";
    public static string ComparacionDistinta(string etiqueta, string a, string b) => $"{etiqueta}: {a}, versus {b}. Different.";
    public static string ComparacionIgual(string etiqueta, string a) => $"{etiqueta}: {a}. The same in both.";
    public const string CerrarComparacion = "Close the comparison";
    public const string GrupoGeneral = "General";
    public const string GrupoEstadisticas = "Stats";
    public const string GrupoIv = "IVs";
    public const string GrupoEv = "EVs";
    public const string GrupoMovimientos = "Moves";
    public const string GrupoOrigen = "Origin";
    public const string Especie = "Species";
    public const string Mote = "Nickname";
    public const string Nivel = "Level";
    public const string Naturaleza = "Nature";
    public const string Habilidad = "Ability";
    public const string Objeto = "Held item";
    public const string PS = "HP";
    public const string Ataque = "Attack";
    public const string Defensa = "Defense";
    public const string AtaqueEspecial = "Sp. Atk";
    public const string DefensaEspecial = "Sp. Def";
    public const string Velocidad = "Speed";
    public const string JuegoDeOrigen = "Origin game";
    public const string GeneracionDeOrigen = "Origin generation";
    public const string EntrenadorOriginal = "Original trainer";
    public const string NivelDeEncuentro = "Met level";
    public const string Formato = "File format";
    public static string MovimientoNumero(int n) => $"Move {n}";
    public static string FormaNumero(int n) => $"form {n}";
    public static string TituloComparacion(string a, string b) => $"{a} versus {b}";
    public static string ResumenComparacion(int distintos, int total) => distintos == 0 ? "The two are the same in everything compared." : $"{distintos} of {total} values are different.";

    // ---- Progress ----
    public const string ProgresoExplicacion = "How many species and forms of the National Pokédex are in the bank, normal and shiny. Eggs and anything in the recycle bin do not count; forms are the ones that exist outside battle.";
    public const string ProgresoVacio = "There is nothing in the bank yet. Progress is calculated from the Pokémon you save.";
    public const string EspeciesTitulo = "Species";
    public const string EspeciesVariocolorTitulo = "Shiny species";
    public const string FormasTitulo = "Forms";
    public const string FormasVariocolorTitulo = "Shiny forms";
    public const string PorGeneracion = "By generation";
    public const string OcultarFaltan = "Hide the missing ones";
    public static string Fraccion(int n, int total) => $"{n} / {total}";
    public static string VerFaltan(int n) => $"Show the {n} missing";
    public static string GeneracionN(int g) => $"Generation {g}";
    public static string GeneracionAccesible(int g, int normales, int variocolor, int total) => $"Generation {g}: {normales} of {total} species, {variocolor} shiny";
    public static string ProgresoAccesible(string titulo, int n, int total) => $"{titulo}: {n} of {total}";
}
