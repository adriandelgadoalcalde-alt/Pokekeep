// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>Una entrada del menu de Partida: que editor, que dice y, si no aplica a este guardado, POR QUE (nunca un boton muerto sin explicacion).</summary>
public sealed record EntradaEditorP(string Id, string Titulo, string Descripcion, string Glifo, string? NoAplica = null)
{
    public bool Aplica => NoAplica is null;
}

/// <summary>
/// Que editores de partida tiene cada tipo de guardado: la misma regla de <c>SAVEditor.ToggleViewSubEditors</c> y de los despachadores de PKHeX (<c>GetTrainerEditor</c>, <c>B_OpenPokedex</c>, <c>B_OpenEventFlags</c>...),
/// con el motivo cuando un editor no existe en ese juego.
/// </summary>
public static class CatalogoPartida
{
    public static EditorDefP? Entrenador(SaveFile sav) => sav switch
    {
        SAV4BR br => Entrenador4BR.Crear(br),
        _ when !(sav.HasParty || sav is SAV7b) => null,
        SAV6 s6 => Entrenador6.Crear(s6),
        SAV7 s7 => Entrenador7.Crear(s7),
        SAV7b b7 => Entrenador7b.Crear(b7),
        SAV8SWSH sw => Entrenador8.Crear(sw),
        SAV8LA la => EntrenadorSwitch.Crear8a(la),
        SAV8BS bs => EntrenadorSwitch.Crear8b(bs),
        SAV9SV sv => EntrenadorSwitch.Crear9(sv),
        SAV9ZA za => EntrenadorSwitch.Crear9a(za),
        _ => EntrenadorSimple.Crear(sav),
    };

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Los bloques de la partida son tipos de PKHeX que la aplicacion conserva enteros (TrimMode parcial): la reflexion los encuentra.")]
    public static EditorDefP? Bloques(SaveFile sav) => BloquesP.NoAplica(sav) is null ? BloquesP.Crear(sav) : null;
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Tipos de PKHeX conservados enteros.")]
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> BloquesOmitidos(SaveFile sav) => BloquesP.Omitidas(sav);
    public static string? NoAplicaBloques(SaveFile sav) => BloquesP.NoAplica(sav);
    public static EditorDefP? Cartas(SaveFile sav) => CartasP.NoAplica(sav) is null ? CartasP.Crear(sav) : null;
    public static string? NoAplicaCartas(SaveFile sav) => CartasP.NoAplica(sav);
    public static EditorDefP? Pokedex(SaveFile sav) => PokedexP.Crear(sav);
    public static string? NoAplicaPokedex(SaveFile sav) => PokedexP.NoAplica(sav);

    public static string? NoAplicaEntrenador(SaveFile sav)
        => sav.HasParty || sav is SAV7b ? null : IdiomaInterfaz.T("Partida.NoAplica.Entrenador");
}
