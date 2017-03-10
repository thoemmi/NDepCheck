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
    public interface IRectangle {
        Vector Center { get; }
        Vector GetAnchor(Vector toCenter, double angle = Math.PI / 4);
    }

    public interface ILine {
    }

    public class VariableVector : Vector {
        public string Name {
            get;
        }
        private double? _x;
        private double? _y;

        public VariableVector(string name, double? x = null, double? y = null) {
            Name = name;
            Set(x, y);
        }

        public void Set(double? x, double? y) {
            SetX(x);
            SetY(y);
        }

        public void SetX(double? x) {
            _x = x;
        }

        public void SetY(double? y) {
            _y = y;
        }

        public override Func<double?> X => () => _x;
        public override Func<double?> Y => () => _y;
    }

    public class DependentVector : Vector {
        public DependentVector(Func<double?> x, Func<double?> y) {
            X = x;
            Y = y;
        }

        public override Func<double?> X {
            get;
        }

        public override Func<double?> Y {
            get;
        }
    }

    public abstract class Vector {
        public abstract Func<double?> X {
            get;
        }
        public abstract Func<double?> Y {
            get;
        }

        public static Vector Fixed(double x, double y) {
            return new FixedVector(x, y);
        }

        public PointF AsPointF() {
            return new PointF(GetX(), (float) GetY());
        }

        public float GetX() {
            double? x = X();
            if (!x.HasValue) {
                throw new InvalidOperationException("X has no value");
            }
            return (float) x.Value;
        }

        public float GetY() {
            double? y = Y();
            if (!y.HasValue) {
                throw new InvalidOperationException("Y has no value");
            }
            return (float) y.Value;
        }

        private class FixedVector : Vector {
            private readonly double _x;
            private readonly double _y;

            public FixedVector(double x, double y) {
                _x = x;
                _y = y;
            }

            public override Func<double?> X => () => _x;
            public override Func<double?> Y => () => _y;
        }

        public static Vector operator +([NotNull] Vector v1, [NotNull] Vector v2) {
            CheckNotNull(v1);
            CheckNotNull(v2);
            return new DependentVector(() => v1.X() + v2.X(), () => v1.Y() + v2.Y());
        }

        private static void CheckNotNull(Vector v) {
            if (v == null) {
                throw new ArgumentNullException(nameof(v));
            }
        }

        public static Vector operator -([NotNull] Vector v1, [NotNull] Vector v2) {
            CheckNotNull(v1);
            CheckNotNull(v2);
            return new DependentVector(() => v1.X() - v2.X(), () => v1.Y() - v2.Y());
        }

        public static Vector operator *([NotNull] Vector v, double d) {
            CheckNotNull(v);
            return new DependentVector(() => v.X() * d, () => v.Y() * d);
        }

        public static Vector operator *(double d, [NotNull] Vector v) {
            CheckNotNull(v);
            return new DependentVector(() => v.X() * d, () => v.Y() * d);
        }

        public static Vector operator /([NotNull] Vector v, double d) {
            CheckNotNull(v);
            return new DependentVector(() => v.X() / d, () => v.Y() / d);
        }
    }

    public static class DrawingExtensions {
        public static Vector C(this double x, double y) {
            return Vector.Fixed(x, y);
        }
    }

    public abstract class Renderer {
        public static Vector C(double x, double y) {
            return Vector.Fixed(x, y);
        }

        public enum TextPlacing {
            Left, Center, Right, LeftUp, CenterUp, RightUp, LeftDown, CenterDown, RightDown
        }

        public enum Tip {
            Simple, Arrow
        }

        private interface IBuilder {
            IEnumerable<Vector> GetAllVectors();
            void Draw(Graphics graphics, StringBuilder htmlForTooltips);
        }

        private class RectangleBuilder : IBuilder, IRectangle {
            private readonly Vector _center;
            private readonly Vector _halfSize;
            private readonly double _borderWidth;
            private readonly string _text;
            private readonly TextPlacing _placing;
            private readonly string _tooltip;
            private readonly Color _textColor;
            private readonly Font _textFont;
            private readonly Color _borderColor;
            private readonly Color _color;

            public RectangleBuilder(Vector center, Vector halfSize, Color color,
            double borderWidth, Color borderColor,
            string text, TextPlacing placing, Font textFont, Color textColor,
            string tooltip) {
                _center = center;
                _halfSize = halfSize;
                _color = color;
                _borderWidth = borderWidth;
                _text = text;
                _placing = placing;
                _tooltip = tooltip;
                _textColor = textColor;
                _textFont = textFont;
                _borderColor = borderColor;
            }

            public Vector Center {
                get { return _center; }
            }

            public void Draw(Graphics graphics, StringBuilder htmlForTooltips) {
                Vector leftUpper = _center - _halfSize;

                Vector diagonal = 2 * _halfSize;
                graphics.FillRectangle(new SolidBrush(_borderColor), leftUpper.GetX(), leftUpper.GetY(), diagonal.GetX(), diagonal.GetY());

                Vector diagonalInner = diagonal - _borderWidth.C(_borderWidth);
                graphics.FillRectangle(new SolidBrush(_color), leftUpper.GetX(), leftUpper.GetY(), diagonalInner.GetX(), diagonalInner.GetY());

                DrawText(graphics, _text, _textFont, _textColor, _center, _placing);

                // Get all these elements somehow and then do a "draw tooltip" ...
            }

            public IEnumerable<Vector> GetAllVectors() {
                yield return _center;
                yield return _halfSize;
            }

            public Vector GetAnchor(Vector toCenter, double angle = 0.785398163397448) {
                throw new NotImplementedException();
            }
        }

        private class LineBuilder : IBuilder, ILine {
            private readonly Vector _tail;
            private readonly Vector _head;
            private readonly double _width;
            private readonly Color _color;
            private readonly Tip _tailTip;
            private readonly Tip _headTip;
            private readonly string _text;
            private readonly TextPlacing _placing;
            private readonly Font _textFont;
            private readonly Color _textColor;
            private readonly string _tooltip;

            internal LineBuilder(Vector tail, Vector head, double width, Color color, Tip tailTip, Tip headTip,
                        string text, TextPlacing placing, Font textFont, Color textColor,
                        string tooltip) {
                _tail = tail;
                _head = head;
                _width = width;
                _color = color;
                _tailTip = tailTip;
                _headTip = headTip;
                _text = text;
                _placing = placing;
                _textFont = textFont;
                _textColor = textColor;
                _tooltip = tooltip;
            }

            public IEnumerable<Vector> GetAllVectors() {
                yield return _tail;
                yield return _head;
            }
            public void Draw(Graphics graphics, StringBuilder htmlForTooltips) {

                graphics.DrawLine(new Pen(_color, (float) _width), _tail.AsPointF(), _head.AsPointF());
                DrawTip(graphics, _tail, _head, _headTip);
                DrawTip(graphics, _head, _tail, _tailTip);

                DrawText(graphics, _text, _textFont, _textColor, (_head + _tail) / 2, _placing);

                // Get all these elements somehow and then do a "draw tooltip" ...
            }

            private void DrawTip(Graphics graphics, Vector tail, Vector head, Tip headTip) {
                // not yet implemented
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

        protected readonly Store<Item, IRectangle> ItemRectangles = new Store<Item, IRectangle>();
        protected readonly Store<Dependency, ILine> DependencyLines = new Store<Dependency, ILine>();

        private static void DrawText(Graphics graphics, string text, Font textFont, Color textColor, Vector center, TextPlacing textPlacing) {
            graphics.DrawString(text, textFont, new SolidBrush(textColor), center.AsPointF(),
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

        protected IRectangle CreateRectangle(Vector center, Vector halfSize, Color? color = null /*White*/,
            double borderWidth = 0, Color? borderColor = null /*Black*/,
            string text = "", TextPlacing placing = TextPlacing.Center, Font textFont = null /*___*/, Color? textColor = null /*Black*/,
            string tooltip = "") {
            var rectangleBuilder = new RectangleBuilder(center, halfSize, color ?? Color.White, borderWidth, borderColor ?? Color.Black,
                text, placing, textFont ?? new Font(FontFamily.GenericSansSerif, 10), textColor ?? Color.Black, tooltip);
            _builders.Add(rectangleBuilder);
            return rectangleBuilder;
        }

        protected ILine CreateLine(Vector tail, Vector head, double width, Color? color = null /*Black*/, Tip tailtip = Tip.Simple, Tip headtip = Tip.Simple,
            string text = "", TextPlacing placing = TextPlacing.Center, Font textFont = null /*___*/, Color? textColor = null /*Black*/,
            string tooltip = "") {
            var lineBuilder = new LineBuilder(tail, head, width, color ?? Color.Black, tailtip, headtip,
                text, placing, textFont ?? new Font(FontFamily.GenericSansSerif, 10), textColor ?? Color.Black, tooltip);
            _builders.Add(lineBuilder);
            return lineBuilder;
        }

        public void DrawToFile(IEnumerable<Item> items, IEnumerable<Dependency> dependencies, string baseFilename, int width, int height) {
            CreateImage(items, dependencies);

            double minX = double.MaxValue;
            double maxX = -double.MaxValue;
            double minY = double.MaxValue;
            double maxY = -double.MaxValue;

            StringBuilder errors = new StringBuilder();
            foreach (var b in _builders) {
                foreach (var v in b.GetAllVectors().OfType<VariableVector>()) {
                    double? x = v.X();
                    if (!x.HasValue) {
                        errors.AppendLine("No x value set in vector " + v.Name);
                    } else {
                        minX = Math.Min(minX, x.Value);
                        maxX = Math.Max(maxX, x.Value);
                    }
                    double? y = v.X();
                    if (!y.HasValue) {
                        errors.AppendLine("No y value set in vector " + v.Name);
                    } else {
                        minY = Math.Min(minY, y.Value);
                        maxY = Math.Max(maxY, y.Value);
                    }
                }
            }
            if (errors.Length > 0) {
                throw new InvalidOperationException(errors.ToString());
            }

            // I tried it with SVG - but SVG support in .Net seems to be non-existent.
            // The library at https://github.com/managed-commons/SvgNet is a nice attempet (a 2015 resurrection of a 2003 attempt),
            // but it closes off the SVG objects in such a way that adding tooltips ("mouse hoverings") seems very hard.
            // If someone knows more about SVG than I (who doesn't know a bit ...), feel free to try it with SVG!

            var bitMap = new Bitmap(width, height);
            var graphics = Graphics.FromImage(bitMap);

            StringBuilder htmlForTooltips = new StringBuilder();

            graphics.Transform = new Matrix(0, 1, 2, 3, 4, 5);

            foreach (var b in _builders) {
                b.Draw(graphics, htmlForTooltips);
            }

            var gifFilename = baseFilename + ".gif";
            bitMap.Save(gifFilename, ImageFormat.Gif);
            using (var tw = new StreamWriter(baseFilename + ".html")) {
                tw.WriteLine($"___{Path.GetFileName(gifFilename)}___{htmlForTooltips}___");
            }


            //< area shape = "poly" coords = "x1,y1,x2,y2,..,xn,yn" title = ".." >< area shape = "poly" coords = "2,5,32,1,33,22,51,36,33,57" title = "The Americas" >< area shape = "poly" coords = "57,14,70,2,111,3,114,23,97,34" title = "Eurasia" >< area shape = "poly" coords = "57,14,86,29,73,52,66,49,50,28" title = "Africa" >< area shape = "poly" coords = "105,40,108,49,122,52,127,41,117,34" title = "Australia" >

        }

        protected abstract void CreateImage(IEnumerable<Item> items, IEnumerable<Dependency> dependencies);
    }
}
