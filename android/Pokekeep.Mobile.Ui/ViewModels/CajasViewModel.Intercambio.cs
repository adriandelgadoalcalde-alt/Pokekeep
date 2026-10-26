// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Herramientas.Showdown;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

// Exportar (archivo .pk*, set de Showdown, QR, volcados de caja) e importar (archivo, volcado, grupo) por los selectores del sistema y el portapapeles. Nada sale del dispositivo por su cuenta (regla 13).
public sealed partial class CajasViewModel
{
    private void Aviso(string texto, bool ok = true) => _anfitrion?.Informar(texto, ok ? "ok" : "error", ok ? "CircleCheck" : "TriangleAlert");

    // ---- Exportar un Pokemon ----
    private void AbrirMenuExportar(CasillaViewModel c)
    {
        if (c.Entidad is not { } pk) return;
        var sin = _anfitrion?.Documentos is null ? IdiomaInterfaz.T("Cajas.ExportarDisponibleAqui") : null;
        var portapapeles = _anfitrion?.Portapapeles is null ? IdiomaInterfaz.T("Editor.PortapapelesDisponible") : null;
        var entradas = new List<EntradaHoja>
        {
            new(IdiomaInterfaz.T("Editor.ArchivoPk"), IdiomaInterfaz.F("Cajas.GuardaSitioElijas", Intercambio.NombreArchivo(pk)), "FileDown", () => _ = ExportarArchivoAsync(pk, cifrado: false), Motivo: sin),
        };
        if (pk.Format >= 3) entradas.Add(new(IdiomaInterfaz.T("Editor.ArchivoCifradoEk"), IdiomaInterfaz.T("Cajas.MismoPokemonCifradoGuarda"), "FileDown", () => _ = ExportarArchivoAsync(pk, cifrado: true), Motivo: sin));
        entradas.Add(new(IdiomaInterfaz.T("Cajas.SetShowdown"), IdiomaInterfaz.T("Editor.CopiaTextoPortapapeles"), "ClipboardCopy", () => _ = ExportarShowdownAsync([pk]), Motivo: portapapeles));
        entradas.Add(new(IdiomaInterfaz.T("Cajas.CodigoQR"), IdiomaInterfaz.T("Cajas.MuestraCodigoQRPokemon"), "QrCode", () => MostrarQr(pk)));
        var compartir = _anfitrion?.Compartir is null ? IdiomaInterfaz.T("Cajas.CompartirDisponibleAqui") : null;
        var enviar = new List<EntradaHoja> { new(IdiomaInterfaz.T("Editor.CompartirPokemon"), IdiomaInterfaz.T("Cajas.MandaArchivoPkHoja"), "Share2", () => CompartirArchivo(pk, cifrado: false), Motivo: compartir) };
        if (pk.Format >= 3) enviar.Add(new(IdiomaInterfaz.T("Editor.CompartirCifradoEk"), IdiomaInterfaz.T("Cajas.ArchivoCifradoGuardaJuego"), "Share2", () => CompartirArchivo(pk, cifrado: true), Motivo: compartir));
        enviar.Add(new(IdiomaInterfaz.T("Cajas.CompartirSetShowdown"), IdiomaInterfaz.T("Cajas.MandaTextoSetHoja"), "Share2", () => CompartirShowdown(pk), Motivo: compartir));
        Mostrar(new MenuHojaViewModel(IdiomaInterfaz.T("Editor.Exportar"), [new SeccionHoja(null, entradas), new SeccionHoja(IdiomaInterfaz.T("Editor.Compartir"), enviar)], Descripcion(c)));
    }

    private async Task ExportarArchivoAsync(PKM pk, bool cifrado)
    {
        var docs = _anfitrion?.Documentos;
        if (docs is null) return;
        try
        {
            var destino = await docs.ElegirDestinoAsync(Intercambio.NombreArchivo(pk, cifrado), CancellationToken.None);
            if (destino is null) return;
            await docs.EscribirAsync(destino, Intercambio.Bytes(pk, cifrado), CancellationToken.None);
            Aviso(IdiomaInterfaz.F("Form.Exportado", destino.Nombre));
        }
        catch (Exception ex) { Aviso(IdiomaInterfaz.T("Editor.PodidoExportar") + ex.Message, ok: false); }
    }

