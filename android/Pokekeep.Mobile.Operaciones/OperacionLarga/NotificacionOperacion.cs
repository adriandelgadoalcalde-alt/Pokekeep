// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Masivas;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Operaciones.OperacionLarga;

/// <summary>
/// Lo que enseña la notificación del servicio en primer plano (F9X-03, ADR 0070, decisión 3): título de la operación, texto con el progreso REAL por unidad (el que compone el núcleo: «Procesando 213 de
/// 866 · Pikachu · 1 en paralelo · quedan ~3 min»), barra determinada (hechos de total) o marquesina hasta el primer avance, y la acción «Cancelar». La plataforma la pinta tal cual; no inventa texto.
/// </summary>
public sealed record EstadoNotificacion(string Titulo, string Texto, int Maximo, int Valor, bool Indeterminada, string? Aviso, bool Cancelando)
{
    public static string TextoCancelar => IdiomaInterfaz.T("NotificacionOperacion.Cancelar");
    public static string TextoCancelando => IdiomaInterfaz.T("NotificacionOperacion.CancelandoNoSeAplicara");
    public static string TextoPreparando => IdiomaInterfaz.T("NotificacionOperacion.PreparandoElMotor");
    public static string NombreCanal => IdiomaInterfaz.T("NotificacionOperacion.OperacionesLargas");
    public static string DescripcionCanal => IdiomaInterfaz.T("NotificacionOperacion.CompletarElJuegoY");

    /// <summary>Hay botón «Cancelar» mientras no se esté cancelando ya.</summary>
    public bool PuedeCancelar => !Cancelando;

    /// <summary>Texto completo (texto y aviso de energía, si lo hay) para la línea expandida de la notificación.</summary>
    public string TextoLargo => Aviso is null ? Texto : $"{Texto}\n{Aviso}";

    public static EstadoNotificacion Inicial(string titulo, string? aviso) => new(titulo, TextoPreparando, 0, 0, true, aviso, false);

    /// <summary>Del progreso del núcleo a la notificación: barra con hechos de total; marquesina hasta el primer avance real (igual que la pantalla).</summary>
    public static EstadoNotificacion De(string titulo, ProgresoMasivo p, string? aviso, bool cancelando = false)
    {
        var determinada = p.Total > 0 && p.Hechos > 0;
        var texto = cancelando ? TextoCancelando : string.IsNullOrWhiteSpace(p.Texto) ? TextoPreparando : p.Texto;
        return new EstadoNotificacion(titulo, texto, determinada ? p.Total : 0, determinada ? Math.Clamp(p.Hechos, 0, p.Total) : 0, !determinada, aviso, cancelando);
    }
}

/// <summary>
/// La parte de plataforma de una operación larga (Android: <c>OperacionLargaService</c>, servicio <c>dataSync</c> del proceso de la INTERFAZ, y <c>PowerManager</c>). Las pruebas usan una falsa.
/// </summary>
public interface IPlataformaOperacionLarga
{
    /// <summary>
    /// Arranca el servicio en primer plano con su notificación. <paramref name="cancelarDuro"/> se llama desde la acción «Cancelar» de la notificación; <paramref name="limiteDeTiempo"/> cuando Android agota
    /// el tiempo del servicio (<c>Service.onTimeout</c> de <c>dataSync</c>, Android 15+). Ambos pueden llegar desde cualquier hilo.
    /// </summary>
    void Iniciar(EstadoNotificacion estado, Action cancelarDuro, Action limiteDeTiempo);

    /// <summary>Cambia la notificación (progreso, texto, «Cancelando…»).</summary>
    void Actualizar(EstadoNotificacion estado);

    /// <summary>Quita la notificación y para el servicio (terminó, falló o se canceló).</summary>
    void Terminar();

    /// <summary>Ahorro de energía y temperatura AHORA.</summary>
    EstadoEnergia LeerEnergia();
}

/// <summary>Sin plataforma (pruebas de la interfaz y el PC): no hay notificación ni servicio, y la energía siempre es normal.</summary>
public sealed class SinPlataformaOperacionLarga : IPlataformaOperacionLarga
{
    public static readonly SinPlataformaOperacionLarga Instancia = new();
    public void Iniciar(EstadoNotificacion estado, Action cancelarDuro, Action limiteDeTiempo) { }
    public void Actualizar(EstadoNotificacion estado) { }
    public void Terminar() { }
    public EstadoEnergia LeerEnergia() => EstadoEnergia.Normal;
}
