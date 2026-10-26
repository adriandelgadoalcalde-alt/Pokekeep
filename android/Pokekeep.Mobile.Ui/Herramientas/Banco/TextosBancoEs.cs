// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;

namespace Pokekeep.Mobile.Ui.Herramientas.Banco;

/// <summary>
/// (X36f: este es el castellano; la clase TextosBanco elige el idioma en uso y delega aqui o en TextosBancoEn.)
/// Todos los textos visibles de «Banco de Pokémon» (X38) en un solo sitio, en español de España: la traducción al inglés (X36) los encuentra aquí sin buscar cadenas sueltas por el código y el XAML. Los que llevan
/// datos son funciones; los demás, constantes. Los términos del juego (especies, movimientos, habilidades, objetos, tipos, naturalezas, nombres de juego) NO están aquí: salen del Core en el idioma de los datos.
/// <see cref="TextosBancoEn"/> tiene los mismos miembros en inglés (una prueba exige que no falte ni sobre ninguno).
/// </summary>
public static class TextosBancoEs
{
    // ---- Pantalla y entrada de «Más» ----
    public const string Titulo = "Banco de Pokémon";
    public const string Subtitulo = "Tu banco propio, fuera de cualquier partida: guarda copias de tus Pokémon, ordénalos y sácalos a otra partida con su veredicto de legalidad. Todo se queda en el dispositivo.";
    public const string EntradaMas = "Tus Pokémon guardados fuera de las partidas: favoritos, etiquetas, papelera y progreso";
    public const string Volver = "Volver";
    public const string Cancelar = "Cancelar";
    public const string Trabajando = "Trabajando";
    public const string Si = "Sí";
    public const string No = "No";
    public const string Ninguno = "Ninguno";
    public const string NoAplica = "No aplica";
    public const string NoConsta = "No consta";
    public const string Huevo = "Huevo";

    // ---- Dónde se guarda ----
    public const string EtiquetaDonde = "Dónde se guarda";
    public const string UbicacionPrivada = "Carpeta privada de la app";
    public const string AvisoCarpetaPrivada = "Ahora el banco está en la carpeta privada de la app: si desinstalas la app, se borra. Elige una carpeta tuya para que sobreviva.";
    public const string ElegirCarpeta = "Elegir carpeta";
    public const string ElegirCarpetaAyuda = "Abre el selector del sistema; lo que ya tengas guardado se traslada a esa carpeta y, si ya había un banco en ella, se recupera";
    public const string SinSelector = "Este dispositivo no permite elegir una carpeta.";
    public const string TrasladandoBanco = "Trasladando el banco…";
    public const string SinCambiosCarpeta = "No se ha elegido ninguna carpeta. El banco sigue donde estaba.";
    public const string TrasladoConFallos = "Algún Pokémon no se pudo trasladar.";
    public const string TrasladoNoComprobado = "No se pudo comprobar que todo estuviera en la carpeta nueva.";
    public static string DondeEsta(string descripcion, bool carpetaDelUsuario)
        => carpetaDelUsuario ? $"Carpeta elegida: «{descripcion}»." : $"{descripcion}.";
    public const string NotaFormato = "Cada Pokémon es un archivo .pk* estándar que también abre PKHeX de escritorio. Si el índice del banco se estropea, se rehace solo desde los archivos.";
    public static string CarpetaPerdida(string nombre)
        => $"La carpeta «{nombre}» ya no está disponible (se quitó el permiso o se borró). Mientras tanto el banco usa la carpeta privada de la app, donde no está lo que guardaste en ella. Vuelve a elegirla para recuperarlo.";
    public static string FalloCarpeta(string carpeta, string motivo) => $"No se ha podido usar la carpeta «{carpeta}»: {motivo} El banco sigue donde estaba y no se ha borrado nada.";
    public static string CarpetaElegida(string carpeta, int yaHabia, int trasladados)
    {
        var sb = new StringBuilder($"El banco está ahora en «{carpeta}».");
        if (yaHabia > 0) sb.Append($" Se ha encontrado un banco con {Cantidad(yaHabia)}.");
        if (trasladados > 0) sb.Append($" Se han trasladado {Cantidad(trasladados)}.");
        return sb.ToString();
    }
    public static string ResumenCarga(InformeCarga i)
    {
        var p = new List<string>();
        if (i.ErrorDeposito is not null) p.Add($"No se ha podido leer la carpeta del banco: {i.ErrorDeposito}");
        if (i.IndiceReconstruido) p.Add("El índice del banco no se podía leer y se ha rehecho desde los archivos (favoritos, etiquetas y papelera no se han podido recuperar).");
        else if (i.IndiceDeLaCopia) p.Add("El índice del banco no se podía leer y se ha recuperado de su copia anterior.");
        if (i.Reconstruidas > 0 && !i.IndiceReconstruido) p.Add($"{Cantidad(i.Reconstruidas)} que no estaban en el índice se han añadido desde sus archivos.");
        if (i.Perdidas > 0) p.Add($"{Cantidad(i.Perdidas)} se han quitado de la lista porque su archivo ya no está o no se puede leer.");
        if (i.Actualizadas > 0) p.Add($"{Cantidad(i.Actualizadas)} se han vuelto a leer porque su archivo había cambiado.");
        if (i.Ilegibles.Count > 0) p.Add($"{i.Ilegibles.Count} archivo(s) de la carpeta no se pueden leer como Pokémon y no se han tocado: {string.Join(", ", i.Ilegibles.Take(3))}{(i.Ilegibles.Count > 3 ? "…" : string.Empty)}.");
        return string.Join(" ", p);
    }
    public const string CargandoBanco = "Cargando el banco…";
    public const string CargaCancelada = "Carga cancelada. El banco no se ha tocado.";
    public const string Reintentar = "Reintentar la carga";
    public const string BancoNoLeido = "No se ha podido leer el banco; no se ha cambiado nada.";

