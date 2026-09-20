using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace BirdyCreditStatus;

/// <summary>T5: teilt die Balkenfüllung (Value 0..100 bei Maximum 100) in zwei
/// Star-Spalten — WinUI3 hat kein MultiBinding, daher je Spalte eine Bindung
/// an Value mit Parameter "Rest" für die zweite Spalte.</summary>
internal sealed class StarWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var v = value is double d ? d : 0;
        var part = parameter as string == "Rest" ? 100 - v : v;
        return new GridLength(Math.Max(0, part), GridUnitType.Star);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
