using System.Collections.Generic;
using UnityEngine;

namespace VersionControl.UserInterface
{
    public static class IconUtils
    {
        // Const
        public const int borderSize = 1;
        public static readonly RubyIcon rubyIcon = new RubyIcon();
        public static readonly ChildIcon childIcon = new ChildIcon();
        public static readonly CircleIcon circleIcon = new CircleIcon();
        public static readonly SquareIcon squareIcon = new SquareIcon();
        public static readonly TriangleIcon triangleIcon = new TriangleIcon();
        public static readonly BoxIcon boxIcon = new BoxIcon();

        public abstract class Icon
        {
            private static readonly Dictionary<int, Texture2D> iconDatabase = new Dictionary<int, Texture2D>();
            public Texture2D GetTexture(Color color)
            {
                int hashCode = color.GetHashCode() ^ GetType().GetHashCode();
                Texture2D texture;
                if (!iconDatabase.TryGetValue(hashCode, out texture))
                {
                    texture = LoadTexture(color);
                    iconDatabase.Add(hashCode, texture);
                }
                return texture;
            }
            public abstract int Size { get; }
            protected abstract Texture2D LoadTexture(Color color);
        }
        public class RubyIcon : Icon
        {
            protected override Texture2D LoadTexture(Color color)
            {
                return CreateTexture(System.Reflection.Assembly.GetCallingAssembly().GetManifestResourceStream("ruby"), Size, color);
            }
            public override int Size { get { return 16; } }
        }
        public class ChildIcon : Icon
        {
            protected override Texture2D LoadTexture(Color color)
            {
                return CreateTexture(System.Reflection.Assembly.GetCallingAssembly().GetManifestResourceStream("child"), Size, color);
            }
            public override int Size { get { return 20; } }
        }
        public class CircleIcon : Icon
        {
            protected override Texture2D LoadTexture(Color color)
            {
                return CreateTexture(System.Reflection.Assembly.GetCallingAssembly().GetManifestResourceStream("circle"), Size, color);
            }
            public override int Size { get { return 16; } }
        }
        public class SquareIcon : Icon
        {
            protected override Texture2D LoadTexture(Color color)
            {
                return CreateTexture(System.Reflection.Assembly.GetCallingAssembly().GetManifestResourceStream("square"), Size, color);
            }
            public override int Size { get { return 16; } }
        }
        public class TriangleIcon : Icon
        {
            protected override Texture2D LoadTexture(Color color)
            {
                return CreateTexture(System.Reflection.Assembly.GetCallingAssembly().GetManifestResourceStream("triangle"), Size, color);
            }
            public override int Size { get { return 12; } }
        }
        public class BoxIcon : Icon
        {
            protected override Texture2D LoadTexture(Color color)
            {
                return CreateSquareTextureWithBorder(Size, borderSize, color, Color.black);
            }
            public override int Size { get { return 12; } }
        }


        private static Texture2D CreateTexture(System.IO.Stream resourceBitmap, int size, Color color)
        {
            D.Assert(resourceBitmap != null, "Assuming the resource file is valid");
            byte[] bytes = new byte[(int)resourceBitmap.Length];
            resourceBitmap.Read(bytes, 0, (int)resourceBitmap.Length);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.LoadImage(bytes);
            for (int x = 0; x < size; ++x)
            {
                for (int y = 0; y < size; ++y)
                {
                    var resourceColor = texture.GetPixel(x, y);
                    bool resourcePixelIsWhite = resourceColor.r == 1 && resourceColor.g == 1 && resourceColor.b == 1;
                    var newColor = resourcePixelIsWhite
                                       ? new Color(color.r, color.g, color.b, Mathf.Min(resourceColor.a, color.a))
                                       : resourceColor;
                    texture.SetPixel(x, y, newColor);
                }
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            texture.Apply();
            return texture;
        }


        public static Texture2D CreateBorderedTexture(Color border, Color body)
        {
            var backgroundTexture = new Texture2D(3, 3, TextureFormat.ARGB32, false) { hideFlags = HideFlags.HideAndDontSave };

            backgroundTexture.SetPixels(new[]
            {
                border, border, border,
                border, body, border,
                border, border, border,
            });
            backgroundTexture.wrapMode = TextureWrapMode.Clamp;
            backgroundTexture.filterMode = FilterMode.Point;
            backgroundTexture.Apply();
            return backgroundTexture;
        }

        public static Texture2D CreateSquareTexture(int size, int borderSize, Color color)
        {
            return CreateSquareTextureWithBorder(size, borderSize, color, color);
        }

        public static Texture2D CreateSquareTextureWithBorder(int size, int borderSize, Color inner, Color border)
        {
            var colors = new Color[size * size];
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    bool onborder = (x < borderSize || x >= size - borderSize || y < borderSize || y >= size - borderSize);
                    colors[x + y * size] = onborder ? border : inner;
                }
            }

            var iconTexture = new Texture2D(size, size, TextureFormat.ARGB32, false) { hideFlags = HideFlags.HideAndDontSave };
            iconTexture.SetPixels(colors);
            iconTexture.wrapMode = TextureWrapMode.Clamp;
            iconTexture.filterMode = FilterMode.Point;
            iconTexture.Apply();
            return iconTexture;
        }
    }
}
