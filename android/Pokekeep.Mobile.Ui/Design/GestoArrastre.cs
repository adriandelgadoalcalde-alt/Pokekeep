// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Las medidas del gesto «mantener pulsado y arrastrar» de las casillas (X10), todas en un sitio para que la vista, las pruebas y el informe hablen de los mismos numeros. Dp salvo que se diga lo contrario.
/// </summary>
public static class GestoArrastre
{
    /// <summary>Lo que hay que mover el dedo, tras levantar el Pokemon, para que cuente como arrastre; menos es temblor y al soltar se empieza la seleccion multiple, como antes.</summary>
    public const double UmbralMovimiento = 10;

    /// <summary>Separación mínima por encima del dedo. La altura real se adapta al tamaño de la casilla; el destino sigue siendo la casilla bajo el dedo.</summary>
    public const double AlturaFantasma = 28;

    /// <summary>Separación suficiente para que el fantasma no cruce el destino al apuntar a su centro; se adapta a casillas grandes de Equipo/Otros.</summary>
    public static double AlturaFantasmaPara(double celda) => Math.Max(AlturaFantasma, celda * (1 + EscalaLevantado) / 2 + 6);

    /// <summary>X34: levantar desplaza y añade sombra; conserva el tamaño de origen, también en Equipo.</summary>
    public const double EscalaLevantado = 1.0;

    /// <summary>Franja (desde el borde izquierdo o derecho de la caja, hacia dentro) donde mantener el fantasma pasa de caja. Fuera de la caja tambien cuenta (el dedo se va al borde de la pantalla).</summary>
    public const double ZonaBorde = 20;

    /// <summary>Cuanto hay que mantener el fantasma junto al borde antes de pasar a la caja vecina; despues, otra vez lo mismo para la siguiente.</summary>
    public const double EsperaBordeMs = 600;

    /// <summary>Margen vertical, por encima y por debajo de la caja, donde el borde sigue contando.</summary>
    public const double MargenVerticalBorde = 60;

    /// <summary>Duraciones (ms) de las animaciones, todas entre 150 y 250 y con curva de salida: coger, soltar encajando, el desplazado volando al origen y la vuelta de una cancelacion.</summary>
    public const double DuracionAgarreMs = 160, DuracionSoltarMs = 180, DuracionIntercambioMs = 220, DuracionVueltaMs = 220;

    /// <summary>
    /// uso10 (propuesta aprobada por el propietario): rebote corto al aterrizar en la casilla de destino. Va dentro del propio vuelo
    /// de soltar (el último 40 % del recorrido: crece hasta <see cref="EscalaRebote"/> y vuelve a su tamaño), así que soltar no dura más.
    /// </summary>
    public const double EscalaRebote = 1.08, InicioRebote = 0.6;

    /// <summary>Lo que crece el fantasma por el rebote en el punto <paramref name="v"/> (0-1) del vuelo de soltar.</summary>
    public static double Rebote(double v) => v <= InicioRebote ? 0 : (EscalaRebote - 1) * Math.Sin(Math.PI * Math.Min(1, (v - InicioRebote) / (1 - InicioRebote)));

    /// <summary>Opacidad de la casilla de la que se ha levantado el Pokemon.</summary>
    public const double OpacidadOrigen = 0.35;

    /// <summary>Desplazamiento directo, no animación decorativa: 48 dp de borde y hasta 360 dp/s, proporcional a la cercanía. También con reducir movimiento.</summary>
    public static double VelocidadDesplazamiento(double y, double alto)
    {
        var zona = Math.Min(48, alto / 2);
        if (zona <= 0 || y < 0 || y > alto) return 0;
        if (y < zona) return -360 * (1 - y / zona);
        if (y > alto - zona) return 360 * (1 - (alto - y) / zona);
        return 0;
    }
}

/// <summary>
/// Animador minimo del arrastre: tiene un reloj propio (lo avanza quien lo usa: el reloj de fotogramas o una prueba) y una lista de interpolaciones con la curva de salida de Material del carrusel
/// (<see cref="GestoCaja.CurvaDeSalida"/>). Sin Avalonia, para probarlo sin pantalla.
/// </summary>
public sealed class AnimadorArrastre
{
    private sealed class Tramo
    {
        public required double Desde, Hasta, DuracionMs; public required TimeSpan Inicio;
        public required Action<double> Aplicar; public Action? Fin; public object? Clave;
    }

