using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using NDepCheck.ConstraintSolving;

namespace NDepCheck.Rendering {
    public interface IBox {
        VariableVector Center { get; }

        VariableVector LowerLeft { get; }
        VariableVector CenterLeft { get; }
        VariableVector UpperLeft { get; }
        VariableVector CenterTop { get; }
        VariableVector UpperRight { get; }
        VariableVector CenterRight { get; }
        VariableVector LowerRight { get; }
        VariableVector CenterBottom { get; }

        VariableVector Diagonal { get; }
        VariableVector TextBox { get; }

        VariableVector GetBestConnector(VariableVector farAway);
    }

    public interface IArrow {
        VariableVector Head { get; }
        VariableVector Tail { get; }
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

        private class GraphicsRendererSolver : SimpleConstraintSolver {
            private readonly GraphicsRenderer<TItem, TDependency> _renderer;

            public GraphicsRendererSolver(GraphicsRenderer<TItem, TDependency> renderer) : base(1.5e-5) {
                _renderer = renderer;
            }

            protected override IEnumerable<NumericVariable> CheckState(IEnumerable<NumericVariable> allVariables) {
                foreach (var b in _renderer._builders.OrderBy(b => b.FixingOrder).ThenBy(b => b.CreationOrder)) {
                    foreach (var v in b.GetFixVariables()) {
                        if (double.IsInfinity(v.Value.Hi)) {
                            if (v.Fix()) {
                                yield return v;
                                yield break;
                            }
                        }
                    }
                }
            }
        }

        protected readonly SimpleConstraintSolver _solver;

        private static readonly bool DEBUG = false;

        public GraphicsRenderer() {
            _solver = new GraphicsRendererSolver(this);
        }

        public VariableVector F(double? x, double? y, string name = "C") {
            return new VariableVector(name, _solver, x, y);
        }

        public VariableVector B(string name, double interpolateMinMax = 0.0) {
            return new VariableVector(name, _solver, null, null);
        }

        private interface IBuilder {
            string Name { get; }
            int CreationOrder { get; }
            int DrawingOrder { get; }
            int FixingOrder { get; }
            IEnumerable<VectorF> GetBoundingVectors();
            void FullyRestrictBoundingVectors(Graphics graphics);
            void Draw(Graphics graphics, StringBuilder htmlForTooltips);
            IEnumerable<NumericVariable> GetFixVariables();
        }

        private abstract class AbstractBuilder {
            private static int _creationOrder = 0; // or use Renderer as parent and place ct there ...

            protected AbstractBuilder(int drawingOrder, int fixingOrder) {
                DrawingOrder = drawingOrder;
                FixingOrder = fixingOrder;
                _creationOrder++;
            }

            public int DrawingOrder { get; }
            public int FixingOrder { get; }
            public int CreationOrder => _creationOrder;
        }

        private class BoxBuilder : AbstractBuilder, IBuilder, IBox {
            private readonly SimpleConstraintSolver _solver;
            private VariableVector _anchor;
            [NotNull]
            private readonly VariableVector _diagonal;
            private readonly VariableVector _textBox;
            private readonly float _borderWidth;
            private readonly string _text;
            private readonly BoxTextPlacement _boxTextPlacement;
            private readonly string _tooltip;
            private readonly Color _textColor;
            private readonly double _textPadding;
            private readonly Font _textFont;
            private readonly Color _borderColor;
            private readonly Color _color;
            private double _sectorAngle;

            private readonly VariableVector _center;
            private readonly VariableVector _lowerLeft;
            private readonly VariableVector _centerLeft;
            private readonly VariableVector _upperLeft;
            private readonly VariableVector _centerTop;
            private readonly VariableVector _upperRight;
            private readonly VariableVector _centerRight;
            private readonly VariableVector _lowerRight;
            private readonly VariableVector _centerBottom;

