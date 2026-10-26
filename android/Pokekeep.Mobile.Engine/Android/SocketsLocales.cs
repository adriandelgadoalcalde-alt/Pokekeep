// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Net.Sockets;
using Pokekeep.Mobile.Engine.Ipc;

namespace Pokekeep.Mobile.Engine.Plataforma;

/// <summary>
/// Socket de dominio Unix de NOMBRE ABSTRACTO (el mismo espacio que <c>LocalServerSocket</c>; en .NET, ruta con un NUL inicial): solo los procesos del mismo UID (la propia app) lo alcanzan, no deja ficheros
/// y desaparece con el proceso. El nombre lo fija la interfaz por sesion (<c>pokekeep-motor-&lt;pid-ui&gt;-&lt;n&gt;</c>).
/// </summary>
public sealed class AceptadorSocketAbstracto : IAceptador
{
    private readonly Socket _escucha;

    public AceptadorSocketAbstracto(string nombre)
    {
        _escucha = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _escucha.Bind(new UnixDomainSocketEndPoint("\0" + nombre));
        _escucha.Listen(1);
    }

    public Stream? Aceptar(CancellationToken parar)
    {
        try
        {
            var cliente = _escucha.AcceptAsync(parar).AsTask().GetAwaiter().GetResult();
            return new NetworkStream(cliente, ownsSocket: true);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException) { return null; }
    }

    public void Dispose()
    {
        try { _escucha.Close(); } catch { }
    }
}

public static class ConectorSocketAbstracto
{
    /// <summary>Conecta con el motor (reintenta hasta <paramref name="espera"/>: el servicio tarda en abrir su socket tras arrancar).</summary>
    public static async Task<Stream> ConectarAsync(string nombre, TimeSpan espera, CancellationToken cancelar)
    {
        var limite = DateTime.UtcNow + espera;
        Exception? ultimo = null;
        while (DateTime.UtcNow < limite)
        {
            cancelar.ThrowIfCancellationRequested();
            var s = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                await s.ConnectAsync(new UnixDomainSocketEndPoint("\0" + nombre), cancelar).ConfigureAwait(false);
                return new NetworkStream(s, ownsSocket: true);
            }
            catch (SocketException ex) { ultimo = ex; s.Dispose(); await Task.Delay(100, cancelar).ConfigureAwait(false); }
        }
        throw new TimeoutException($"El motor no abrió su socket «{nombre}» en {espera.TotalSeconds:0.#} s.", ultimo);
    }
}
