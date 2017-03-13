using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace NDepCheck.Rendering {
    public interface IBox {
        Vector Center { get; }

        Vector LowerLeft { get; }
        Vector CenterLeft { get; }
        Vector UpperLeft { get; }
        Vector CenterTop { get; }
        Vector UpperRight { get; }
        Vector CenterRight { get; }
        Vector LowerRight { get; }
        Vector CenterBottom { get; }

        BoundedVector Diagonal { get; }
        BoundedVector TextBox { get; }

        Vector GetBestConnector(Vector farAway);
    }

    public interface IArrow {
        Vector Head { get; }
        Vector Tail { get; }
    }

    public enum BoxTextPlacement {
        Left, Center, Right, LeftUp, CenterUp, RightUp, LeftDown, CenterDown, RightDown
    }

    public enum BoxAnchoring {
        Center, LowerLeft, CenterLeft, UpperLeft, CenterTop, UpperRight, CenterRight, LowerRight, CenterBottom
    }

    public enum LineTextPlacement {
        Left, Center, Right, LeftInclined, CenterInclined, RightInclined
    }

    public abstract class GraphicsRenderer<TItem, TDependency> : IRenderer<TItem, TDependency>
            where TItem : class, INode
            where TDependency : class, IEdge {
        private static readonly bool DEBUG = false;

        public static Vector F(double? x, double? y, string name = null) {
            return Vector.Fixed(x, y, name);
        }

        public static BoundedVector B(string name, double interpolateMinMax = 0.0) {
            return Vector.Bounded(name, interpolateMinMax);
        }

        private interface IBuilder {
            IEnumerable<Vector> GetBoundingVectors();
            void FullyRestrictBoundingVectors(Graphics graphics);
            void Draw(Graphics graphics, StringBuilder htmlForTooltips);
        }

        private class BoxBuilder : IBuilder, IBox {
            private readonly Vector _center;
            [NotNull]
            private readonly BoundedVector _diagonal;
            private readonly BoundedVector _textBox;
            private readonly double _borderWidth;
            private readonly string _text;
            private readonly BoxTextPlacement _boxTextPlacement;
            private readonly string _tooltip;
            private readonly Color _textColor;
            private readonly double _textPadding;
            private readonly Font _textFont;
            private readonly Color _borderColor;
            private readonly int _connectors;
            private readonly Color _color;

            public BoxBuilder(Vector center, BoundedVector diagonal, Color color,
                              double borderWidth, Color borderColor, int connectors, string text,
                              BoxTextPlacement boxTextPlacement, Font textFont, Color textColor, double textPadding, string tooltip) {
                _center = center;
                _textBox = new BoundedVector($"${_center.Name}['{text}']");
                _diagonal = diagonal.Restrict(_textBox);
                _color = color;
                _borderWidth = borderWidth;
                _text = text;
                _boxTextPlacement = boxTextPlacement;
                _tooltip = tooltip;
                _textColor = textColor;
                _textPadding = textPadding;
                _textFont = textFont;
                _borderColor = borderColor;
                _connectors = connectors;
            }

            public Vector Center => _center;
            public Vector LowerLeft => _center - _diagonal / 2;
            public Vector CenterLeft => _center - _diagonal.Horizontal() / 2;
            public Vector UpperLeft => _center - ~_diagonal / 2;
            public Vector CenterTop => _center + _diagonal.Vertical() / 2;
            public Vector UpperRight => _center + _diagonal / 2;
            public Vector CenterRight => _center + _diagonal.Horizontal() / 2;
            public Vector LowerRight => _center + ~_diagonal / 2;
            public Vector CenterBottom => _center + (~_diagonal).Vertical() / 2;

            public BoundedVector Diagonal => _diagonal;

            public BoundedVector TextBox => _textBox;

            public void FullyRestrictBoundingVectors(Graphics graphics) {
                // Textbox is set here, because it is needed for restricting the Diagonal.

                SizeF size = graphics.MeasureString(_text, _textFont);
                switch (_boxTextPlacement) {
                    case BoxTextPlacement.Left:
                    case BoxTextPlacement.Center:
                    case BoxTextPlacement.Right:
                        break;
                    case BoxTextPlacement.LeftUp:
                    case BoxTextPlacement.CenterUp:
                    case BoxTextPlacement.RightUp:
                    case BoxTextPlacement.LeftDown:
                    case BoxTextPlacement.CenterDown:
                    case BoxTextPlacement.RightDown:
                        // Flip size for vertical text
                        size = new SizeF(size.Height, size.Width);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                _textBox.Set(Vector.Fixed(size.Width * (1 + _textPadding) + 2 * _borderWidth, size.Height * (1 + _textPadding) + 2 * _borderWidth, "|" + _text + "|"));
            }

            public void Draw(Graphics graphics, StringBuilder htmlForTooltips) {
                Vector leftUpper = _center - ~_diagonal / 2;

                FillBox(graphics, new SolidBrush(_borderColor), leftUpper.GetX(), -leftUpper.GetY(), _diagonal.GetX(), _diagonal.GetY());

                Vector borderDiagonal = F(_borderWidth, _borderWidth);
                Vector leftUpperInner = leftUpper + ~borderDiagonal;
                Vector diagonalInner = _diagonal - 2 * borderDiagonal;
                FillBox(graphics, new SolidBrush(_color), leftUpperInner.GetX(), -leftUpperInner.GetY(), diagonalInner.GetX(), diagonalInner.GetY());

                Matrix m = new Matrix();
                switch (_boxTextPlacement) {
                    case BoxTextPlacement.Left:
                        m.Translate(-(_diagonal - _textBox).GetX() / 2, 0);
                        break;
                    case BoxTextPlacement.Center:
                        break;
                    case BoxTextPlacement.Right:
                        m.Translate((_diagonal - _textBox).GetX() / 2, 0);
                        break;
                    case BoxTextPlacement.LeftUp:
                        m.Translate(0, -(_diagonal - _textBox).AsMirroredPointF().Y / 2);
                        m.RotateAt(-90, _center.AsMirroredPointF());
                        break;
                    case BoxTextPlacement.CenterUp:
                        m.RotateAt(-90, _center.AsMirroredPointF());
                        break;
                    case BoxTextPlacement.RightUp:
                        m.Translate(0, (_diagonal - _textBox).AsMirroredPointF().Y / 2);
                        m.RotateAt(-90, _center.AsMirroredPointF());
                        break;
                    case BoxTextPlacement.LeftDown:
                        m.Translate(0, (_diagonal - _textBox).AsMirroredPointF().Y / 2);
                        m.RotateAt(90, _center.AsMirroredPointF());
                        break;
                    case BoxTextPlacement.CenterDown:
                        m.RotateAt(90, _center.AsMirroredPointF());
                        break;
                    case BoxTextPlacement.RightDown:
                        m.Translate(0, -(_diagonal - _textBox).AsMirroredPointF().Y / 2);
                        m.RotateAt(90, _center.AsMirroredPointF());
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(_boxTextPlacement), _boxTextPlacement, null);
                }

                DrawText(graphics, _text, _textFont, _textColor, _center, m);

                // Get all these elements somehow and then do a "draw tooltip" ...
            }

            private void FillBox(Graphics graphics, SolidBrush b, float x, float y, float width, float height) {
                //Console.WriteLine($"FillBox({x},{y},{width},{height})");
                graphics.FillRectangle(b, x, y, width, height);
            }

            public IEnumerable<Vector> GetBoundingVectors() {
                yield return _center + _diagonal / 2;
                yield return _center - _diagonal / 2;
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

            public Vector GetBestConnector(Vector farAway) {
                // Current algorithm: There are 360°/_connectors equal-sized sectors;
                // the "best connector" is the intersection of a sector center line
                // nearest to the line from center to farAway.
                // Other ideas: 
                // - Divide the circumference of the box into equal-sized lengths.
                // - Like before, but with guaranteed connectors at corners.
                // - Like before, but with additional guaranteed connectors at edge midpoints.

                double sectorAngle = 2 * Math.PI / _connectors;
                Func<Vector> findNearestConnector = () => {
                    var d = farAway - _center;
                    double angle = Math.Atan2(d.GetY(), d.GetX());
                    double roundedAngle =
                        NormalizedAngle(Math.Round(angle / sectorAngle) * sectorAngle);
                    double diagX = _diagonal.GetX() / 2;
                    double diagY = _diagonal.GetY() / 2;
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
                    return _center + F(x, y);
                };
                return new DependentVector(() => findNearestConnector().GetX(), () => findNearestConnector().GetY(), farAway.Name + ".NC()");
            }
        }

        private class ArrowBuilder : IBuilder, IArrow {
            private readonly Vector _head;
            private readonly Vector _tail;
            private readonly double _width;
            private readonly Color _color;
            private readonly string _text;
            private readonly BoundedVector _textBox;
            private readonly LineTextPlacement _lineTextPlacement;
            private readonly Font _textFont;
            private readonly Color _textColor;
            private readonly float _textPadding;
            private readonly double _textLocation;
            private readonly string _tooltip;

            internal ArrowBuilder(Vector tail, Vector head, double width, Color color,
                        string text, LineTextPlacement lineTextPlacement, Font textFont, Color textColor, double textPadding, double textLocation,
                        string tooltip) {
                _tail = tail;
                _head = head;
                _width = width;
                _color = color;
                _text = text;
                _textBox = new BoundedVector($"${tail.Name}->{head.Name}['{text}']");
                _lineTextPlacement = lineTextPlacement;
                _textFont = textFont;
                _textColor = textColor;
                _textPadding = (float)textPadding;
                _textLocation = textLocation;
                _tooltip = tooltip;
            }

            public Vector Tail => _tail;

            public Vector Head => _head;

            public IEnumerable<Vector> GetBoundingVectors() {
                yield return _tail;
                yield return _head;
            }

            public void FullyRestrictBoundingVectors(Graphics graphics) {
                // With LineTextPlacement.*Inclined, the _textbox set here is wrong. This is a "feature",
                // because it is hard to repair; and the usecases where another vector depends on the
                // rotated text are too rare to spend effort on them ...
                SizeF textSize = graphics.MeasureString(_text, _textFont);
                _textBox.Set(Vector.Fixed(textSize.Width * (1 + _textPadding), textSize.Height * (1 + _textPadding), "|" + _text + "|"));
            }

            public void Draw(Graphics graphics, StringBuilder htmlForTooltips) {
                float fWidth = (float)_width;

                PointF tailPoint = _tail.AsMirroredPointF();
                PointF headPoint = _head.AsMirroredPointF();
                if (tailPoint != headPoint) {
                    float absoluteArrowSize = Math.Min(10 * fWidth, (float)_head.To(_tail) / 4);
                    var pen = new Pen(_color, fWidth) {
                        StartCap = LineCap.RoundAnchor,
                        // arrowsize is relative to line width, therefore we divide by fWidth
                        CustomEndCap = new AdjustableArrowCap(absoluteArrowSize / fWidth / 2, absoluteArrowSize / fWidth, isFilled: false)
                    };
                    graphics.DrawLine(pen, tailPoint, headPoint);
                } else {
                    graphics.FillEllipse(new SolidBrush(_color), headPoint.X - fWidth / 2, headPoint.Y - fWidth / 2, fWidth, fWidth);
                }

                if (DEBUG) {
                    graphics.FillEllipse(new SolidBrush(Color.GreenYellow), headPoint.X - fWidth / 2, headPoint.Y - fWidth / 2, fWidth, fWidth);
                    graphics.FillEllipse(new SolidBrush(Color.Aqua), tailPoint.X - fWidth / 2, tailPoint.Y - fWidth / 2, fWidth, fWidth);
                }

                Vector textCenter = _tail * (1 - _textLocation) + _head * _textLocation;
                float halfTextWidth = _textBox.GetX() / 2;
                float lineAngleDegrees = (float)(-Math.Atan2(_head.GetY() - _tail.GetY(), _head.GetX() - _tail.GetX()) * 180 / Math.PI);

                var textTransform = new Matrix();
                switch (_lineTextPlacement) {
                    case LineTextPlacement.Left:
                        textTransform.Translate(-halfTextWidth, 0);
                        break;
                    case LineTextPlacement.Center:
                        break;
                    case LineTextPlacement.Right:
                        textTransform.Translate(halfTextWidth, 0);
                        break;
                    case LineTextPlacement.LeftInclined:
                        textTransform.RotateAt(lineAngleDegrees, textCenter.AsMirroredPointF());
                        textTransform.Translate(-halfTextWidth, 0);
                        break;
                    case LineTextPlacement.CenterInclined:
                        textTransform.RotateAt(lineAngleDegrees, textCenter.AsMirroredPointF());
                        break;
                    case LineTextPlacement.RightInclined:
                        textTransform.RotateAt(lineAngleDegrees, textCenter.AsMirroredPointF());
                        textTransform.Translate(halfTextWidth, 0);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                // Move text away form line; needs improvement for steeply inclined lines
                textTransform.Translate(0, -_textBox.GetY() / 2);
                DrawText(graphics, _text, _textFont, _textColor, textCenter, textTransform);

                // TODO: Get all these elements somehow and then do a "draw tooltip" ...
            }
        }

        private readonly Font _defaultTextFont = new Font(FontFamily.GenericSansSerif, 10);
        private readonly List<IBuilder> _builders = new List<IBuilder>();

        private static void DrawText(Graphics graphics, string text, Font textFont, Color textColor, Vector center, Matrix m) {
            StringFormat centered = new StringFormat {Alignment = StringAlignment.Center};
            PointF position = (center + F(0, textFont.GetHeight() / 2)).AsMirroredPointF();
            if (DEBUG) {
                graphics.FillEllipse(new SolidBrush(Color.Red), center.AsMirroredPointF().X - 3, center.AsMirroredPointF().Y - 3, 6, 6);
                graphics.DrawString(text, textFont, new SolidBrush(Color.LightGray), position, centered);
            }

            GraphicsContainer containerForTextPlacement = graphics.BeginContainer();

            graphics.MultiplyTransform(m);
            graphics.DrawString(text, textFont, new SolidBrush(textColor), position, centered);

            graphics.EndContainer(containerForTextPlacement);
        }

        public IBox Box([NotNull] Vector anchor, [CanBeNull] string text, [CanBeNull] Vector minDiagonal = null,
            BoxAnchoring boxAnchoring = BoxAnchoring.Center,
            [CanBeNull] Color? boxColor = null /*White*/, int connectors = 8,
            double borderWidth = 0, [CanBeNull] Color? borderColor = null /*Black*/,
            BoxTextPlacement boxTextPlacement = BoxTextPlacement.Center, [CanBeNull] Font textFont = null /*___*/, [CanBeNull] Color? textColor = null /*Black*/,
            double textPadding = 0.2, [CanBeNull] string tooltip = null) {
            if (anchor == null) {
                throw new ArgumentNullException(nameof(anchor));
            }
            Vector center;
            var diagonal = new BoundedVector("/" + (text ?? anchor.Name)).Restrict(minDiagonal);
            var halfDiagonal = diagonal / 2;
            switch (boxAnchoring) {
                case BoxAnchoring.Center:
                    center = anchor;
                    break;
                case BoxAnchoring.LowerLeft:
                    center = anchor + halfDiagonal;
                    break;
                case BoxAnchoring.CenterLeft:
                    center = anchor + halfDiagonal.Horizontal();
                    break;
                case BoxAnchoring.UpperLeft:
                    center = anchor + ~halfDiagonal;
                    break;
                case BoxAnchoring.CenterTop:
                    center = anchor - halfDiagonal.Vertical();
                    break;
                case BoxAnchoring.UpperRight:
                    center = anchor - halfDiagonal;
                    break;
                case BoxAnchoring.CenterRight:
                    center = anchor - halfDiagonal.Horizontal();
                    break;
                case BoxAnchoring.LowerRight:
                    center = anchor - ~halfDiagonal;
                    break;
                case BoxAnchoring.CenterBottom:
                    center = anchor + halfDiagonal.Vertical();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(boxAnchoring), boxAnchoring, null);
            }

            var boxBuilder = new BoxBuilder(center, diagonal, boxColor ?? Color.White,
                borderWidth, borderColor ?? Color.Black, connectors,
                text ?? "", boxTextPlacement, textFont ?? _defaultTextFont, textColor ?? Color.Black, textPadding, tooltip ?? "");
            _builders.Add(boxBuilder);
            return boxBuilder;
        }

        public IArrow Arrow([NotNull] Vector tail, [NotNull] Vector head, double width, [CanBeNull] Color? color = null /*Black*/,
            [CanBeNull] string text = null, LineTextPlacement placement = LineTextPlacement.Center, [CanBeNull] Font textFont = null /*___*/,
            [CanBeNull] Color? textColor = null /*Black*/, double textPadding = 0.2, double textLocation = 0.5, [CanBeNull] string tooltip = null) {
            if (tail == null) {
                throw new ArgumentNullException(nameof(tail));
            }
            if (head == null) {
                throw new ArgumentNullException(nameof(head));
            }
            var arrowBuilder = new ArrowBuilder(tail, head, width, color ?? Color.Black,
                text ?? "", placement, textFont ?? _defaultTextFont, textColor ?? Color.Black,
                textPadding, textLocation, tooltip);
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
                    b.FullyRestrictBoundingVectors(graphics);

                    foreach (var v in b.GetBoundingVectors()) {
                        double? x = v.X();
                        if (!x.HasValue) {
                            errors.AppendLine("No x value set in vector " + (v.Name ?? "dependent on other vectors"));
                        } else {
                            minX = Math.Min(minX, x.Value);
                            maxX = Math.Max(maxX, x.Value);
                        }
                        double? y = v.Y();
                        if (!y.HasValue) {
                            errors.AppendLine("No y value set in vector " + (v.Name ?? "dependent on other vectors"));
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
                float scale = (float)Math.Min(scaleX, scaleY); // No distortion!

                graphics.Transform = new Matrix(scale, 0, 0, scale, (float)(-scale * minX + size.Width * BORDER),
                    (float)(scale * maxY + size.Height * BORDER));

                List<IBuilder> openBuilders = _builders.ToList();

                RETRY:
                bool someRemoved = false;
                try {
                    foreach (var b in openBuilders.ToArray()) {
                        b.Draw(graphics, htmlForTooltips);
                        someRemoved = openBuilders.Remove(b);
                    }
                } catch (MissingValueException) {
                    // Coordinates of one builder not set (probably not possible right now, but might be with more complex dependencies)
                    if (someRemoved) {
                        // A retry will not lead to an endless loop
                        goto RETRY;
                    } else {
                        throw;
                    }
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

        public abstract void CreateSomeTestItems(out IEnumerable<TItem> items, out IEnumerable<TDependency> dependencies);
    }

    public abstract class GraphicsDependencyRenderer : GraphicsRenderer<Item, Dependency>, IDependencyRenderer { }
}