    private async Task ExportarShowdownAsync(IReadOnlyList<PKM> pokemon)
    {
        var p = _anfitrion?.Portapapeles;
        if (p is null) return;
        var texto = Intercambio.Showdown(pokemon, Sesion.Guardado.Context, AjustesPkhexActuales);   // con los ajustes BattleTemplate.Export vigentes
        if (string.IsNullOrWhiteSpace(texto)) { Aviso(IdiomaInterfaz.T("Form.SinExportar"), ok: false); return; }
        var ok = await p.CopiarAsync(texto);
        Aviso(ok ? (pokemon.Count == 1 ? MessageStrings.MsgSimulatorExportSuccess.TrimEnd(':', ' ') + "." : MessageStrings.MsgSimulatorExportList) : MessageStrings.MsgClipboardFailWrite, ok);
    }

    private void MostrarQr(PKM pk)
    {
        try
        {
            Mostrar(new QrHojaViewModel(IdiomaInterfaz.T("Cajas.CodigoQR"), pk, Sesion.Textos.Species[pk.Species], () => _anfitrion?.Portapapeles, () => _anfitrion?.Compartir));
        }
        catch (Exception ex) { Aviso(IdiomaInterfaz.T("Cajas.PodidoCrearCodigoQR") + ex.Message, ok: false); }
    }

    // ---- Exportar la seleccion ----
    [RelayCommand]
    public void ExportarSeleccion()
    {
        var lista = MarcadasOcupadas();
        if (lista.Count == 0) return;
        var pokemon = lista.Select(x => Sesion.Casillas.Leer(x.Info)).Where(p => p.Species != 0).ToList();
        var carpeta = _anfitrion?.Carpetas is null || _anfitrion.Documentos is null ? IdiomaInterfaz.T("ExportarCajas.ExportarCarpetaDisponibleAqui") : null;
        Mostrar(new MenuHojaViewModel(IdiomaInterfaz.F("Cajas.ExportarPokemon", pokemon.Count),
        [
            new SeccionHoja(null,
            [
                new(IdiomaInterfaz.T("Cajas.SetsShowdown"), IdiomaInterfaz.T("Cajas.CopiaTextoTodosPortapapeles"), "ClipboardCopy", () => _ = ExportarShowdownAsync(pokemon), Motivo: _anfitrion?.Portapapeles is null ? IdiomaInterfaz.T("Editor.PortapapelesDisponible") : null),
                new(IdiomaInterfaz.T("Cajas.ArchivosPkCarpeta"), IdiomaInterfaz.T("Cajas.ArchivoPokemonCarpetaElijas"), "FileDown", () => _ = ExportarACarpetaAsync(pokemon), Motivo: carpeta),
            ]),
        ]));
    }

    private async Task ExportarACarpetaAsync(IReadOnlyList<PKM> pokemon)
    {
        if (_anfitrion?.Carpetas is not { } carpetas) return;
        try
        {
            var carpeta = await carpetas.ElegirCarpetaAsync(CancellationToken.None);
            if (carpeta is null) return;
            var docs = _anfitrion.Documentos!;
            var n = 0;
            foreach (var pk in pokemon)
            {
                var doc = await carpetas.CrearEnCarpetaAsync(carpeta, Intercambio.NombreArchivo(pk), CancellationToken.None);
                await docs.EscribirAsync(doc, Intercambio.Bytes(pk), CancellationToken.None);
                n++;
            }
            Aviso(IdiomaInterfaz.F("Cajas.ExportadosArchivos", n, carpeta.Nombre));
        }
        catch (Exception ex) { Aviso(IdiomaInterfaz.T("Editor.PodidoExportar") + ex.Message, ok: false); }
    }

