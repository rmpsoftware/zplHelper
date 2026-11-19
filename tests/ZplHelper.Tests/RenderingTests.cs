using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Label.Helpers;
using BinaryKits.Zpl.Label.ImageConverters;
using BinaryKits.Zpl.Viewer;
using BinaryKits.Zpl.Viewer.ElementDrawers;
using BinaryKits.Zpl.Viewer.Helpers;
using NUnit.Framework;
using SkiaSharp;
using ZXing;
using FormatException = System.FormatException;

namespace ZplHelper.Tests;

public class RenderingTests
{
    [Test]
    public void RendersUtf8FieldBlockAndScannableQrCodeAt12Dpmm()
    {
        const string payload = "ZPL-TEST-4711";
        const string zpl = $"""
            ^XA^CI28^LT0
            ^FO24,24^AQN,32,28^FB300,2,0,L,0^FH_^FDGrüße M_C3_BCnchen^FS
            ^FO360,110^BQN,2,5^FDLA,{payload}^FS
            ^FO24,240^GB180,40,3^FS
            ^XZ
            """;
        var storage = new PrinterStorage();
        var options = new DrawerOptions { OpaqueBackground = true };
        using var bitmap = SKBitmap.Decode(Render(storage, zpl, options));
        Assert.That((bitmap.Width, bitmap.Height), Is.EqualTo((685, 912)));
        Assert.That(bitmap.GetPixel(0, 0), Is.EqualTo(SKColors.White));
        Assert.That(bitmap.GetPixel(25, 241), Is.EqualTo(SKColors.Black));
        Assert.That(HasInk(bitmap, new SKRectI(24, 24, 350, 104)), Is.True);

        var qrBounds = new SKRectI(340, 90, 600, 340);
        var luminance = new byte[qrBounds.Width * qrBounds.Height];
        for (int y = 0; y < qrBounds.Height; y++)
        {
            for (int x = 0; x < qrBounds.Width; x++)
            {
                luminance[y * qrBounds.Width + x] = bitmap.GetPixel(qrBounds.Left + x, qrBounds.Top + y).Red;
            }
        }

        var reader = new BarcodeReaderGeneric();
        var decoded = reader.Decode(new RGBLuminanceSource(luminance, qrBounds.Width, qrBounds.Height,
            RGBLuminanceSource.BitmapFormat.Gray8));
        Assert.That(decoded?.Text, Is.EqualTo(payload));
    }

    [TestCase("8040")]
    [TestCase("80:")]
    public void RendersInlineGraphicWithTransparentBackground(string data)
    {
        using var bitmap = SKBitmap.Decode(Render(new PrinterStorage(), $"^XA^FO10,10^GFA,2,2,1,{data}^FS^XZ"));
        Assert.That(bitmap.GetPixel(10, 10), Is.EqualTo(SKColors.Black));
        Assert.That(bitmap.GetPixel(11, 10).Alpha, Is.Zero);
        int secondRowX = data == "8040" ? 11 : 10;
        Assert.That(bitmap.GetPixel(secondRowX, 11), Is.EqualTo(SKColors.Black));
    }

    [Test]
    public void RendersZ64AndPreloadedImageThroughPrinterStorage()
    {
        var storage = new PrinterStorage();
        byte[] pixels = [0x80, 0x40];
        var converter = new SkiaImageConverter();
        storage.AddFile('E', "ASSET.GRF", converter.ConvertImage(pixels, 1));
        string z64 = ZebraZ64CompressionHelper.Compress(pixels);
        string zpl = $"~DGR:IMAGE.GRF,2,1,{z64}^XA^FO10,20^XGR:IMAGE.GRF,1,1^FS^FO30,20^XGE:ASSET.GRF,1,1^FS^XZ";
        using var label = SKBitmap.Decode(Render(storage, zpl));
        Assert.That(label.GetPixel(10, 20), Is.EqualTo(SKColors.Black));
        Assert.That(label.GetPixel(11, 21), Is.EqualTo(SKColors.Black));
        Assert.That(label.GetPixel(30, 20), Is.EqualTo(SKColors.Black));
        Assert.That(label.GetPixel(31, 21), Is.EqualTo(SKColors.Black));
        Assert.That(label.GetPixel(31, 20).Alpha, Is.Zero);
    }

