// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Modelo;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>
/// Bloque 10-3: lo que la Shell hace con dos ajustes de PKHeX que se notan en la cabecera y al cambiar de partida.
/// <list type="bullet">
/// <item><c>SlotWrite.ModifyUnset</c> (PK-SET-058, PK-MENU-006): PKHeX, al cargar otra partida con la actual modificada (<c>Main.OpenSAV</c>: <c>State.Edited &amp;&amp; ModifyUnset</c>), pregunta
/// «Cualquier cambio no guardado se perderá. ¿Estás seguro de que quieres cargar un nuevo archivo de guardado?». Aquí, la misma pregunta con los textos del Core en castellano, antes de enseñar la partida nueva.
/// En Android no hay «cerrar el programa» (<c>Main_FormClosing</c>): el sistema cierra y reanuda la app y la copia de recuperación (F7) conserva la sesión; por eso el aviso es al cambiar de partida.
/// Además de los cambios aplicados cuenta lo que tenga un borrador sin aplicar (la Mochila, un editor de la partida, el editor de entidad), que también se perdería.</item>
/// <item><c>Privacy.HideSAVDetails</c> (PK-SET-059, PK-MENU-043): la cabecera (el «título» de PKHeX, <c>GetProgramTitle</c>) deja solo la versión del juego, sin el nombre del archivo.</item>
/// </list>
/// </summary>
public sealed partial class ShellViewModel
{
    /// <summary>¿La cabecera oculta los detalles de la partida? Se lee en el momento (los ajustes de la Shell).</summary>
    public bool OcultarDetallesPartida => AjustesPkhex.Actual.Privacy.HideSAVDetails;

    /// <summary>Título de la cabecera: el nombre del archivo, o solo el juego con <c>Privacy.HideSAVDetails</c> (PKHeX: <c>title + $"[{version}]"</c>).</summary>
    private string TituloDePartida(Sesion s) => TituloDePartida(s.NombreArchivo, s.Guardado.Version);

    /// <summary>El mismo título de una partida aún sin sesión (la pregunta de <c>ModifyUnset</c> se hace antes de crearla).</summary>
    private string TituloDePartida(string nombreArchivo, GameVersion version) => OcultarDetallesPartida ? NombresJuego.Corto(version) : nombreArchivo;

    /// <summary>Segunda línea de la tarjeta: el juego con su nombre oficial («Pokémon Espada»), porque el nombre del archivo de los guardados modernos («main») no lo dice (QA uso 6). Con <c>HideSAVDetails</c> el juego ya es el título y no se repite.</summary>
    // uso9 (propietario, 19-oct): con el logo del juego a la derecha, la segunda línea ya no repite el juego (la cabecera no se ensancha);
    // sin logo lo sigue diciendo. El nombre accesible (ResumenPartida) siempre lo dice: un logo no se lee.
    private string SubtituloDePartida(Sesion s) => OcultarDetallesPartida ? IdiomaInterfaz.F("Cabecera.CopiaLocal", s.Plataforma)
        : Design.LogosPartida.HayLogo(JuegoPartida, IdiomaPartida) ? IdiomaInterfaz.T("Cabecera.SoloCopiaLocal") : IdiomaInterfaz.F("Cabecera.JuegoCopiaLocal", s.JuegoOficial);

    /// <summary>Título de la tarjeta compacta: solo el nombre del juego (en una línea si cabe y como mucho en dos). El nombre del archivo no cabe con letra grande: sigue en el nombre accesible (<c>ResumenPartida</c>) y se ve al tocar la tarjeta (<c>MostrarArchivoDePartida</c>).</summary>
    private string TituloCompactoDePartida(Sesion s) => OcultarDetallesPartida ? TituloDePartida(s) : s.JuegoOficial;

    /// <summary>Toque en la tarjeta compacta: enseña el nombre del archivo, que la tarjeta ya no lleva (con Privacy.HideSAVDetails no hay nada que enseñar).</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void MostrarArchivoDePartida()
    {
        if (Sesion is null || OcultarDetallesPartida) return;
        Informar(IdiomaInterfaz.F("Cabecera.Archivo", Sesion.NombreArchivo), "info", "Info");
    }

    private Action? _suscripcionAjustes;

