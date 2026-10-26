// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Herramientas;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>
/// La parte de la Shell que conoce las herramientas y los ajustes de PKHeX de la F10 (ADR 0072): aparte para que los bloques de la F10 y la F9 no se pisen en <c>ShellViewModel.cs</c>.
/// Aquí vive <b>todo</b> lo que la Shell hace por «Más &gt; Herramientas» y «Más &gt; Ajustes &gt; PKHeX»; un bloque nuevo NO tiene que tocar la Shell (declara su herramienta en <c>CatalogoHerramientas.B10_N.cs</c>).
/// </summary>
public sealed partial class ShellViewModel
{
    /// <summary>Ajustes de PKHeX (ajustes-pkhex.json). Los pone quien crea la Shell; sin ellos valen los de fábrica en memoria y NO tocan el Core.</summary>
    public IAjustesPkhex AjustesPkhex
    {
        get;
        set { SuscribirAjustesPkhex(field, value); field = value; AlCambiarAjustesPkhex(); }   // la cabecera sigue a Privacy.HideSAVDetails (bloque 10-3)
    } = new AjustesPkhexEnMemoria(aplicar: false);

    /// <summary>El contexto que reciben las herramientas: siempre la partida y los ajustes VIGENTES de la Shell.</summary>
    public ContextoHerramienta ContextoDeHerramientas() => new(Sesion, AjustesPkhex, AbrirHoja, (t, c, g) => Informar(t, c, g), () => Portapapeles, () => Documentos, () => Carpetas, p => Pantalla = p)
    {
        AbrirBytes = (d, n) => AbrirBytesAsync(d, n, Pokekeep.Mobile.Ui.Abrir.OrigenArchivo.Hexadecimal),
        // Bloque 10-4: «Lista de carpetas» y «Forzar carga» abren por el despachador (documentos) o con el cerrojo de la Shell (sin documento).
        Servicios = _servicios,
        AbrirDocumento = _servicios?.Abridor is null ? null : d => AbrirDocumentoAsync(d, Pokekeep.Mobile.Ui.Abrir.OrigenArchivo.Selector),
        AbrirSesion = AbrirSinDocumentoAsync,
        CajaActual = () => Cajas?.Caja ?? 0,
        Acciones = _acciones,
        LeerCarpeta = LectorCarpetasParaHerramientas(),
    };

    private Func<Almacen.CarpetaRef, CancellationToken, Task<IReadOnlyList<(string Nombre, byte[] Datos)>>>? LectorCarpetasParaHerramientas()
    {
        if (Carpetas is not { } carpetas || Documentos is not { } documentos) return null;
        return async (carpeta, cancelar) => [.. (await Pokekeep.Mobile.Ui.Cajas.CargaCajas.LeerArchivosAsync(await carpetas.ListarAsync(carpeta, cancelar), documentos, cancelar)).Select(a => (a.Nombre, a.Datos))];
    }

    [RelayCommand]
    private void AbrirHerramientas()
    {
        Pantalla = new HerramientasViewModel(ContextoDeHerramientas(), CerrarPantalla);
    }

    /// <summary>Añade «Herramientas» a «Más», justo tras «Edición masiva» (las dos son de trabajo sobre la partida).</summary>
    private IReadOnlyList<EntradaMenu> ConEntradasHerramientas(IReadOnlyList<EntradaMenu> entradas)
    {
        var lista = entradas.ToList();
        var indice = lista.FindIndex(e => e.ClaveTitulo == "Menu.Mas.Masivas.Titulo");
        lista.Insert(indice < 0 ? Math.Min(2, lista.Count) : indice + 1, EntradaMenu.Localizada("Menu.Mas.Herramientas.Titulo", "Menu.Mas.Herramientas.Descripcion", "ScanSearch", "F10", AbrirHerramientasCommand));
        return lista;
    }

    /// <summary>
    /// Navegación de pruebas y capturas a las pantallas de la F10 (el equivalente de <c>Navegar</c> para los bloques nuevos; un bloque no toca la Shell): <c>herramientas</c> (el concentrador), <c>herramienta=&lt;id&gt;</c> o
    /// <c>herramienta=&lt;id&gt;:&lt;estado&gt;</c> (una herramienta, opcionalmente en uno de sus <see cref="EstadoPantalla"/>) y <c>ajustes-pkhex=&lt;grupo&gt;</c> (un grupo de ajustes de PKHeX).
    /// </summary>
    private bool NavegarF10(string pantalla, out bool resultado)
    {
        resultado = false;
        if (pantalla == "herramientas") { Destino = Destino.Mas; AbrirHerramientas(); resultado = ViendoPantalla; return true; }
        if (pantalla.StartsWith("herramienta=", StringComparison.Ordinal))
        {
            var partes = pantalla["herramienta=".Length..].Split(':', 2);
            Destino = Destino.Mas;
            if (CatalogoHerramientas.Buscar(partes[0]) is not { } def) return true;
            AbrirHerramientas();
            var hub = (HerramientasViewModel)Pantalla!;
            if (def.Motivo(Sesion) is not null) return true;
            hub.Abrir(def);
            if (Pantalla is HerramientaViewModel vm && partes.Length > 1)
            {
                var estado = def.Estados?.FirstOrDefault(e => e.Id == partes[1]);
                if (estado is null) return true;
                estado.Preparar(vm);
            }
            resultado = Pantalla is HerramientaViewModel;
            return true;
        }
        if (pantalla.StartsWith("ajustes-pkhex=", StringComparison.Ordinal))
        {
            Destino = Destino.Mas; AbrirAjustes();
            var grupo = pantalla["ajustes-pkhex=".Length..];
            if (Pantalla is AjustesViewModel a && a.GruposPkhex.FirstOrDefault(g => g.Nombre == grupo) is { } entrada) { entrada.Abrir.Execute(null); resultado = Pantalla is AjustesPkhexGrupoViewModel; }
            return true;
        }
        return false;
    }
}
