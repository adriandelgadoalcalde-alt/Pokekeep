// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Herramientas.ExportarCajas;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

// Bloque 10-8: «Exportar cajas», «Exportar caja» y «Cargar cajas» del menú Herramientas > Datos de PKHeX, desde el menú ⋮ de la pestaña (Main.MainMenuBoxDump, MainMenuBoxDumpSingle y MainMenuBoxLoad).
public sealed partial class CajasViewModel
{
    /// <summary>Por qué no se puede exportar a archivos sueltos aquí (sin carpetas del sistema o sin cajas); null si se puede.</summary>
    private string? MotivoExportarCajas => !TieneCajas ? MessageStrings.MsgSaveBoxFailNone : _anfitrion?.MotivoExportadorCajas;

    /// <summary>Por qué no se puede cargar una carpeta aquí; null si se puede.</summary>
    private string? MotivoCargarCajas => !TieneCajas ? MessageStrings.MsgSaveBoxFailNone
        : _anfitrion?.Carpetas is not IAlmacenCarpetasCajas || _anfitrion.Documentos is null ? IdiomaInterfaz.T("Cajas.CargarDesdeCarpetaDisponible") : null;

    /// <summary>Las tres entradas del menú ⋮ (las añade <see cref="AbrirMenuMas"/> a sus secciones).</summary>
    private EntradaHoja EntradaExportarCaja() => new(IdiomaInterfaz.T("Cajas.ExportarCaja2"), IdiomaInterfaz.T("Cajas.CadaPokemonCajaArchivo"), "FolderOpen", () => AbrirExportarCajas(todas: false), Motivo: MotivoExportarCajas);
    private EntradaHoja EntradaExportarCajas() => new(IdiomaInterfaz.T("Cajas.ExportarCajas"), IdiomaInterfaz.T("Cajas.CadaPokemonTodasCajas"), "FolderOpen", () => AbrirExportarCajas(todas: true), Motivo: MotivoExportarCajas);
    private EntradaHoja EntradaCargarCajas() => new(IdiomaInterfaz.T("Cajas.CargarCajas"), IdiomaInterfaz.T("Cajas.PoneCajasDesdePokemon"), "Upload", CargarCajasDeCarpeta, Motivo: MotivoCargarCajas);

    /// <summary>«Exportar cajas» (todas) o «Exportar caja» (la que se ve): el exportador con el alcance forzado, como PKHeX.</summary>
    public void AbrirExportarCajas(bool todas)
    {
        if (MotivoExportarCajas is not null) return;
        _anfitrion?.AbrirExportadorCajas(todas ? AlcanceExportador.Todas : AlcanceExportador.CajaActual, Caja);
    }

    /// <summary>«Cargar cajas»: elige una carpeta, la lee y pregunta lo que pregunta PKHeX (vaciar y adaptar) con botones.</summary>
    public void CargarCajasDeCarpeta() => _ = CargarCajasDeCarpetaAsync();

    private async Task CargarCajasDeCarpetaAsync()
    {
        if (MotivoCargarCajas is not null) return;
        var carpetas = (IAlmacenCarpetasCajas)_anfitrion!.Carpetas!;
        CarpetaRef? carpeta;
        try { carpeta = await _anfitrion.Carpetas!.ElegirCarpetaAsync(CancellationToken.None); }
        catch (Exception ex) { Aviso(IdiomaInterfaz.T("ExportarCajas.PodidoAbrirSelectorCarpetas") + ex.Message, ok: false); return; }
        if (carpeta is null) return;
        Mostrar(new TrabajandoHojaViewModel(IdiomaInterfaz.T("Cajas.CargarCajas2"), IdiomaInterfaz.F("Cajas.LeyendoArchivos", carpeta.Nombre)));
        IReadOnlyList<ArchivoLeido> archivos;
        try { archivos = await CargaCajas.LeerCarpetaAsync(carpeta, carpetas, _anfitrion.Documentos!); }
        catch (Exception ex) { Ocultar(); Aviso(IdiomaInterfaz.T("Cajas.PodidoLeerCarpeta") + ex.Message, ok: false); return; }
        Ocultar();
        ProcesarCarpeta(archivos, carpeta.Nombre);
    }