    // ---- Pestañas ----
    public static string PestanaPokemon(int n) => $"Pokémon ({n})";
    public static string PestanaPapelera(int n) => $"Papelera ({n})";
    public static string PestanaDuplicados(int n) => $"Repetidos ({n})";
    public const string PestanaProgreso = "Progreso";

    // ---- Lista, búsqueda y filtros ----
    public const string BancoVacioTexto = "El banco está vacío. Abre una partida, mantén pulsado un Pokémon de las cajas y elige «Guardar en el banco» (o selecciona varios).";
    public const string BuscarMarca = "Nombre o etiqueta";
    public const string BuscarAccesible = "Buscar en el banco";
    public const string Buscar = "Buscar";
    public const string MostrarFiltros = "Filtros";
    public const string OcultarFiltros = "Ocultar filtros";
    public const string QuitarFiltros = "Quitar filtros";
    public const string SoloFavoritos = "Solo favoritos";
    public const string ElegirTodas = "Elegir todos";
    public const string QuitarSeleccion = "Quitar selección";
    public const string SinCoincidenciasTexto = "Ningún Pokémon cumple esos filtros.";
    public const string CualquierTipo = "Cualquier tipo";
    public const string CualquierJuego = "Cualquier juego";
    public const string CualquierEspecie = "Cualquier especie";
    public const string CualquierEtiqueta = "Cualquier etiqueta";
    public const string ElegirTipo = "Filtrar por tipo";
    public const string ElegirJuego = "Filtrar por juego de origen";
    public const string ElegirEspecie = "Filtrar por especie";
    public const string ElegirEtiqueta = "Filtrar por etiqueta";
    public const string OrdenarPor = "Ordenar por";
    public const string OrdenReciente = "Más reciente primero";
    public const string OrdenPokedex = "Número de Pokédex";
    public const string OrdenNombre = "Nombre";
    public const string OrdenNivel = "Nivel, de mayor a menor";
    public static string Cantidad(int n) => n == 1 ? "1 Pokémon" : $"{n} Pokémon";
    public static string CantidadFiltrada(int mostrados, int total) => $"{mostrados} de {total} Pokémon";
    public static string MostrarMas(int n, int restantes) => $"Mostrar {n} más ({restantes} sin ver)";
    public static string FiltroTipo(string? t) => t is null ? "Tipo: cualquiera" : $"Tipo: {t}";
    public static string FiltroJuego(string? j) => j is null ? "Juego: cualquiera" : $"Juego: {j}";
    public static string FiltroEspecie(string? e) => e is null ? "Especie: cualquiera" : $"Especie: {e}";
    public static string FiltroEtiqueta(string? e) => e is null ? "Etiqueta: cualquiera" : $"Etiqueta: {e}";
    public static string FiltroVariocolor(Tri3 v) => v switch { Tri3.Si => "Variocolor: sí", Tri3.No => "Variocolor: no", _ => "Variocolor: da igual" };
    public static string FiltroLegalidad(Tri3 v) => v switch { Tri3.Si => "Legalidad: legal", Tri3.No => "Legalidad: ilegal", _ => "Legalidad: da igual" };
    public static string FiltroOrden(OrdenBanco o) => o switch { OrdenBanco.Pokedex => "Orden: Pokédex", OrdenBanco.Nombre => "Orden: nombre", OrdenBanco.Nivel => "Orden: nivel", _ => "Orden: más reciente" };

