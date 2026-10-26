// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using System.Text.Json.Serialization;
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Herramientas.Equipo;

/// <summary>Un Pokémon guardado en una plantilla: los bytes que escribe el Core (parte de equipo, descifrados) y la extensión de su formato (<c>pk4</c>, <c>pk9</c>...).</summary>
public sealed record MiembroPlantilla(string Extension, int Contexto, string Datos);

/// <summary>
/// Una plantilla de equipo (X37): nombre, cuándo se creó, la generación del juego de donde salió y sus Pokémon. Se guarda como JSON en la carpeta privada de la app (nada sale del dispositivo, regla 15);
/// cada Pokémon va como lo escribe el Core (<c>WriteDecryptedDataParty</c>), así que la ida y vuelta es exacta cuando el juego es el mismo.
/// </summary>
public sealed record PlantillaEquipo(string Id, string Nombre, DateTime Creada, int Generacion, string Juego, IReadOnlyList<MiembroPlantilla> Miembros, int Version = PlantillaEquipo.VersionActual)
{
    public const int VersionActual = 1;

    public int Cantidad => Miembros.Count;

    /// <summary>Crea una plantilla con los Pokémon dados (los huecos se saltan).</summary>
    public static PlantillaEquipo Nueva(string nombre, IEnumerable<PKM> pokemon, int generacion, string juego, DateTime? creada = null)
    {
        var miembros = pokemon.Where(p => p.Species != 0).Select(DeEntidad).ToList();
        return new PlantillaEquipo(Guid.NewGuid().ToString("N"), nombre.Trim(), creada ?? DateTime.Now, generacion, juego, miembros);
    }

    private static MiembroPlantilla DeEntidad(PKM pk)
    {
        var datos = new byte[pk.SIZE_PARTY];
        pk.WriteDecryptedDataParty(datos);
        return new MiembroPlantilla(pk.Extension, (int)pk.Context, Convert.ToBase64String(datos));
    }

    /// <summary>
    /// Lee un Pokémon de la plantilla; null si los datos no valen (archivo dañado). El formato sale de la EXTENSIÓN guardada y no de adivinarlo por el tamaño: el Core confunde PK5 con BK4 y PA9 con PK9
    /// (<c>EntityFormat.GetFormat</c> solo puede mirar los bytes), y aquí se sabe exactamente qué se guardó.
    /// </summary>
    public static PKM? Leer(MiembroPlantilla m)
    {
        try
        {
            var bytes = Convert.FromBase64String(m.Datos);
            Memory<byte> d = bytes;
            PKM? pk = m.Extension.ToLowerInvariant() switch
            {
                "pk1" => PokeList1.ReadFromSingle(bytes),   // Gen. 1 y 2: el Pokémon va envuelto en una lista de una casilla (con su entrenador y apodo)
                "pk2" => PokeList2.ReadFromSingle(bytes),
                "sk2" => new SK2(d),
                "pk3" => new PK3(d),
                "ck3" => new CK3(d),
                "xk3" => new XK3(d),
                "pk4" => new PK4(d),
                "bk4" => new BK4(d),
                "rk4" => new RK4(d),
                "pk5" => new PK5(d),
                "pk6" => new PK6(d),
                "pk7" => new PK7(d),
                "pb7" => new PB7(d),
                "pk8" => new PK8(d),
                "pa8" => new PA8(d),
                "pb8" => new PB8(d),
                "pk9" => new PK9(d),
                "pa9" => new PA9(d),
                _ => null,
            };
            // Un tamaño que no es el del formato (ni de equipo ni guardado) es un archivo dañado, no un Pokémon.
            if (pk is null || (bytes.Length != pk.SIZE_PARTY && bytes.Length != pk.SIZE_STORED)) return null;
            return pk.Species == 0 ? null : pk;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or IndexOutOfRangeException or InvalidOperationException) { return null; }
    }

    /// <summary>Los Pokémon legibles de la plantilla (los dañados se saltan) y cuántos hubo dañados.</summary>
    public IReadOnlyList<PKM> Pokemon(out int danados)
    {
        var l = new List<PKM>(); danados = 0;
        foreach (var m in Miembros) { if (Leer(m) is { } p) l.Add(p); else danados++; }
        return l;
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PlantillaEquipo))]
internal sealed partial class PlantillaJson : JsonSerializerContext;