    /// <summary>
    /// Lo que se hace con los archivos leídos (aparte del selector para probarlo): sin ningún Pokémon, el mensaje del Core; si hay, la hoja con «Vaciar las cajas posteriores» y las tres formas de colocarlos
    /// (las respuestas de <c>GetImportSettingsOverride</c>). PKHeX pregunta con dos cuadros seguidos; aquí es una hoja con un interruptor y tres botones (regla 4: sin diálogos de alternativas).
    /// </summary>
    public void ProcesarCarpeta(IReadOnlyList<ArchivoLeido> archivos, string nombreCarpeta)
    {
        var sav = Sesion.Guardado;
        var pokemon = CargaCajas.Pokemon(archivos, sav, out var noReconocidos);
        if (pokemon.Count == 0) { Aviso(MessageStrings.MsgSaveBoxImportNoFiles + (archivos.Count > 0 ? IdiomaInterfaz.F("Cajas.NingunoArchivosPokemon", archivos.Count, nombreCarpeta) : IdiomaInterfaz.F("Cajas.TieneArchivos", nombreCarpeta)), ok: false); return; }
        var vaciar = new InterruptorHoja(IdiomaInterfaz.T("Cajas.VaciarCajasPosteriores"), IdiomaInterfaz.F("Cajas.SoloQuieresRellenarHuecos", MessageStrings.MsgSaveBoxImportClear, NombreCaja));
        var ajustes = SaveFile.SetUpdateSettings;
        static string Estado(EntityImportOption o) => o == EntityImportOption.Enable ? IdiomaInterfaz.T("Cajas.Si") : IdiomaInterfaz.T("Cajas.No");
        var actual = IdiomaInterfaz.F("Cajas.AjustesActuales", Estado(ajustes.UpdateToSaveFile), Estado(ajustes.UpdatePokeDex), Estado(ajustes.UpdateRecord));
        var filas = new List<EntradaHoja>
        {
            new(IdiomaInterfaz.T("Cajas.ModificarAlColocar"),IdiomaInterfaz.T("Cajas.AdaptaCadaPokemonPartida"), "Wand", () => AplicarCarga(pokemon, vaciar.Activo, AdaptarAlCargar.Modificar)),
            new(IdiomaInterfaz.T("Cajas.NoModificar"),IdiomaInterfaz.T("Cajas.PoneArchivosTalEstan"), "Box", () => AplicarCarga(pokemon, vaciar.Activo, AdaptarAlCargar.NoModificar)),
            new(IdiomaInterfaz.T("Cajas.UsarAjustesActuales"), IdiomaInterfaz.F("Cajas.DigaEscrituraRanuras2", actual), "Settings", () => AplicarCarga(pokemon, vaciar.Activo, AdaptarAlCargar.SegunAjustes)),
        };
        var detalle = IdiomaInterfaz.P("Cajas.NPokemon", pokemon.Count) + IdiomaInterfaz.P("Cajas.DeArchivosDeCarpeta", archivos.Count, nombreCarpeta) + (noReconocidos > 0 ? IdiomaInterfaz.F("Cajas.NoSonPokemonSeSaltan", noReconocidos) : string.Empty) + IdiomaInterfaz.F("Cajas.SePonenHuecosLibres", NombreCaja);
        Mostrar(new MenuHojaViewModel(IdiomaInterfaz.T("Cajas.CargarCajas2"), [new SeccionHoja(MessageStrings.MsgSaveBoxImportModifyIntro.TrimEnd(':', ' '), filas)], detalle, [vaciar], pie: CargaCajas.PieDeshacer));
    }

    /// <summary>Aplica la carga en un lote del historial (Deshacer devuelve todas las casillas desde la caja actual) y avisa con el mensaje del Core.</summary>
    public void AplicarCarga(IReadOnlyList<PKM> pokemon, bool vaciar, AdaptarAlCargar adaptar)
    {
        var sav = Sesion.Guardado;
        var inicio = Caja;
        var casillas = CargaCajas.CasillasDesde(sav, inicio);
        var mensaje = string.Empty;
        var cargados = 0;
        // Con «vaciar», el Core vacía las cajas ANTES de mirar si algún Pokémon es compatible: aunque no cargue ninguno, la partida cambió y el lote se anota para poder deshacerlo.
        Sesion.Casillas.Lote(casillas, () => { cargados = CargaCajas.Cargar(sav, pokemon, inicio, vaciar, adaptar, out mensaje); return cargados > 0 ? cargados : vaciar ? casillas.Count : 0; }, descripcion: IdiomaInterfaz.T("Cajas.CargarCajas2"));
        if (vaciar || cargados > 0) CargarCaja(Caja, seleccionar: false);
        if (cargados > 0) { Resultado(InformeOperacion.Ok(mensaje.TrimEnd('.') + ". " + CargaCajas.AvisoDeshacer(adaptar), cargados)); }
        else if (vaciar) Resultado(InformeOperacion.Ok(IdiomaInterfaz.T("Cajas.CajasVaciadasNingunArchivo"), casillas.Count));   // la partida SÍ cambió (PKHeX solo dice «Ningún archivo cargado»): la hoja no puede decir que no hay cambios
        else Resultado(InformeOperacion.Falla(TipoResultado.SinCambios, string.IsNullOrEmpty(mensaje) ? MessageStrings.MsgSaveBoxImportNoFiles : mensaje));
    }
}