    // ---- Filas ----
    public const string Legal = "Legal";
    public const string RevisarLegalidad = "Revisar legalidad";
    public const string SinAnalizar = "Sin analizar";
    public const string Variocolor = "Variocolor";
    public const string QuitarFavorito = "Quitar de favoritos";
    public const string PonerFavorito = "Marcar como favorito";
    public static string DetalleFila(string especie, string forma, int nivel, int generacion, string juego)
        => string.Join(" · ", new[] { forma.Length > 0 ? (especie.Length > 0 ? $"{especie} ({forma})" : forma) : especie, $"Nv. {nivel}", generacion > 0 ? $"Gen. {generacion}" : string.Empty, juego }.Where(p => p.Length > 0));
    public static string DetalleHuevo(int generacion) => generacion > 0 ? $"Huevo · Gen. {generacion}" : "Huevo";
    public static string FavoritoAccesible(string titulo, bool favorito) => favorito ? $"{titulo}, favorito. Quitar de favoritos" : $"{titulo}. Marcar como favorito";
    public static string FilaAccesible(string titulo, string subtitulo, string legal, bool variocolor, bool favorito, bool elegida, string etiquetas)
        => $"{titulo}, {subtitulo}. {legal}.{(variocolor ? " Variocolor." : string.Empty)}{(favorito ? " Favorito." : string.Empty)}{(etiquetas.Length > 0 ? " Etiquetas: " + etiquetas + "." : string.Empty)} {(elegida ? "Elegido" : "No elegido")}";
    public static string EliminadoEl(DateTime d) => $"En la papelera desde el {d:dd/MM/yyyy}";
    public static string GuardadoEl(DateTime d) => $"Guardado el {d:dd/MM/yyyy}";

