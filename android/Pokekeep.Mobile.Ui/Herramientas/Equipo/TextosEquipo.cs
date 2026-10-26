// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.Equipo;

/// <summary>
/// Todos los textos visibles de «Cobertura y plantillas» (X37) en un solo sitio: la traducción al inglés (X36) los encuentra aquí sin buscar cadenas sueltas por el código y el XAML.
/// Los que llevan datos son funciones; los demás, constantes.
/// </summary>
public static class TextosEquipo
{
    // ---- Pantalla ----
    public static string Titulo => IdiomaInterfaz.T("TextosEquipo.CoberturaPlantillas");
    public static string Subtitulo => IdiomaInterfaz.T("TextosEquipo.MiraTiposAguantanCastigan");
    public static string Volver => IdiomaInterfaz.T("Menu.Volver");
    public static string EtiquetaFuente => IdiomaInterfaz.T("TextosEquipo.EquipoMirar");
    public static string EquipoDeLaPartida => IdiomaInterfaz.T("TextosEquipo.EquipoPartida");
    public static string SinPartida => IdiomaInterfaz.T("TextosEquipo.AbrePartidaVerCobertura");
    public static string PartidaSinEquipo => IdiomaInterfaz.T("TextosEquipo.PartidaTieneEquipoCombate");
    public static string EquipoVacio => IdiomaInterfaz.T("TextosEquipo.EquipoPartidaTieneNingun");
    public static string Viendo => IdiomaInterfaz.T("TextosEquipo.Viendo");
    public static string EtiquetaPlantillas => IdiomaInterfaz.T("TextosEquipo.PlantillasGuardadas");
    public static string SinPlantillas => IdiomaInterfaz.T("TextosEquipo.TodaviaPlantillasGuardaEquipo");
    public static string EtiquetaAcciones => IdiomaInterfaz.T("Mochila.Acciones");
    public static string NombreMarca => IdiomaInterfaz.T("TextosEquipo.NombrePlantilla");
    public static string NombreSugerencia => IdiomaInterfaz.T("TextosEquipo.EscribeNombre");
    public static string GuardarComoPlantilla => IdiomaInterfaz.T("TextosEquipo.GuardarEquipoPlantilla");
    public static string Renombrar => IdiomaInterfaz.T("TextosEquipo.Renombrar");
    public static string Borrar => IdiomaInterfaz.T("Seleccion.Borrar");
    public static string AplicarALaPartida => IdiomaInterfaz.T("Form.Vista.EditorPartidaView.6");
    public static string AplicarAyuda => IdiomaInterfaz.T("TextosEquipo.PlantillaPasaSerEquipo3");
    public static string RenombrarAccesible => IdiomaInterfaz.T("TextosEquipo.RenombrarPlantilla");
    public static string BorrarAccesible => IdiomaInterfaz.T("TextosEquipo.BorrarPlantilla");
    public static string PuntoDebil => IdiomaInterfaz.T("TextosEquipo.PuntoDebil");
    public static string TodosNeutros => IdiomaInterfaz.T("TextosEquipo.TodosNeutros");

    // ---- Resumen ----
    public static string EtiquetaResumen => IdiomaInterfaz.T("Editor.Resumen");
    public static string PuntosDebiles => IdiomaInterfaz.T("TextosEquipo.PuntosDebilesEquipo");
    public static string PuntosDebilesAyuda => IdiomaInterfaz.T("TextosEquipo.TiposGolpeanSupereficazTres");
    public static string NingunPuntoDebil => IdiomaInterfaz.T("TextosEquipo.NingunoTipoCastigueTres");
    public static string TiposSinCubrir => IdiomaInterfaz.T("TextosEquipo.TiposCubrir");
    public static string TiposSinCubrirAyuda => IdiomaInterfaz.T("TextosEquipo.TiposNingunMovimientoDano");
    public static string TodoCubierto => IdiomaInterfaz.T("TextosEquipo.NingunoMovimientosEquipoGolpean");
    public static string SinMovimientosConDano => IdiomaInterfaz.T("TextosEquipo.EquipoTieneNingunMovimiento");

    // ---- Listas ----
    public static string EtiquetaMiembros => IdiomaInterfaz.T("Nav.Pokemon");
    public static string EtiquetaDefensa => IdiomaInterfaz.T("TextosEquipo.DefensaTipoAtacante");
    public static string DefensaAyuda => IdiomaInterfaz.T("TextosEquipo.CuantosPokemonEquipoReciben");
    public static string EtiquetaOfensiva => IdiomaInterfaz.T("TextosEquipo.CoberturaOfensiva");
    public static string OfensivaAyuda => IdiomaInterfaz.T("TextosEquipo.MovimientosDanoGolpeanSupereficaz");
    public static string Cubierto => IdiomaInterfaz.T("TextosEquipo.CubiertoTexto");
    public static string SinCubrir => IdiomaInterfaz.T("TextosEquipo.Cubrir");
    public static string Ninguno => IdiomaInterfaz.T("Form.Ninguno");
    public static string SinMovimientosCubren => IdiomaInterfaz.T("TextosEquipo.NingunMovimiento");
    public static string Sin => IdiomaInterfaz.T("TextosEquipo.SinTexto");