    [Test]
    public void SkiaConverterPacksRowsAndIgnoresTransparentPixels()
    {
        using var source = new SKBitmap(9, 2);
        source.Erase(SKColors.Transparent);
        source.SetPixel(0, 0, SKColors.Black);
        source.SetPixel(8, 0, SKColors.Black);
        source.SetPixel(5, 1, SKColors.Black);
        source.SetPixel(6, 1, SKColors.White);
        using var png = source.Encode(SKEncodedImageFormat.Png, 100);

        var result = new SkiaImageConverter().ConvertImage(png.ToArray());
        Assert.That(result.BytesPerRow, Is.EqualTo(2));
        Assert.That(result.BinaryByteCount, Is.EqualTo(4));
        Assert.That(result.RawData, Is.EqualTo(new byte[] { 0x80, 0x80, 0x04, 0x00 }));
    }

    [Test]
    public void EncodesDownloadObjectsAndGraphicsWithSkia()
    {
        byte[] image = new SkiaImageConverter().ConvertImage([0x80, 0x40], 1);
        var options = new ZplRenderOptions();
        string graphics = string.Concat(new ZplDownloadGraphics('R', "TEST", image, ZplCompressionScheme.None).Render(options));
        Assert.That(graphics, Is.EqualTo("~DGR:TEST.GRF,2,1,8040"));
        string download = string.Concat(new ZplDownloadObjects('R', "TEST.PNG", image).Render(options));
        byte[] decoded = Convert.FromHexString(download.Split(',')[5]);
        using var bitmap = SKBitmap.Decode(decoded);
        Assert.That((bitmap.Width, bitmap.Height), Is.EqualTo((8, 2)));
        Assert.That(bitmap.GetPixel(1, 1), Is.EqualTo(SKColors.Black));
        using var label = SKBitmap.Decode(Render(new PrinterStorage(),
            $"{download}^XA^FO10,20^IMR:TEST.PNG^FS^XZ"));
        Assert.That(label.GetPixel(10, 20), Is.EqualTo(SKColors.Black));
        Assert.That(label.GetPixel(11, 21), Is.EqualTo(SKColors.Black));
    }

    [Test]
    public void RendersWithTheBundledGraphicsSymbolFont()
    {
        using var bitmap = SKBitmap.Decode(Render(new PrinterStorage(), "^XA^FO10,10^GSN,28,28^FDA^FS^XZ",
            new DrawerOptions { OpaqueBackground = true }));
        Assert.That(HasInk(bitmap, new SKRectI(0, 0, 60, 60)), Is.True);
    }

    [Test]
    public void RejectsInvalidBitonalRows()
    {
        var converter = new SkiaImageConverter();
        Assert.That(() => converter.ConvertImage([0x80], 2), Throws.TypeOf<FormatException>());
        Assert.That(() => converter.ConvertImage([0x80], 0), Throws.TypeOf<FormatException>());
    }

    [Test]
    public void RendersIndependentJobsConcurrently()
    {
        Parallel.For(0, 24, i =>
        {
            int x = 4 + 5 * i;
            using var bitmap = SKBitmap.Decode(Render(new PrinterStorage(), $"^XA^FO{x},12^GFA,1,1,1,80^FS^XZ"));
            Assert.That(bitmap.GetPixel(x, 12), Is.EqualTo(SKColors.Black));
            Assert.That(bitmap.GetPixel(x + 1, 12).Alpha, Is.Zero);
        });
    }

