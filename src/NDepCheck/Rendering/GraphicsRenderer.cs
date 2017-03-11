using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using JetBrains.Annotations;

namespace NDepCheck.Rendering {
    public interface IBox {
        Vector Center { get; }
        Vector HalfDiagonal { get; }
        Vector GetBestAnchor(Vector farAway);
    }

    public interface IArrow {
        Vector Head { get; }
        Vector Tail { get; }
    }

    public abstract class GraphicsRenderer<TItem, TDependency> : IRenderer<TItem, TDependency> 
            where TItem : class, INode 
            where TDependency : class, IEdge {
        public static Vector C(double x, double y) {
            return Vector.Fixed(x, y);
        }

        public static Vector V(string name, double? x, double? y) {
            return Vector.Variable(name, x, y);
        }

        public enum TextPlacing {
            Left, Center, Right, LeftUp, CenterUp, RightUp, LeftDown, CenterDown, RightDown
        }

        private interface IBuilder {
            IEnumerable<Vector> GetBoundingVectors();
            void BeforeDrawing(Graphics graphics);
            void Draw(Graphics graphics, StringBuilder htmlForTooltips);
        }

        private class BoxBuilder : IBuilder, IBox {
            private readonly Vector _center;
            private readonly Vector _halfDiagonal;
            private readonly double _borderWidth;
            private readonly string _text;
            private readonly TextPlacing _placing;
            private readonly string _tooltip;
            private readonly Color _textColor;
            private readonly Font _textFont;
            private readonly Color _borderColor;
            private readonly int _anchorNr;
            private readonly Color _color;

            public BoxBuilder(Vector center, Vector halfDiagonal, Color color, double borderWidth, Color borderColor,
                int anchorNr, string text, TextPlacing placing, Font textFont, Color textColor, string tooltip) {
                _center = center;
                _halfDiagonal = halfDiagonal;
                _color = color;
                _borderWidth = borderWidth;
                _text = text;
                _placing = placing;
                _tooltip = tooltip;
                _textColor = textColor;
                _textFont = textFont;
                _borderColor = borderColor;
                _anchorNr = anchorNr;
            }

            public Vector Center => _center;
            public Vector HalfDiagonal => _halfDiagonal;

            public void Draw(Graphics graphics, StringBuilder htmlForTooltips) {
                Vector leftUpper = _center - ~_halfDiagonal;

                Vector diagonal = 2 * _halfDiagonal;
                FillBox(graphics, new SolidBrush(_borderColor), leftUpper.GetX(), -leftUpper.GetY(), diagonal.GetX(),
                    diagonal.GetY());

                Vector borderDiagonal = C(_borderWidth, _borderWidth);
                Vector leftUpperInner = leftUpper + ~borderDiagonal;
                Vector diagonalInner = diagonal - 2 * borderDiagonal;
                FillBox(graphics, new SolidBrush(_color), leftUpperInner.GetX(), -leftUpperInner.GetY(),
                    diagonalInner.GetX(), diagonalInner.GetY());

                DrawText(graphics, _text, _textFont, _textColor, _center, _placing);

                // Get all these elements somehow and then do a "draw tooltip" ...
            }

            private void FillBox(Graphics graphics, SolidBrush b, float x, float y, float width, float height) {
                //Console.WriteLine($"FillBox({x},{y},{width},{height})");
                graphics.FillRectangle(b, x, y, width, height);
            }

            public IEnumerable<Vector> GetBoundingVectors() {
                yield return _center + _halfDiagonal;
                yield return _center - _halfDiagonal;
            }

            /// <summary>
            ///  Assert angle is in [0, 2*pi)
            /// </summary>
            /// <param name="a"></param>
            /// <returns></returns>
            private static double NormalizedAngle(double a) {
                const double twoPI = 2 * Math.PI;
                return a - Math.Floor(a / twoPI) * twoPI;
            }

            public Vector GetBestAnchor(Vector farAway) {
                double sectorAngle = 2 * Math.PI / _anchorNr;
                Func<Vector> findNearestAnchor = () => {
                    var d = farAway - _center;
                    double angle = Math.Atan2(d.GetY(), d.GetX());
                    double roundedAngle =
                        NormalizedAngle(Math.Round(angle / sectorAngle) * sectorAngle);
                    double diagX = _halfDiagonal.GetX();
                    double diagY = _halfDiagonal.GetY();
                    double diagonalAngle = NormalizedAngle(Math.Atan2(diagY, diagX));
                    double x, y;
                    if (roundedAngle < diagonalAngle) {
                        x = diagX;
                        y = x * Math.Tan(roundedAngle);
                    } else if (roundedAngle < Math.PI - diagonalAngle) {
                        y = diagY;
                        x = y * Math.Tan(Math.PI / 2 - roundedAngle);
                    } else if (roundedAngle < Math.PI + diagonalAngle) {
                        x = -diagX;
                        y = x * Math.Tan(roundedAngle);
                    } else if (roundedAngle < 2 * Math.PI - diagonalAngle) {
                        y = -diagY;
                        x = y * Math.Tan(Math.PI / 2 - roundedAngle);
                    } else {
                        x = diagX;
                        y = x * Math.Tan(roundedAngle);
                    }
                    return _center + C(x, y);
                };
                return new DependentVector(() => findNearestAnchor().GetX(), () => findNearestAnchor().GetY());
            }

            public void BeforeDrawing(Graphics graphics) {
                var hd = _halfDiagonal as VariableVector;
                if (hd != null) {
                    bool hasX = hd.X().HasValue;
                    bool hasY = hd.Y().HasValue;
                    if (!hasX || !hasY) {
                        // Compute missing parts from text and font
                        SizeF size = graphics.MeasureString(_text, _textFont);
                        if (!hasX) {
                            // 20% padding by default
                            hd.SetX(size.Width / 2 * 1.2);
                        }
                        if (!hasY) {
                            // 20% padding by default
                            hd.SetY(size.Height / 2 * 1.2);
                        }
                    }
                }
            }
        }

        private class ArrowBuilder : IBuilder, IArrow {
            private readonly Vector _head;
            private readonly Vector _tail;
            private readonly double _width;
            private readonly Color _color;
            private readonly string _text;
            private readonly TextPlacing _placing;
            private readonly Font _textFont;
            private readonly Color _textColor;
            private readonly double _textLocation;
            private readonly string _tooltip;

            internal ArrowBuilder(Vector tail, Vector head, double width, Color color,
                        string text, TextPlacing placing, Font textFont, Color textColor, double textLocation,
                        string tooltip) {
                _tail = tail;
                _head = head;
                _width = width;
                _color = color;
                _text = text;
                _placing = placing;
                _textFont = textFont;
                _textColor = textColor;
                _textLocation = textLocation;
                _tooltip = tooltip;
            }

            public Vector Tail => _tail;

            public Vector Head => _head;

            public IEnumerable<Vector> GetBoundingVectors() {
                yield return _tail;
                yield return _head;
            }

            public void Draw(Graphics graphics, StringBuilder htmlForTooltips) {
                float fWidth = (float)_width;

                if (_tail.AsPointF() != _head.AsPointF()) {
                    float absoluteArrowSize = Math.Min(10 * fWidth, (float)_head.To(_tail) / 4);
                    var pen = new Pen(_color, fWidth) {
                        StartCap = LineCap.RoundAnchor,
                        // arrowsize is relative to line width
                        CustomEndCap = new AdjustableArrowCap(absoluteArrowSize / fWidth, absoluteArrowSize / fWidth, isFilled: false)
                    };
                    graphics.DrawLine(pen, (~_tail).AsPointF(), (~_head).AsPointF());
                } else {
                    graphics.FillEllipse(new SolidBrush(_color), _head.GetX() - fWidth / 2, -_head.GetY() - fWidth / 2, fWidth, fWidth);
                }
                DrawText(graphics, _text, _textFont, _textColor, _tail * (1 - _textLocation) + _head * _textLocation, _placing);

                // TODO: Get all these elements somehow and then do a "draw tooltip" ...
            }

            public void BeforeDrawing(Graphics graphics) {
                // empty
            }
        }

        private readonly List<IBuilder> _builders = new List<IBuilder>();

        protected class Store<TKey, TValue> {
            private readonly Dictionary<TKey, TValue> _dict = new Dictionary<TKey, TValue>();
            public TValue Put(TKey key, TValue value) {
                _dict[key] = value;
                return value;
            }
            public TValue Get(TKey key) {
                return _dict[key];
            }
        }

        protected readonly Store<Item, IBox> ItemBoxes = new Store<Item, IBox>();
        protected readonly Store<Dependency, IArrow> DependencyArrows = new Store<Dependency, IArrow>();

        private static void DrawText(Graphics graphics, string text, Font textFont, Color textColor, Vector center, TextPlacing textPlacing) {
            //graphics.FillEllipse(new SolidBrush(Color.Red), center.GetX() - 3, -center.GetY() - 3, 6, 6);
            graphics.DrawString(text, textFont, new SolidBrush(textColor), (~center - C(0, textFont.GetHeight() / 2)).AsPointF(),
                new StringFormat(GetDirection(textPlacing)) { Alignment = GetStringAlignment(textPlacing), });
        }

        private static StringAlignment GetStringAlignment(TextPlacing p) {
            switch (p) {
                case TextPlacing.Left:
                case TextPlacing.LeftUp:
                case TextPlacing.LeftDown:
                    return StringAlignment.Near;
                case TextPlacing.Center:
                case TextPlacing.CenterUp:
                case TextPlacing.CenterDown:
                    return StringAlignment.Center;
                case TextPlacing.Right:
                case TextPlacing.RightUp:
                case TextPlacing.RightDown:
                    return StringAlignment.Far;
                default:
                    throw new ArgumentOutOfRangeException(nameof(p), p, null);
            }
        }

        private static StringFormatFlags GetDirection(TextPlacing p) {
            switch (p) {
                case TextPlacing.Left:
                case TextPlacing.Center:
                case TextPlacing.Right:
                    return 0;
                case TextPlacing.LeftUp:
                case TextPlacing.CenterUp:
                case TextPlacing.RightUp:
                case TextPlacing.LeftDown:
                case TextPlacing.CenterDown:
                case TextPlacing.RightDown:
                    return StringFormatFlags.DirectionVertical; // opder Transformation????????????
                default:
                    throw new ArgumentOutOfRangeException(nameof(p), p, null);
            }
        }

        public IBox Box([NotNull] Vector center, [CanBeNull] Vector halfDiagonal, [CanBeNull] string text,
            [CanBeNull] Color? color = null /*White*/, int anchorNr = 8,
            double borderWidth = 0, [CanBeNull] Color? borderColor = null /*Black*/,
            TextPlacing placing = TextPlacing.Center, [CanBeNull] Font textFont = null /*___*/, [CanBeNull] Color? textColor = null /*Black*/,
            [CanBeNull] string tooltip = null) {
            if (center == null) {
                throw new ArgumentNullException(nameof(center));
            }
            var boxBuilder = new BoxBuilder(center, halfDiagonal ?? new VariableVector(text ?? center.ToString()), color ?? Color.White,
                borderWidth, borderColor ?? Color.Black, anchorNr,
                text ?? "", placing, textFont ?? new Font(FontFamily.GenericSansSerif, 10), textColor ?? Color.Black, tooltip ?? "");
            _builders.Add(boxBuilder);
            return boxBuilder;
        }

        public IArrow Arrow([NotNull] Vector tail, [NotNull] Vector head, double width, [CanBeNull] Color? color = null /*Black*/,
            [CanBeNull] string text = null, TextPlacing placing = TextPlacing.Center, [CanBeNull] Font textFont = null /*___*/,
            [CanBeNull] Color? textColor = null /*Black*/, double textLocation = 0.5, [CanBeNull] string tooltip = null) {
            if (tail == null) {
                throw new ArgumentNullException(nameof(tail));
            }
            if (head == null) {
                throw new ArgumentNullException(nameof(head));
            }
            var arrowBuilder = new ArrowBuilder(tail, head, width, color ?? Color.Black,
                text ?? "", placing, textFont ?? new Font(FontFamily.GenericSansSerif, 10), textColor ?? Color.Black,
                textLocation, tooltip);
            _builders.Add(arrowBuilder);
            return arrowBuilder;
        }

        private Bitmap Render(IEnumerable<TItem> items, IEnumerable<TDependency> dependencies, Size size) {
            PlaceObjects(items, dependencies);

            // I tried it with SVG - but SVG support in .Net seems to be non-existent.
            // The library at https://github.com/managed-commons/SvgNet is a nice attempet (a 2015 resurrection of a 2003 attempt),
            // but it closes off the SVG objects in such a way that adding tooltips ("mouse hoverings") seems very hard.
            // If someone knows more about SVG than I (who doesn't know a bit ...), feel free to try it with SVG!

            var bitmap = new Bitmap(size.Width, size.Height);
            using (Graphics graphics = Graphics.FromImage(bitmap)) {
                graphics.Clear(GetBackGroundColor);

                double minX = double.MaxValue;
                double maxX = -double.MaxValue;
                double minY = double.MaxValue;
                double maxY = -double.MaxValue;

                StringBuilder errors = new StringBuilder();
                foreach (var b in _builders) {
                    b.BeforeDrawing(graphics);

                    foreach (var v in b.GetBoundingVectors()) {
                        var vv = v as VariableVector;

                        double? x = v.X();
                        if (!x.HasValue) {
                            errors.AppendLine("No x value set in vector " + (vv?.Name ?? "dependent on other vectors"));
                        } else {
                            minX = Math.Min(minX, x.Value);
                            maxX = Math.Max(maxX, x.Value);
                        }
                        double? y = v.Y();
                        if (!y.HasValue) {
                            errors.AppendLine("No y value set in vector " + (vv?.Name ?? "dependent on other vectors"));
                        } else {
                            minY = Math.Min(minY, y.Value);
                            maxY = Math.Max(maxY, y.Value);
                        }
                    }
                }
                if (errors.Length > 0) {
                    throw new InvalidOperationException(errors.ToString());
                }

                StringBuilder htmlForTooltips = new StringBuilder();

                // 5% margin on all sides
                float BORDER = 0.1f;
                double scaleX = size.Width * (1 - 2 * BORDER) / (maxX - minX);
                double scaleY = size.Height * (1 - 2 * BORDER) / (maxY - minY);
                float scale = (float) Math.Min(scaleX, scaleY); // No distortion!

                graphics.Transform = new Matrix(scale, 0, 0, scale, (float) (-scale * minX + size.Width * BORDER),
                    (float) (scale * maxY + size.Height * BORDER));

                foreach (var b in _builders) {
                    b.Draw(graphics, htmlForTooltips);
                }
            }

            //var f = new Font(FontFamily.GenericSansSerif, 10);
            //DrawText(graphics, "0|0", f, Color.Blue, C(0, 0), TextPlacing.Center);
            //DrawText(graphics, "C|0", f, Color.Blue, C(100, 0), TextPlacing.Center);
            //DrawText(graphics, "O|C", f, Color.Blue, C(0, 100), TextPlacing.Center);
            //DrawText(graphics, "C|C", f, Color.Blue, C(100, 100), TextPlacing.Center);
            Bitmap bitMap = bitmap;
            return bitMap;
        }

        public void RenderToFile(IEnumerable<TItem> items, IEnumerable<TDependency> dependencies, string baseFilename, int? optionsStringLength) {

            Size size = GetSize();
            Bitmap bitMap = Render(items, dependencies, size);

            string gifFilename = Path.ChangeExtension(baseFilename, ".gif");
            bitMap.Save(gifFilename, ImageFormat.Gif);
            using (var tw = new StreamWriter(Path.ChangeExtension(baseFilename, ".html"))) {
                tw.WriteLine($@"
<html>
<body>
<img src = ""{ Path.GetFileName(gifFilename)}"" width = ""{size.Width}"" height = ""{size.Height}"" usemap = ""#map"" alt = ""Webdesign Group"">
</ body>
</ html>
"); // ______{htmlForTooltips}___
            }


            //< area shape = "poly" coords = "x1,y1,x2,y2,..,xn,yn" title = ".." >< area shape = "poly" coords = "2,5,32,1,33,22,51,36,33,57" title = "The Americas" >< area shape = "poly" coords = "57,14,70,2,111,3,114,23,97,34" title = "Eurasia" >< area shape = "poly" coords = "57,14,86,29,73,52,66,49,50,28" title = "Africa" >< area shape = "poly" coords = "105,40,108,49,122,52,127,41,117,34" title = "Australia" >

        }

        public void RenderToStream(IEnumerable<TItem> items, IEnumerable<TDependency> dependencies, Stream stream, int? optionsStringLength) {
            Size size = GetSize();
            Bitmap bitMap = Render(items, dependencies, size);

            bitMap.Save(stream, ImageFormat.Gif);
        }

        protected abstract Size GetSize();

        protected virtual Color GetBackGroundColor => Color.White;

        protected abstract void PlaceObjects(IEnumerable<TItem> items, IEnumerable<TDependency> dependencies);
    }

    public abstract class GraphicsDependencyRenderer : GraphicsRenderer<Item, Dependency>, IDependencyRenderer { }
}
