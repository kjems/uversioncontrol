// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace UVC.UserInterface
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
                return LoadTextureFromFile(ruby, Size, color);
            }
            public override int Size { get { return 16; } }
        }
        public class ChildIcon : Icon
        {
            protected override Texture2D LoadTexture(Color color)
            {
                return LoadTextureFromFile(child, Size, color);
            }
            public override int Size { get { return 16; } }
        }
        public class CircleIcon : Icon
        {
            protected override Texture2D LoadTexture(Color color)
            {
                return LoadTextureFromFile(circle, Size, color);
            }
            public override int Size { get { return 16; } }
        }
        public class SquareIcon : Icon
        {
            protected override Texture2D LoadTexture(Color color)
            {
                return LoadTextureFromFile(square, Size, color);
            }
            public override int Size { get { return 16; } }
        }
        public class TriangleIcon : Icon
        {
            protected override Texture2D LoadTexture(Color color)
            {
                return LoadTextureFromFile(triangle, Size, color);
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


        private static Texture2D LoadTextureFromFile(byte[] bytes, int size, Color color)
        {
            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false, true) { hideFlags = HideFlags.HideAndDontSave };
            texture.LoadRawTextureData(bytes);
            for (int x = 0; x < size; ++x)
            {
                for (int y = 0; y < size; ++y)
                {
                    var resourceColor = texture.GetPixel(x, y);
                    bool useResourceColor = resourceColor.a > 0.05f && resourceColor.r < 0.05f && resourceColor.g < 0.05f && resourceColor.b < 0.05f;
                    var newColor = useResourceColor
                                       ? resourceColor
                                       : new Color(color.r, color.g, color.b, Mathf.Min(resourceColor.a, color.a));
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
            var backgroundTexture = new Texture2D(3, 3, TextureFormat.ARGB32, false, true) { hideFlags = HideFlags.HideAndDontSave };

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

            var iconTexture = new Texture2D(size, size, TextureFormat.ARGB32, false, true) { hideFlags = HideFlags.HideAndDontSave };
            iconTexture.SetPixels(colors);
            iconTexture.wrapMode = TextureWrapMode.Clamp;
            iconTexture.filterMode = FilterMode.Point;
            iconTexture.Apply();
            return iconTexture;
        }

        /*
        // Convert selected texture in project to a compressed string used below to store icons in code
        [MenuItem("Assets/UVC/Texture2String")]
        static void DumpSelectedTextureToString()
        {
            var texture = Selection.activeObject as Texture2D;
            if (texture)
            {
                string base64texture = System.Convert.ToBase64String(Compress(texture.GetRawTextureData()));
                Debug.Log(base64texture);
            }
        }

        private static byte[] Compress(byte[] input)
        {
            using(var inputStream = new MemoryStream(input))
            using(var compressStream = new MemoryStream())
            using(var compressor = new DeflateStream(compressStream, CompressionMode.Compress))
            {
                inputStream.CopyTo(compressor);
                compressor.Close();
                return compressStream.ToArray();
            }
        }
        */

        private static byte[] Decompress(byte[] input)
        {
            var output = new MemoryStream();

            using (var compressStream = new MemoryStream(input))
            using (var decompressor = new DeflateStream(compressStream, CompressionMode.Decompress))
                decompressor.CopyTo(output);

            output.Position = 0;
            return output.ToArray();
        }


        static byte[] child     = Decompress(System.Convert.FromBase64String("Y2DADpiYmBhOnz4NxiA2qYAK+hmBeplAGMQmQ385UO8ZEAaxydA/G6j3PwiD2GTon4Gkf8ao/lH9g1E/AA=="));
        static byte[] circle    = Decompress(System.Convert.FromBase64String("Y2AYtuA/GiZJLzogwQwMvUSawYlPL5FmDGX9goTMIGA3XneQoBduBgP56YeHRPUDBgA="));
        static byte[] ruby      = Decompress(System.Convert.FromBase64String("Y2CgKfgPxWTphQEyzPiPDkgwA0MvCWbg1EuEGQT1EjCDUv2Uup8a4YfTDBL0YphBhl64GRToHRIAAA=="));
        static byte[] square    = Decompress(System.Convert.FromBase64String("Y2AYVGAzkRin/v8EwKj+4a2fSDwKoAAA"));
        static byte[] triangle  = Decompress(System.Convert.FromBase64String("Y2AgGfwnEe//TyQAqYXZQYTa/0huImgHktkMhOxAM5ugHVjMxmkHDrNx2oHHbAw7CJiNoocEtQxQN2B1BwA="));

    }

}