    [Test]
    public void MaintainsOptionalPdfRendering()
    {
        var storage = new PrinterStorage();
        var label = new ZplAnalyzer(storage).Analyze("^XA^FO12,12^GB20,20,2^FS^XZ").LabelInfos.Single();
        var drawer = new ZplElementDrawer(storage, new DrawerOptions { PdfOutput = true });
        byte[] pdf = drawer.DrawPdf(label.ZplElements);
        Assert.That(pdf.Take(5), Is.EqualTo("%PDF-"u8.ToArray()));
    }

    [Test]
    public void RendersRotatedFieldBlocksWithFontQOnSeparateColumns()
    {
        // Font Q is a font 0 variant; the text must fit a single 250 dot line and not spill into the next column.
        const string zpl = """
            ^XA^CI28
            ^FO20,600^FB250,1^AQB,32,32^FDPrinted: 2030.01.01^FS
            ^FO80,600^FB250,1^AQB,32,32^FDSample: ABC 000001^FS
            ^XZ
            """;
        using var bitmap = SKBitmap.Decode(Render(new PrinterStorage(), zpl, new DrawerOptions { OpaqueBackground = true }));
        Assert.That(HasInk(bitmap, new SKRectI(20, 600, 50, 851)), Is.True);
        Assert.That(HasInk(bitmap, new SKRectI(51, 560, 79, 912)), Is.False);
        Assert.That(HasInk(bitmap, new SKRectI(0, 851, 684, 912)), Is.False);
        Assert.That(HasInk(bitmap, new SKRectI(0, 0, 684, 599)), Is.False);
    }

    [Test]
    public void OverprintsFieldBlockLinesBeyondMaximumOnLastLine()
    {
        const string zpl = "^XA^FO20,20^A0N,30,30^FB200,2,0,L,0^FDone two three four five six seven eight nine ten^FS^XZ";
        using var bitmap = SKBitmap.Decode(Render(new PrinterStorage(), zpl, new DrawerOptions { OpaqueBackground = true }));
        Assert.That(HasInk(bitmap, new SKRectI(20, 50, 220, 80)), Is.True);
        Assert.That(HasInk(bitmap, new SKRectI(0, 90, 684, 912)), Is.False);
    }

    [Test]
    public void ScalesFont0VariantsLikeFont0WithBaseCellRatio()
    {
        // Q has a 28x24 cell; magnification rounds half-way values up (60 / 24 = 2.5 -> 3)
        Assert.That(FontScale.GetFontScaling("Q", 32, 32, 12), Is.EqualTo((28f, 24f / 28f)));
        Assert.That(FontScale.GetFontScaling("Q", 60, 60, 12), Is.EqualTo((56f, 72f / 56f)));
    }

    [Test]
    public void IgnoresLabelTopUnlessRequested()
    {
        const string zpl = "^XA^LT47^FO10,10^GB20,20,20^FS^XZ";
        var storage = new PrinterStorage();
        using var ignored = SKBitmap.Decode(Render(storage, zpl));
        Assert.That(ignored.GetPixel(15, 15), Is.EqualTo(SKColors.Black));

        var analyzer = new ZplAnalyzer(storage) { ApplyLabelTop = true };
        var label = analyzer.Analyze(zpl).LabelInfos.Single();
        using var shifted = SKBitmap.Decode(new ZplElementDrawer(storage).Draw(label.ZplElements, 57.1, 76, 12));
        Assert.That(shifted.GetPixel(15, 15).Alpha, Is.Zero);
        Assert.That(shifted.GetPixel(15, 62), Is.EqualTo(SKColors.Black));
    }

    private static byte[] Render(PrinterStorage storage, string zpl, DrawerOptions? options = null)
    {
        var label = new ZplAnalyzer(storage).Analyze(zpl).LabelInfos.Single();
        return new ZplElementDrawer(storage, options).Draw(label.ZplElements, 57.1, 76, 12);
    }

    private static bool HasInk(SKBitmap bitmap, SKRectI area)
    {
        for (int y = area.Top; y < area.Bottom; y++)
        {
            for (int x = area.Left; x < area.Right; x++)
            {
                if (bitmap.GetPixel(x, y).Red < 100)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