            public BoxBuilder([NotNull] SimpleConstraintSolver solver,
                             [NotNull] VariableVector anchor, [NotNull] VariableVector diagonal,
                             BoxAnchoring boxAnchoring, Color color,
                              double borderWidth, Color borderColor, int connectors, string text,
                              BoxTextPlacement boxTextPlacement, Font textFont, Color textColor, double textPadding,
                              string tooltip, int drawingOrder, int fixingOrder, string name) : base(drawingOrder, fixingOrder) {
                _solver = solver;
                Name = name ?? anchor.Name;

                _anchor = anchor;
                _textBox = new VariableVector(Name + ".TXT", _solver);
                _diagonal = diagonal.AlsoNamed(Name + "./").Restrict(_textBox);

                var halfDiagonal = diagonal / 2;
                var halfHorizontal = halfDiagonal.Horizontal();
                var halfVertical = halfDiagonal.Vertical();
                var vertical = diagonal.Vertical();
                var horizontal = diagonal.Horizontal();

                switch (boxAnchoring) {
                    case BoxAnchoring.Center:
                        _center = anchor;
                        _lowerLeft = anchor - halfDiagonal;
                        _centerLeft = anchor - halfHorizontal;
                        _upperLeft = anchor + ~halfDiagonal;
                        _centerTop = anchor + halfVertical;
                        _upperRight = anchor + halfDiagonal;
                        _centerRight = anchor + halfHorizontal;
                        _lowerRight = anchor + !halfDiagonal;
                        _centerBottom = anchor - halfVertical;
                        break;
                    case BoxAnchoring.LowerLeft:
                        _center = anchor + halfDiagonal;
                        _lowerLeft = anchor;
                        _centerLeft = anchor + halfVertical;
                        _upperLeft = anchor + vertical;
                        _centerTop = _upperLeft + halfHorizontal;
                        _upperRight = anchor + diagonal;
                        _centerRight = _centerLeft + horizontal;
                        _lowerRight = anchor + horizontal;
                        _centerBottom = anchor + halfHorizontal;
                        break;
                    case BoxAnchoring.CenterLeft:
                        _center = anchor + halfHorizontal;
                        _lowerLeft = anchor - halfVertical;
                        _centerLeft = anchor;
                        _upperLeft = anchor + halfVertical;
                        _centerTop = anchor + halfDiagonal;
                        _upperRight = _centerTop + halfHorizontal;
                        _centerRight = anchor + horizontal;
                        _lowerRight = _centerRight - halfVertical;
                        _centerBottom = anchor + !halfDiagonal;
                        break;
                    case BoxAnchoring.UpperLeft:
                        _center = anchor - ~halfDiagonal;
                        _lowerLeft = anchor - vertical;
                        _centerLeft = anchor - halfVertical;
                        _upperLeft = anchor;
                        _centerTop = anchor + halfHorizontal;
                        _upperRight = anchor + horizontal;
                        _centerRight = _upperRight - halfVertical;
                        _lowerRight = anchor - ~diagonal;
                        _centerBottom = anchor + new VariableVector(null, diagonal.X / 2, -diagonal.Y);
                        break;
                    case BoxAnchoring.CenterTop:
                        _center = anchor - halfVertical;
                        _lowerLeft = _center - halfDiagonal;
                        _centerLeft = anchor - halfDiagonal;
                        _upperLeft = anchor - halfHorizontal;
                        _centerTop = anchor;
                        _upperRight = anchor + halfHorizontal;
                        _centerRight = anchor + !halfDiagonal;
                        _lowerRight = _centerRight - halfVertical;
                        _centerBottom = anchor - vertical;
                        break;
                    case BoxAnchoring.UpperRight:
                        _center = anchor - halfDiagonal;
                        _lowerLeft = anchor - diagonal;
                        _centerLeft = anchor + new VariableVector(null, -diagonal.X, diagonal.Y / -2);
                        _upperLeft = anchor - horizontal;
                        _centerTop = anchor - halfHorizontal;
                        _upperRight = anchor;
                        _centerRight = anchor - halfVertical;
                        _lowerRight = anchor - vertical;
                        _centerBottom = anchor + new VariableVector(null, diagonal.X / -2, -diagonal.Y);
                        break;
                    case BoxAnchoring.CenterRight:
                        _center = anchor - halfHorizontal;
                        _lowerLeft = _center - halfDiagonal;
                        _centerLeft = anchor - horizontal;
                        _upperLeft = anchor + new VariableVector(null, -diagonal.X, diagonal.Y / 2);
                        _centerTop = anchor + ~halfDiagonal;
                        _upperRight = anchor + halfVertical;
                        _centerRight = anchor;
                        _lowerRight = anchor - halfVertical;
                        _centerBottom = anchor - halfDiagonal;
                        break;
                    case BoxAnchoring.LowerRight:
                        _center = anchor + ~halfDiagonal;
                        _lowerLeft = anchor - horizontal;
                        _centerLeft = anchor + new VariableVector(null, -diagonal.X, diagonal.Y / 2);
                        _upperLeft = anchor + ~diagonal;
                        _centerTop = anchor + new VariableVector(null, diagonal.X / -2, diagonal.Y);
                        _upperRight = anchor + vertical;
                        _centerRight = anchor + halfVertical;
                        _lowerRight = anchor;
                        _centerBottom = anchor - halfHorizontal;
                        break;
                    case BoxAnchoring.CenterBottom:
                        _center = anchor + halfVertical;
                        _lowerLeft = anchor - halfHorizontal;
                        _centerLeft = anchor + ~halfDiagonal;
                        _upperLeft = _centerLeft + halfVertical;
                        _centerTop = anchor + vertical;
                        _upperRight = _centerTop + halfHorizontal;
                        _centerRight = anchor + halfDiagonal;
                        _lowerRight = anchor + halfHorizontal;
                        _centerBottom = anchor;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(boxAnchoring), boxAnchoring, null);
                }

