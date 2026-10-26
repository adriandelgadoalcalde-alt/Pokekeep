// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Abrir;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Herramientas.ForzarCarga;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>
/// «Partida &gt; Abrir archivo…», «Abrir con Pokekeep» y «Compartir con Pokekeep» (bloque 10-2): TODO lo que llega se reconoce con el mismo despachador (<see cref="ReconocedorArchivos"/>, el <c>Main.LoadFile</c> de
/// PKHeX) y se hace lo mismo que PKHeX con cada tipo: una partida se abre como copia de trabajo; un Pokémon, una tarjeta de regalo, un volcado de caja o un grupo enseñan una ficha con sus acciones sobre la
/// partida abierta (o piden abrir una primero); una tarjeta de GameCube con varias partidas pregunta cuál; lo que no se reconoce lo dice con el mensaje de PKHeX y qué formatos lee Pokekeep.
/// Aparte de <c>ShellViewModel.cs</c> para que los bloques de la F9 y la F10 no se pisen.
/// </summary>
public sealed partial class ShellViewModel
{
    /// <summary>La última apertura despachada (pruebas sin pantalla: qué se reconoció y qué se enseñó).</summary>
    public Apertura? UltimaApertura { get; private set; }
#if POKEKEEP_PRUEBAS
    /// <summary>Solo el APK de pruebas: confirma el paso por el despachador real, sin registrar datos del Pokémon o entrenador.</summary>
    public static Action<string>? DiagnosticoEntrada { get; set; }
#endif

    /// <summary>Las «Otras extensiones de partidas» del ajuste de PKHeX, leídas EN EL MOMENTO (PK-IO-003).</summary>
    private IEnumerable<string> ExtensionesExtra => AjustesPkhex.Actual.Backup.OtherSaveFileExtensions;

    /// <summary>«Abrir archivo…» (PK-MENU-002, PK-IO-001): el selector del sistema sin filtro (todo archivo; decide el Core por contenido y extensión) y el despachador.</summary>
    [RelayCommand]
    private Task AbrirArchivo() => Proteger(async () =>
    {
        if (_servicios?.Abridor is null || Documentos is null) { Informar(IdiomaInterfaz.T("Shell.AbrirArchivosDisponibleVentana"), "aviso", "CircleAlert"); return; }
        var doc = await Documentos.ElegirParaAbrirAsync(CancellationToken.None);
        if (doc is not null) await AbrirDocumentoSinProteger(doc, OrigenArchivo.Selector);
    });

    /// <summary>Un archivo que otra app pasa a Pokekeep («Abrir con» o «Compartir»; F10-ABRIR-001/002).</summary>
    public Task AbrirDocumentoAsync(DocumentoRef documento, OrigenArchivo origen) => Proteger(() => AbrirDocumentoSinProteger(documento, origen));

    /// <summary>
    /// Recoge UN archivo del buzón (llegó antes de que la Shell estuviera lista, o con la app abierta) y lo despacha. Uno por llamada: si es una partida, la Shell se sustituye por otra y es la nueva la que
    /// atiende el siguiente (quien hospeda la Shell la llama al crearla y en cada <see cref="EntradasExternas.Llegada"/>).
    /// </summary>
    public async Task AtenderEntradasExternasAsync()
    {
        if (!Ocupado && !_sustituida && EntradasExternas.TryTomar(out var e)) await AbrirDocumentoAsync(e.Documento, e.Origen);
    }

    /// <summary>Esta Shell ya pasó la partida a otra (una partida nueva sustituye a la Shell): la vieja no atiende más el buzón.</summary>
    private bool _sustituida;

    /// <summary>El ÚNICO punto que pasa la partida a otra Shell (<see cref="SesionNueva"/>); antes, <see cref="ConfirmarCambioDePartida"/> ya preguntó si hacía falta.</summary>
    private void Sustituir(Sesion s)
    {
        _sustituida = true;
        SesionNueva?.Invoke(s);
    }

    /// <summary>
    /// Si llegó un archivo mientras la Shell estaba ocupada (guardando, exportando, abriendo), se atiende al quedar libre: no se queda en la cola sin aviso. PKHeX, en <c>OpenQuick</c>, lo descarta con un aviso;
    /// en el móvil la otra app ya cerró y el usuario no sabría que tiene que repetirlo, así que se abre en cuanto se puede.
    /// </summary>
    partial void OnOcupadoChanged(bool value)
    {
        if (!value && !_sustituida && EntradasExternas.Pendientes > 0) _ = AtenderEntradasExternasAsync();
    }

