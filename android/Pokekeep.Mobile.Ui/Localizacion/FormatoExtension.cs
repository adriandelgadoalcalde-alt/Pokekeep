// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace Pokekeep.Mobile.Ui.Localizacion;

/// <summary>Formato localizado que observa tanto el valor enlazado como el idioma. No almacena texto ya traducido.</summary>
public sealed class FormatoExtension(string clave, BindingBase argumento) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => new MultiBinding
    {
        Bindings = { argumento, new Binding(nameof(ServicioIdioma.Codigo)) { Source = IdiomaInterfaz.Actual, Mode = BindingMode.OneWay } },
        Converter = new Conversor(clave),
        Mode = BindingMode.OneWay,
    };

    private sealed class Conversor(string clave) : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count != 2 || values[1] is not string codigo || values[0] == AvaloniaProperty.UnsetValue) return AvaloniaProperty.UnsetValue;
            if (ServicioIdioma.Recursos(codigo).ContainsKey(clave + ".otro"))
                return values[0] is null ? AvaloniaProperty.UnsetValue : IdiomaInterfaz.P(clave, System.Convert.ToInt64(values[0], CultureInfo.InvariantCulture));
            return string.Format(CultureInfo.GetCultureInfo(codigo), ServicioIdioma.Recursos(codigo)[clave], values[0]);
        }
    }
}
