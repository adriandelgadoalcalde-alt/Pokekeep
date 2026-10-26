// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Design;

/// <summary>A donde va la caja al soltar el dedo.</summary>
public enum DestinoCaja { Quedarse, Anterior, Siguiente }

/// <summary>
/// La matematica del carrusel de cajas, sin pantalla (X6): cuando se completa un deslizamiento, que curva sigue el movimiento y como rebota si no llega. Todo son funciones puras para probarlas sin Avalonia
/// (la vista solo les pone un reloj y aplica el resultado a la transformacion). Convenio de signo del DEDO, el mismo que ya usaba la pestana: positivo = dedo hacia la izquierda = caja siguiente.
/// </summary>
public static class GestoCaja
{
    /// <summary>Fraccion del ancho de la caja que hay que arrastrar para pasar de caja sin necesidad de velocidad (el cuarto: con la vecina a la vista, menos se siente como un roce y mas se parece a un paginador).</summary>
    public const double FraccionUmbral = 0.25;

    /// <summary>Velocidad (dp/s) a partir de la cual un gesto cuenta como «lanzamiento»: pasa de caja aunque no llegue al umbral de distancia (con el minimo de distancia del ajuste). ~10 veces la minima de Android (50 dp/s).</summary>
    public const double VelocidadLanzamiento = 500;

    /// <summary>Duracion (ms) de completar la transicion desde el reposo (botones) y limites cuando hay velocidad de lanzamiento: un lanzamiento rapido termina antes, uno lento no pasa de aqui.</summary>
    public const double DuracionMs = 300, DuracionMinimaMs = 180, DuracionMaximaMs = 320;

    /// <summary>Duracion (ms) tope del rebote cuando el gesto no llega al umbral y la caja vuelve a su sitio.</summary>
    public const double DuracionReboteMs = 460;

    /// <summary>Con una sola caja no hay vecina a la que llegar: el dedo mueve la caja con esta resistencia (fraccion) y vuelve, para que el gesto «responda» sin enganar.</summary>
    public const double Resistencia = 0.3;

    /// <summary>Hasta que fraccion del ancho se deja arrastrar una caja sin vecina.</summary>
    public const double TopeSinVecina = 0.12;

    /// <summary>
    /// Decide al soltar. <paramref name="desplazamiento"/> y <paramref name="velocidad"/> (dp y dp/s) con el signo del dedo; <paramref name="umbralMinimo"/> es el recorrido minimo del ajuste «DragStartThreshold» (nunca menos).
    /// Pasa de caja si se arrastro el cuarto del ancho o si fue un lanzamiento en el mismo sentido con al menos el recorrido minimo; un lanzamiento rapido EN CONTRA (el dedo se echa atras) la cancela.
    /// </summary>
    public static DestinoCaja Resolver(double desplazamiento, double ancho, double velocidad, double umbralMinimo, bool hayVecina)
    {
        if (!hayVecina || ancho <= 0 || desplazamiento == 0) return DestinoCaja.Quedarse;
        var distancia = Math.Abs(desplazamiento);
        var mismoSentido = velocidad == 0 || Math.Sign(velocidad) == Math.Sign(desplazamiento);
        var echaAtras = !mismoSentido && Math.Abs(velocidad) >= VelocidadLanzamiento;
        var pasa = !echaAtras && (distancia >= Math.Max(umbralMinimo, ancho * FraccionUmbral) || (mismoSentido && Math.Abs(velocidad) >= VelocidadLanzamiento && distancia >= umbralMinimo));
        return !pasa ? DestinoCaja.Quedarse : desplazamiento > 0 ? DestinoCaja.Siguiente : DestinoCaja.Anterior;
    }

    /// <summary>Posicion del contenido (dp, 0 = en reposo, negativo = desplazado a la izquierda) mientras el dedo arrastra: sigue al dedo 1:1 hasta un ancho, y con resistencia si no hay vecina.</summary>
    public static double PosicionAlArrastrar(double acumuladoDedo, double ancho, bool hayVecina)
    {
        if (ancho <= 0) return 0;
        if (hayVecina) return Math.Clamp(-acumuladoDedo, -ancho, ancho);
        return Math.Clamp(-acumuladoDedo * Resistencia, -ancho * TopeSinVecina, ancho * TopeSinVecina);
    }