    // ---- Notas de alcance ----
    public static string NotaAlcance => IdiomaInterfaz.T("TextosEquipo.CalculoUsaTablaTipos");
    public static string NotaSinHabilidades => IdiomaInterfaz.T("TextosEquipo.GeneracionTieneHabilidades");

    // ---- Funciones con datos ----
    public static string DatosFuenteEquipo(int pokemon, string juego) => $"{Pokemones(pokemon)} · {juego}";
    public static string DatosPlantilla(int pokemon, int generacion, DateTime creada) => IdiomaInterfaz.F("TextosEquipo.GeneracionDdMMYyyy", Pokemones(pokemon), generacion, creada);
    public static string Pokemones(int n) => n == 1 ? "1 Pokémon" : IdiomaInterfaz.F("Cajas.Pokemon2", n);
    public static string EncabezadoCobertura(string fuente, int generacion) => IdiomaInterfaz.F("TextosEquipo.CoberturaGeneracion", fuente, generacion);
    public static string Debiles(int n) => n == 1 ? IdiomaInterfaz.T("TextosEquipo.Debil") : IdiomaInterfaz.F("TextosEquipo.Debiles", n);
    public static string Resisten(int n) => n == 1 ? IdiomaInterfaz.T("TextosEquipo.UnoResiste") : IdiomaInterfaz.F("TextosEquipo.Resisten", n);
    public static string Inmunes(int n) => n == 1 ? IdiomaInterfaz.T("TextosEquipo.UnoInmune") : IdiomaInterfaz.F("TextosEquipo.Inmunes", n);
    public static string DefensaAccesible(string tipo, int debiles, int resisten, int inmunes) => $"{tipo}: {Debiles(debiles)}, {Resisten(resisten)}, {Inmunes(inmunes)}";
    public static string PuntoDebilLinea(string tipo, int debiles) => IdiomaInterfaz.F("TextosEquipo.Debiles2", tipo, debiles);
    public static string Multiplicador(double m) => m switch { 0 => "×0", 0.125 => "×⅛", 0.25 => "×¼", 0.5 => "×½", 1 => "×1", 2 => "×2", 4 => "×4", 8 => "×8", _ => $"×{m:0.##}" };
    public static string PorHabilidad => IdiomaInterfaz.T("TextosEquipo.PorHabilidadTexto");
    public static string MiembroLinea(string nombre, string tipos) => $"{nombre} · {tipos}";
    public static string MiembroHabilidad(string habilidad) => IdiomaInterfaz.F("TextosEquipo.Habilidad", habilidad);
    public static string MiembroMovimientos(string movimientos) => IdiomaInterfaz.F("TextosEquipo.Movimientos", movimientos);
    public static string MovimientoSinDano(string movimiento) => IdiomaInterfaz.F("TextosEquipo.Dano", movimiento);
    public static string MovimientoConTipo(string movimiento, string tipo) => $"{movimiento} ({tipo})";
    public static string GrupoMultiplicador(string multiplicador, string nombres) => $"{multiplicador}: {nombres}";

    // ---- Avisos ----
    public static string NombreVacio => IdiomaInterfaz.T("TextosEquipo.EscribeNombrePlantilla");
    public static string NombreRepetido => IdiomaInterfaz.T("TextosEquipo.PlantillaNombre");
    public static string SinEquipoQueGuardar => IdiomaInterfaz.T("TextosEquipo.NingunPokemonEquipoGuardar");
    public static string PlantillasNoDisponibles => IdiomaInterfaz.T("TextosEquipo.PlantillasPuedenGuardarVentana");
    public static string PlantillaGuardada(string nombre) => IdiomaInterfaz.F("TextosEquipo.PlantillaGuardada", nombre);
    public static string PlantillaRenombrada(string nombre) => IdiomaInterfaz.F("TextosEquipo.PlantillaRenombrada", nombre);
    public static string PlantillaBorrada(string nombre) => IdiomaInterfaz.F("TextosEquipo.PlantillaBorrada", nombre);
    public static string FalloAlGuardar(string detalle) => IdiomaInterfaz.F("TextosEquipo.PodidoGuardarPlantilla", detalle);
    public static string PlantillaIlegible(string nombre) => IdiomaInterfaz.F("TextosEquipo.PlantillaDanadaPuedeLeer", nombre);
    public static string SinPlantillaSeleccionada => IdiomaInterfaz.T("TextosEquipo.EligePrimeroPlantillaLista");

