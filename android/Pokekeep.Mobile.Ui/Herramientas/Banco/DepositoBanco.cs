// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Almacen;

namespace Pokekeep.Mobile.Ui.Herramientas.Banco;

/// <summary>Un archivo del depósito del banco: su nombre y su tamaño.</summary>
public readonly record struct ArchivoBanco(string Nombre, long Bytes);

/// <summary>Resultado de escribir un archivo: el error (null si fue bien) y el nombre con el que ha quedado (un proveedor de SAF puede cambiarlo si ya existía).</summary>
public readonly record struct ResultadoEscritura(string? Error, string Nombre);

/// <summary>
/// Dónde vive el banco (X38): una carpeta de archivos. Los Pokémon son archivos estándar (<c>.pk*</c>) y el índice un JSON aparte, así que cualquier implementación solo tiene que listar, leer, escribir y borrar
/// archivos por nombre. Las operaciones no lanzan: devuelven el motivo del fallo.
/// </summary>
public interface IDepositoBanco
{
    /// <summary>Texto corto para decirle al usuario dónde está («Carpeta privada de la app», «Carpeta Pokémon del teléfono»).</summary>
    string Descripcion { get; }
    /// <summary>La carpeta privada de la app (se pierde al desinstalar) frente a una carpeta que el usuario eligió (sobrevive).</summary>
    bool EsPrivado { get; }
    IReadOnlyList<ArchivoBanco> Listar(out string? error);
    byte[]? Leer(string nombre, out string? error);
    /// <summary>Escribe el archivo entero. En una carpeta de ficheros es atómico (temporal + reemplazo): un corte a medias no deja el archivo a medio escribir.</summary>
    ResultadoEscritura Escribir(string nombre, byte[] datos);
    string? Borrar(string nombre);
}

/// <summary>El banco en memoria (pruebas y capturas).</summary>
public sealed class DepositoBancoMemoria(bool privado = true, string descripcion = "memoria") : IDepositoBanco
{
    private readonly Dictionary<string, byte[]> _archivos = new(StringComparer.Ordinal);
    public string Descripcion => descripcion;
    public bool EsPrivado => privado;
    public IReadOnlyList<ArchivoBanco> Listar(out string? error) { error = null; return [.. _archivos.Select(a => new ArchivoBanco(a.Key, a.Value.Length)).OrderBy(a => a.Nombre, StringComparer.Ordinal)]; }
    public byte[]? Leer(string nombre, out string? error)
    {
        if (_archivos.TryGetValue(nombre, out var d)) { error = null; return [.. d]; }
        error = "No existe " + nombre; return null;
    }
    public ResultadoEscritura Escribir(string nombre, byte[] datos) { _archivos[nombre] = [.. datos]; return new(null, nombre); }
    public string? Borrar(string nombre) { _archivos.Remove(nombre); return null; }
    /// <summary>Para las pruebas: sustituye el contenido de un archivo sin pasar por el servicio (simula un archivo roto o cambiado a mano).</summary>
    public void Poner(string nombre, byte[] datos) => _archivos[nombre] = [.. datos];
    public bool Existe(string nombre) => _archivos.ContainsKey(nombre);
}

/// <summary>
/// El banco en una carpeta del sistema de ficheros: la carpeta privada de la app (<c>FilesDir</c>) o, en el PC, una carpeta de pruebas. Escritura atómica: temporal en la misma carpeta y reemplazo.
/// </summary>
public sealed class DepositoBancoCarpeta(string ruta, bool privado = true, string? descripcion = null) : IDepositoBanco
{
    public string Ruta => ruta;
    public string Descripcion => descripcion ?? ruta;
    public bool EsPrivado => privado;

    private string De(string nombre) => Path.Combine(ruta, Path.GetFileName(nombre));

