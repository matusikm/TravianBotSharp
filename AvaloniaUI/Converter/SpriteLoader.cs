using System.Collections.Concurrent;
using System.Drawing;

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AvaloniaUI.Converter
{
    /// <summary>Loads the tribe/troop sprite sheets from Assets and crops them.</summary>
    internal static class SpriteLoader
    {
        private static readonly ConcurrentDictionary<string, Bitmap> _cache = new();

        public static CroppedBitmap Crop(string file, Rectangle mask)
        {
            var bitmap = _cache.GetOrAdd(file, f =>
            {
                using var stream = AssetLoader.Open(new Uri($"avares://TravianBotSharp/Assets/{f}"));
                return new Bitmap(stream);
            });
            return new CroppedBitmap(bitmap, new PixelRect(mask.X, mask.Y, mask.Width, mask.Height));
        }
    }
}