    // ---- Acciones sobre lo elegido ----
    public const string SacarALaPartida = "Sacar a la partida";
    public const string SacarAyuda = "Convierte los elegidos al juego de la partida abierta, comprueba su legalidad y los coloca en casillas vacías; antes enseña un plan";
    public const string SacarSinPartida = "Abre una partida para sacar Pokémon del banco.";
    public const string Comparar = "Comparar";
    public const string CompararAyuda = "Compara los dos Pokémon elegidos";
    public const string CompararNecesitaDos = "Para comparar, elige exactamente dos Pokémon.";
    public const string EtiquetasTitulo = "Etiquetas de lo elegido";
    public const string EtiquetaMarca = "Nueva etiqueta";
    public const string EtiquetaAccesible = "Texto de la etiqueta";
    public const string AnadirEtiqueta = "Añadir etiqueta";
    public const string QuitarEtiquetaBoton = "Quitar etiqueta";
    public const string EscribeEtiqueta = "Escribe primero la etiqueta.";
    public const string PonerFavoritos = "Marcar favoritos";
    public const string QuitarFavoritos = "Quitar favoritos";
    public const string ElegirAntes = "Elige antes algún Pokémon.";
    public const string AlaPapelera = "Enviar a la papelera";
    public const string AlaPapeleraAyuda = "No borra nada: se puede restaurar desde la pestaña Papelera";
    public static string Elegidas(int n) => n == 0 ? "Ninguno elegido" : n == 1 ? "1 elegido" : $"{n} elegidos";
    public static string EtiquetaAnadida(string t, int n) => $"Etiqueta «{t}» añadida a {Cantidad(n)}.";
    public static string EtiquetaQuitada(string t, int n) => $"Etiqueta «{t}» quitada de {Cantidad(n)}.";
    public static string PuestoFavorito(string t) => $"{t} es ahora favorito.";
    public static string QuitadoFavorito(string t) => $"{t} ya no es favorito.";
    public static string PuestosFavoritos(int n) => $"{Cantidad(n)} marcados como favoritos.";
    public static string QuitadosFavoritos(int n) => $"{Cantidad(n)} quitados de favoritos.";
    public static string EnviadosAPapelera(int n) => $"{Cantidad(n)} enviados a la papelera. Se pueden restaurar desde la pestaña Papelera.";
    public static string FalloAlGuardar(string motivo) => $"No se ha podido guardar el cambio en el banco: {motivo}";

    // ---- Guardar desde la partida (menús de casilla y de selección) ----
    public const string GrupoMenu = "Banco de Pokémon";
    public const string GuardarEnElBanco = "Guardar en el banco";
    public const string MoverAlBanco = "Mover al banco";
    public const string NoDisponibleAqui = "No disponible en esta ventana.";
    public const string SoloLecturaNoSeQuita = "Solo lectura: no se puede quitar de aquí. Usa «Guardar en el banco».";
    public const string MoverTitulo = "Mover al banco";
    public const string MoverAyuda = "Solo se quitan de la partida los que quedan guardados en el banco. Puedes deshacerlo con «Deshacer».";
    public const string MoverConfirmar = "Mover";
    public const string NadaQueGuardar = "No hay ningún Pokémon que guardar en esas casillas.";
    public const string AbrirElBanco = "Abrir el banco";
    public const string PokemonVacio = "Esa casilla está vacía.";
    public const string NoEstaEnElBanco = "Ese Pokémon ya no está en el banco.";
    public static string GuardarEnElBancoAyuda(int n) => n == 1 ? "Guarda una copia de este Pokémon; el de la partida no se toca" : $"Guarda una copia de {Cantidad(n)}; los de la partida no se tocan";
    public static string MoverAlBancoAyuda(int n) => n == 1 ? "Lo guarda en el banco y lo quita de la partida; se puede deshacer" : $"Guarda {Cantidad(n)} en el banco y los quita de la partida; se puede deshacer";
    public static string MoverTexto(int n) => $"Se guardará una copia de {Cantidad(n)} en el banco y se quitarán de la partida.";
    public static string ArchivoNoEncontrado(string nombre) => $"No se encuentra el archivo «{nombre}» del banco.";
    public static string ArchivoIlegible(string nombre) => $"El archivo «{nombre}» no se puede leer como un Pokémon.";
    public static string ResumenGuardado(InformeGuardadoBanco i, bool mover)
    {
        var p = new List<string>();
        if (i.Guardados > 0) p.Add($"{Cantidad(i.Guardados)} guardados en el banco.");
        if (i.YaEstaban > 0) p.Add($"{Cantidad(i.YaEstaban)} ya estaban en el banco (no se duplican).");
        if (i.Recuperados > 0) p.Add($"{Cantidad(i.Recuperados)} estaban en la papelera y se han recuperado.");
        if (i.Fallos > 0) p.Add($"No se han podido guardar {Cantidad(i.Fallos)}{(i.Motivo is { Length: > 0 } ? ": " + i.Motivo : ".")}");
        if (mover && i.Quitados > 0) p.Add($"{Cantidad(i.Quitados)} quitados de la partida. Puedes deshacerlo con «Deshacer».");
        if (mover && i.NoQuitados > 0) p.Add($"{Cantidad(i.NoQuitados)} no se han podido quitar de la partida; su copia está en el banco.");
        return string.Join(" ", p);
    }

