using System.Collections.Immutable;
using System.Drawing;

namespace MainCore.UI.Models.Output
{
    public class TroopItem : ReactiveObject
    {
        public static readonly ImmutableDictionary<int, int> ImageOffset = new Dictionary<int, int>
        {
            {1 , 0 },
            {2, 19 },
            {3, 38 },
            {4, 57 },
            {5, 76 },
            {6, 95 },
            {7, 114},
            {8, 133},
            {9, 152},
            {0, 171},
        }.ToImmutableDictionary();

        public static readonly ImmutableDictionary<TribeEnums, string> TribeImage = new Dictionary<TribeEnums, string>
        {
            {TribeEnums.Romans, "roman.png" },
            {TribeEnums.Teutons, "teuton.png" },
            {TribeEnums.Gauls, "gaul.png" },
            {TribeEnums.Nature, "nature.png" },
            {TribeEnums.Natars, "natar.png" },
            {TribeEnums.Egyptians, "egyptian.png" },
            {TribeEnums.Huns, "hun.png" },
            {TribeEnums.Spartan, "spartan.png" },
        }.ToImmutableDictionary();

        public TroopItem(TroopEnums troop)
        {
            Troop = troop;
        }

        /// <summary>File name of the sprite sheet; each UI maps it to its own resource URI.</summary>
        public static string GetImageFile(TroopEnums troop)
        {
            if (troop == TroopEnums.None) return TribeImage[TribeEnums.Natars];
            return TribeImage[troop.GetTribe()];
        }

        public static Rectangle GetImageMask(TroopEnums troop)
        {
            if (troop == TroopEnums.None) return new(57, 0, 16, 16);
            return new(ImageOffset[(int)troop % 10], 0, 16, 16);
        }

        public TroopEnums Troop { get; }

        public string ImageFile => GetImageFile(Troop);
        public Rectangle ImageMask => GetImageMask(Troop);
    }
}