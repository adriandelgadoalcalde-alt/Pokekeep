// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Windows.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>
/// Lo que la pestana Pokemon necesita de la raiz de la app: abrir hojas, avisos con «Deshacer», el editor, las acciones del motor y los servicios del sistema. La implementa <see cref="ShellViewModel"/>
/// (y las pruebas, un anfitrion minimo).
/// </summary>
public interface IAnfitrionCajas
{
    void ReproducirGrito(PKM pk) { }
    void PararGrito() { }
    void AbrirHoja(HojaViewModel hoja);
    void CerrarHoja();
    void Informar(string texto, string clase, string glifo, string? textoAccion = null, ICommand? accion = null);
    /// <summary>Abre el editor sobre una casilla (con <paramref name="pk"/> como borrador de partida). Si la casilla es de solo lectura, el editor no escribe en ella.</summary>
    void AbrirEditor(CasillaRef casilla, PKM pk);
    void LanzarAccion(AccionPokemon accion, CasillaRef casilla);
    /// <summary>Por que las dos acciones Keep no se pueden lanzar ahora (sin motor, motor caido...); null si se pueden.</summary>
    string? MotivoSinAcciones { get; }
    /// <summary>Por que ESA accion no se puede lanzar sobre ESA casilla ahora mismo (sin nombre de entrenador, casilla vacia o bloqueada, operacion en curso, motor...); el mismo texto que el escritorio da como ayuda. Null si se puede.</summary>
    string? MotivoAccion(AccionPokemon accion, CasillaRef casilla) => MotivoSinAcciones;
    IPortapapeles? Portapapeles { get; }
    IAlmacenDocumentos? Documentos { get; }
    IAlmacenCarpetas? Carpetas { get; }
    string? CarpetaPreferenciasInterfaz => null;
    /// <summary>Por que la edicion masiva sobre la seleccion no se puede abrir (sin motor de masivas en esta ventana, sin partida...); null si se puede.</summary>
    string? MotivoEdicionMasiva => IdiomaInterfaz.T("IAnfitrionCajas.LaEdicionMasivaNo");
    /// <summary>Abre la Edicion masiva sobre las casillas marcadas (una sola = «Este Pokemon»).</summary>
    void AbrirEdicionMasiva(IReadOnlyList<CasillaRef> casillas) { }
    /// <summary>Abre la Edicion masiva desde el menu de la caja: con <paramref name="caja"/>, «Cajas concretas» con esa caja marcada; sin ella, el ambito Todo.</summary>
    void AbrirEdicionMasivaDeCaja(int? caja) { }
    void AbrirEdicionMasivaIlegales() { }
    void AbrirCompletarJuego() { }
    string? MotivoCompletarJuego => MotivoEdicionMasiva;
    /// <summary>Los textos de las entradas de Pokekeep en los menus de caja y de casilla (del nucleo); null si esta ventana no tiene operaciones masivas (y entonces no se ofrecen).</summary>
    Masivas.TextosEdicionMasiva? TextosMasivas => null;
    /// <summary>Lleva a la Libreria (para anadir un Pokemon del catalogo a una casilla vacia).</summary>
    void IrALibreria();
    /// <summary>Bloque 10-8: por que no se puede abrir el exportador de cajas («Exportar cajas»/«Exportar caja» de PKHeX) desde aqui; null si se puede.</summary>
    string? MotivoExportadorCajas => IdiomaInterfaz.T("IAnfitrionCajas.ExportarAUnaCarpeta");
    /// <summary>Bloque 10-8: abre el exportador de cajas con el alcance forzado y la caja que se esta viendo.</summary>
    void AbrirExportadorCajas(Pokekeep.Mobile.Ui.Herramientas.ExportarCajas.AlcanceExportador alcance, int caja) { }
    /// <summary>Los ajustes de PKHeX VIGENTES (formato de los sets de Showdown, bloque 10-9). Null en anfitriones de prueba: valen los de fábrica.</summary>
    Ajustes.Pkhex.AjustesPkhex? AjustesDePkhex => null;
    /// <summary>Hoja de compartir del sistema (bloque 10-9). Null donde no existe (pruebas, escritorio).</summary>
    ICompartirSistema? Compartir => null;
    /// <summary>X37: abre «Cobertura y plantillas» (desde Pokémon > Equipo). Por defecto no hace nada (anfitriones de prueba sin esa pantalla).</summary>
    void AbrirCoberturaYPlantillas() { }
    /// <summary>X38: por qué no se puede guardar en el banco desde las cajas (null si se puede).</summary>
    string? MotivoGuardarEnBanco => null;
    /// <summary>X38: guarda una copia de las casillas en el banco propio; con <paramref name="mover"/>, las quita de la partida (con confirmación y un Deshacer). Por defecto no hace nada (anfitriones de prueba).</summary>
    void GuardarEnBanco(IReadOnlyList<CasillaRef> casillas, bool mover) { }
}
