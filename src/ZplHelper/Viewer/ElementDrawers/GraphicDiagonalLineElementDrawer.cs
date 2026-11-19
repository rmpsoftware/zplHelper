using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;

using SkiaSharp;

namespace BinaryKits.Zpl.Viewer.ElementDrawers
{
    public class GraphicDiagonalLineElementDrawer : ElementDrawerBase
    {
        ///<inheritdoc/>
        public override bool CanDraw(ZplElementBase element)
        {
            return element is ZplGraphicDiagonalLine;
        }

        public override bool IsReverseDraw(ZplElementBase element)
        {
            if (element is ZplGraphicDiagonalLine graphicLine)
            {
                return graphicLine.ReversePrint;
            }

            return false;
        }

        public override bool IsWhiteDraw(ZplElementBase element)
        {
            if (element is ZplGraphicDiagonalLine graphicLine)
            {
                return graphicLine.LineColor == LineColor.White;
            }

            return false;
        }

        ///<inheritdoc/>
        public override SKPoint Draw(ZplElementBase element, DrawerOptions options, SKPoint currentPosition, InternationalFont internationalFont)
        {
            if (element is ZplGraphicDiagonalLine graphicLine)
            {
                int border = graphicLine.BorderThickness;
                int width = graphicLine.Width;
                int height = graphicLine.Height;

                float x = graphicLine.PositionX;
                float y = graphicLine.PositionY;

                if (graphicLine.UseDefaultPosition)
                {
                    x = currentPosition.X;
                    y = currentPosition.Y;
                }

                if (graphicLine.FieldTypeset != null)
                {
                    y -= height;
                    if (y < 0)
                    {
                        y = 0;
                    }
                }

                SKPoint skPointLL = new(x, y + height);
                SKPoint skPointUL = new(x, y);

                using SKPaint skPaint = new()
                {
                    IsAntialias = options.Antialias,
                    Style = SKPaintStyle.Fill,
                    Color = SKColors.Black
                };

                if (graphicLine.LineColor == LineColor.White)
                {
                    skPaint.Color = SKColors.White;
                }

                if (graphicLine.ReversePrint)
                {
                    skPaint.BlendMode = SKBlendMode.Xor;
                }

                if(graphicLine.RightLeaningDiagonal)
                {
                    using SKPathBuilder builder = new();
                    builder.MoveTo(skPointLL);
                    builder.RLineTo(border, 0);
                    builder.RLineTo(width, -height);
                    builder.RLineTo(-border, 0);
                    builder.Close();
                    using SKPath path = builder.Detach();
                    this.skCanvas.DrawPath(path, skPaint);
                }
                else
                {
                    using SKPathBuilder builder = new();
                    builder.MoveTo(skPointUL);
                    builder.RLineTo(border, 0);
                    builder.RLineTo(width, height);
                    builder.RLineTo(-border, 0);
                    builder.Close();
                    using SKPath path = builder.Detach();
                    this.skCanvas.DrawPath(path, skPaint);
                }

                // Calculate next position based on box dimensions
                return this.CalculateNextDefaultPosition(x, y, width, height, graphicLine.FieldOrigin != null, FieldOrientation.Normal, currentPosition);
            }

            return currentPosition;
        }
    }
}