    // ---- Sacar a la partida ----
    public const string SacarTitulo = "Sacar a la partida";
    public const string NoSeColoca = "No se coloca";
    public const string LegalizadoYLegal = "Legalizado y legal";
    public const string SigueIlegal = "Sigue siendo ilegal";
    public const string Legalizar = "Legalizar";
    public const string LegalizarAyuda = "Usa el motor de Pokekeep para dejar este Pokémon legal antes de colocarlo; no escribe nada en la partida";
    public const string LegalizarNoDisponible = "«Legalizar» no está disponible en esta ventana.";
    public const string LegalizacionCancelada = "Legalización cancelada. No se ha cambiado nada.";
    public const string MoverAlSacar = "Mover: quitarlos del banco";
    public const string MoverAlSacarAyuda = "Con esto activado, los Pokémon que se coloquen pasan a la papelera del banco (se pueden recuperar). Si no, el banco conserva su copia.";
    public const string ColocarEnLaPartida = "Colocar en la partida";
    public const string ColocarAyuda = "Escribe los Pokémon en las casillas indicadas; se puede deshacer con «Deshacer» y nunca sustituye nada";
    public const string CancelarSacar = "Cancelar";
    public const string ColocarIlegalesTitulo = "Colocar Pokémon ilegales";
    public const string ColocarIlegalesAyuda = "Puedes volver y usar «Legalizar». Pokekeep no los modifica por su cuenta y nada se fuerza.";
    public const string ColocarIgualmente = "Colocar igualmente";
    public const string PartidaSinCajas = "Esta partida no tiene cajas donde colocar Pokémon.";
    public const string NoExisteEnElJuego = "Esa especie o forma no existe en el juego de la partida.";
    public const string SinSitio = "No queda ninguna casilla vacía en las cajas para este Pokémon.";
    public const string CasillaYaOcupada = "La casilla ya no estaba vacía; no se ha sustituido.";
    public const string NoSeHaPodidoEscribir = "No se ha podido escribir en la partida.";
    public static string VaA(string casilla) => $"Irá a: {casilla}";
    public static string NoSeColocaMotivo(string motivo) => $"No se coloca: {motivo}";
    public static string NoSePuedeConvertir(string motivo) => $"No se puede llevar al juego de la partida: {motivo}";
    public static string ItemSacarAccesible(string nombre, string destino, string veredicto) => $"{nombre}. {destino}. {veredicto}.";
    public static string ResumenPlan(int colocables, int ilegales, int noColocados, int libres)
    {
        var sb = new StringBuilder(colocables == 0 ? "No se puede colocar ninguno." : colocables == 1 ? "1 Pokémon irá a una casilla vacía de las cajas." : $"{colocables} Pokémon irán a casillas vacías de las cajas.");
        if (ilegales > 0) sb.Append($" {ilegales} {(ilegales == 1 ? "es ilegal: puedes legalizarlo" : "son ilegales: puedes legalizarlos")} antes de colocar.");
        if (noColocados > 0) sb.Append($" {noColocados} no se {(noColocados == 1 ? "puede colocar" : "pueden colocar")}.");
        sb.Append($" Casillas libres: {libres}.");
        return sb.ToString();
    }
    public static string LegalizarTodos(int n) => n == 1 ? "Legalizar el ilegal" : $"Legalizar los {n} ilegales";
    public static string Legalizado(string nombre, string titular) => $"{nombre}: {titular}.";
    public static string NoSeHaLegalizado(string nombre, string titular, string detalle) => $"{nombre}: {titular}. {detalle}".TrimEnd();
    public static string ColocarIlegalesTexto(int n) => n == 1 ? "1 Pokémon sigue marcado como ilegal y se colocará tal cual." : $"{n} Pokémon siguen marcados como ilegales y se colocarán tal cual.";
    public static string HistorialSacar(int n) => $"Sacar {Cantidad(n)} del banco";
    public static string NadaColocado(string motivo) => $"No se ha colocado nada. {motivo}".TrimEnd();
    public static string Colocados(int colocados, int omitidos, int movidos)
    {
        var sb = new StringBuilder($"{Cantidad(colocados)} colocados en la partida. Puedes deshacerlo con «Deshacer».");
        if (omitidos > 0) sb.Append($" {omitidos} no se han colocado.");
        if (movidos > 0) sb.Append($" {Cantidad(movidos)} pasan a la papelera del banco.");
        return sb.ToString();
    }

