// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Engine.Ipc;

/// <summary>
/// Lo que el motor ejecuta por solicitud: en produccion <c>WorkerHost.Run</c> TAL CUAL (lee UN JSON de <paramref name="entrada"/>, escribe UNO en <paramref name="salida"/>, escribe el diagnostico en <paramref name="registro"/>);
/// las pruebas del protocolo usan ejecutores falsos (lentos, que lanzan, que no obedecen). <paramref name="paradaDura"/> es lo que hace el vigilante duro del Worker tras responder Timeout (matar el proceso).
/// </summary>
public delegate int EjecutorMotor(Stream entrada, Stream salida, TextWriter registro, CancellationToken cancelar, Action<int> paradaDura);

/// <summary>Quien acepta la conexion de la interfaz: en Android un socket Unix de nombre abstracto por sesion; en las pruebas, un socket de fichero o TCP local.</summary>
public interface IAceptador : IDisposable
{
    /// <summary>Espera una conexion. Devuelve null si <paramref name="parar"/> se cancela o el aceptador se cierra.</summary>
    Stream? Aceptar(CancellationToken parar);
}

/// <summary>Como la interfaz arranca el proceso del motor (servicio <c>:motor</c>), se conecta a el y, si hace falta, lo mata. Las pruebas usan un motor falso en un hilo.</summary>
public interface IMotorLanzador
{
    /// <summary>Texto de por que el motor no puede arrancar (null si puede).</summary>
    string? NoDisponible { get; }

    /// <summary>Arranca el motor si no vive (o no responde) y devuelve una conexion nueva con el saludo YA leido por quien llama (el primer marco). <paramref name="nombre"/> es el nombre del socket de ESTA conexion.</summary>
    Task<Stream> ConectarAsync(string nombre, long generacion, CancellationToken cancelar);

    /// <summary>Mata el proceso del motor (Android: <c>Process.KillProcess(pid)</c>).</summary>
    void Matar(int pid);

    /// <summary>Para el servicio del motor (sesion cerrada).</summary>
    void Parar();
}