                _center = _center.AlsoNamed(Name + "CC");
                _lowerLeft = _lowerLeft.AlsoNamed(Name + ".LL");
                _centerLeft = _centerLeft.AlsoNamed(Name + ".CL");
                _upperLeft = _upperLeft.AlsoNamed(Name + ".UL");
                _centerTop = _centerTop.AlsoNamed(Name + ".CT");
                _upperRight = _upperRight.AlsoNamed(Name + ".UR");
                _centerRight = _centerRight.AlsoNamed(Name + ".CR");
                _lowerRight = _lowerRight.AlsoNamed(Name + ".LR");
                _centerBottom = _centerBottom.AlsoNamed(Name + ".CB");

                _upperRight.SetY(_centerTop.Y);
                _upperRight.SetY(_upperLeft.Y);
                _centerTop.SetY(_upperLeft.Y);
                _centerRight.SetY(_center.Y);
                _centerRight.SetY(_centerLeft.Y);
                _center.SetY(_centerLeft.Y);
                _lowerRight.SetY(_centerBottom.Y);
                _lowerRight.SetY(_lowerLeft.Y);
                _centerBottom.SetY(_lowerLeft.Y);

                // Simple stuff
                _color = color;
                _borderWidth = (float)borderWidth;
                _text = text;
                _boxTextPlacement = boxTextPlacement;
                _tooltip = tooltip;
                _textColor = textColor;
                _textPadding = textPadding;
                _textFont = textFont;
                _borderColor = borderColor;
                _sectorAngle = 2 * Math.PI / connectors;
            }

            public override string ToString() {
                return "BoxBuilder " + Name;
            }

            public IEnumerable<NumericVariable> GetFixVariables() {
                yield return _anchor.X;
                yield return _anchor.Y;
                yield return _diagonal.X;
                yield return _diagonal.Y;
            }

            public VariableVector Center => _center;
            public VariableVector LowerLeft => _lowerLeft;
            public VariableVector CenterLeft => _centerLeft;
            public VariableVector UpperLeft => _upperLeft;
            public VariableVector CenterTop => _centerTop;
            public VariableVector UpperRight => _upperRight;
            public VariableVector CenterRight => _centerRight;
            public VariableVector LowerRight => _lowerRight;
            public VariableVector CenterBottom => _centerBottom;
            public VariableVector Diagonal => _diagonal;
            public VariableVector TextBox => _textBox;