    // ---- Papelera ----
    public const string PapeleraExplicacion = "Lo que envías aquí no se borra: sigue en la carpeta del banco hasta que lo borres para siempre.";
    public const string PapeleraVaciaTexto = "La papelera está vacía.";
    public const string Restaurar = "Restaurar";
    public const string RestaurarAyuda = "Devuelve los elegidos al banco";
    public const string BorrarParaSiempre = "Borrar para siempre";
    public const string BorrarParaSiempreAyuda = "Borra los archivos de los elegidos; no se puede deshacer";
    public const string VaciarPapelera = "Vaciar la papelera";
    public const string VaciarPapeleraAyuda = "Borra para siempre todo lo que hay en la papelera";
    public const string BorrarTitulo = "Borrar para siempre";
    public const string BorrarSinVuelta = "Se borran sus archivos de la carpeta del banco. Esto no se puede deshacer.";
    public const string BorrarConfirmar = "Borrar";
    public const string VaciarTitulo = "Vaciar la papelera";
    public const string VaciarConfirmar = "Vaciar";
    public const string PapeleraYaVacia = "La papelera ya está vacía.";
    public static string BorrarTexto(int n) => $"Vas a borrar {Cantidad(n)}.";
    public static string Restaurados(int n) => $"{Cantidad(n)} restaurados en el banco.";
    public static string BorradosParaSiempre(int n) => $"{Cantidad(n)} borrados para siempre.";
    public static string BorradosConFallos(int borrados, int fallos) => $"{Cantidad(borrados)} borrados; {Cantidad(fallos)} no se han podido borrar y siguen en la papelera.";

    // ---- Repetidos ----
    public const string DuplicadosExplicacion = "Los idénticos son el mismo Pokémon guardado dos veces. Las versiones son el mismo Pokémon guardado en momentos distintos (por ejemplo, antes y después de subir de nivel).";
    public const string SinDuplicados = "No hay Pokémon repetidos en el banco.";
    public const string ExplicacionIdenticos = "Mismos datos, byte a byte. Sobra todo menos una copia: la favorita o, si no hay, la más antigua.";
    public const string ExplicacionVersiones = "Es el mismo Pokémon (misma personalidad, entrenador y origen) pero con datos distintos. No sobra ninguna: puedes compararlas.";
    public const string CompararVersiones = "Comparar la primera y la última";
    public const string EnviarTodosAyuda = "Envía a la papelera las copias idénticas que sobran de todos los grupos; no borra nada";
    public static string TituloIdenticos(string nombre, int n) => $"{nombre}: {n} copias idénticas";
    public static string TituloVersiones(string nombre, int n) => $"{nombre}: {n} versiones del mismo Pokémon";
    public static string EnviarSobrantes(int n) => n == 1 ? "Enviar la copia que sobra a la papelera" : $"Enviar las {n} copias que sobran a la papelera";
    public static string EnviarSobrantesAccesible(string nombre, int n) => $"{nombre}: {EnviarSobrantes(n)}";
    public static string CompararVersionesAccesible(string nombre) => $"{nombre}: comparar la primera y la última versión";
    public static string SobrantesEnviados(int n) => $"{n} {(n == 1 ? "copia enviada" : "copias enviadas")} a la papelera.";
    public const string SinSobrantes = "No hay copias idénticas que sobren.";

