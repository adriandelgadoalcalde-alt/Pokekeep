// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Sprites;

/// <summary>
/// Recursos suplementarios de pantalla. Los originales siempre tienen prioridad y no se modifican; el compositor canónico y sus pruebas siguen usando solo los originales.
/// Las correspondencias, revisiones y huellas de los PNG suplementarios se registran al construir el paquete, nunca se descargan en el móvil.
/// </summary>
public sealed class RecursosPresentacion(IRecursosSprites originales, IRecursosSprites? adicionales) : IRecursosSprites
{
    public SpriteImage? Obtener(string nombre) => originales.Obtener(nombre) ?? adicionales?.Obtener(nombre);
}