    /// <summary>
    /// La Shell escucha los cambios de ajustes SIN que el almacén (que vive todo el proceso) la retenga: se crea una Shell por partida y la vieja debe poder liberarse. El manejador solo guarda una referencia débil
    /// y se da de baja solo cuando la Shell ya no existe.
    /// </summary>
    private void SuscribirAjustesPkhex(IAjustesPkhex anterior, IAjustesPkhex nuevo)
    {
        if (_suscripcionAjustes is not null) anterior.Cambiado -= _suscripcionAjustes;
        var debil = new WeakReference<ShellViewModel>(this);
        var hiloDeInterfaz = SynchronizationContext.Current;   // la Shell se crea en el hilo de interfaz; <c>Cambiado</c> lo dispara quien llame a <c>Guardar</c>, y las propiedades enlazadas solo se notifican desde aquel hilo
        Action? manejador = null;
        manejador = () =>
        {
            if (!debil.TryGetTarget(out var shell)) { nuevo.Cambiado -= manejador; return; }
            if (hiloDeInterfaz is null || ReferenceEquals(SynchronizationContext.Current, hiloDeInterfaz)) shell.AlCambiarAjustesPkhex();
            else hiloDeInterfaz.Post(_ => { if (debil.TryGetTarget(out var s2)) s2.AlCambiarAjustesPkhex(); }, null);
        };
        _suscripcionAjustes = manejador;
        nuevo.Cambiado += manejador;
    }

    /// <summary>Lo que se perdería al dejar esta partida: los cambios aplicados sin guardar y los borradores sin aplicar. Null si no hay nada.</summary>
    public string? CambiosQueSePerderian()
    {
        if (Sesion is null) return null;
        List<string> l = [];
        if (Sesion.CambiosPendientes > 0) l.Add(Sesion.CambiosPendientes == 1 ? IdiomaInterfaz.T("Shell.CambioGuardar") : IdiomaInterfaz.F("Partida.Cambios.otro", Sesion.CambiosPendientes));
        if (BorradoresSinAplicar() is { } b) l.Add(IdiomaInterfaz.F("Shell.CambiosAplicar", b));
        if (Editor is { HayCambios: true }) l.Add(IdiomaInterfaz.T("Shell.CambiosAplicarEditorPokemon"));
        return l.Count == 0 ? null : string.Join("; ", l);
    }

    /// <summary>
    /// Antes de pasar a otra partida (cualquier camino: «Abrir», recientes, «Abrir con» y «Compartir», el buzón de archivos externos, una partida de la tarjeta de GameCube, «Importar hexadecimal», «Recuperar»):
    /// con <c>SlotWrite.ModifyUnset</c> y algo que se perdería, pregunta con los textos de <c>Main.OpenSAV</c>; <paramref name="abrir"/> solo corre si el usuario sigue. Sin nada que perder, corre ya.
    /// </summary>
    private Task ConfirmarCambioDePartida(string tituloNueva, Func<Task> abrir, Action? alDescartar = null)
    {
        if (!AjustesPkhex.Actual.SlotWrite.ModifyUnset || CambiosQueSePerderian() is not { } perdidos) return abrir();
        TextosNucleo.Preparar();
        AbrirHoja(new ConfirmarHojaViewModel(IdiomaInterfaz.T("Shell.CambiosGuardar"),
            [MessageStrings.MsgProgramCloseUnsaved, IdiomaInterfaz.F("Shell.En0Hay1", TituloDePartida(Sesion!), perdidos), MessageStrings.MsgProgramSaveFileConfirm],
            IdiomaInterfaz.F("Shell.Abrir", tituloNueva), () => _ = Proteger(abrir), destructiva: true, textoCancelar: IdiomaInterfaz.T("Shell.SeguirPartida"), alDescartar: alDescartar));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Los caminos sin despachador (los servicios crean la sesión antes de que la Shell la vea, y «Recuperar»): la pregunta llega con la partida nueva ya leída, así que mientras se pregunta el entrenador
    /// reciente del Core vuelve a ser el de la partida actual (PKHeX lo fija tras el «Sí») y la nueva se descarta si el usuario no sigue (su archivo solo se leyó).
    /// </summary>
    private void CambiarDeSesion(Sesion nueva, IReadOnlyList<RecienteRef>? recientesAntes = null)
    {
        if (AjustesPkhex.Actual.SlotWrite.ModifyUnset && CambiosQueSePerderian() is not null && Sesion is not null) RecentTrainerCache.SetRecentTrainer(Sesion.Guardado);
        _ = ConfirmarCambioDePartida(TituloDePartida(nueva), () =>
        {
            RecentTrainerCache.SetRecentTrainer(nueva.Guardado);
            Sustituir(nueva);
            return Task.CompletedTask;
        }, recientesAntes is null ? null : () => _servicios?.DeshacerApertura(nueva, recientesAntes));   // «Abrir» y recientes ya anotaron la apertura: si el usuario descarta, no queda copia de trabajo ni reciente (como Main.OpenSAV)
    }
}