            public string Name { get; }

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
                _textBox.Set(size.Width * (1 + _textPadding) + 2 * _borderWidth, size.Height * (1 + _textPadding) + 2 * _borderWidth);
            }

            public void Draw(Graphics graphics, StringBuilder htmlForTooltips) {
                var diagonalF = _diagonal.AsVectorF();
                var textBoxF = _textBox.AsVectorF();
                var centerF = _center.AsVectorF();
                VectorF leftUpperF = _center.AsVectorF() - ~diagonalF / 2;

                FillBox(graphics, new SolidBrush(_borderColor), leftUpperF.GetX(), -leftUpperF.GetY(), diagonalF.GetX(), diagonalF.GetY());

                VectorF borderDiagonal = new VectorF(_borderWidth, _borderWidth, "45°@" + _borderWidth);
                VectorF leftUpperInner = leftUpperF + ~borderDiagonal;
                VectorF diagonalInner = diagonalF - 2 * borderDiagonal;

                FillBox(graphics, new SolidBrush(_color), leftUpperInner.GetX(), -leftUpperInner.GetY(), diagonalInner.GetX(), diagonalInner.GetY());

                Matrix m = new Matrix();
                switch (_boxTextPlacement) {
                    case BoxTextPlacement.Left:
                        m.Translate(-(diagonalF - textBoxF).GetX() / 2, 0);
                        break;
                    case BoxTextPlacement.Center:
                        break;
                    case BoxTextPlacement.Right:
                        m.Translate((diagonalF - textBoxF).GetX() / 2, 0);
                        break;
                    case BoxTextPlacement.LeftUp:
                        m.Translate(0, -(diagonalF - textBoxF).AsMirroredPointF().Y / 2);
                        m.RotateAt(-90, centerF.AsMirroredPointF());
                        break;
                    case BoxTextPlacement.CenterUp:
                        m.RotateAt(-90, centerF.AsMirroredPointF());
                        break;
                    case BoxTextPlacement.RightUp:
                        m.Translate(0, (diagonalF - textBoxF).AsMirroredPointF().Y / 2);
                        m.RotateAt(-90, centerF.AsMirroredPointF());
                        break;
                    case BoxTextPlacement.LeftDown:
                        m.Translate(0, (diagonalF - textBoxF).AsMirroredPointF().Y / 2);
                        m.RotateAt(90, centerF.AsMirroredPointF());
                        break;
                    case BoxTextPlacement.CenterDown:
                        m.RotateAt(90, centerF.AsMirroredPointF());
                        break;
                    case BoxTextPlacement.RightDown:
                        m.Translate(0, -(diagonalF - textBoxF).AsMirroredPointF().Y / 2);
                        m.RotateAt(90, centerF.AsMirroredPointF());
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(_boxTextPlacement), _boxTextPlacement, null);
                }

                DrawText(graphics, _text, _textFont, _textColor, _center.AsVectorF(), m);