    /// <summary>
    /// Duracion (ms) de completar la transicion: <paramref name="restante"/> dp que faltan de un total de <paramref name="ancho"/>, soltado con <paramref name="velocidad"/> dp/s (valor absoluto; 0 en los botones). Despacio, proporcional a lo que falta
    /// (nunca menos del 55 % de la duracion base, para que no parezca un salto); con velocidad de lanzamiento no va mas despacio que el dedo, entre los limites.
    /// </summary>
    public static double DuracionDeCompletar(double restante, double ancho, double velocidad)
    {
        restante = Math.Abs(restante); velocidad = Math.Abs(velocidad);
        if (velocidad < VelocidadLanzamiento) return DuracionMs * Math.Clamp(ancho <= 0 ? 1 : restante / ancho, 0.55, 1);
        return Math.Clamp(restante / velocidad * 1000 * 1.6, DuracionMinimaMs, DuracionMaximaMs);
    }

    /// <summary>
    /// Curva de salida de la transicion: el «decelerado enfatizado» de Material (cubic-bezier 0,05 / 0,7 / 0,1 / 1): arranca rapida y se asienta con una cola larga y suave. <paramref name="t"/> de 0 a 1 devuelve el avance de 0 a 1.
    /// </summary>
    public static double CurvaDeSalida(double t) => Bezier(0.05, 0.7, 0.1, 1.0, Math.Clamp(t, 0, 1));

    /// <summary>cubic-bezier(x1, y1, x2, y2) como en CSS: se busca el parametro cuya X vale <paramref name="x"/> (Newton con respaldo de biseccion) y se devuelve su Y.</summary>
    public static double Bezier(double x1, double y1, double x2, double y2, double x)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;
        static double Eje(double a, double b, double u) => 3 * a * u * (1 - u) * (1 - u) + 3 * b * u * u * (1 - u) + u * u * u;
        static double Derivada(double a, double b, double u) => 3 * a * (1 - u) * (1 - u) + 6 * (b - a) * u * (1 - u) + 3 * (1 - b) * u * u;
        var u = x;
        for (var i = 0; i < 8; i++)
        {
            var e = Eje(x1, x2, u) - x;
            if (Math.Abs(e) < 1e-6) return Eje(y1, y2, u);
            var d = Derivada(x1, x2, u);
            if (Math.Abs(d) < 1e-6) break;
            u -= e / d;
        }
        double lo = 0, hi = 1;
        u = x;
        for (var i = 0; i < 40; i++)
        {
            var e = Eje(x1, x2, u);
            if (Math.Abs(e - x) < 1e-6) break;
            if (e < x) lo = u; else hi = u;
            u = (lo + hi) / 2;
        }
        return Eje(y1, y2, u);
    }

    /// <summary>Posicion (dp) en <paramref name="t"/> segundos de un muelle subamortiguado que parte de <paramref name="x0"/> con velocidad <paramref name="v0"/> (dp/s) hacia 0: el rebote suave de «no llego».</summary>
    public static double PosicionDelMuelle(double t, double x0, double v0)
    {
        const double zeta = 0.62, periodo = 0.30;
        var w0 = 2 * Math.PI / periodo;
        var wd = w0 * Math.Sqrt(1 - zeta * zeta);
        return Math.Exp(-zeta * w0 * t) * (x0 * Math.Cos(wd * t) + (v0 + zeta * w0 * x0) / wd * Math.Sin(wd * t));
    }

    /// <summary>El muelle ya esta en reposo (menos de medio dp de distancia): se deja la caja exactamente en su sitio.</summary>
    public static bool MuelleAsentado(double t, double x0, double v0)
        => t * 1000 >= DuracionReboteMs || (t > 0.05 && Math.Abs(PosicionDelMuelle(t, x0, v0)) < 0.4 && Math.Abs(PosicionDelMuelle(t + 0.02, x0, v0)) < 0.4);
}
