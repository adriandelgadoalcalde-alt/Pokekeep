// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using PKHeX.Core.Searching;

namespace Pokekeep.Mobile.Ui.Herramientas.BaseDatosPkm;

/// <summary>
/// Los filtros de la pestaña «General» de <c>SAV_Database</c> (el <c>EntitySearchControl</c>, PK-SUB-008) tal y como los lee <c>CreateSearchSettings</c>: especie (0 = cualquiera), objeto, habilidad, tipo de Poder Oculto
/// (-1 = cualquiera), naturaleza (<see cref="Nature.Random"/> = cualquiera), apodo, nivel con su comparador, potencial de IV y entrenamiento de EV (0 = cualquiera), hasta cuatro movimientos, variocolor y huevo
/// (null = indiferente, la casilla «a medias» de WinForms) con el ESV, versión del EO, generación y formato (comparador y contexto), más las instrucciones de la pestaña «Avanzado» (PK-SUB-009).
/// </summary>
public sealed record FiltrosPkm
{
    public ushort Especie { get; init; }
    public int Objeto { get; init; } = -1;
    public int Habilidad { get; init; } = -1;
    public Nature Naturaleza { get; init; } = Nature.Random;
    public int TipoPoderOculto { get; init; } = -1;
    public string Apodo { get; init; } = string.Empty;
    public SearchComparison ComparadorNivel { get; init; }
    public byte? Nivel { get; init; }
    public int PotencialIv { get; init; }
    public int EntrenamientoEv { get; init; }
    public IReadOnlyList<ushort> Movimientos { get; init; } = [];
    public bool? Variocolor { get; init; }
    public bool? Huevo { get; init; }
    public int? Esv { get; init; }
    public GameVersion Version { get; init; }
    public byte Generacion { get; init; }
    public SearchComparison ComparadorFormato { get; init; }
    public EntityContext Formato { get; init; }
    public string Instrucciones { get; init; } = string.Empty;

    /// <summary>
    /// Lo que el formulario tiene al abrirse. <c>InitializeSelections(SAV)</c> deja el comparador de formato en «==» (8.ª generación en adelante) o «&lt;=» (antes) y <c>PopulateComboBoxes</c> vuelve a enlazar la lista
    /// de formatos, así que el formato queda en «Cualquiera»: con <see cref="EntityContext.None"/> el Core ignora el comparador (<c>SearchSettings.SearchSimple</c> exige <c>Context.IsValid</c>) y no se filtra
    /// por formato. El resto, indiferente (<c>ResetFilters</c>). Pendiente de Windows: <c>BaseDatosPkmContraWinFormsTests.ElFormularioAbreConLosFiltrosIniciales</c> lo comprueba en el formulario real.
    /// </summary>
    public static FiltrosPkm Iniciales(SaveFile sav) => new() { ComparadorFormato = sav.Generation >= 8 ? SearchComparison.Equals : SearchComparison.LessThanEquals, Formato = EntityContext.None };
}

/// <summary>Los tres orígenes del menú «Ajustes de búsqueda» de <c>SAV_Database</c> (cajas de la partida, archivos de la carpeta de la base de datos, copias y otras partidas), las casillas «Mostrar legales / ilegales» y «Solo clones».</summary>
public sealed record OpcionesBusqueda
{
    public bool Cajas { get; init; } = true;
    public bool BaseDeDatos { get; init; } = true;
    public bool Copias { get; init; } = true;
    public bool Legales { get; init; } = true;
    public bool Ilegales { get; init; } = true;
    public bool SoloClones { get; init; }
    /// <summary>Con Ctrl pulsado PKHeX detecta clones por el PID (<c>HashPID</c>); sin él, por todos los datos (<c>HashDetails</c>). En el móvil se elige.</summary>
    public CloneDetectionMethod MetodoClones { get; init; } = CloneDetectionMethod.HashDetails;
    /// <summary>
    /// Respuesta a <c>MsgDBSearchLegalityWordfilter</c> («¿Verificar la legalidad del filtro de palabras?»), que PKHeX pregunta al buscar por legalidad con el filtro de palabras activo: true = sí (lo normal),
    /// false = «No», se apaga el filtro de palabras SOLO durante esa búsqueda. En el móvil es un interruptor en vez de un cuadro de diálogo (regla 4).
    /// </summary>
    public bool VerificarFiltroDePalabras { get; init; } = true;
}
