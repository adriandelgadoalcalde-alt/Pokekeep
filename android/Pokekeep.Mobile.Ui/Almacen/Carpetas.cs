// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;

namespace Pokekeep.Mobile.Ui.Almacen;

/// <summary>Una carpeta elegida por el usuario (Android: URI de arbol de SAF con permiso persistente).</summary>
public sealed record CarpetaRef(string Uri, string Nombre);

/// <summary>Carpeta de copias persistente (plan 5.5): <c>OPEN_DOCUMENT_TREE</c> + <c>takePersistableUriPermission</c>. Sobrevive a reinicios de la app y del movil mientras el permiso siga concedido.</summary>
public interface IAlmacenCarpetas
{
    Task<CarpetaRef?> ElegirCarpetaAsync(CancellationToken cancelar);
    /// <summary>La carpeta si el permiso persistente sigue vigente y existe; null si no.</summary>
    Task<CarpetaRef?> ObtenerCarpetaAsync(string uri, CancellationToken cancelar);
    /// <summary>Crea un documento NUEVO en la carpeta (si el nombre existe, el proveedor lo desambigua) y lo devuelve.</summary>
    Task<DocumentoRef> CrearEnCarpetaAsync(CarpetaRef carpeta, string nombre, CancellationToken cancelar);

    /// <summary>
    /// Los documentos de la carpeta y de sus subcarpetas (bloque 10-4: «Lista de carpetas» y las «Otras rutas de copias» de PKHeX, que busca con <c>SearchOption.AllDirectories</c>), como <see cref="DocumentoRef"/>
    /// que se leen con <see cref="IAlmacenDocumentos.LeerAsync"/>. Nunca escribe. Por defecto (anfitriones sin árbol de SAF) no hay ninguno.
    /// </summary>
    Task<IReadOnlyList<DocumentoRef>> ListarAsync(CarpetaRef carpeta, CancellationToken cancelar) => Task.FromResult<IReadOnlyList<DocumentoRef>>([]);

    /// <summary>
    /// Igual que <see cref="ListarAsync(CarpetaRef, CancellationToken)"/> pero con <paramref name="profundo"/> falso solo los documentos del primer nivel de la carpeta, sin entrar en las subcarpetas
    /// (<c>SearchOption.TopDirectoryOnly</c> de PKHeX; lo usa la base de datos de PKM con el ajuste <c>EntityDb.SearchExtraSavesDeep</c>). Por defecto, el listado completo: un anfitrión que no distingue niveles no recorta.
    /// </summary>
    Task<IReadOnlyList<DocumentoRef>> ListarAsync(CarpetaRef carpeta, bool profundo, CancellationToken cancelar) => ListarAsync(carpeta, cancelar);

    /// <summary>
    /// X38: borra un documento de la carpeta (el banco propio de Pokémon, al vaciar la papelera o al trasladarse a otra carpeta). Solo lo llama el banco, por orden explícita del usuario y sobre sus propios archivos.
    /// Por defecto no se puede borrar (anfitriones sin árbol de SAF).
    /// </summary>
    Task BorrarAsync(DocumentoRef documento, CancellationToken cancelar) => throw new NotSupportedException("Esta carpeta no permite borrar archivos.");
}

/// <summary>Recuerda la carpeta elegida en <c>FilesDir/carpeta-copias.json</c> (solo URI y nombre de carpeta).</summary>
public sealed class CarpetaStore(string carpetaPrivada)
{
    private string Ruta => Path.Combine(carpetaPrivada, "carpeta-copias.json");
    public CarpetaRef? Leer()
    {
        try { return File.Exists(Ruta) ? JsonSerializer.Deserialize<CarpetaRef>(File.ReadAllText(Ruta)) : null; }
        catch (Exception ex) when (ex is IOException or JsonException) { return null; }
    }
    public void Guardar(CarpetaRef c) { Directory.CreateDirectory(carpetaPrivada); File.WriteAllText(Ruta, JsonSerializer.Serialize(c)); }
    public void Olvidar() { try { File.Delete(Ruta); } catch (IOException) { } }
}
