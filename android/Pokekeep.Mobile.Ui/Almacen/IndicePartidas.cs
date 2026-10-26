using System.Text.Json;
using Pokekeep.Mobile.Ui.Guardados;

namespace Pokekeep.Mobile.Ui.Almacen;

public sealed record PartidaRecordada(DocumentoRef Documento, string Juego, string Carpeta);

/// <summary>Índice del gestor, independiente del límite de recientes. Solo guarda ubicaciones y juegos.</summary>
public sealed class IndicePartidas(string carpetaPrivada)
{
    public sealed record Contenido(List<PartidaRecordada> Partidas, List<CarpetaRef> Carpetas);
    private string Ruta => Path.Combine(carpetaPrivada, "gestor-partidas.json");

    internal byte[]? Capturar() => File.Exists(Ruta) ? File.ReadAllBytes(Ruta) : null;

    internal void Restaurar(byte[]? anterior)
    {
        if (anterior is null) { if (File.Exists(Ruta)) File.Delete(Ruta); return; }
        Directory.CreateDirectory(carpetaPrivada);
        var temporal = Ruta + ".tmp";
        File.WriteAllBytes(temporal, anterior);
        File.Move(temporal, Ruta, true);
    }

    public Contenido Leer()
    {
        try { return File.Exists(Ruta) ? JsonSerializer.Deserialize<Contenido>(File.ReadAllText(Ruta)) ?? new([], []) : new([], []); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new([], []); }
    }

    private void Guardar(Contenido datos)
    {
        Directory.CreateDirectory(carpetaPrivada);
        var temporal = Ruta + ".tmp";
        File.WriteAllText(temporal, JsonSerializer.Serialize(datos));
        File.Move(temporal, Ruta, true);
    }

    public void Anotar(DocumentoRef documento, string juego)
    {
        var datos = Leer();
        datos.Partidas.RemoveAll(x => x.Documento.Uri == documento.Uri);
        datos.Partidas.Insert(0, new(documento, juego, ModeloCarpetas.CarpetaDeUri(documento.Uri)));
        Guardar(datos);
    }

    public void Recordar(CarpetaRef carpeta)
    {
        var datos = Leer();
        datos.Carpetas.RemoveAll(x => x.Uri == carpeta.Uri);
        datos.Carpetas.Insert(0, carpeta);
        Guardar(datos);
    }
}
