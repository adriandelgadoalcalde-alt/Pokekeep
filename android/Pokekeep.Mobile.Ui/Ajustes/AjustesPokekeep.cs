// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.ComponentModel;
using System.Reflection;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Ajustes;

/// <summary>Qué formas genera «Completar el juego» (los mismos valores que <c>LivingDexForms</c> del núcleo; el JSON guarda el nombre).</summary>
public enum FormasCompletar { OnePerSpecies, AllStorable, OnePlusRegional }

/// <summary>Orden de las casillas de «Completar el juego» (los mismos valores que <c>LivingDexOrder</c> del núcleo).</summary>
public enum OrdenCompletar { National, Regional }

/// <summary>Nombres y textos que el escritorio comparte entre los textos y el nombre de un ajuste.</summary>
public static class TextosAjustes
{
    /// <summary>Nombre EXACTO del ajuste de HOME (0.6.6, B1): los textos del resultado mandan a este ajuste por este nombre.</summary>
    public static string AjusteHome => IdiomaInterfaz.T("Ajustes.Propiedad.AllowHomeTrackerless.Nombre");
    public const string CategoriaPokekeep = "Pokekeep";
}

/// <summary>
/// Los ajustes de Pokekeep en el móvil: MISMAS propiedades, mismos nombres, mismos textos y mismos valores por defecto que <c>PokekeepSettings</c> del escritorio (una fila por propiedad; la prueba
/// <c>AjustesParidadTests</c> los compara por reflexión con la clase real y falla si falta, sobra o difiere alguno). Excluidas con motivo: <c>WorkerPath</c> y <c>CatalogPath</c> (rutas del PC; en el móvil el
/// motor es un proceso de la propia app y el catálogo vive en su carpeta privada). Se guardan en <c>ajustes.json</c> (<see cref="AlmacenAjustes"/>).
/// </summary>
public sealed class AjustesPokekeep
{
    public const int MinTimeoutSeconds = 10;
    public const int MaxTimeoutSeconds = 3600;
    public const int DefaultLegalizeTimeoutSeconds = 60;
    public const int DefaultOptimizeTimeoutSeconds = 300;

    [Category("Pokekeep"), NombreLocalizado("Ajustes.Propiedad.OptimizeStatsOnLegalize.Nombre")]
    [DescripcionLocalizada("Ajustes.Propiedad.OptimizeStatsOnLegalize.Descripcion")]
    public bool OptimizeStatsOnLegalize { get; set; } = true;

    [Category("Pokekeep"), NombreLocalizado("Ajustes.Propiedad.CoherentHistory.Nombre")]
    [DescripcionLocalizada("Ajustes.Propiedad.CoherentHistory.Descripcion")]
    public bool CoherentHistory { get; set; } = true;

    [Category("Pokekeep"), NombreLocalizado("Ajustes.Propiedad.LegalizeTimeoutSeconds.Nombre")]
    [DescripcionLocalizada("Ajustes.Propiedad.LegalizeTimeoutSeconds.Descripcion")]
    public int LegalizeTimeoutSeconds
    {
        get;
        set => field = Math.Clamp(value, MinTimeoutSeconds, MaxTimeoutSeconds);
    } = DefaultLegalizeTimeoutSeconds;

    [Category("Pokekeep"), NombreLocalizado("Ajustes.Propiedad.OptimizeTimeoutSeconds.Nombre")]
    [DescripcionLocalizada("Ajustes.Propiedad.OptimizeTimeoutSeconds.Descripcion")]
    public int OptimizeTimeoutSeconds
    {
        get;
        set => field = Math.Clamp(value, MinTimeoutSeconds, MaxTimeoutSeconds);
    } = DefaultOptimizeTimeoutSeconds;

    [Category("Pokekeep"), NombreLocalizado("Ajustes.Propiedad.AllowHomeTrackerless.Nombre")]
    [DescripcionLocalizada("Ajustes.Propiedad.AllowHomeTrackerless.Descripcion")]
    public bool AllowHomeTrackerless { get; set; }

    [Category("Pokekeep"), NombreLocalizado("Ajustes.Propiedad.BulkWorkers.Nombre")]
    [DescripcionLocalizada("Ajustes.Propiedad.BulkWorkers.Descripcion")]
    public int BulkWorkers
    {
        get;
        set => field = Math.Clamp(value, 1, 8);
    } = Math.Max(1, Math.Clamp(Environment.ProcessorCount / 2, 1, 4));