    // ---- Volcados de caja ----
    private void VolcarCaja(bool todas) => _ = VolcarCajaAsync(todas);

    private async Task VolcarCajaAsync(bool todas)
    {
        var docs = _anfitrion?.Documentos;
        if (docs is null || !TieneCajas) return;
        try
        {
            var datos = todas ? Intercambio.VolcarTodo(Sesion.Guardado) : Intercambio.VolcarCaja(Sesion.Guardado, Caja);
            var destino = await docs.ElegirDestinoAsync(Intercambio.NombreVolcado(NombreCaja, todas), CancellationToken.None);
            if (destino is null) return;
            await docs.EscribirAsync(destino, datos, CancellationToken.None);
            Aviso(todas ? IdiomaInterfaz.F("Cajas.TodasCajasVolcadas", destino.Nombre) : IdiomaInterfaz.F("Cajas.CajaVolcada", NombreCaja, destino.Nombre));
        }
        catch (Exception ex) { Aviso(IdiomaInterfaz.T("Cajas.PodidoVolcar") + ex.Message, ok: false); }
    }

    private void CargarVolcado() => _ = ImportarArchivoAsync(null, soloCajas: true);

    // ---- Importar ----
    /// <summary>Importa un archivo: un Pokemon va a la casilla elegida (o a la primera libre si no hay), un volcado de caja a la caja actual y un grupo de Pokemon (equipos de alquiler) a la caja actual.</summary>
    public void ImportarArchivo(CasillaRef? destino) => _ = ImportarArchivoAsync(destino, soloCajas: false);

    private async Task ImportarArchivoAsync(CasillaRef? destino, bool soloCajas)
    {
        var docs = _anfitrion?.Documentos;
        if (docs is null) return;
        Importado contenido;
        try
        {
            var doc = await docs.ElegirParaAbrirAsync(CancellationToken.None);
            if (doc is null) return;
            var datos = await docs.LeerAsync(doc, CancellationToken.None);
            contenido = Intercambio.Analizar(datos, doc.Nombre, Sesion.Guardado);
        }
        catch (Exception ex) { Aviso(IdiomaInterfaz.T("Editor.PodidoLeerArchivo") + ex.Message, ok: false); return; }
        ProcesarImportado(contenido, destino, soloCajas);
    }

    /// <summary>Lo que se hace con lo importado (separado del selector para poder probarlo): confirma lo que sobrescribe y lo aplica por el historial.</summary>
    public void ProcesarImportado(Importado contenido, CasillaRef? destino, bool soloCajas = false)
    {
        switch (contenido)
        {
            case ImportadoError e:
                Aviso(e.Mensaje, ok: false);
                break;
            case ImportadoPokemon p when !soloCajas:
                ColocarImportado(p, destino ?? PrimeraCasillaLibre());
                break;
            case ImportadoCajas c:
                ConfirmarCargaDeCajas(c);
                break;
            case ImportadoGrupo g when !soloCajas:
                ConfirmarGrupo(g);
                break;
            default:
                Aviso(IdiomaInterfaz.T("Cajas.ArchivoVolcadoCajaPartida"), ok: false);
                break;
        }
    }

    private CasillaRef? PrimeraCasillaLibre()
    {
        var sav = Sesion.Guardado;
        if (!sav.HasBox) return null;
        for (var b = 0; b < sav.BoxCount; b++)
        {
            var caja = (Caja + b) % sav.BoxCount;
            for (var s = 0; s < sav.BoxSlotCount; s++)
            {
                var info = new SlotInfoBox(caja, s, sav);
                if (info.CanWriteTo(sav) && info.Read(sav).Species == 0) return new CasillaRef(info, caja);
            }
        }
        return null;
    }

