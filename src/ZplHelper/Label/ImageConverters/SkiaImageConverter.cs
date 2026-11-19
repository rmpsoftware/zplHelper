using System;
using System.IO;
using SkiaSharp;

namespace BinaryKits.Zpl.Label.ImageConverters
{
    /// <summary>Converts between encoded images and Zebra's packed 1-bit graphics.</summary>
    public sealed class SkiaImageConverter : IImageConverter
    {
        internal const int MaxImagePixels = 32_000_000;

        public ImageResult ConvertImage(byte[] imageData)
        {
            using var bitmap = Decode(imageData);
            return ConvertBitmap(bitmap);
        }

        internal static ImageResult ConvertBitmap(SKBitmap bitmap)
        {
            int bytesPerRow = (bitmap.Width + 7) / 8;
            var data = new byte[bitmap.Height * bytesPerRow];
            using var pixels = bitmap.PeekPixels();

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    SKColor pixel = pixels.GetPixelColor(x, y);
                    // Keep Zebra's threshold and treat transparent pixels as paper.
                    int color = (pixel.Red + pixel.Green + pixel.Blue) * pixel.Alpha
                        + 3 * 255 * (255 - pixel.Alpha);
                    if (color < 3 * 128 * 255)
                    {
                        data[y * bytesPerRow + x / 8] |= (byte)(0x80 >> (x % 8));
                    }
                }
            }

            return new ImageResult
            {
                RawData = data,
                BinaryByteCount = data.Length,
                BytesPerRow = bytesPerRow
            };
        }

        /// <summary>Encodes rows of MSB-first bitonal data as black pixels on transparent PNG.</summary>
        public byte[] ConvertImage(byte[] imageData, int bytesPerRow)
        {
            ArgumentNullException.ThrowIfNull(imageData);
            if (bytesPerRow <= 0 || imageData.Length == 0 || imageData.Length % bytesPerRow != 0
                || (long)imageData.Length * 8 > MaxImagePixels)
            {
                throw new FormatException("Invalid dimensions for a ZPL graphic.");
            }

            using var bitmap = new SKBitmap(new SKImageInfo(bytesPerRow * 8, imageData.Length / bytesPerRow,
                SKColorType.Rgba8888, SKAlphaType.Premul));
            Span<byte> pixels = bitmap.GetPixelSpan();
            pixels.Clear();
            for (int y = 0; y < bitmap.Height; y++)
            {
                Span<byte> row = pixels.Slice(y * bitmap.RowBytes, bitmap.Width * 4);
                for (int x = 0; x < bitmap.Width; x++)
                {
                    if ((imageData[y * bytesPerRow + x / 8] & (0x80 >> (x % 8))) != 0)
                    {
                        row[x * 4 + 3] = byte.MaxValue;
                    }
                }
            }

            return EncodePng(bitmap);
        }

        internal static SKBitmap Decode(byte[] imageData)
        {
            ArgumentNullException.ThrowIfNull(imageData);
            using var stream = new SKMemoryStream(imageData);
            using var codec = SKCodec.Create(stream);
            if (codec == null)
            {
                throw new FormatException("SkiaSharp could not read the image.");
            }

            ValidateDimensions(codec.Info.Width, codec.Info.Height);
            return SKBitmap.Decode(codec) ?? throw new FormatException("SkiaSharp could not decode the image.");
        }

        internal static SKBitmap Resize(SKBitmap bitmap, double scale)
        {
            if (!double.IsFinite(scale) || scale <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scale));
            }

            int width = Math.Max(1, checked((int)Math.Round(bitmap.Width * scale)));
            int height = Math.Max(1, checked((int)Math.Round(bitmap.Height * scale)));
            ValidateDimensions(width, height);
            return bitmap.Resize(new SKImageInfo(width, height, bitmap.ColorType, bitmap.AlphaType),
                new SKSamplingOptions(SKCubicResampler.Mitchell))
                ?? throw new InvalidOperationException("SkiaSharp could not resize the image.");
        }

        internal static byte[] EncodePng(SKBitmap bitmap)
        {
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new InvalidOperationException("SkiaSharp could not encode the image.");
            return data.ToArray();
        }

        internal static void ValidateDimensions(int width, int height)
        {
            if (width <= 0 || height <= 0 || (long)width * height > MaxImagePixels)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be positive and fit within 32 million pixels.");
            }
        }
    }
}