    private readonly List<Tramo> _tramos = [];
    // uso10: una animación que empieza en el «fin» de otra (soltar → la página vuelve a su sitio) arranca en el instante exacto en
    // que acabó la anterior, no en el siguiente fotograma; así una sola llamada a Avanzar recorre la cadena entera.
    private TimeSpan? _inicioEncadenado;
    private bool _encadenada;
    public TimeSpan Ahora { get; private set; }
    public bool Activo => _tramos.Count > 0;

    /// <summary>Anima un valor de <paramref name="desde"/> a <paramref name="hasta"/>. Con <paramref name="clave"/>, sustituye la animacion anterior con la misma clave (sin llamar a su fin).</summary>
    public void Iniciar(double desde, double hasta, double duracionMs, Action<double> aplicar, Action? fin = null, object? clave = null)
    {
        if (clave is not null) _tramos.RemoveAll(t => Equals(t.Clave, clave));
        aplicar(desde);
        _tramos.Add(new Tramo { Desde = desde, Hasta = hasta, DuracionMs = Math.Max(1, duracionMs), Inicio = _inicioEncadenado ?? Ahora, Aplicar = aplicar, Fin = fin, Clave = clave });
        if (_inicioEncadenado is not null) _encadenada = true;
    }

    /// <summary>Pone el reloj en <paramref name="ahora"/> y avanza todas las animaciones; las que llegan al final dejan su valor exacto y avisan.</summary>
    public void Avanzar(TimeSpan ahora)
    {
        if (ahora > Ahora) Ahora = ahora;
        for (var vuelta = 0; vuelta < 8; vuelta++)
        {
            _encadenada = false;
            foreach (var t in _tramos.ToArray())
            {
                var f = (Ahora - t.Inicio).TotalMilliseconds / t.DuracionMs;
                if (f >= 1)
                {
                    _tramos.Remove(t); t.Aplicar(t.Hasta);
                    _inicioEncadenado = t.Inicio + TimeSpan.FromMilliseconds(t.DuracionMs);
                    try { t.Fin?.Invoke(); } finally { _inicioEncadenado = null; }
                }
                else t.Aplicar(t.Desde + (t.Hasta - t.Desde) * GestoCaja.CurvaDeSalida(Math.Max(0, f)));
            }
            if (!_encadenada) break;   // otra vuelta solo si un «fin» encadenó una animación nueva
        }
    }

    /// <summary>Termina todas ya (valor final y aviso): se usa con «reducir movimiento» y cuando algo nuevo no puede esperar.</summary>
    public void Concluir()
    {
        // También las que un «fin» encadena (la página que vuelve a su sitio tras soltar): «concluir» deja todo terminado.
        for (var vuelta = 0; vuelta < 8 && _tramos.Count > 0; vuelta++)
            foreach (var t in _tramos.ToArray()) { _tramos.Remove(t); t.Aplicar(t.Hasta); t.Fin?.Invoke(); }
    }

    /// <summary>Suelta las animaciones sin llamar a nada (la vista se ha ido).</summary>
    public void Descartar() => _tramos.Clear();
}

/// <summary>Vibracion corta del aparato (haptica) al coger un Pokemon. Android la implementa con la vibracion del sistema; sin ella (PC, pruebas) no hace nada.</summary>
public interface IVibracion
{
    /// <summary>Un toque breve, como el de una pulsacion larga del sistema.</summary>
    void PulsoCorto();
}

/// <summary>Donde la plataforma deja su vibracion (null = ninguna). Las pruebas ponen una que apunta lo que se habria hecho.</summary>
public static class Haptica
{
    public static IVibracion? Proveedor { get; set; }
    public static void PulsoCorto() { try { Proveedor?.PulsoCorto(); } catch (Exception) { /* la vibracion nunca debe romper el gesto */ } }
}
