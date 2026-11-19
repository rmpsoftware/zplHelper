using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Label.ImageConverters;
using BinaryKits.Zpl.Viewer.ElementDrawers;
using BinaryKits.Zpl.Viewer.Helpers;

using SkiaSharp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BinaryKits.Zpl.Viewer
{
    public class ZplElementDrawer
    {
        /// <summary>
        /// The array of <see cref="IElementDrawer"/> to draw <see cref="ZplElementBase"/>
        /// </summary>
        public IElementDrawer[] ElementDrawers { get; } = [
                new AztecBarcodeElementDrawer(),
                new Barcode128ElementDrawer(),
                new Barcode39ElementDrawer(),
                new Barcode93ElementDrawer(),
                new BarcodeEAN13ElementDrawer(),
                new BarcodeUpcAElementDrawer(),
                new BarcodeUpcEElementDrawer(),
                new BarcodeUpcExtensionElementDrawer(),
                new DataMatrixElementDrawer(),
                new FieldBlockElementDrawer(),
                new GraphicBoxElementDrawer(),
                new GraphicCircleElementDrawer(),
                new GraphicDiagonalLineElementDrawer(),
                new GraphicEllipseElementDrawer(),
                new GraphicFieldElementDrawer(),
                new GraphicSymbolElementDrawer(),
                new ImageMoveElementDrawer(),
                new Interleaved2of5BarcodeDrawer(),
                new MaxiCodeElementDrawer(),
                new Pdf417ElementDrawer(),
                new QrCodeElementDrawer(),
                new RecallGraphicElementDrawer(),
                new TextFieldElementDrawer(),
                new BarcodeAnsiCodabarElementDrawer(),
            ];

        private static readonly int pdfDpi = 72;
        private static readonly float zplDpi = 203.2f;
        private static readonly float pdfScaleFactor = pdfDpi / zplDpi;

        private readonly DrawerOptions drawerOptions;
        private readonly IPrinterStorage printerStorage;

        public ZplElementDrawer(IPrinterStorage printerStorage, DrawerOptions drawerOptions = null)
        {
            this.drawerOptions = drawerOptions ?? new DrawerOptions();
            this.printerStorage = printerStorage;
        }

        /// <summary>
        /// Draw the label
        /// </summary>
        /// <param name="elements">Zpl elements</param>
        /// <param name="labelWidth">Label width in millimeter</param>
        /// <param name="labelHeight">Label height in millimeter</param>
        /// <param name="printDensityDpmm">Dots per millimeter</param>
        /// <returns></returns>
        public byte[] Draw(
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8)
        {
            return this.DrawMulti(elements, labelWidth, labelHeight, printDensityDpmm)[0];
        }

        /// <summary>
        /// Draw the label as PDF
        /// </summary>
        /// <param name="elements">Zpl elements</param>
        /// <param name="labelWidth">Label width in millimeter</param>
        /// <param name="labelHeight">Label height in millimeter</param>
        /// <param name="printDensityDpmm">Dots per millimeter</param>
        /// <returns></returns>
        public byte[] DrawPdf(
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8)
        {
            return this.DrawMulti(elements, labelWidth, labelHeight, printDensityDpmm)[1];
        }

        /// <summary>
        /// Draw the label on multiple canvases
        /// </summary>
        /// <param name="elements">Zpl elements</param>
        /// <param name="labelWidth">Label width in millimeter</param>
        /// <param name="labelHeight">Label height in millimeter</param>
        /// <param name="printDensityDpmm">Dots per millimeter</param>
        /// <returns></returns>
        public List<byte[]> DrawMulti(
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8)
        {
            List<byte[]> result = [];
            List<SKImage> imageHistory = [];
            int labelImageWidth = Convert.ToInt32(labelWidth * printDensityDpmm);
            int labelImageHeight = Convert.ToInt32(labelHeight * printDensityDpmm);
            SkiaImageConverter.ValidateDimensions(labelImageWidth, labelImageHeight);

            //use SKNWayCanvas to be able to draw on multiple canvases
            using SKNWayCanvas skCanvas = new(labelImageWidth, labelImageHeight);

            //add Bitmap canvas
            SKImageInfo info = new(labelImageWidth, labelImageHeight);
            using SKSurface surface = SKSurface.Create(info)
                ?? throw new InvalidOperationException("SkiaSharp could not create the label surface.");
            skCanvas.AddCanvas(surface.Canvas);

            // The PDF path also needs the raster surface for inverted fields.
            using var pdfStream = this.drawerOptions.PdfOutput ? new MemoryStream() : null;
            using var document = pdfStream == null ? null : SKDocument.CreatePdf(pdfStream);
            if (document != null)
            {
                SKCanvas pdfCanvas = document.BeginPage(
                    (float)(UnitsHelper.ConvertMillimetersToInches(labelWidth) * pdfDpi),
                    (float)(UnitsHelper.ConvertMillimetersToInches(labelHeight) * pdfDpi));
                pdfCanvas.Scale(pdfScaleFactor, pdfScaleFactor);
                skCanvas.AddCanvas(pdfCanvas);
            }

            //make sure to have a transparent canvas for SKBlendMode.Xor to work properly
            skCanvas.Clear(SKColors.Transparent);
            InternationalFont internationalFont = InternationalFont.ZCP850;
            SKPoint currentDefaultPosition = SKPoint.Empty;

            foreach (ZplElementBase element in elements)
            {
                if (element is ZplChangeInternationalFont changeFont)
                {
                    internationalFont = changeFont.InternationalFont;
                    continue;
                }

                IElementDrawer drawer = ElementDrawers.SingleOrDefault(o => o.CanDraw(element));
                if (drawer == null)
                {
                    continue;
                }

                try
                {
                    //The inverse drawing is moved to the element drawer, so only collect imageHistory for the PDF 
                    if ((element is ZplFieldBlock
                         || element is ZplTextField
                         || element is ZplGraphicCircle
                         || element is ZplGraphicBox
                        )
                        && drawer.IsReverseDraw(element)
                        && !drawer.IsWhiteDraw(element)
                        && !drawer.ForceBitmapDraw(element))
                    {
                        //save state before inverted draw
                        if (this.drawerOptions.PdfOutput == true)
                        {
                            imageHistory.Add(surface.Snapshot());
                        }
                    }
                    else if (drawer.IsReverseDraw(element))
                    {
                        //basically only ZplGraphicBox/Circle depending on requirements
                        using SKBitmap skBitmapInvert = new(labelImageWidth, labelImageHeight);
                        using SKCanvas skCanvasInvert = new(skBitmapInvert);
                        skCanvasInvert.Clear(SKColors.Transparent);

                        drawer.Prepare(this.printerStorage, skCanvasInvert);
                        currentDefaultPosition = drawer.Draw(element, this.drawerOptions, currentDefaultPosition, internationalFont, printDensityDpmm);

                        //save state before inverted draw
                        if (this.drawerOptions.PdfOutput == true)
                        {
                            imageHistory.Add(surface.Snapshot());
                        }

                        //use color inversion on an reverse draw white element
                        if (drawer.IsWhiteDraw(element))
                        {
                            InvertDrawWhite(skCanvas, skBitmapInvert);
                        }
                        else
                        {
                            InvertDraw(skCanvas, skBitmapInvert);
                        }

                        continue;
                    }

                    drawer.Prepare(this.printerStorage, skCanvas);
                    currentDefaultPosition = drawer.Draw(element, this.drawerOptions, currentDefaultPosition, internationalFont, printDensityDpmm);

                    continue;
                }
                catch (Exception ex)
                {
                    if (element is ZplBarcode barcodeElement)
                    {
                        throw new Exception($"Error on zpl element \"{barcodeElement.Content}\": {ex.Message}", ex);
                    }
                    else if (element is ZplDataMatrix dataMatrixElement)
                    {
                        throw new Exception($"Error on zpl element \"{dataMatrixElement.Content}\": {ex.Message}", ex);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            using SKImage image = surface.Snapshot();
            if (this.drawerOptions.OpaqueBackground)
            {
                using SKSurface whiteSurface = SKSurface.Create(info);
                whiteSurface.Canvas.Clear(SKColors.White);
                whiteSurface.Canvas.DrawImage(image, 0f, 0f, SKSamplingOptions.Default);
                using SKImage compositedImage = whiteSurface.Snapshot();
                result.Add(this.EncodeImage(compositedImage));
            }
            else
            {
                result.Add(this.EncodeImage(image));
            }

            if (document == null)
            {
                result.Add(null);
                return result;
            }

            //Fix the PDF blend
            FixPdfInvertDraw(info, imageHistory, surface, skCanvas);

            //close the PDF document
            document.EndPage();
            document.Close();

            result.Add(pdfStream.ToArray());
            return result;
        }

        private byte[] EncodeImage(SKImage image)
        {
            using SKData imageData = image.Encode(this.drawerOptions.RenderFormat, this.drawerOptions.RenderQuality)
                ?? throw new InvalidOperationException("SkiaSharp could not encode the label.");
            return imageData.ToArray();
        }

        /// <summary>
        /// Draw the label on the provided surface
        /// </summary>
        /// <param name="surface">Skia Surface</param>
        /// <param name="elements">Zpl elements</param>
        /// <param name="labelWidth">Label width in millimeter</param>
        /// <param name="labelHeight">Label height in millimeter</param>
        /// <param name="printDensityDpmm">Dots per millimeter</param>
        /// <returns></returns>
        public void DrawSurface(SKSurface surface,
            IEnumerable<ZplElementBase> elements,
            double labelWidth = 101.6,
            double labelHeight = 152.4,
            int printDensityDpmm = 8)
        {
            List<byte[]> result = [];
            List<SKImage> imageHistory = [];
            int labelImageWidth = Convert.ToInt32(labelWidth * printDensityDpmm);
            int labelImageHeight = Convert.ToInt32(labelHeight * printDensityDpmm);
            SkiaImageConverter.ValidateDimensions(labelImageWidth, labelImageHeight);

            SKCanvas skCanvas = surface.Canvas;
            //This has an issue with AvaloniaUI making the window transparent. 
            skCanvas.Clear(SKColors.Transparent);
            InternationalFont internationalFont = InternationalFont.ZCP850;
            SKPoint currentDefaultPosition = SKPoint.Empty;

            foreach (ZplElementBase element in elements)
            {
                if (element is ZplChangeInternationalFont changeFont)
                {
                    internationalFont = changeFont.InternationalFont;
                    continue;
                }

                IElementDrawer drawer = ElementDrawers.SingleOrDefault(o => o.CanDraw(element));
                if (drawer == null)
                {
                    continue;
                }

                try
                {
                    if (drawer.IsReverseDraw(element))
                    {
                        //basically only ZplGraphicBox/Circle depending on requirements
                        using SKBitmap skBitmapInvert = new(labelImageWidth, labelImageHeight);
                        using SKCanvas skCanvasInvert = new(skBitmapInvert);
                        skCanvasInvert.Clear(SKColors.Transparent);

                        drawer.Prepare(this.printerStorage, skCanvasInvert);
                        currentDefaultPosition = drawer.Draw(element, this.drawerOptions, currentDefaultPosition, internationalFont, printDensityDpmm);

                        //use color inversion on an reverse draw white element
                        if (drawer.IsWhiteDraw(element))
                        {
                            InvertDrawWhite(skCanvas, skBitmapInvert);
                        }
                        else
                        {
                            InvertDraw(skCanvas, skBitmapInvert);
                        }

                        continue;
                    }

                    drawer.Prepare(this.printerStorage, skCanvas);
                    currentDefaultPosition = drawer.Draw(element, this.drawerOptions, currentDefaultPosition, internationalFont, printDensityDpmm);

                    continue;
                }
                catch (Exception ex)
                {
                    if (element is ZplBarcode barcodeElement)
                    {
                        throw new Exception($"Error on zpl element \"{barcodeElement.Content}\": {ex.Message}", ex);
                    }
                    else if (element is ZplDataMatrix dataMatrixElement)
                    {
                        throw new Exception($"Error on zpl element \"{dataMatrixElement.Content}\": {ex.Message}", ex);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        /**
         * PDF transparency and SKBlendMode are not very good friends, SKBlendMode.Xor behaves as SKBlendMode.SrcOver.
         * 
         * This function extracts all the pixels that are removed in the draw process
         * Then that is used to make a white image as overlay in the PDF to get the same effect as SKBlendMode.Xor
         */
        private static void FixPdfInvertDraw(SKImageInfo info, List<SKImage> imageHistory, SKSurface surface, SKCanvas skCanvas)
        {
            //fix inverted colors
            using SKSurface surfacePdfInvertColorFix = SKSurface.Create(info);
            using SKCanvas skImageCanvasPdfInvertColorFix = surfacePdfInvertColorFix.Canvas;
            skImageCanvasPdfInvertColorFix.Clear(SKColors.Transparent);

            //make an image of everything that was once colored
            foreach (SKImage imageHistoryState in imageHistory)
            {
                using SKPaint pdfPaint = new()
                {
                    BlendMode = SKBlendMode.SrcOver
                };
                skImageCanvasPdfInvertColorFix.DrawImage(imageHistoryState, 0f, 0f, SKSamplingOptions.Default, pdfPaint);
            }

            //subtract the parts that are transparent in the final image
            using SKImage finalSurfaceImage = surface.Snapshot();
            using SKPaint pdfFinalPaint = new()
            {
                BlendMode = SKBlendMode.DstOut
            };
            skImageCanvasPdfInvertColorFix.DrawImage(finalSurfaceImage, 0f, 0f, SKSamplingOptions.Default, pdfFinalPaint);

            //now invert the colors of the pixels that should be white place it on the canvas
            using SKImage pdfTransparentPartsImage = surfacePdfInvertColorFix.Snapshot();
            using SKBitmap pdfTransparentPartsBitmap = SKBitmap.FromImage(pdfTransparentPartsImage);
            using SKPaint pdfFinalPaintInverted = new();
            float[] inverter = [
                -1f,  0f,  0f, 0f, 1f,
                0f, -1f,  0f, 0f, 1f,
                0f,  0f, -1f, 0f, 1f,
                0f,  0f,  0f, 1f, 0f
            ];
            pdfFinalPaintInverted.ColorFilter = SKColorFilter.CreateColorMatrix(inverter);
            pdfFinalPaintInverted.BlendMode = SKBlendMode.SrcOver;
            skCanvas.DrawBitmap(pdfTransparentPartsBitmap, 0, 0, SKSamplingOptions.Default, pdfFinalPaintInverted);
        }

        private static void InvertDraw(SKCanvas baseCanvas, SKBitmap bmToInvert)
        {
            using (SKPaint paint = new())
            {
                paint.BlendMode = SKBlendMode.Xor;
                baseCanvas.DrawBitmap(bmToInvert, 0, 0, SKSamplingOptions.Default, paint);
            }
        }

        private static void InvertDrawWhite(SKCanvas baseCanvas, SKBitmap bmToInvert)
        {
            using (SKPaint paint = new())
            {
                float[] inverter = [
                    -1f,  0f,  0f, 0f, 1f,
                    0f, -1f,  0f, 0f, 1f,
                    0f,  0f, -1f, 0f, 1f,
                    0f,  0f,  0f, 1f, 0f
                ];
                paint.ColorFilter = SKColorFilter.CreateColorMatrix(inverter);
                paint.BlendMode = SKBlendMode.Xor;
                baseCanvas.DrawBitmap(bmToInvert, 0, 0, SKSamplingOptions.Default, paint);
            }
        }
    }
}