/// <summary>Dónde viven las plantillas. En Android es la carpeta privada de la app; en las pruebas, la memoria o una carpeta temporal.</summary>
public interface IAlmacenPlantillas
{
    /// <summary>Todas las plantillas, de la más reciente a la más antigua; las dañadas se cuentan en <paramref name="danadas"/> (su nombre de archivo) y no se listan.</summary>
    IReadOnlyList<PlantillaEquipo> Listar(out IReadOnlyList<string> danadas);
    /// <summary>Guarda (crea o reemplaza por <see cref="PlantillaEquipo.Id"/>). Devuelve null si fue bien, o el motivo.</summary>
    string? Guardar(PlantillaEquipo plantilla);
    /// <summary>Borra la plantilla. Devuelve null si fue bien, o el motivo.</summary>
    string? Borrar(string id);
}

/// <summary>Plantillas en memoria (pruebas y capturas).</summary>
public sealed class AlmacenPlantillasMemoria : IAlmacenPlantillas
{
    private readonly Dictionary<string, PlantillaEquipo> _datos = [];
    public IReadOnlyList<PlantillaEquipo> Listar(out IReadOnlyList<string> danadas) { danadas = []; return [.. _datos.Values.OrderByDescending(p => p.Creada).ThenBy(p => p.Nombre, StringComparer.CurrentCultureIgnoreCase)]; }
    public string? Guardar(PlantillaEquipo plantilla) { _datos[plantilla.Id] = plantilla; return null; }
    public string? Borrar(string id) { _datos.Remove(id); return null; }
}

/// <summary>
/// Plantillas como archivos <c>plantilla-&lt;id&gt;.json</c> en una carpeta de la app. Escritura atómica (temporal + reemplazo): un corte a medias no deja una plantilla a medio escribir. Un archivo dañado no impide
/// leer las demás: se salta y se cuenta.
/// </summary>
public sealed class AlmacenPlantillasArchivos : IAlmacenPlantillas
{
    private const string Prefijo = "plantilla-";
    private readonly string _carpeta;

    public AlmacenPlantillasArchivos(string carpeta) => _carpeta = carpeta;

    public string Carpeta => _carpeta;

    private string Ruta(string id) => Path.Combine(_carpeta, Prefijo + id + ".json");

    public IReadOnlyList<PlantillaEquipo> Listar(out IReadOnlyList<string> danadas)
    {
        var lista = new List<PlantillaEquipo>(); var malas = new List<string>();
        danadas = malas;
        if (!Directory.Exists(_carpeta)) return lista;
        foreach (var f in Directory.EnumerateFiles(_carpeta, Prefijo + "*.json"))
        {
            try
            {
                var p = JsonSerializer.Deserialize(File.ReadAllText(f), PlantillaJson.Default.PlantillaEquipo);
                if (p is null || string.IsNullOrWhiteSpace(p.Id) || p.Miembros is null || Path.GetFileName(f) != Prefijo + p.Id + ".json") malas.Add(Path.GetFileName(f)); else lista.Add(p);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { malas.Add(Path.GetFileName(f)); }
        }
        return [.. lista.OrderByDescending(p => p.Creada).ThenBy(p => p.Nombre, StringComparer.CurrentCultureIgnoreCase)];
    }

    public string? Guardar(PlantillaEquipo plantilla)
    {
        try
        {
            Directory.CreateDirectory(_carpeta);
            var ruta = Ruta(plantilla.Id); var temporal = ruta + ".tmp";
            File.WriteAllText(temporal, JsonSerializer.Serialize(plantilla, PlantillaJson.Default.PlantillaEquipo), new System.Text.UTF8Encoding(false));
            File.Move(temporal, ruta, overwrite: true);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return ex.Message; }
    }

    public string? Borrar(string id)
    {
        try { var r = Ruta(id); if (File.Exists(r)) File.Delete(r); return null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return ex.Message; }
    }
}
