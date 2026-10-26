using System.Text.Json;

namespace Pokekeep.Mobile.Ui.Ajustes;

public sealed class PreferenciasArrastre(string carpeta)
{
    private string Ruta => Path.Combine(carpeta, "arrastre.json");
    public bool Leer()
    {
        try { return File.Exists(Ruta) && JsonSerializer.Deserialize<bool>(File.ReadAllText(Ruta)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return false; }
    }
    public void Guardar(bool centrar)
    {
        Directory.CreateDirectory(carpeta);
        File.WriteAllText(Ruta + ".tmp", JsonSerializer.Serialize(centrar));
        File.Move(Ruta + ".tmp", Ruta, true);
    }
}