    private async Task AbrirDocumentoSinProteger(DocumentoRef documento, OrigenArchivo origen)
    {
        if (_servicios?.Abridor is not { } abridor) { Informar(IdiomaInterfaz.T("Shell.AbrirArchivosDisponibleVentana"), "aviso", "CircleAlert"); return; }
        var apertura = await abridor.LeerAsync(documento, origen, Sesion?.Guardado, ExtensionesExtra, CancellationToken.None);
        await DespacharAsync(apertura);
    }

    /// <summary>Bytes que no vienen de un documento (el hexadecimal pegado de «Importar hexadecimal»): el mismo despachador que un archivo.</summary>
    public Task AbrirBytesAsync(byte[] datos, string nombre, OrigenArchivo origen) => Proteger(() => DespacharAsync(AbridorArchivos.DesdeBytes(datos, nombre, origen, Sesion?.Guardado, ExtensionesExtra)));

    /// <summary>El <c>LoadFile</c> de PKHeX con el resultado del reconocedor.</summary>
    internal async Task DespacharAsync(Apertura a)
    {
        UltimaApertura = a;
#if POKEKEEP_PRUEBAS
        DiagnosticoEntrada?.Invoke($"nombre={a.Nombre}\torigen={a.Origen}\ttipo={a.Contenido.GetType().Name}\tsoloLectura={a.Documento?.SoloLectura}");
#endif
        switch (a.Contenido)
        {
            case ArchivoPartida p:
                {
                    if (p.Partida.Generation <= 3) SaveLanguage.TryRevise(p.Partida);
                    var versiones = ModeloForzarCarga.VersionesAElegir(p.Partida, false, out var actual);
                    if (versiones.Count > 0)
                    {
                        var titulo = $"{string.Format(MessageStrings.MsgFileLoadVersionDetect, $"3 ({NombresJuego.Corto(p.Partida.Version)})")} {MessageStrings.MsgFileLoadSaveSelectVersion}\n{a.Nombre}";
                        AbrirHoja(new SelectorHojaViewModel(titulo, [.. versiones.Select((v, i) => new OpcionSelector(i, v.Texto))], actual, o =>
                        {
                            if (o.Id < 0 || o.Id >= versiones.Count || ModeloForzarCarga.AplicarVersion(p.Partida, versiones[o.Id].Valor, false) is not { } elegida)
                            {
                                Informar(MessageStrings.MsgFileLoadSaveLoadFail, "aviso", "CircleAlert");
                                return;
                            }
                            _ = Proteger(() => AbrirPartidaReconocida(a, p with { Partida = elegida }));
                        }));
                        break;
                    }
                    await AbrirPartidaReconocida(a, p);
                    break;
                }
            case ArchivoTarjetaGC t:
                ElegirPartidaDeTarjeta(a, t);
                break;
            case ArchivoNoReconocido n:
                AbrirHoja(new InformeHojaViewModel(IdiomaInterfaz.T("Shell.PuedeAbrirArchivo"), $"{n.Mensaje}\n\n{n.Detalle}", estado: IdiomaInterfaz.F("Shell.Tocado", a.Nombre), claseEstado: "aviso"));
                break;
            default:
                MostrarFicha(a);
                break;
        }
    }

    private async Task AbrirPartidaReconocida(Apertura a, ArchivoPartida p)
    {
        // SlotWrite.ModifyUnset (bloque 10-3): se pregunta ANTES de crear la sesión, como Main.OpenSAV, que no anota el reciente ni fija el entrenador reciente hasta el «Sí».
        var nombre = a.Documento is null ? AbridorArchivos.NombreSinDocumento(p.Partida) : a.Nombre;
        await ConfirmarCambioDePartida(TituloDePartida(nombre, p.Partida.Version), async () =>
        {
            Sesion s;
            if (_servicios?.Abridor is { } abridor) s = await abridor.AbrirPartidaAsync(a, p, CancellationToken.None);
            else s = new Sesion(p.Partida, AbridorArchivos.NombreSinDocumento(p.Partida)) { Envoltorio = p.Envoltorio };
            Sustituir(s);
        });
    }