    // ---- Aplicar ----
    public static string AplicarTitulo => IdiomaInterfaz.T("TextosEquipo.AplicarPlantilla");
    public static string AplicarConfirmar => IdiomaInterfaz.T("Editor.Aplicar");
    public static string AplicarSinPartida => IdiomaInterfaz.T("TextosEquipo.AbrePartidaAplicarPlantilla");
    public static string AplicarPuedesDeshacer => IdiomaInterfaz.T("Cajas.PuedesDeshacerloDeshacer");
    public static string AplicarNadaQueColocar => IdiomaInterfaz.T("TextosEquipo.PuedeColocarNingunPokemon");
    public static string AplicarSinSitio => IdiomaInterfaz.T("TextosEquipo.NingunaCasillaLibre");
    public static string AplicarEnEquipo(int n) => n == 1 ? IdiomaInterfaz.T("TextosEquipo.PlantillaPasaSerEquipo") : IdiomaInterfaz.F("TextosEquipo.PlantillaPasaSerEquipo2", n);
    public static string AplicarEnEquipoLinea(int casilla, string nombre) => $"{casilla}. {nombre}";
    public static string AplicarEquipoActualACajas => IdiomaInterfaz.T("TextosEquipo.EquipoActualPasaCajas");
    public static string AplicarDesplazadoLinea(string nombre, string casilla) => $"{nombre} → {casilla}";
    public static string AplicarEnCaja(int n, string caja) => n == 1 ? IdiomaInterfaz.F("TextosEquipo.CabeEquipoPokemonPlantilla", caja) : IdiomaInterfaz.F("TextosEquipo.CabenEquipoPokemonPlantilla", n, caja);
    public static string AplicarSinSitioEnCajas(int necesarias, int libres) => IdiomaInterfaz.F("TextosEquipo.SitioCajasEquipoActual", necesarias, libres);
    public static string AplicarSoloHuevos => IdiomaInterfaz.T("TextosEquipo.PlantillaDejariaEquipoSolo");
    public static string AplicarNoColocado(string nombre, string motivo) => IdiomaInterfaz.F("TextosEquipo.Coloca", nombre, motivo);
    public static string AplicarAviso(string nombre, string aviso) => $"{nombre}: {aviso}";
    public static string AplicadaSustituyendo(string plantilla, int n, int alasCajas) => (n == 1 ? IdiomaInterfaz.F("TextosEquipo.PlantillaAplicadaPokemonColocado", plantilla) : IdiomaInterfaz.F("TextosEquipo.PlantillaAplicadaPokemonColocados", plantilla, n))
        + (alasCajas == 0 ? "." : alasCajas == 1 ? IdiomaInterfaz.T("TextosEquipo.PokemonEquipoAnteriorPasado") : IdiomaInterfaz.F("TextosEquipo.PokemonEquipoAnteriorPasado2", alasCajas)) + IdiomaInterfaz.T("TextosEquipo.PuedesDeshacerloDeshacer");
    public static string AplicadaOmitidos(int omitidos) => omitidos == 1 ? IdiomaInterfaz.T("TextosEquipo.Colocado2") : IdiomaInterfaz.F("TextosEquipo.Colocado", omitidos);
    public static string PartidaCambio => IdiomaInterfaz.T("TextosEquipo.PartidaCambiadoDesdeEnseno");
    public static string AplicadaFallo => IdiomaInterfaz.T("TextosEquipo.PodidoEscribirNingunPokemon");
    public static string NoExisteEnElJuego => IdiomaInterfaz.T("TextosEquipo.ExisteJuego");
    public static string NoSePuedeConvertir(string detalle) => IdiomaInterfaz.F("TextosEquipo.PuedeConvertirJuego", detalle);
    public static string PlantillaDanada => IdiomaInterfaz.T("TextosEquipo.DatosPlantillaEstanDanados");
    public static string BorrarTitulo => IdiomaInterfaz.T("TextosEquipo.BorrarPlantilla");
    public static string BorrarTexto(string nombre) => IdiomaInterfaz.F("TextosEquipo.BorraraDispositivoPokemonPartida", nombre);
    public static string BorrarConfirmar => IdiomaInterfaz.T("Seleccion.Borrar");

    // ---- Acceso desde Pokémon > Equipo ----
    public static string BotonEquipo => IdiomaInterfaz.T("TextosEquipo.CoberturaPlantillas");
    public static string BotonEquipoAyuda => IdiomaInterfaz.T("TextosEquipo.MiraTiposCastiganAguantan");
}