    private void ColocarImportado(ImportadoPokemon p, CasillaRef? destino)
    {
        if (destino is null) { Aviso(IdiomaInterfaz.T("Cajas.NingunaCasillaLibreDonde"), ok: false); return; }
        void Hacer()
        {
            var r = Sesion.Casillas.Colocar(Fresca(destino.Info), p.Pokemon);
            if (r.Correcto && destino.Info is SlotInfoBox b && EsCajas && b.Box != Caja) CargarCaja(b.Box, seleccionar: false);
            Resultado(r);
        }
        var ocupada = !Sesion.Casillas.EstaVacia(destino.Info);
        if (p.Avisos.Count == 0 && !ocupada) { Hacer(); return; }
        var parrafos = new List<string>();
        if (ocupada) parrafos.Add(IdiomaInterfaz.F("Cajas.TienePokemonSustituira", destino.Descripcion));
        parrafos.AddRange(p.Avisos);
        parrafos.Add(IdiomaInterfaz.T("Cajas.PuedesDeshacerloDeshacer"));
        Mostrar(new ConfirmarHojaViewModel(IdiomaInterfaz.T("Editor.ImportarPokemon"), parrafos, IdiomaInterfaz.T("Form.Fama3.Importar"), Hacer, destructiva: ocupada));
    }

    private void ConfirmarCargaDeCajas(ImportadoCajas c)
    {
        var sav = Sesion.Guardado;
        if (!sav.HasBox) { Aviso(MessageStrings.MsgSaveBoxFailNone, ok: false); return; }
        if (Intercambio.MotivoNoSeCarga(sav, c.Datos, Caja) is { } motivo) { Aviso(motivo, ok: false); return; }
        var todas = sav.GetPCBinary().Length == c.Datos.Length;
        Mostrar(new ConfirmarHojaViewModel(todas ? IdiomaInterfaz.T("Cajas.CargarTodoPC") : IdiomaInterfaz.T("Cajas.CargarCaja"),
            [todas ? IdiomaInterfaz.T("Cajas.VanSustituirTODASCajas") : IdiomaInterfaz.F("Cajas.VaSustituirContenidoArchivo", NombreCaja), IdiomaInterfaz.T("Cajas.CasillasProtegidasTocanPuedes")], IdiomaInterfaz.T("Cajas.Cargar"), () => CargarCajas(c, todas), destructiva: true));
    }

    private void CargarCajas(ImportadoCajas c, bool todas)
    {
        var sav = Sesion.Guardado;
        var inicio = todas ? 0 : Caja; var fin = todas ? sav.BoxCount - 1 : Caja;
        var casillas = new List<ISlotInfo>();
        for (var b = inicio; b <= fin; b++) for (var s = 0; s < sav.BoxSlotCount; s++) casillas.Add(new SlotInfoBox(b, s, sav));
        var cargado = false;
        var tocadas = Sesion.Casillas.Lote(casillas, () => { cargado = todas ? sav.SetPCBinary(c.Datos) : sav.SetBoxBinary(c.Datos, Caja); return cargado ? casillas.Count : 0; });
        if (tocadas > 0) Resultado(InformeOperacion.Ok((todas ? MessageStrings.MsgSaveBoxImportPCBinary : MessageStrings.MsgSaveBoxImportBoxBinary).TrimEnd('.') + IdiomaInterfaz.T("Cajas.PuedesDeshacerlo"), tocadas));
        else Resultado(InformeOperacion.Falla(TipoResultado.Fallo, string.Format(MessageStrings.MsgSaveCurrentGeneration, sav.Generation)));
    }

