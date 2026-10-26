// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.ComponentModel;

namespace AutoLegalidad.Ui;

/// <summary>
/// Ajustes de Pokekeep (Hito 4, punto 8). Se guardan dentro de la configuración de PKHeX (<c>cfg.json</c>, sección <c>Pokekeep</c>) y se editan en
/// Opciones &gt; Ajustes. Los nombres y descripciones están en castellano: el sistema de idiomas de PKHeX solo traduce las claves que conoce y deja
/// intacto el resto.
/// </summary>
public sealed class PokekeepSettings
{
    public const int MinTimeoutSeconds = 10;
    public const int MaxTimeoutSeconds = 3600;
    public const int DefaultLegalizeTimeoutSeconds = 60;
    public const int DefaultOptimizeTimeoutSeconds = 300;

    [Category("Pokekeep"), DisplayName("Optimizar al legalizar")]
    [Description("Tras legalizar, optimiza los IV al máximo que permita el encuentro (R-001). Activado por defecto. No cambia ningún dato de origen ni rompe la legalidad: si optimizar dejara el Pokémon inválido, se aplica el resultado válido sin optimizar.")]
    public bool OptimizeStatsOnLegalize { get; set; } = true;

    [Category("Pokekeep"), DisplayName("Historia coherente")]
    [Description("Los Pokémon que Pokekeep construye llevan una historia creíble: solo las cintas de su origen, fechas reales y sin entrenador anterior que no corresponda. Nunca rompe la legalidad. «Optimizar stats» no la toca.")]
    public bool CoherentHistory { get; set; } = true;

    [Category("Pokekeep"), DisplayName("Tiempo máx. legalizar (s)")]
    [Description("Tiempo máximo de la búsqueda de «Legalizar automáticamente» y de «Añadir a la caja». Al agotarse se avisa con «Tiempo agotado», que no prueba que sea imposible. Entre 10 y 3600 segundos.")]
    public int LegalizeTimeoutSeconds
    {
        get;
        set => field = Math.Clamp(value, MinTimeoutSeconds, MaxTimeoutSeconds);
    } = DefaultLegalizeTimeoutSeconds;

    [Category("Pokekeep"), DisplayName("Tiempo máx. optimizar (s)")]
    [Description("Tiempo máximo de «Optimizar stats». La búsqueda exhaustiva de semillas de algunos encuentros (incursiones, Leyendas) puede tardar un par de minutos. Entre 10 y 3600 segundos.")]
    public int OptimizeTimeoutSeconds
    {
        get;
        set => field = Math.Clamp(value, MinTimeoutSeconds, MaxTimeoutSeconds);
    } = DefaultOptimizeTimeoutSeconds;

    [Category("Pokekeep"), DisplayName(Textos.AjusteHome)]
    [Description("Desactivado por defecto. RIESGO: sin la matrícula que asigna Pokémon HOME, PKHeX da el Pokémon por inválido hasta que lo pases por HOME, y Pokekeep no puede comprobar qué harán con él los servidores de Nintendo ni los juegos. Nunca se inventa la matrícula. Afecta a Pokémon GO hacia Switch, regalos de HOME y algunas evoluciones y formas; el resumen avisa con «Necesita pasar por HOME».")]
    public bool AllowHomeTrackerless { get; set; }

    [Category("Pokekeep"), DisplayName("Procesos en paralelo")]
    [Description("Cuántos procesos del motor trabajan a la vez en «Completar el juego» y «Edición masiva». Entre 1 y 8. Por defecto, la mitad de los núcleos del equipo (máximo 4).")]
    public int BulkWorkers
    {
        get;
        set => field = Math.Clamp(value, 1, 8);
    } = Bulk.BulkRunnerOptions.DefaultWorkers();

    [Category("Pokekeep"), DisplayName("Completar: formas")]
    [Description("Qué formas genera «Completar el juego»: una por especie (por defecto), todas las formas almacenables o una por especie más las formas regionales.")]
    public Bulk.LivingDexForms LivingDexForms { get; set; } = Bulk.LivingDexForms.OnePerSpecies;

    [Category("Pokekeep"), DisplayName("Completar: orden")]
    [Description("Orden de las casillas de «Completar el juego»: Pokédex nacional (por defecto) o la regional del juego, cuando PKHeX la conoce.")]
    public Bulk.LivingDexOrder LivingDexOrder { get; set; } = Bulk.LivingDexOrder.National;

    [Category("Pokekeep"), DisplayName("Completar: sin repetir")]
    [Description("No genera una especie y forma de la que ya hay un Pokémon legal en tus cajas o equipo. Activado por defecto.")]
    public bool LivingDexSkipOwned { get; set; } = true;

    [Category("Pokekeep"), DisplayName("Completar: preferir evento")]
    [Description("Desactivado por defecto. En «Completar el juego», los legendarios, sublegendarios y singulares se intentan primero desde una tarjeta de evento legal en ese juego (la mejor según el criterio de Pokekeep: tarjeta en español, variocolor, exclusiva de un país, distribución más antigua); si ninguna sale legal con el motor, se usa el origen de siempre. Nunca fuerza nada: lo decide PKHeX.")]
    public bool PreferEventVersion { get; set; }

    [Category("Pokekeep"), DisplayName("Registrar en la Pokédex")]
    [Description("Al añadir un Pokémon desde la Librería o con «Completar el juego», lo marca como visto y capturado en la Pokédex de la partida, igual que cuando lo consigues en el juego (usa el registro oficial de PKHeX; no cambia el Pokémon ni su legalidad). Activado por defecto. «Deshacer» devuelve el Pokémon pero no borra el registro de la Pokédex.")]
    public bool RegisterInPokedex { get; set; } = true;

    [Category("Pokekeep"), DisplayName("Mostrar resumen")]
    [Description("Muestra la barra de resumen (con «Ver cambios» y «Deshacer») cuando una operación termina. Los fallos se muestran siempre.")]
    public bool ShowSummary { get; set; } = true;

    [Category("Pokekeep"), DisplayName("Ruta del motor")]
    [Description("Ruta del ejecutable pokekeep-worker.exe. Vacío = el que va junto a PKHeX.exe (el paquete portable ignora este ajuste y usa siempre el suyo).")]
    public string WorkerPath { get; set; } = string.Empty;

    [Category("Pokekeep"), DisplayName("Ruta de la base de datos")]
    [Description("Ruta de autolegalidad.sqlite. Vacío = la carpeta Pokekeep\\datos junto a PKHeX.exe (el paquete portable ignora este ajuste y usa siempre la suya).")]
    public string CatalogPath { get; set; } = string.Empty;

    public TimeSpan TimeoutFor(Contracts.LegalizeAction action) => TimeSpan.FromSeconds(action == Contracts.LegalizeAction.Optimize ? OptimizeTimeoutSeconds : LegalizeTimeoutSeconds);
}
