using System.Globalization;

using Avalonia.Data.Converters;

using MainCore.UI.Models.Output;

namespace AvaloniaUI.Converter
{
    public class TribeItemToCroppedBitmap : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not TribeItem item) return null;
            return SpriteLoader.Crop(item.ImageFile, TribeItem.ImageMask);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