    [Category("Pokekeep"), NombreLocalizado("Ajustes.Propiedad.LivingDexForms.Nombre")]
    [DescripcionLocalizada("Ajustes.Propiedad.LivingDexForms.Descripcion")]
    public FormasCompletar LivingDexForms { get; set; } = FormasCompletar.OnePerSpecies;

    [Category("Pokekeep"), NombreLocalizado("Ajustes.Propiedad.LivingDexOrder.Nombre")]
    [DescripcionLocalizada("Ajustes.Propiedad.LivingDexOrder.Descripcion")]
    public OrdenCompletar LivingDexOrder { get; set; } = OrdenCompletar.National;

    [Category("Pokekeep"), NombreLocalizado("Ajustes.Propiedad.LivingDexSkipOwned.Nombre")]
    [DescripcionLocalizada("Ajustes.Propiedad.LivingDexSkipOwned.Descripcion")]
    public bool LivingDexSkipOwned { get; set; } = true;

    [Category("Pokekeep"), NombreLocalizado("Ajustes.Propiedad.PreferEventVersion.Nombre")]
    [DescripcionLocalizada("Ajustes.Propiedad.PreferEventVersion.Descripcion")]
    public bool PreferEventVersion { get; set; }

    [Category("Pokekeep"), NombreLocalizado("Ajustes.Propiedad.RegisterInPokedex.Nombre")]
    [DescripcionLocalizada("Ajustes.Propiedad.RegisterInPokedex.Descripcion")]
    public bool RegisterInPokedex { get; set; } = true;

    [Category("Pokekeep"), NombreLocalizado("Ajustes.Propiedad.ShowSummary.Nombre")]
    [DescripcionLocalizada("Ajustes.Propiedad.ShowSummary.Descripcion")]
    public bool ShowSummary { get; set; } = true;

    [Category("Pokekeep"), NombreLocalizado("Ajustes.Propiedad.SmoothBoxSprites.Nombre")]
    [DescripcionLocalizada("Ajustes.Propiedad.SmoothBoxSprites.Descripcion")]
    public bool SmoothBoxSprites { get; set; }

    public TimeSpan TimeoutFor(AccionPokemon accion) => TimeSpan.FromSeconds(accion == AccionPokemon.Optimizar ? OptimizeTimeoutSeconds : LegalizeTimeoutSeconds);

    public AjustesPokekeep Copiar() => (AjustesPokekeep)MemberwiseClone();
}

public enum TipoAjuste { Interruptor, Entero, Opcion }

/// <summary>Una fila de la pantalla de Ajustes de Pokekeep: sale por reflexión de <see cref="AjustesPokekeep"/>, así que siempre hay una por propiedad con el nombre y la descripción de la clase.</summary>
public sealed record DefinicionAjuste(string Propiedad, string ClaveNombre, string ClaveDescripcion, TipoAjuste Tipo, string Grupo, int Minimo, int Maximo, int Paso, IReadOnlyList<(string Valor, string Etiqueta)> RecursosOpciones)
{
    public string Nombre => IdiomaInterfaz.T(ClaveNombre);
    public string Descripcion => IdiomaInterfaz.T(ClaveDescripcion);
    public IReadOnlyList<(string Valor, string Etiqueta)> Opciones => [.. RecursosOpciones.Select(o => (o.Valor, IdiomaInterfaz.T(o.Etiqueta)))];
    public const string GrupoMotor = "Motor";
    public const string GrupoHistoria = "Historia";
    public const string GrupoLotes = "Operaciones por lotes";
    public const string GrupoInterfaz = "Interfaz";

    /// <summary>Orden y agrupación de la pantalla (destino de cada fila del censo: «Más &gt; Ajustes &gt; Motor / Historia / Operaciones por lotes / Interfaz»).</summary>
    public static readonly string[] Grupos = [GrupoMotor, GrupoHistoria, GrupoLotes, GrupoInterfaz];