    public IReadOnlyList<ArchivoBanco> Listar(out string? error)
    {
        error = null;
        try
        {
            if (!Directory.Exists(ruta)) return [];
            return [.. new DirectoryInfo(ruta).EnumerateFiles().Where(f => !f.Name.EndsWith(".tmp", StringComparison.Ordinal)).Select(f => new ArchivoBanco(f.Name, f.Length))];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { error = ex.Message; return []; }
    }

    public byte[]? Leer(string nombre, out string? error)
    {
        try { error = null; return File.ReadAllBytes(De(nombre)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { error = ex.Message; return null; }
    }

    public ResultadoEscritura Escribir(string nombre, byte[] datos)
    {
        try
        {
            Directory.CreateDirectory(ruta);
            var destino = De(nombre); var temporal = destino + ".tmp";
            using (var fs = new FileStream(temporal, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(datos, 0, datos.Length);
                fs.Flush(true);
            }
            File.Move(temporal, destino, overwrite: true);
            return new(null, Path.GetFileName(destino));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new(ex.Message, nombre); }
    }

    public string? Borrar(string nombre)
    {
        try { var r = De(nombre); if (File.Exists(r)) File.Delete(r); return null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return ex.Message; }
    }
}

/// <summary>
/// El banco en una carpeta que el usuario eligió con el selector del sistema (SAF): sobrevive a desinstalar la app. Crea documentos nuevos con <see cref="IAlmacenCarpetas.CrearEnCarpetaAsync"/> (el proveedor puede cambiar el
/// nombre si ya existe: por eso un archivo nuevo siempre lleva su identificador en el nombre) y sustituye el contenido de los existentes en una pasada. El SAF no tiene reemplazo atómico: por eso los Pokémon son
/// archivos que no se reescriben nunca y el índice tiene copia y se reconstruye (ver <see cref="BancoPokemon"/>).
/// </summary>
public sealed class DepositoBancoSaf(IAlmacenCarpetas carpetas, IAlmacenDocumentos documentos, CarpetaRef carpeta) : IDepositoBanco
{
    private Dictionary<string, DocumentoRef> _conocidos = new(StringComparer.Ordinal);

    public string Descripcion => carpeta.Nombre;
    public bool EsPrivado => false;
    public CarpetaRef Carpeta => carpeta;

    private static T Esperar<T>(Task<T> t) => t.GetAwaiter().GetResult();

    public IReadOnlyList<ArchivoBanco> Listar(out string? error)
    {
        try
        {
            // Solo el primer nivel: el banco no usa subcarpetas.
            var docs = Esperar(carpetas.ListarAsync(carpeta, false, CancellationToken.None));
            var nuevos = new Dictionary<string, DocumentoRef>(StringComparer.Ordinal);
            foreach (var d in docs) nuevos.TryAdd(d.Nombre, d);
            _conocidos = nuevos;
            error = null;
            return [.. nuevos.Values.Select(d => new ArchivoBanco(d.Nombre, d.Bytes))];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException) { error = ex.Message; return []; }
    }

    public byte[]? Leer(string nombre, out string? error)
    {
        try
        {
            if (!_conocidos.TryGetValue(nombre, out var doc)) { Listar(out _); if (!_conocidos.TryGetValue(nombre, out doc)) { error = "No existe " + nombre; return null; } }
            error = null;
            return Esperar(documentos.LeerAsync(doc, CancellationToken.None));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException) { error = ex.Message; return null; }
    }

    public ResultadoEscritura Escribir(string nombre, byte[] datos)
    {
        try
        {
            if (!_conocidos.TryGetValue(nombre, out var doc))
            {
                Listar(out _);
                _conocidos.TryGetValue(nombre, out doc);
            }
            doc ??= Esperar(carpetas.CrearEnCarpetaAsync(carpeta, nombre, CancellationToken.None));
            Esperar(Envolver(documentos.EscribirAsync(doc, datos, CancellationToken.None)));
            _conocidos[doc.Nombre] = doc;
            return new(null, doc.Nombre);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException) { return new(ex.Message, nombre); }
    }

    public string? Borrar(string nombre)
    {
        try
        {
            if (!_conocidos.TryGetValue(nombre, out var doc)) { Listar(out _); if (!_conocidos.TryGetValue(nombre, out doc)) return null; }
            Esperar(Envolver(carpetas.BorrarAsync(doc, CancellationToken.None)));
            _conocidos.Remove(nombre);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException) { return ex.Message; }
    }

    private static async Task<bool> Envolver(Task t) { await t.ConfigureAwait(false); return true; }
}