    /// <summary>PK-MENU-048, PK-IO-007: «Múltiples juegos detectados. Escoge un juego para editar.» con los juegos que de verdad tiene la tarjeta.</summary>
    private void ElegirPartidaDeTarjeta(Apertura a, ArchivoTarjetaGC t)
    {
        var entradas = t.Juegos.Select(j => new EntradaHoja(ReconocedorArchivos.NombreJuegoTarjeta(j), IdiomaInterfaz.T("Shell.PartidaTarjetaMemoria"), "HardDrive",
            () => _ = Proteger(() => DespacharAsync(a with { Contenido = ReconocedorArchivos.AbrirDeTarjeta(t.Datos, t.Nombre, j) })))).ToList();
        AbrirHoja(new MenuHojaViewModel(MessageStrings.MsgFileLoadSaveMultiple, [new SeccionHoja(MessageStrings.MsgFileLoadSaveSelectGame, entradas)], t.Nombre));
    }

    /// <summary>
    /// F10-ABRIR-004/005, PK-IO-015/062/065: la ficha de un Pokémon, una tarjeta de regalo, un encuentro (GP1), un volcado de caja o un grupo. Sin partida, pide abrir una. Con partida, el Core lo convierte al
    /// formato de la partida (<c>EntityConverter.ConvertToType</c>, con los ajustes de conversión vigentes) y ofrece lo mismo que PKHeX al soltarlo: al editor o a la caja.
    /// </summary>
    private void MostrarFicha(Apertura a)
    {
        var tipo = DescribirTipo(a.Contenido);
        if (Sesion is null || Cajas is null || a.Datos is null)
        {
            AbrirHoja(new ConfirmarHojaViewModel(IdiomaInterfaz.F("Shell.AbreAntesPartida", tipo),
                [IdiomaInterfaz.F("Shell.0Es1", a.Nombre, tipo.ToLowerInvariant()), IdiomaInterfaz.T("Shell.AnadirloCajaEditarloHace")],
                IdiomaInterfaz.T("Shell.AbrirPartida"), () => AbrirArchivoCommand.Execute(null)));
            return;
        }
        var importado = Intercambio.Analizar(a.Datos, a.Nombre, Sesion.Guardado);
        switch (importado)
        {
            case ImportadoError e:
                AbrirHoja(new InformeHojaViewModel(IdiomaInterfaz.T("Shell.PuedeUsarPartida"), IdiomaInterfaz.F("Shell.01Es2", e.Mensaje, a.Nombre, tipo.ToLowerInvariant()), estado: IdiomaInterfaz.T("AbridorArchivos.ArchivoTocado"), claseEstado: "aviso"));
                return;
            case ImportadoPokemon p:
                {
                    var nombre = p.Pokemon.Species < Sesion.Textos.Species.Count ? Sesion.Textos.Species[p.Pokemon.Species] : tipo;
                    var libre = PrimeraCasillaLibre();
                    var lineas = new List<string>();
                    try { lineas.Add(Intercambio.Resumen(p.Pokemon)); } catch { }
                    lineas.AddRange(p.Avisos);
                    var entradas = new List<EntradaHoja>
                    {
                        new(IdiomaInterfaz.T("Libreria.Vista.12"), libre is null ? null : IdiomaInterfaz.F("Shell.PrimeraCasillaLibrePuede", libre.Descripcion), "Box",
                            () => { Destino = Destino.Pokemon; Cajas.ProcesarImportado(p, null); }, Motivo: libre is null ? IdiomaInterfaz.T("Shell.NingunaCasillaLibreCajas") : null),
                        new(IdiomaInterfaz.T("Shell.AbrirEditor"), IdiomaInterfaz.T("Shell.EnsenaEditorAplicarVa"), "Pencil",
                            () => AbrirEnEditor(a, p, libre!), Motivo: libre is null ? IdiomaInterfaz.T("Shell.NingunaCasillaLibreCajas") : null),
                    };
                    AbrirHoja(new MenuHojaViewModel(nombre, [new SeccionHoja(null, entradas)], $"{tipo} · {a.Nombre}", pie: string.Join("\n", lineas)));
                    return;
                }
            case ImportadoCajas c:
                {
                    var todas = Sesion.Guardado.GetPCBinary().Length == c.Datos.Length;
                    AbrirHoja(new MenuHojaViewModel(todas ? IdiomaInterfaz.T("Shell.VolcadoTodoPC") : IdiomaInterfaz.T("Shell.VolcadoCaja"),
                        [new SeccionHoja(null, [new(todas ? IdiomaInterfaz.T("Cajas.CargarTodoPC") : IdiomaInterfaz.T("Shell.CargarCajaActual"), IdiomaInterfaz.T("Shell.PideConfirmacionPuedeDeshacer"), "Upload", () => { Destino = Destino.Pokemon; Cajas.ProcesarImportado(c, null); })])], a.Nombre));
                    return;
                }
            case ImportadoGrupo g:
                AbrirHoja(new MenuHojaViewModel($"{tipo} ({g.Cantidad} Pokémon)",
                    [new SeccionHoja(null, [new(IdiomaInterfaz.T("Shell.CargarCajaActual"), IdiomaInterfaz.T("Shell.PideConfirmacionPuedeDeshacer"), "Upload", () => { Destino = Destino.Pokemon; Cajas.ProcesarImportado(g, null); })])], a.Nombre));
                return;
        }
    }

