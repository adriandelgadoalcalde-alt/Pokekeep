// SPDX-License-Identifier: GPL-3.0-or-later
using System.ComponentModel;

namespace Pokekeep.Mobile.Ui.Localizacion;

/// <summary>Metadatos visibles que siguen el idioma, también al consultarlos por reflexión.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NombreLocalizadoAttribute(string clave) : DisplayNameAttribute
{
    public string Clave { get; } = clave;
    public override string DisplayName => IdiomaInterfaz.T(Clave);
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class DescripcionLocalizadaAttribute(string clave) : DescriptionAttribute
{
    public string Clave { get; } = clave;
    public override string Description => IdiomaInterfaz.T(Clave);
}
