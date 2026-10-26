// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.Concurrent;
using Pokekeep.Mobile.Ui.Almacen;

namespace Pokekeep.Mobile.Ui.Abrir;

/// <summary>Un archivo que otra app le pasa a Pokekeep («Abrir con» o «Compartir»): el documento (URI de contenido) y por dónde llegó.</summary>
public sealed record EntradaExterna(DocumentoRef Documento, OrigenArchivo Origen);

/// <summary>
/// Buzón de los archivos que llegan de fuera (F10-ABRIR-001/002, PK-MENU-045: el equivalente en Android de los argumentos de línea de comandos de PKHeX). La actividad deja aquí lo que trae el Intent (al
/// arrancar o con la app ya abierta) y la Shell lo recoge cuando está lista: si el archivo llega mientras se copian los datos del primer arranque, espera en la cola y no se pierde.
/// </summary>
public static class EntradasExternas
{
    private static readonly ConcurrentQueue<EntradaExterna> Cola = new();

    /// <summary>Se dispara (en el hilo de quien recibe) cada vez que llega un archivo.</summary>
    public static event Action? Llegada;

    public static void Recibir(EntradaExterna entrada)
    {
        Cola.Enqueue(entrada);
        Llegada?.Invoke();
    }

    public static bool TryTomar(out EntradaExterna entrada) => Cola.TryDequeue(out entrada!);

    public static int Pendientes => Cola.Count;
}