    /// <summary>
    /// «Abrir en el editor»: como <c>OpenPKM</c>/<c>OpenMysteryGift</c> de PKHeX, que llevan la entidad al editor tras <c>AdaptToSaveFile</c> (siempre en un regalo; en un Pokémon solo si no hubo conversión y la
    /// partida es exportable). El editor la aplica en la casilla libre elegida, por el historial (Deshacer exacto).
    /// </summary>
    private void AbrirEnEditor(Apertura a, ImportadoPokemon p, CasillaRef destino)
    {
        if (Sesion is null) return;
        var pk = p.Pokemon;
        var sinConvertir = a.Contenido is ArchivoPokemon ap && ap.Pokemon.GetType() == Sesion.Guardado.PKMType;
        if (a.Contenido is ArchivoRegalo || (sinConvertir && Sesion.Guardado.State.Exportable)) Sesion.Guardado.AdaptToSaveFile(pk);
        Destino = Destino.Pokemon;
        AbrirEditor(destino, pk);
    }

    /// <summary>La primera casilla de caja vacía y escribible, empezando por la caja que se está viendo (como «Importar» de la pestaña Pokémon).</summary>
    private CasillaRef? PrimeraCasillaLibre()
    {
        if (Sesion is null || Cajas is null) return null;
        var sav = Sesion.Guardado;
        if (!sav.HasBox) return null;
        for (var b = 0; b < sav.BoxCount; b++)
        {
            var caja = (Cajas.Caja + b) % sav.BoxCount;
            for (var s = 0; s < sav.BoxSlotCount; s++)
            {
                var info = new SlotInfoBox(caja, s, sav);
                if (info.CanWriteTo(sav) && info.Read(sav).Species == 0) return new CasillaRef(info, caja);
            }
        }
        return null;
    }

    private static string DescribirTipo(ArchivoReconocido c) => c switch
    {
        ArchivoPokemon => IdiomaInterfaz.T("ShellViewModelAbrir.UnPokemon"),
        ArchivoRegalo => IdiomaInterfaz.T("Shell.TarjetaRegaloMisterioso"),
        ArchivoEncuentro => IdiomaInterfaz.T("Shell.PokemonComplejoGO"),
        ArchivoCajas => IdiomaInterfaz.T("Shell.VolcadoCajas"),
        ArchivoGrupo g => g.Grupo is IBattleVideo ? IdiomaInterfaz.T("Shell.VideoBatalla") : IdiomaInterfaz.T("Shell.EquipoPokemon"),
        _ => IdiomaInterfaz.T("Shell.Archivo"),
    };

    /// <summary>Al abrir una partida que llegó sin permiso de escritura (o dentro de un contenedor), la Shell nueva lo dice una vez (F10-ABRIR-003).</summary>
    private void AvisoDeApertura(Sesion? sesion)
    {
        if (sesion?.Documento is { SoloLectura: true })
            Informar(IdiomaInterfaz.T("Shell.AbiertaSoloLeerPulsa"), "aviso", "CircleAlert", IdiomaInterfaz.T("Shell.GuardarOriginal"), GuardarEnOriginalCommand);
        else if (sesion?.Envoltorio.Descripcion is { } dentro && sesion.Documento is not null)
            Informar(IdiomaInterfaz.F("Shell.PartidaAbiertaGuardarEscribe", dentro), "ok", "CircleCheck");
    }
}