    /// <summary>
    /// Un grupo (equipos de alquiler, vídeos de batalla) a la caja actual (<c>SAVEditor.OpenGroup</c>, PK-IO-063): PKHeX pregunta si sobrescribe la caja y después cómo adaptar los Pokémon
    /// (<c>GetImportSettingsOverride</c>); aquí es una hoja con las tres respuestas como botones (bloque 10-8).
    /// </summary>
    private void ConfirmarGrupo(ImportadoGrupo g)
    {
        var filas = new List<EntradaHoja>
        {
            new(IdiomaInterfaz.T("Cajas.ModificarAlColocar"), IdiomaInterfaz.T("Cajas.AdaptaCadaPokemonPartida"), "Wand", () => ImportarGrupo(g, AdaptarAlCargar.Modificar), Destructiva: true),
            new(IdiomaInterfaz.T("Cajas.NoModificar"), IdiomaInterfaz.T("Cajas.PonePokemonTalEstan"), "Box", () => ImportarGrupo(g, AdaptarAlCargar.NoModificar), Destructiva: true),
            new(IdiomaInterfaz.T("Cajas.UsarAjustesActuales"), IdiomaInterfaz.T("Cajas.DigaEscrituraRanuras"), "Settings", () => ImportarGrupo(g, AdaptarAlCargar.SegunAjustes), Destructiva: true),
        };
        Mostrar(new MenuHojaViewModel(IdiomaInterfaz.T("Cajas.CargarGrupo"), [new SeccionHoja(MessageStrings.MsgSaveBoxImportModifyIntro.TrimEnd(':', ' '), filas)],
            IdiomaInterfaz.F("Cajas.ContienePokemon", string.Format(MessageStrings.MsgSaveBoxImportGroup, NombreCaja), MessageStrings.MsgSaveBoxImportOverwrite, g.Cantidad), pie: CargaCajas.PieDeshacer));
    }

    /// <summary>Pone el grupo en la caja actual como <c>SAVEditor.ImportGroup</c>: el Pokémon N en la casilla N, saltando los protegidos y los que no se pueden convertir al formato de la partida.</summary>
    public void ImportarGrupo(ImportadoGrupo g, AdaptarAlCargar adaptar)
    {
        var sav = Sesion.Guardado;
        var casillas = Enumerable.Range(0, sav.BoxSlotCount).Select(i => (ISlotInfo)new SlotInfoBox(Caja, i, sav)).ToList();
        var omitidas = 0;
        var tocadas = Sesion.Casillas.Lote(casillas, () =>
        {
            var tipo = sav.PKMType; var i = 0; var n = 0;
            foreach (var x in g.Grupo.Contents)
            {
                var ranura = i++;
                if (ranura >= sav.BoxSlotCount || sav.IsBoxSlotOverwriteProtected(Caja, ranura)) { omitidas++; continue; }
                var conv = EntityConverter.ConvertToType(x, tipo, out _);
                if (conv?.GetType() != tipo) { omitidas++; continue; }
                sav.SetBoxSlotAtIndex(conv, Caja, ranura, CargaCajas.Ajustes(adaptar));   // PKHeX escribe «x» (sin convertir) tras comprobar la conversión: con el mismo formato es lo mismo (ADR 0078)
                n++;
            }
            return n;
        });
        if (tocadas > 0) Resultado(InformeOperacion.Ok((omitidas > 0 ? string.Format(MessageStrings.MsgSaveBoxImportSkippedLocked, omitidas) : MessageStrings.MsgSaveBoxImportGroupSuccess).TrimEnd('.') + ". " + CargaCajas.AvisoDeshacer(adaptar), tocadas, omitidas));
        else Resultado(InformeOperacion.Falla(TipoResultado.Fallo, IdiomaInterfaz.T("Cajas.PodidoCargarNingunPokemon")));
    }

    // ---- Compartir (bloque 10-9): la hoja de compartir del sistema, solo si el usuario la abre ----
    private void CompartirArchivo(PKM pk, bool cifrado)
    {
        if (_anfitrion?.Compartir is not { } sistema) { Aviso(IdiomaInterfaz.T("Cajas.CompartirDisponibleAqui"), ok: false); return; }
        try
        {
            var archivo = ModeloCompartir.Pokemon(pk, cifrado);
            if (sistema.CompartirArchivo(IdiomaInterfaz.T("Editor.CompartirPokemon2"), archivo.Nombre, archivo.Datos, archivo.TipoMime) is { } motivo) Aviso(motivo, ok: false);
        }
        catch (Exception ex) { Aviso(IdiomaInterfaz.T("Editor.PodidoCompartir") + ex.Message, ok: false); }
    }

