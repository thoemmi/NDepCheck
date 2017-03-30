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
        VariableVector Center {
            get;
        }

        VariableVector LowerLeft {
            get;
        }
        VariableVector CenterLeft {
            get;
        }
        VariableVector UpperLeft {
            get;
        }
        VariableVector CenterTop {
            get;
        }
        VariableVector UpperRight {
            get;
        }
        VariableVector CenterRight {
            get;
        }
        VariableVector LowerRight {
            get;
        }
        VariableVector CenterBottom {
            get;
        }

        VariableVector Diagonal {
            get;
        }
        VariableVector TextBox {
            get;
        }

        VariableVector GetBestConnector(VariableVector farAway);
    }

    public interface IArrow {
        VariableVector Head {
            get;
        }
        VariableVector Tail {
            get;
        }
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

        protected GraphicsRenderer() {
            _solver = new GraphicsRendererSolver(this);
        }

        public VariableVector F(double? x, double? y, string name = "C") {
            return new VariableVector(name, _solver, x, y);
        }

        public VariableVector B(string name, double interpolateMinMax = 0.0) {
            return new VariableVector(name, _solver, null, null);
        }

        private interface IBuilder {
            string Name {
                get;
            }
            int CreationOrder {
                get;
            }
            int DrawingOrder {
                get;
            }
            int FixingOrder {
                get;
            }
            IEnumerable<VectorF> GetBoundingVectors();
            void FullyRestrictBoundingVectors(Graphics graphics);
            float? GetTextSizeInPoints();
            void Draw(Graphics graphics, StringBuilder htmlForClicks);
            IEnumerable<NumericVariable> GetFixVariables();
        }

        private abstract class AbstractBuilder {
            protected AbstractBuilder(int creationOrder, int drawingOrder, int fixingOrder) {
                DrawingOrder = drawingOrder;
                FixingOrder = fixingOrder;
                CreationOrder = creationOrder;
            }

            public int DrawingOrder {
                get;
            }
            public int FixingOrder {
                get;
            }
            public int CreationOrder {
                get;
            }
        }

        private class BoxBuilder : AbstractBuilder, IBuilder, IBox {
            private readonly SimpleConstraintSolver _solver;
            private readonly VariableVector _anchor;
            [NotNull]
            private readonly VariableVector _diagonal;
            private readonly VariableVector _textBox;
            private readonly float _borderWidth;
            private readonly string[] _text;
            private readonly BoxTextPlacement _boxTextPlacement;
            private readonly Color _textColor;
            private readonly double _textPadding;
            [CanBeNull]
            private readonly string _htmlRef;
            private readonly Font _textFont;
            private readonly Color _borderColor;
            private readonly Color _color;
            private readonly double _sectorAngle;

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
                              int creationOrder, int drawingOrder, int fixingOrder,
                              string name, [CanBeNull] string htmlRef) : base(creationOrder, drawingOrder, fixingOrder) {
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
                _text = text.Split('\r', '\n');
                _boxTextPlacement = boxTextPlacement;
                _textColor = textColor;
                _textPadding = textPadding;
                _htmlRef = htmlRef;
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

            public string Name {
                get;
            }

            public void FullyRestrictBoundingVectors(Graphics graphics) {
                // Textbox is set here, because it is needed for restricting the Diagonal.

                SizeF size = ComputeTextSize(graphics);

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

            private SizeF ComputeTextSize(Graphics graphics) {
                float textWidth = 0;
                float textHeight = 0;
                foreach (var line in _text) {
                    SizeF lineSize = graphics.MeasureString(line, _textFont);
                    textWidth = Math.Max(textWidth, lineSize.Width);
                    textHeight += lineSize.Height;
                }
                return new SizeF(textWidth, textHeight);
            }

            public void Draw(Graphics graphics, StringBuilder htmlForClicks) {
                var diagonalF = _diagonal.AsVectorF();
                var textBoxF = _textBox.AsVectorF();
                var centerF = _center.AsVectorF();
                VectorF upperLeftF = _center.AsVectorF() - ~diagonalF / 2;

                FillBox(graphics, new SolidBrush(_borderColor), upperLeftF.GetX(), -upperLeftF.GetY(), diagonalF.GetX(), diagonalF.GetY());

                VectorF borderDiagonalF = new VectorF(_borderWidth, _borderWidth, "45°@" + _borderWidth);
                VectorF upperLeftInnerF = upperLeftF + ~borderDiagonalF;
                VectorF diagonalInnerF = diagonalF - 2 * borderDiagonalF;

                FillBox(graphics, new SolidBrush(_color), upperLeftInnerF.GetX(), -upperLeftInnerF.GetY(), diagonalInnerF.GetX(), diagonalInnerF.GetY());

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

                var lineHeight = new VectorF(0, _textFont.GetHeight() * 1.1f, "lineHeight");
                VectorF textLocation = centerF + new VectorF(0, lineHeight.GetY() / 2 * (_text.Length - 1), "textOffset");
                for (int i = 0; i < _text.Length; i++, textLocation -= lineHeight) {
                    DrawText(graphics, _text[i], _textFont, _textColor, textLocation, m);
                }

                if (_htmlRef != null) {
                    VectorF lowerRightF = _center.AsVectorF() + ~diagonalF / 2;
                    WriteBoxHtml(graphics, htmlForClicks, _htmlRef, upperLeftF.AsMirroredPointF(),
                        lowerRightF.AsMirroredPointF(), _text.Length > 0 ? _text[0] : "");
                }
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
                const double twoPi = 2 * Math.PI;
                return a - Math.Floor(a / twoPi) * twoPi;
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
                UnidirectionalComputationConstraint.CreateUnidirectionalComputationConstraint(input: new[] { farAway.X, farAway.Y, _center.X, _center.Y, _diagonal.X, _diagonal.Y },
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

            public float? GetTextSizeInPoints() {
                return _text.Any(t => !string.IsNullOrWhiteSpace(t)) ? _textFont.SizeInPoints : (float?)null;
            }

            private static void WriteBoxHtml(Graphics graphics, StringBuilder htmlForClicks,
                string htmlRef, PointF upperLeft, PointF lowerRight, string item) {
                PointF[] ulAndLr = { upperLeft, lowerRight };
                graphics.Transform.TransformPoints(ulAndLr);
                int lx = (int)ulAndLr[0].X;
                int ly = (int)ulAndLr[0].Y;
                int ux = (int)ulAndLr[1].X;
                int uy = (int)ulAndLr[1].Y;

                htmlForClicks.AppendLine($@"<!--{item}--><area shape=""rect"" coords=""{lx},{ly},{ux},{uy}"" href=""{htmlRef}"" />");
            }
        }

        private class ArrowBuilder : AbstractBuilder, IBuilder, IArrow {
            private readonly VariableVector _head;
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
            private readonly string _edgeInfo;
            private readonly string _name;

            internal ArrowBuilder(SimpleConstraintSolver solver, string name,
                              VariableVector tail, VariableVector head, double width, Color color,
                        string text, LineTextPlacement lineTextPlacement, Font textFont, Color textColor, double textPadding, double textLocation,
                        string edgeInfo, int creationOrder, int drawingOrder, int fixingOrder) : base(creationOrder, drawingOrder, fixingOrder) {
                // Simple stuff
                _name = name;
                _width = width;
                _color = color;
                _text = text;
                _lineTextPlacement = lineTextPlacement;
                _textFont = textFont;
                _textColor = textColor;
                _textPadding = (float)textPadding;
                _textLocation = textLocation;
                _edgeInfo = edgeInfo;

                // Vectors
                _tail = tail.AlsoNamed(_name + ".T");
                _head = head.AlsoNamed(_name + ".H");
                _textBox = new VariableVector(_name + ".TXT", solver);
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

            public void Draw(Graphics graphics, StringBuilder htmlForClicks) {
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

                if (!string.IsNullOrWhiteSpace(_edgeInfo)) {
                    WriteToolTipHtml(graphics, htmlForClicks, _edgeInfo, tailPoint, headPoint);
                }
            }

            private static void WriteToolTipHtml(Graphics graphics, StringBuilder htmlForClicks, string edgeInfo, PointF tailPoint, PointF headPoint) {
                PointF[] tailAndHead = { tailPoint, headPoint };
                graphics.Transform.TransformPoints(tailAndHead);
                int tx = (int)tailAndHead[0].X;
                int ty = (int)tailAndHead[0].Y;
                int hx = (int)tailAndHead[1].X;
                int hy = (int)tailAndHead[1].Y;
                edgeInfo = edgeInfo.Replace('\'', ' ').Replace('\"', ' ');
                const int d = 4;

                htmlForClicks.AppendLine(
                    $@"<!-- -->" +
                    $@"<area shape=""poly"" coords=""{tx},{ty - d},{tx},{ty + d},{hx},{hy + d},{hx},{hy - d}"" " +
                    $@"href=""default.htm"" onClick=""alert('{edgeInfo}');return false""/>");
            }


            public float? GetTextSizeInPoints() {
                return !string.IsNullOrWhiteSpace(_text) ? _textFont.SizeInPoints : (float?)null;
            }
        }

        private readonly Font _defaultTextFont = new Font(FontFamily.GenericSansSerif, 10);
        private readonly List<IBuilder> _builders = new List<IBuilder>();

        private static void DrawText(Graphics graphics, string text, Font textFont, Color textColor, VectorF textCenter, Matrix m) {
            StringFormat centered = new StringFormat { Alignment = StringAlignment.Center };
            float halfFontHeight = textFont.GetHeight() / 2;
            PointF position = (textCenter + new VectorF(0, halfFontHeight, "font/2 " + halfFontHeight)).AsMirroredPointF();
            if (DEBUG) {
                graphics.FillEllipse(new SolidBrush(Color.Red), textCenter.AsMirroredPointF().X - 3, textCenter.AsMirroredPointF().Y - 3, 6, 6);
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
            double textPadding = 0.2, int drawingOrder = 0, int fixingOrder = 100, [CanBeNull] string name = null, [CanBeNull] string htmlRef = null) {
            if (anchor == null) {
                throw new ArgumentNullException(nameof(anchor));
            }

            var boxBuilder = new BoxBuilder(_solver, anchor, new VariableVector((text ?? anchor.Name) + "./", _solver).Restrict(minDiagonal),
                boxAnchoring, boxColor ?? Color.White, borderWidth, borderColor ?? Color.Black, connectors,
                text ?? "", boxTextPlacement, textFont ?? _defaultTextFont, textColor ?? Color.Black, textPadding,
                _builders.Count, drawingOrder, fixingOrder, name, htmlRef);

            _builders.Add(boxBuilder);
            return boxBuilder;
        }

        public IArrow Arrow([NotNull] VariableVector tail, [NotNull] VariableVector head, double width, [CanBeNull] Color? color = null /*Black*/,
            [CanBeNull] string text = null, LineTextPlacement placement = LineTextPlacement.Center, [CanBeNull] Font textFont = null /*___*/,
            [CanBeNull] Color? textColor = null /*Black*/, double textPadding = 0.2, double textLocation = 0.5, [CanBeNull] string edgeInfo = null,
            int drawingOrder = 0, int fixingOrder = 200, string name = null) {
            if (tail == null) {
                throw new ArgumentNullException(nameof(tail));
            }
            if (head == null) {
                throw new ArgumentNullException(nameof(head));
            }
            var arrowBuilder = new ArrowBuilder(_solver, name ?? $"${tail.Name}->{head.Name}['{text}']", tail, head, width, color ?? Color.Black,
                text ?? "", placement, textFont ?? _defaultTextFont, textColor ?? Color.Black,
                textPadding, textLocation, edgeInfo, _builders.Count, drawingOrder, fixingOrder);
            _builders.Add(arrowBuilder);
            return arrowBuilder;
        }

        private Bitmap Render(IEnumerable<TDependency> dependencies, float minTextHeight,
                              ref int width, ref int height, StringBuilder htmlForClicks) {
            PlaceObjects(dependencies);

            // I tried it with SVG - but SVG support in .Net seems to be non-existent.
            // The library at https://github.com/managed-commons/SvgNet is a nice attempet (a 2015 resurrection of a 2003 attempt),
            // but it closes off the SVG objects in such a way that adding edgeInfos ("mouse hoverings") seems very hard.
            // If someone knows more about SVG than I (who doesn't know a bit ...), feel free to try it with SVG!

            // A helper Bitmap is used to measure strings; only afterwards can
            // we compute the actual width and height from the solved vectors.
            using (Graphics graphics = Graphics.FromImage(new Bitmap(1000, 1000))) {
                foreach (var b in _builders) {
                    b.FullyRestrictBoundingVectors(graphics);
                }
            }

            try {
                _solver.Solve();
            } catch (SolverException) {
                Console.WriteLine(_solver.GetState(maxLines: 20000));
                throw;
            }

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
                throw new InvalidOperationException(errors + _solver.GetState(maxLines: 20000));
            }

            // 5% margin on all sides
            const float BORDER = 0.1f;

            if (width <= 0) {
                if (height <= 0) {
                    float? smallestTextHeight = null;
                    foreach (var b in _builders) {
                        float? h = b.GetTextSizeInPoints();
                        if (h.HasValue) {
                            smallestTextHeight = smallestTextHeight.HasValue ? Math.Min(smallestTextHeight.Value, h.Value) : h.Value;
                        }
                    }
                    float textBasedScale = minTextHeight / smallestTextHeight ?? 1;
                    height = (int)(textBasedScale * (maxY - minY));
                    width = (int)(textBasedScale * (maxX - minX));
                } else {
                    width = (int)(height / (maxY - minY) * (maxX - minX));
                }
            } else {
                if (height <= 0) {
                    height = (int)(width / (maxX - minX) * (maxY - minY));
                } else {
                    // nothing to compute
                }
            }

            double scaleX = width * (1 - 2 * BORDER) / (maxX - minX);
            double scaleY = height * (1 - 2 * BORDER) / (maxY - minY);
            float scale = (float)Math.Min(scaleX, scaleY); // No distortion!

            var bitmap = new Bitmap(width, height);
            using (Graphics graphics = Graphics.FromImage(bitmap)) {
                graphics.Clear(GetBackGroundColor);

                graphics.Transform = new Matrix(scale, 0, 0, scale, -scale * minX + width * BORDER, scale * maxY + height * BORDER);

                List<IBuilder> openBuilders = _builders.OrderBy(b => b.DrawingOrder).ThenBy(b => b.CreationOrder).ToList();

                foreach (var b in openBuilders.ToArray()) {
                    b.Draw(graphics, htmlForClicks);
                }
            }
            return bitmap;
        }

        public virtual string Render(IEnumerable<TDependency> dependencies, string argsAsString, string baseFilename) {
            return DoRender(dependencies, argsAsString, baseFilename);
        }

        protected string DoRender(IEnumerable<TDependency> dependencies,
                                string argsAsString, string baseFilename, params OptionAction[] additionalOptions) {
            int width = -1;
            int height = -1;
            int minTextHeight = 15;
            if (baseFilename == null || GlobalContext.IsConsoleOutFileName(baseFilename)) {
                Options.Throw("Cannot write graphics file to Console.Out", argsAsString);
            }

            Options.Parse(argsAsString,
                new[] {
                new OptionAction('w', (args, j) => {
                    if (!int.TryParse(Options.ExtractOptionValue(args, ref j), out width)) {
                        Options.Throw("No valid width after w", args);
                    }
                    return j;
                }), new OptionAction('h', (args, j) => {
                    if (!int.TryParse(Options.ExtractOptionValue(args, ref j), out height)) {
                        Options.Throw("No valid height after h", args);
                    }
                    return j;
                }), new OptionAction('f', (args, j) => {
                    if (!int.TryParse(Options.ExtractOptionValue(args, ref j), out minTextHeight)) {
                        Options.Throw("No valid height after f", args);
                    }
                    return j;
                })}.Concat(additionalOptions).ToArray());


            StringBuilder htmlForClicks = new StringBuilder();
            Bitmap bitMap = Render(dependencies, minTextHeight, ref width, ref height, htmlForClicks);

            string gifFileName = Path.ChangeExtension(baseFilename, ".gif");
            try {
                Log.WriteInfo("Writing " + gifFileName);
                // ReSharper disable once AssignNullToNotNullAttribute -- baseFilename != null, thus gifFilename != null
                bitMap.Save(gifFileName, ImageFormat.Gif);
            } catch (Exception ex) {
                Log.WriteError("Cannot save GIF image to file " + gifFileName + ". Make sure the file can be written. Internal message: " + ex.Message);
                throw;
            }

            string htmlFileName = Path.ChangeExtension(baseFilename, ".html");
            // ReSharper disable once AssignNullToNotNullAttribute -- baseFilename != null, thus htmlFileName != null
            using (var tw = new StreamWriter(htmlFileName)) {
                Log.WriteInfo("Writing " + htmlFileName);
                tw.WriteLine($@"<!DOCTYPE html>
<html>
<body>
<img src=""{ Path.GetFileName(gifFileName)}"" width=""{width}"" height=""{height}"" usemap=""#map""/>
<map name=""map"">
{htmlForClicks}
</map>
</ body>
</ html>
");
            }
            return htmlFileName;
        }

        public void RenderToStreamForUnitTests(IEnumerable<TDependency> dependencies, Stream stream) {
            int width = 1000;
            int height = 1000;
            Bitmap bitMap = Render(dependencies, 15, ref width, ref height, htmlForClicks: null);

            bitMap.Save(stream, ImageFormat.Gif);
        }

        protected virtual Color GetBackGroundColor => Color.White;

        protected SimpleConstraintSolver Solver => _solver;

        protected abstract void PlaceObjects(IEnumerable<TDependency> dependencies);

        public abstract void CreateSomeTestItems(out IEnumerable<TItem> items, out IEnumerable<TDependency> dependencies);

        public abstract string GetHelp(bool detailedHelp);

        protected string GetHelpUsage() => "  [-w #] [-h #] [-f #]";
        protected string GetHelpExplanations() =>
@"    -w #          width of graphics in pixels; default: computed from height, or via minimal text size
    -h #          height of graphics in pixels; default: computed from width, or via minimal text size
    -f #          minimal textheight in points (if -w and -h are not provided); default: 15
    The fileName is used as a base name for writing a .gif file as well as an .html file, which contains
    the link and popup information.
";
    }

    public abstract class GraphicsDependencyRenderer : GraphicsRenderer<Item, Dependency>, IDependencyRenderer {
    }
}
