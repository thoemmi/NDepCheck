using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace NDepCheck.Rendering {
    public interface IRenderer {
        void Render(IEnumerable<Dependency> dependencies, string filename);
    }

    public class VariableVector : Vector {
        public string Name { get; }
        private float? _x;
        private float? _y;

        VariableVector(string name, float? x = null, float? y = null) {
            Name = name;
            Set(x, y);
        }

        public void Set(float? x, float? y) {
            SetX(x);
            SetY(y);
        }

        public void SetX(float? x) {
            _x = x;
        }

        public void SetY(float? y) {
            _y = y;
        }

        public override Func<float?> X => () => _x;
        public override Func<float?> Y => () => _y;
    }

    public abstract class Vector {
        public abstract Func<float?> X { get; }
        public abstract Func<float?> Y { get; }

        public static Vector Fixed(float x, float y) {
            return new FixedVector(x, y);
        }

        public float GetX() {
            float? x = X();
            if (!x.HasValue) {
                throw new InvalidOperationException("X has no value");
            }
            return x.Value;
        }

        public float GetY() {
            float? y = Y();
            if (!y.HasValue) {
                throw new InvalidOperationException("Y has no value");
            }
            return y.Value;
        }

        private class FixedVector : Vector {
            private readonly float _x;
            private readonly float _y;

            public FixedVector(float x, float y) {
                _x = x;
                _y = y;
            }

            public override Func<float?> X => () => _x;
            public override Func<float?> Y => () => _y;
        }

        private class DependentVector : Vector {
            public DependentVector(Func<float?> x, Func<float?> y) {
                X = x;
                Y = y;
            }

            public override Func<float?> X { get; }

            public override Func<float?> Y { get; }
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

        public static Vector operator *([NotNull] Vector v, float d) {
            CheckNotNull(v);
            return new DependentVector(() => v.X() * d, () => v.Y() * d);
        }

        public static Vector operator *(float d, [NotNull] Vector v) {
            CheckNotNull(v);
            return new DependentVector(() => v.X() * d, () => v.Y() * d);
        }

        public static Vector operator /([NotNull] Vector v, float d) {
            CheckNotNull(v);
            return new DependentVector(() => v.X() / d, () => v.Y() / d);
        }
    }

    public static class DrawingExtensions {
        public static Vector C(this float x, float y) { return Vector.Fixed(x, y); }
    }

    public class Renderer {
        public enum TextPlacing { Left, Center, Right, LeftUp, CenterUp, RightUp, LeftDown, CenterDown, RightDown }

        public enum Tip { Simple, Arrow }

        private interface IBuilder {
            IEnumerable<Vector> GetAllVectors();
            void Draw(Graphics graphics, StringBuilder htmlForTooltips);
        }

        private class RectangleBuilder : IBuilder {
            private readonly Vector _center;
            private readonly Vector _halfSize;
            private readonly float _borderWidth;
            private readonly string _text;
            private readonly TextPlacing _placing;
            private readonly string _tooltip;
            private readonly Color _textColor;
            private readonly Font _textFont;
            private readonly Color _borderColor;
            private readonly Color _color;

            public RectangleBuilder(Vector center, Vector halfSize, Color color,
            float borderWidth, Color borderColor,
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

            public void Draw(Graphics graphics, StringBuilder htmlForTooltips) {
                Brush b = new SolidBrush(_color);

                Vector leftUpper = _center - _halfSize;
                Vector diagonal = 2 * _halfSize;

                graphics.FillRectangle(b, new RectangleF(leftUpper.GetX(), leftUpper.GetY(), diagonal.GetX(), diagonal.GetY()));


                throw new NotImplementedException();
            }

            public IEnumerable<Vector> GetAllVectors() {
                yield return _center;
                yield return _halfSize;
            }
        }

        private class LineBuilder : IBuilder {
            private readonly Vector _tail;
            private readonly Vector _head;
            private readonly float _width;
            private readonly Color _color;
            private readonly Tip _tailTip;
            private readonly Tip _headTip;
            private readonly string _text;
            private readonly TextPlacing _placing;
            private readonly Font _textFont;
            private readonly Color _textColor;
            private readonly string _tooltip;

            internal LineBuilder(Vector tail, Vector head, float width, Color color, Tip tailTip, Tip headTip,
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
                throw new NotImplementedException();
            }
        }

        private readonly List<IBuilder> _builders = new List<IBuilder>();

        public Renderer CreateRectangle(Vector center, Vector halfSize, Color? color = null /*White*/,
            float borderWidth = 0, Color? borderColor = null /*Black*/,
            string text = "", TextPlacing placing = TextPlacing.Center, Font textFont = null /*___*/, Color? textColor = null /*Black*/,
            string tooltip = "") {
            _builders.Add(new RectangleBuilder(center, halfSize, color ?? Color.White, borderWidth, borderColor ?? Color.Black,
                text, placing, textFont ?? new Font(FontFamily.GenericSansSerif, 10), textColor ?? Color.Black, tooltip));

            return this;
        }

        public Renderer CreateLine(Vector tail, Vector head, float width, Color? color = null /*Black*/, Tip tailtip = Tip.Simple, Tip headtip = Tip.Simple,
            string text = "", TextPlacing placing = TextPlacing.Center, Font textFont = null /*___*/, Color? textColor = null /*Black*/,
            string tooltip = "") {
            _builders.Add(new LineBuilder(tail, head, width, color ?? Color.Black, tailtip, headtip,
                text, placing, textFont ?? new Font(FontFamily.GenericSansSerif, 10), textColor ?? Color.Black, tooltip));

            return this;
        }

        public void DrawToFile(string baseFilename, int width, int height) {
            float minX = float.MaxValue;
            float maxX = -float.MaxValue;
            float minY = float.MaxValue;
            float maxY = -float.MaxValue;

            StringBuilder errors = new StringBuilder();
            foreach (var b in _builders) {
                foreach (var v in b.GetAllVectors().OfType<VariableVector>()) {
                    float? x = v.X();
                    if (!x.HasValue) {
                        errors.AppendLine("No x value set in vector " + v.Name);
                    } else {
                        minX = Math.Min(minX, x.Value);
                        maxX = Math.Max(maxX, x.Value);
                    }
                    float? y = v.X();
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

            Bitmap img = new Bitmap(width, height);
            Graphics graphics = Graphics.FromImage(img);
            StringBuilder htmlForTooltips = new StringBuilder();

            graphics.Transform = new Matrix(0, 1, 2, 3, 4, 5);

            foreach (var b in _builders) {
                b.Draw(graphics, htmlForTooltips);
            }

            var gifFilename = baseFilename + ".gif";
            img.Save(gifFilename, ImageFormat.Gif);
            using (var tw = new StreamWriter(baseFilename + ".html")) {
                tw.WriteLine($"___{Path.GetFileName(gifFilename)}___{htmlForTooltips}___");
            }

        }
    }
}