    // ---- Comparación ----
    public const string Distinto = "Distinto";
    public static string ComparacionDistinta(string etiqueta, string a, string b) => $"{etiqueta}: {a}, frente a {b}. Distinto.";
    public static string ComparacionIgual(string etiqueta, string a) => $"{etiqueta}: {a}. Igual en los dos.";
    public const string CerrarComparacion = "Cerrar la comparación";
    public const string GrupoGeneral = "General";
    public const string GrupoEstadisticas = "Estadísticas";
    public const string GrupoIv = "IV";
    public const string GrupoEv = "EV";
    public const string GrupoMovimientos = "Movimientos";
    public const string GrupoOrigen = "Origen";
    public const string Especie = "Especie";
    public const string Mote = "Mote";
    public const string Nivel = "Nivel";
    public const string Naturaleza = "Naturaleza";
    public const string Habilidad = "Habilidad";
    public const string Objeto = "Objeto";
    public const string PS = "PS";
    public const string Ataque = "Ataque";
    public const string Defensa = "Defensa";
    public const string AtaqueEspecial = "At. especial";
    public const string DefensaEspecial = "Def. especial";
    public const string Velocidad = "Velocidad";
    public const string JuegoDeOrigen = "Juego de origen";
    public const string GeneracionDeOrigen = "Generación de origen";
    public const string EntrenadorOriginal = "Entrenador original";
    public const string NivelDeEncuentro = "Nivel de encuentro";
    public const string Formato = "Formato de archivo";
    public static string MovimientoNumero(int n) => $"Movimiento {n}";
    public static string FormaNumero(int n) => $"forma {n}";
    public static string TituloComparacion(string a, string b) => $"{a} frente a {b}";
    public static string ResumenComparacion(int distintos, int total) => distintos == 0 ? "Los dos son iguales en todo lo comparado." : $"{distintos} de {total} datos son distintos.";

    // ---- Progreso ----
    public const string ProgresoExplicacion = "Cuántas especies y formas de la Pokédex nacional hay en el banco, normales y variocolor. Los huevos y lo que está en la papelera no cuentan; las formas son las que existen fuera del combate.";
    public const string ProgresoVacio = "Todavía no hay nada en el banco. El progreso se calcula con los Pokémon que guardes.";
    public const string EspeciesTitulo = "Especies";
    public const string EspeciesVariocolorTitulo = "Especies variocolor";
    public const string FormasTitulo = "Formas";
    public const string FormasVariocolorTitulo = "Formas variocolor";
    public const string PorGeneracion = "Por generación";
    public const string OcultarFaltan = "Ocultar las que faltan";
    public static string Fraccion(int n, int total) => $"{n} / {total}";
    public static string VerFaltan(int n) => $"Ver las {n} que faltan";
    public static string GeneracionN(int g) => $"Generación {g}";
    public static string GeneracionAccesible(int g, int normales, int variocolor, int total) => $"Generación {g}: {normales} de {total} especies, {variocolor} variocolor";
    public static string ProgresoAccesible(string titulo, int n, int total) => $"{titulo}: {n} de {total}";
}