                // Get all these elements somehow and then do a "draw tooltip" ...
            }

            private void FillBox(Graphics graphics, SolidBrush b, float x, float y, float width, float height) {
                //Console.WriteLine($"FillBox({x},{y},{width},{height})");
                graphics.FillRectangle(b, x, y, width, height);
            }

            public IEnumerable<VectorF> GetBoundingVectors() {
                yield return _lowerLeft.AsVectorF().Suffixed("ll");
                yield return _upperRight.AsVectorF().Suffixed("ur");
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

            public VariableVector GetBestConnector(VariableVector farAway) {
                // Current algorithm: There are 360°/_connectors equal-sized sectors;
                // the "best connector" is the intersection of a sector center line
                // nearest to the line from center to farAway.
                // Other ideas: 
                // - Divide the circumference of the box into equal-sized lengths.
                // - Like before, but with guaranteed connectors at corners.
                // - Like before, but with additional guaranteed connectors at edge midpoints.

                var result = new VariableVector("Connector", _solver);
                new UnidirectionalComputationConstraint(
                    input: new[] { farAway.X, farAway.Y, _center.X, _center.Y, _diagonal.X, _diagonal.Y },
                    output: new[] { result.X, result.Y },
                    computation: (input, output) => {
                        // float[] inputValues = _input.Select(v => (float) v.Value.Lo).ToArray();
                        // ... I ignore "input" here and use the fields and local variables ... well ... ... should work ...
                        VectorF centerF = _center.AsVectorF();
                        VectorF diagonalF = _diagonal.AsVectorF();
                        var farAwayF = farAway.AsVectorF();

                        VectorF d = farAwayF - centerF;
                        double angle = Math.Atan2(d.GetY(), d.GetX());
                        double roundedAngle =
                            NormalizedAngle(Math.Round(angle / _sectorAngle) * _sectorAngle);
                        double diagX = diagonalF.GetX() / 2;
                        double diagY = diagonalF.GetY() / 2;
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

                        result.X.Set(centerF.GetX() + x);
                        result.Y.Set(centerF.GetY() + y);
                    });
                return result;
            }
        }

        private class ArrowBuilder : AbstractBuilder, IBuilder, IArrow {
            private readonly VariableVector _head;
            private readonly SimpleConstraintSolver _solver;
            private readonly VariableVector _tail;
            private readonly double _width;
            private readonly Color _color;
            private readonly string _text;
            private readonly VariableVector _textBox;
            private readonly LineTextPlacement _lineTextPlacement;
            private readonly Font _textFont;
            private readonly Color _textColor;
            private readonly float _textPadding;
            private readonly double _textLocation;
            private readonly string _tooltip;
            private readonly string _name;

            internal ArrowBuilder(SimpleConstraintSolver solver, string name,
                              VariableVector tail, VariableVector head, double width, Color color,
                        string text, LineTextPlacement lineTextPlacement, Font textFont, Color textColor, double textPadding, double textLocation,
                        string tooltip, int drawingOrder, int fixingOrder) : base(drawingOrder, fixingOrder) {
                // Simple stuff
                _name = name;
                _solver = solver;
                _width = width;
                _color = color;
                _text = text;
                _lineTextPlacement = lineTextPlacement;
                _textFont = textFont;
                _textColor = textColor;
                _textPadding = (float)textPadding;
                _textLocation = textLocation;
                _tooltip = tooltip;

                // Vectors
                _tail = tail.AlsoNamed(_name + ".T");
                _head = head.AlsoNamed(_name + ".H");
                _textBox = new VariableVector(_name + ".TXT", _solver);
            }

            public override string ToString() {
                return "ArrowBuilder " + Name;
            }

            public string Name => _name;

            public VariableVector Tail => _tail;

            public VariableVector Head => _head;

            public IEnumerable<NumericVariable> GetFixVariables() {
                yield return _head.X;
                yield return _head.Y;
                yield return _tail.X;
                yield return _tail.Y;
            }

            public IEnumerable<VectorF> GetBoundingVectors() {
                yield return _tail.AsVectorF();
                yield return _head.AsVectorF();
            }

            public void FullyRestrictBoundingVectors(Graphics graphics) {
                // With LineTextPlacement.*Inclined, the _textbox set here is wrong. This is a "feature",
                // because it is hard to repair; and the usecases where another vector depends on the
                // rotated text are too rare to spend effort on them ...
                SizeF textSize = graphics.MeasureString(_text, _textFont);
                _textBox.Set(textSize.Width * (1 + _textPadding), textSize.Height * (1 + _textPadding));
            }

            public void Draw(Graphics graphics, StringBuilder htmlForTooltips) {
                VectorF tailF = _tail.AsVectorF();
                VectorF headF = _head.AsVectorF();
                VectorF textBoxF = _textBox.AsVectorF();
                float fWidth = (float)_width;

                PointF tailPoint = tailF.AsMirroredPointF();
                PointF headPoint = headF.AsMirroredPointF();
                if (tailPoint != headPoint) {
                    float absoluteArrowSize = Math.Min(10 * fWidth, (headF - tailF).Length() / 4);
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

                VectorF textCenterF = _textLocation >= 0
                    ? tailF * (1 - _textLocation) + headF * _textLocation
                    : tailF + (headF - tailF).Unit() * -_textLocation;
                float halfTextWidth = textBoxF.GetX() / 2;
                float lineAngleDegrees = (float)(-Math.Atan2(headF.GetY() - tailF.GetY(), headF.GetX() - tailF.GetX()) * 180 / Math.PI);

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
                        textTransform.RotateAt(lineAngleDegrees, textCenterF.AsMirroredPointF());
                        textTransform.Translate(-halfTextWidth, 0);
                        break;
                    case LineTextPlacement.CenterInclined:
                        textTransform.RotateAt(lineAngleDegrees, textCenterF.AsMirroredPointF());
                        break;
                    case LineTextPlacement.RightInclined:
                        textTransform.RotateAt(lineAngleDegrees, textCenterF.AsMirroredPointF());
                        textTransform.Translate(halfTextWidth, 0);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                // Move text away form line; needs improvement for steeply inclined lines
                textTransform.Translate(0, -textBoxF.GetY() / 2);
                DrawText(graphics, _text, _textFont, _textColor, textCenterF, textTransform);

                // TODO: Get all these elements somehow and then do a "draw tooltip" ...
            }
        }

        private readonly Font _defaultTextFont = new Font(FontFamily.GenericSansSerif, 10);
        private readonly List<IBuilder> _builders = new List<IBuilder>();

        private static void DrawText(Graphics graphics, string text, Font textFont, Color textColor, VectorF center, Matrix m) {
            StringFormat centered = new StringFormat { Alignment = StringAlignment.Center };
            var halfFontHeight = textFont.GetHeight() / 2;
            PointF position = (center + new VectorF(0, halfFontHeight, "font/2 " + halfFontHeight)).AsMirroredPointF();
            if (DEBUG) {
                graphics.FillEllipse(new SolidBrush(Color.Red), center.AsMirroredPointF().X - 3, center.AsMirroredPointF().Y - 3, 6, 6);
                graphics.DrawString(text, textFont, new SolidBrush(Color.LightGray), position, centered);
            }

            GraphicsContainer containerForTextPlacement = graphics.BeginContainer();

            graphics.MultiplyTransform(m);
            graphics.DrawString(text, textFont, new SolidBrush(textColor), position, centered);

            graphics.EndContainer(containerForTextPlacement);
        }

        public IBox Box([NotNull] VariableVector anchor, [CanBeNull] string text, [CanBeNull] VariableVector minDiagonal = null,
            BoxAnchoring boxAnchoring = BoxAnchoring.Center, [CanBeNull] Color? boxColor = null /*White*/, int connectors = 8,
            double borderWidth = 0, [CanBeNull] Color? borderColor = null /*Black*/,
            BoxTextPlacement boxTextPlacement = BoxTextPlacement.Center, [CanBeNull] Font textFont = null /*___*/, [CanBeNull] Color? textColor = null /*Black*/,
            double textPadding = 0.2, [CanBeNull] string tooltip = null, int drawingOrder = 0, int fixingOrder = 100, [CanBeNull] string name = null) {
            if (anchor == null) {
                throw new ArgumentNullException(nameof(anchor));
            }

            var boxBuilder = new BoxBuilder(_solver, anchor, new VariableVector((text ?? anchor.Name) + "./", _solver).Restrict(minDiagonal),
                boxAnchoring, boxColor ?? Color.White, borderWidth, borderColor ?? Color.Black, connectors,
                text ?? "", boxTextPlacement, textFont ?? _defaultTextFont, textColor ?? Color.Black, textPadding, tooltip ?? "", drawingOrder, fixingOrder, name);

            _builders.Add(boxBuilder);
            return boxBuilder;
        }

        public IArrow Arrow([NotNull] VariableVector tail, [NotNull] VariableVector head, double width, [CanBeNull] Color? color = null /*Black*/,
            [CanBeNull] string text = null, LineTextPlacement placement = LineTextPlacement.Center, [CanBeNull] Font textFont = null /*___*/,
            [CanBeNull] Color? textColor = null /*Black*/, double textPadding = 0.2, double textLocation = 0.5, [CanBeNull] string tooltip = null,
            int drawingOrder = 0, int fixingOrder = 200, string name = null) {
            if (tail == null) {
                throw new ArgumentNullException(nameof(tail));
            }
            if (head == null) {
                throw new ArgumentNullException(nameof(head));
            }
            var arrowBuilder = new ArrowBuilder(_solver, name ?? $"${tail.Name}->{head.Name}['{text}']", tail, head, width, color ?? Color.Black,
                text ?? "", placement, textFont ?? _defaultTextFont, textColor ?? Color.Black,
                textPadding, textLocation, tooltip, drawingOrder, fixingOrder);
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
                foreach (var b in _builders) {
                    b.FullyRestrictBoundingVectors(graphics);
                }

                try {
                    _solver.Solve();
                } catch (SolverException) {
                    Console.WriteLine(_solver.GetState());
                    throw;
                }

                graphics.Clear(GetBackGroundColor);

                float minX = float.MaxValue;
                float maxX = -float.MaxValue;
                float minY = float.MaxValue;
                float maxY = -float.MaxValue;

                StringBuilder errors = new StringBuilder();
                foreach (var b in _builders.OrderBy(b => b.DrawingOrder).ThenBy(b => b.CreationOrder)) {
                    foreach (var v in b.GetBoundingVectors()) {
                        float x = v.GetX();
                        if (float.IsInfinity(x)) {
                            errors.AppendLine($"Dimensions of {b.Name} not fully computed - no value for {v.Definition}.x");
                        } else {
                            minX = Math.Min(minX, x);
                            maxX = Math.Max(maxX, x);
                        }
                        float y = v.GetY();
                        if (float.IsInfinity(y)) {
                            errors.AppendLine($"Dimensions of {b.Name} not fully computed - no value for {v.Definition}.y");
                        } else {
                            minY = Math.Min(minY, y);
                            maxY = Math.Max(maxY, y);
                        }
                    }
                }
                if (errors.Length > 0) {
                    throw new InvalidOperationException(errors.ToString() + _solver.GetState());
                }

                StringBuilder htmlForTooltips = new StringBuilder();

                // 5% margin on all sides
                float BORDER = 0.1f;
                double scaleX = size.Width * (1 - 2 * BORDER) / (maxX - minX);
                double scaleY = size.Height * (1 - 2 * BORDER) / (maxY - minY);
                float scale = (float)Math.Min(scaleX, scaleY); // No distortion!

                graphics.Transform = new Matrix(scale, 0, 0, scale, (float)(-scale * minX + size.Width * BORDER),
                    (float)(scale * maxY + size.Height * BORDER));

                List<IBuilder> openBuilders = _builders.OrderBy(b => b.DrawingOrder).ThenBy(b => b.CreationOrder).ToList();

                foreach (var b in openBuilders.ToArray()) {
                    b.Draw(graphics, htmlForTooltips);
                }
            }

            //var f = new Font(FontFamily.GenericSansSerif, 10);
            //DrawText(graphics, "0|0", f, Color.Blue, C(0, 0), TextPlacing.Center);
            //DrawText(graphics, "C|0", f, Color.Blue, C(100, 0), TextPlacing.Center);
            //DrawText(graphics, "O|C", f, Color.Blue, C(0, 100), TextPlacing.Center);
            //DrawText(graphics, "C|C", f, Color.Blue, C(100, 100), TextPlacing.Center);
            return bitmap;
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

        protected SimpleConstraintSolver Solver => _solver;

        protected abstract void PlaceObjects(IEnumerable<TItem> items, IEnumerable<TDependency> dependencies);

        public abstract void CreateSomeTestItems(out IEnumerable<TItem> items, out IEnumerable<TDependency> dependencies);
    }

    public abstract class GraphicsDependencyRenderer : GraphicsRenderer<Item, Dependency>, IDependencyRenderer { }
}
