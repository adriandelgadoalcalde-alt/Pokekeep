// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Operaciones.OperacionLarga;

/// <summary>
/// Guarda UNA instancia por proceso (revisión del coordinador, 9c): Android recrea la actividad (y con ella la raíz de Avalonia) al salir y volver, pero el proceso sigue vivo con su operación larga en marcha.
/// El supervisor y la pasarela del motor han de ser los mismos para toda la vida del proceso: un segundo supervisor no sabría de la operación en curso.
/// </summary>
public sealed class UnoPorProceso<T> where T : class
{
    private readonly object _cerrojo = new();
    private T? _valor;
    private int _creaciones;

    /// <summary>Cuántas veces se ejecutó la fábrica (1 como mucho).</summary>
    public int Creaciones { get { lock (_cerrojo) return _creaciones; } }

    public T ObtenerOCrear(Func<T> crear)
    {
        lock (_cerrojo)
        {
            if (_valor is null) { _valor = crear(); _creaciones++; }
            return _valor;
        }
    }
}

/// <summary>
/// Decide si hay que avisar de que una operación larga se interrumpió: el registro se lee UNA vez por proceso (al arrancar el proceso, no al recrear la actividad) y solo si no hay una operación viva
/// (su registro es el de esa operación: leerlo lo borraría y avisaría en falso).
/// </summary>
public sealed class RecuperacionOperacionLarga
{
    private readonly object _cerrojo = new();
    private bool _leido;

    public OperacionInterrumpida? TomarUnaVez(string carpetaPrivada, SupervisorOperacionLarga supervisor)
    {
        lock (_cerrojo)
        {
            if (_leido) return null;
            _leido = true;
        }
        return supervisor.EnCurso ? null : RegistroOperacionLarga.LeerInterrumpida(carpetaPrivada);
    }
}