    private static readonly Dictionary<string, string> GrupoDe = new()
    {
        [nameof(AjustesPokekeep.OptimizeStatsOnLegalize)] = GrupoMotor,
        [nameof(AjustesPokekeep.LegalizeTimeoutSeconds)] = GrupoMotor,
        [nameof(AjustesPokekeep.OptimizeTimeoutSeconds)] = GrupoMotor,
        [nameof(AjustesPokekeep.AllowHomeTrackerless)] = GrupoMotor,
        [nameof(AjustesPokekeep.RegisterInPokedex)] = GrupoMotor,
        [nameof(AjustesPokekeep.CoherentHistory)] = GrupoHistoria,
        [nameof(AjustesPokekeep.BulkWorkers)] = GrupoLotes,
        [nameof(AjustesPokekeep.LivingDexForms)] = GrupoLotes,
        [nameof(AjustesPokekeep.LivingDexOrder)] = GrupoLotes,
        [nameof(AjustesPokekeep.LivingDexSkipOwned)] = GrupoLotes,
        [nameof(AjustesPokekeep.PreferEventVersion)] = GrupoLotes,
        [nameof(AjustesPokekeep.ShowSummary)] = GrupoInterfaz,
        [nameof(AjustesPokekeep.SmoothBoxSprites)] = GrupoInterfaz,
    };

    /// <summary>Etiquetas en castellano de los valores de enum (las del parche 0007 de PKHeX; el JSON guarda el nombre del valor).</summary>
    private static readonly Dictionary<string, string> EtiquetasEnum = new()
    {
        ["FormasCompletar.OnePerSpecies"] = "Ajustes.Opcion.FormasCompletar.OnePerSpecies",
        ["FormasCompletar.AllStorable"] = "Ajustes.Opcion.FormasCompletar.AllStorable",
        ["FormasCompletar.OnePlusRegional"] = "Ajustes.Opcion.FormasCompletar.OnePlusRegional",
        ["OrdenCompletar.National"] = "Ajustes.Opcion.OrdenCompletar.National",
        ["OrdenCompletar.Regional"] = "Ajustes.Opcion.OrdenCompletar.Regional",
    };

    public static string NombreGrupo(string grupo) => IdiomaInterfaz.T("Ajustes.Grupo." + Array.IndexOf(Grupos, grupo));

    public static IReadOnlyList<DefinicionAjuste> Todas { get; } = Construir();

    public static IReadOnlyList<DefinicionAjuste> Del(string grupo) => [.. Todas.Where(d => d.Grupo == grupo)];

    private static List<DefinicionAjuste> Construir()
    {
        var lista = new List<DefinicionAjuste>();
        foreach (var p in typeof(AjustesPokekeep).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetCustomAttribute<NombreLocalizadoAttribute>() is not { } nombre) continue;
            var desc = p.GetCustomAttribute<DescripcionLocalizadaAttribute>()?.Clave ?? string.Empty;
            if (!GrupoDe.TryGetValue(p.Name, out var grupo)) throw new InvalidOperationException($"Falta el grupo de pantalla del ajuste {p.Name}.");
            if (p.PropertyType == typeof(bool)) lista.Add(new(p.Name, nombre.Clave, desc, TipoAjuste.Interruptor, grupo, 0, 1, 1, []));
            else if (p.PropertyType == typeof(int))
            {
                var tiempo = p.Name.EndsWith("TimeoutSeconds", StringComparison.Ordinal);
                lista.Add(new(p.Name, nombre.Clave, desc, TipoAjuste.Entero, grupo, tiempo ? AjustesPokekeep.MinTimeoutSeconds : 1, tiempo ? AjustesPokekeep.MaxTimeoutSeconds : 8, tiempo ? 10 : 1, []));
            }
            else if (p.PropertyType.IsEnum)
                lista.Add(new(p.Name, nombre.Clave, desc, TipoAjuste.Opcion, grupo, 0, 0, 1,
                    [.. Enum.GetNames(p.PropertyType).Select(v => (v, EtiquetasEnum.TryGetValue($"{p.PropertyType.Name}.{v}", out var e) ? e : throw new InvalidOperationException($"Falta la etiqueta de {p.PropertyType.Name}.{v}.")))]));
            else throw new InvalidOperationException($"Tipo de ajuste sin pantalla: {p.PropertyType}.");
        }
        return lista.OrderBy(d => Array.IndexOf(Grupos, d.Grupo)).ThenBy(d => Orden(d.Propiedad)).ToList();
    }

    private static int Orden(string propiedad) => Array.FindIndex(typeof(AjustesPokekeep).GetProperties(BindingFlags.Public | BindingFlags.Instance), p => p.Name == propiedad);
}
