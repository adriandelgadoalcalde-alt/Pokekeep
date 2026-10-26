// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using Pokekeep.Sprites;

/// <summary>
/// Vistas previas de Let's Go (X57/X58): verifica la carpeta <c>vistas-previas-lgpe</c>
/// y genera <c>vista-lgpe.pkpack</c>, <c>manifest-lgpe.json</c> y su procedencia.
/// WebP sin pérdida determinista. X66 genera la caja nativa por separado con <see cref="LgpeCaja"/>.
/// </summary>
internal static class Lgpe
{
    internal const string OrigenVista = "Let's Go, Pikachu! y Eevee!: vistas previas renderizadas de los modelos 3D del juego (X57/X58). La caja usa los iconos nativos de X66.";
    public static int Ejecutar(string modo, string entrega, string salida)
    {
        string[] solo = [SpritesDeJuego.Lgpe];
        // X66: este modo solo regenera las vistas. Los iconos de caja se generan
        // desde la entrega nativa R1 con generar-lgpe-caja y nunca desde renders.
        var vista = Juegos.Construir(Path.Combine(entrega, "vistas-previas-lgpe"), solo, 0, "sprites-render");
        var salidas = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (n, b) in vista.Paquetes) salidas[n] = b;
        var entradas = vista.Entradas;
        var manifiesto = new Manifiesto
        {
            Origen = OrigenVista,
            Paquetes = vista.Info,
            Recursos = entradas,
        };
        salidas["manifest-lgpe.json"] = manifiesto.Escribir();
        salidas["procedencia-lgpe.tsv"] = new UTF8Encoding(false).GetBytes(vista.Tsv);
        Console.WriteLine($"manifest-lgpe.json: {entradas.Count} vistas previas; la caja nativa no se modifica.");
        return IconosCaja.Escribir(modo == "generar-lgpe", salida, salidas, $"{entradas.Count} recursos de Let's Go");
    }
}