    private void CompartirShowdown(PKM pk)
    {
        if (_anfitrion?.Compartir is not { } sistema) { Aviso(IdiomaInterfaz.T("Cajas.CompartirDisponibleAqui"), ok: false); return; }
        if (sistema.CompartirTexto(IdiomaInterfaz.T("Cajas.SetShowdown"), ModeloShowdown.Exportar(pk, AjustesPkhexActuales)) is { } motivo) Aviso(motivo, ok: false);
    }

    // ---- Showdown de la partida (PK-MENU-011/012 y PK-IO-047): equipo, caja actual y todas las cajas ----
    /// <summary>Los ajustes de PKHeX vigentes de la ventana (los de fábrica en los anfitriones de prueba).</summary>
    private Ajustes.Pkhex.AjustesPkhex AjustesPkhexActuales => _anfitrion?.AjustesDePkhex ?? new Ajustes.Pkhex.AjustesPkhex();

    private async Task ExportarShowdownDePartidaAsync(ZonaShowdown zona)
    {
        var p = _anfitrion?.Portapapeles;
        if (p is null) return;
        var sav = Sesion.Guardado; var a = AjustesPkhexActuales; var caja = Caja;
        var texto = await Task.Run(() => zona switch
        {
            ZonaShowdown.Equipo => ModeloShowdown.ExportarEquipo(sav, a),
            ZonaShowdown.CajaActual => ModeloShowdown.ExportarCaja(sav, caja, a),
            _ => ModeloShowdown.ExportarTodasLasCajas(sav, a),
        });
        if (string.IsNullOrWhiteSpace(texto)) { Aviso(zona == ZonaShowdown.Equipo ? IdiomaInterfaz.T("Cajas.EquipoTieneNingunPokemon") : IdiomaInterfaz.T("Cajas.NingunPokemonExportar"), ok: false); return; }
        var ok = await p.CopiarAsync(texto);
        Aviso(ok ? ModeloShowdown.AvisoExito(zona == ZonaShowdown.Equipo) : MessageStrings.MsgClipboardFailWrite, ok);
    }

    /// <summary>La sección «Showdown» del menú ⋮: solo con lo que la partida tiene (equipo y/o cajas), como los elementos del menú de PKHeX que se ocultan sin <c>HasParty</c> o <c>HasBox</c>.</summary>
    private IEnumerable<SeccionHoja> SeccionShowdown()
    {
        var sav = Sesion.Guardado;
        var motivo = _anfitrion?.Portapapeles is null ? IdiomaInterfaz.T("Editor.PortapapelesDisponible") : null;
        var entradas = new List<EntradaHoja>();
        if (sav.HasParty) entradas.Add(new(IdiomaInterfaz.T("Cajas.ExportarEquipo"), IdiomaInterfaz.T("Cajas.CopiaSetsShowdownEquipo"), "ClipboardCopy", () => _ = ExportarShowdownDePartidaAsync(ZonaShowdown.Equipo), Motivo: motivo));
        if (sav.HasBox)
        {
            entradas.Add(new(IdiomaInterfaz.T("Cajas.ExportarCaja"), IdiomaInterfaz.F("Cajas.CopiaSetsPortapapeles", NombreCaja), "ClipboardCopy", () => _ = ExportarShowdownDePartidaAsync(ZonaShowdown.CajaActual), Motivo: motivo));
            entradas.Add(new(IdiomaInterfaz.T("Cajas.ExportarTodasCajas"), IdiomaInterfaz.T("Cajas.CopiaSetsTodasCajas"), "ClipboardCopy", () => _ = ExportarShowdownDePartidaAsync(ZonaShowdown.TodasLasCajas), Motivo: motivo));
        }
        if (entradas.Count > 0) yield return new SeccionHoja(IdiomaInterfaz.T("Ajustes.Pkhex.Grupo.16"), entradas);
    }

    private enum ZonaShowdown { Equipo, CajaActual, TodasLasCajas }
}
