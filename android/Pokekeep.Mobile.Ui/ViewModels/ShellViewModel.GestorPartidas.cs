using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Guardados;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

public sealed partial class ShellViewModel
{
    private IndicePartidas? Indice => _servicios?.CarpetaPrivada is { } ruta ? new IndicePartidas(ruta) : null;

    [RelayCommand]
    private async Task DesplegarRecientes()
    {
        await ValidarRecientesAsync();
        TextosMenuHoja Textos() => new(IdiomaInterfaz.T("Gestor.Recientes"),
            [new(null, [.. Recientes.Select(f => new EntradaHoja(f.Nombre, f.Detalle, "HardDrive", () => AbrirRecienteCommand.Execute(f)) { Juego = f.Juego })]),
             new(null, [new(IdiomaInterfaz.T("Gestor.Todas"), null, "Layers", () => AbrirGestorPartidasCommand.Execute(null))])]);
        var textos = Textos();
        AbrirHoja(new MenuHojaViewModel(textos.Titulo, textos.Secciones, actualizarTextos: Textos));
    }

    [RelayCommand]
    private void AbrirGestorPartidas()
    {
        TextosMenuHoja Textos()
        {
            var datos = Indice?.Leer();
            var secciones = new List<SeccionHoja>
            {
                new(null, [new(IdiomaInterfaz.T("Gestor.ElegirCarpeta"), IdiomaInterfaz.T("Gestor.Recursivo"), "FolderOpen",
                    () => _ = ElegirCarpetaPartidas(), Motivo: Carpetas is null ? IdiomaInterfaz.T("Cajas.ExportarDisponibleAqui") : null)])
            };
            if (datos is not null)
            {
                secciones.Add(new(IdiomaInterfaz.T("ListaCarpetas.Carpetas"), [.. datos.Carpetas.Select(c =>
                    new EntradaHoja(c.Nombre, IdiomaInterfaz.T("Gestor.Recursivo"), "FolderOpen", () => _ = ListarCarpetaPartidas(c)))]));
                foreach (var grupo in datos.Partidas.GroupBy(p => p.Carpeta))
                    secciones.Add(new(grupo.Key, [.. grupo.Select(p => new EntradaHoja(p.Documento.Nombre, JuegoDeReciente(p.Juego), "HardDrive",
                        () => _ = AbrirPartidaRecordada(p.Documento)) { Juego = Design.LogosPartida.VersionDeNombre(p.Juego) })]));
            }
            return new(IdiomaInterfaz.T("Gestor.Todas"), secciones);
        }
        var textos = Textos();
        AbrirHoja(new MenuHojaViewModel(textos.Titulo, textos.Secciones, actualizarTextos: Textos));
    }

    private Task ElegirCarpetaPartidas() => Proteger(async () =>
    {
        if (Carpetas is null) return;
        var carpeta = await Carpetas.ElegirCarpetaAsync(CancellationToken.None);
        if (carpeta is null) return;
        Indice?.Recordar(carpeta);
        await ListarCarpetaPartidasSinProteger(carpeta);
    });

    private Task ListarCarpetaPartidas(CarpetaRef carpeta) => Proteger(() => ListarCarpetaPartidasSinProteger(carpeta));

    private async Task ListarCarpetaPartidasSinProteger(CarpetaRef carpeta)
    {
        var modelo = new ModeloCarpetas(AjustesPkhex, Documentos, Carpetas);
        var partidas = await Task.Run(() => modelo.DeCarpetaAsync(new(carpeta.Nombre, carpeta.Uri, false, true, string.Empty), CancellationToken.None));
        TextosMenuHoja Textos() => new(carpeta.Nombre, [new(null, [.. partidas.Where(p => p.Documento is not null).Select(p =>
            new EntradaHoja(p.Nombre, p.JuegoCorto + " · " + p.Carpeta, "HardDrive", () => _ = AbrirPartidaRecordada(p.Documento!)) { Juego = p.Partida.Version })])],
            Pie: partidas.Count == 0 ? IdiomaInterfaz.T("Gestor.SinPartidas") : IdiomaInterfaz.F("Gestor.Cuenta", partidas.Count));
        var textos = Textos();
        AbrirHoja(new MenuHojaViewModel(textos.Titulo, textos.Secciones, pie: textos.Pie, actualizarTextos: Textos));
    }

    private Task AbrirPartidaRecordada(DocumentoRef documento) => Proteger(async () =>
    {
        if (Documentos is null) return;
        var actual = await Documentos.ObtenerAsync(documento.Uri, CancellationToken.None);
        if (actual is null) { Informar(IdiomaInterfaz.T("Gestor.NoDisponible"), "aviso", "CircleAlert"); return; }
        await AbrirDocumentoSinProteger(actual, Pokekeep.Mobile.Ui.Abrir.OrigenArchivo.Selector);
    });
}
