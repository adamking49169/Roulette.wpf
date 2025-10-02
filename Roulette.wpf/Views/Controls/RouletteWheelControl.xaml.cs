using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Roulette.Wpf.Views.Controls;

public partial class RouletteWheelControl : UserControl
{
    private static readonly int[] EuropeanOrder =
    {
        0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23,
        10, 5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26
    };
    private static readonly int[] RedNumbers =
   {
        1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36
    };


    private readonly double _slotAngle = 360.0 / 37.0;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };

    private double _wheelAngle;
    private double _wheelVel;
    private double _ballAngle;
    private double _ballVel;
    private double _ballRadius;
    private double _ballRadVel;
    private bool _captured;
    private bool _spinning;

    private int _targetNumber;
    private int _targetIndex;
    private double _targetPocketAngle;

    private const double CanvasSize = 700.0;
    private const double WheelSize = 620.0;
    private readonly Point _center = new(CanvasSize / 2.0, CanvasSize / 2.0);

    private const double BallOuterRadius = 300;
    private const double PocketCenterRadius = 240;
    private const double PocketInnerRadius = 210;
    private const double DividerInnerRadius = 165;
    private const double BallRestRadius = PocketInnerRadius + 16;
    public RouletteWheelControl()
    {
        InitializeComponent();
        BuildPockets();
        PlaceBallAt(_center.X, _center.Y - BallOuterRadius);
        _timer.Tick += OnTick;
    }

    public bool IsSpinning => _spinning;

    public event EventHandler? SpinCompleted;

    public void Reset()
    {
        _timer.Stop();
        _spinning = false;
        _captured = false;
        _wheelVel = 0;
        _ballVel = 0;
        _ballRadVel = 0;
        _ballRadius = BallOuterRadius;
        MoveBall(_ballAngle, _ballRadius);
    }

    public void SpinToNumber(int number)
    {
        if (_spinning)
            return;

        _spinning = true;
        _captured = false;

        _targetNumber = number;
        _targetIndex = Array.IndexOf(EuropeanOrder, _targetNumber);
        if (_targetIndex < 0) _targetIndex = 0;
        _targetPocketAngle = PocketAngleForIndex(_targetIndex);

        _wheelAngle = NormalizeDeg(_wheelAngle);
        _wheelVel = 650;
        _ballAngle = _wheelAngle - 90 + 180;
        _ballVel = -1400;
        _ballRadius = BallOuterRadius;
        _ballRadVel = 0;

        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        double dt = _timer.Interval.TotalSeconds;

        const double wheelFric = 180;
        const double ballFric = 280;

        _wheelVel = ApproachZero(_wheelVel, wheelFric * dt);
        _ballVel = ApproachZero(_ballVel, ballFric * dt);

        _wheelAngle += _wheelVel * dt;
        _ballAngle += _ballVel * dt;

        if (Math.Abs(_ballVel) < 520 && _ballRadius > PocketCenterRadius + 6 && !_captured)
        {
            _ballRadVel = -60;
        }

        _ballRadius += _ballRadVel * dt;
        double minRadius = _captured ? BallRestRadius : PocketCenterRadius;
        if (_ballRadius < minRadius)
        {
            _ballRadius = minRadius;
            if (_captured)
            {
                _ballRadVel = 0;
            }
        }

        if (!_captured && Math.Abs(_ballVel) < 120 && Math.Abs(_wheelVel) < 160 && _ballRadius <= PocketCenterRadius + 1)
        {
            double targetGlobal = NormalizeDeg(_wheelAngle + _targetPocketAngle);
            double diff = ShortestDeltaDeg(_ballAngle, targetGlobal);
            if (Math.Abs(diff) < 7.0)
            {
                _captured = true;
                _ballAngle = targetGlobal;
                _ballVel = 0;
                _ballRadVel = -80;
            }
            else
            {
                double nudge = Clamp(diff, -80 * dt, 80 * dt);
                _ballAngle = NormalizeDeg(_ballAngle + nudge);
            }
        }

        if (_captured && _ballRadius > BallRestRadius)
        {
            _ballRadVel = -120;
        }
        if (_captured && _ballRadius <= BallRestRadius)
        {
            _ballRadius = BallRestRadius;
            _wheelVel = 0;
            _ballRadVel = 0;
            _ballVel = 0;
            _timer.Stop();
            _spinning = false;
            SpinCompleted?.Invoke(this, EventArgs.Empty);
        }
        if (_captured)
        {
            _ballAngle = NormalizeDeg(_wheelAngle + _targetPocketAngle);
        }
        if (_captured && Math.Abs(_wheelVel) < 1e-3 && Math.Abs(_ballRadVel) < 1e-3 && _timer.IsEnabled)
        {
            _timer.Stop();
            _spinning = false;
            SpinCompleted?.Invoke(this, EventArgs.Empty);
        }

        WheelRotate.Angle = _wheelAngle;
        MoveBall(_ballAngle, _ballRadius);
    }

    private void BuildPockets()
    {
        PocketsLayer.Children.Clear();

        double cx = WheelSize / 2.0;
        double cy = WheelSize / 2.0;

        double slotInnerRadius = PocketInnerRadius + 4;
        double slotOuterRadius = PocketInnerRadius + 28;
        double slotMargin = _slotAngle * 0.18;

        for (int i = 0; i < EuropeanOrder.Length; i++)
        {
            int num = EuropeanOrder[i];
            string col = PocketBrushKey(num);
            double startAngle = i * _slotAngle - 90;
            double endAngle = (i + 1) * _slotAngle - 90;

            var slice = CreateSlice(cx, cy, PocketInnerRadius, PocketCenterRadius + 32, startAngle, endAngle);
            slice.Fill = FindResourceOrDefault(col, new SolidColorBrush(ColorFromHex("#d32f2f")));
            slice.Stroke = SlotStrokeForNumber(num);
            slice.StrokeThickness = 1.2;
            slice.Opacity = 0.96;
            PocketsLayer.Children.Add(slice);

            var sliceGloss = CreateSlice(cx, cy, PocketCenterRadius + 18, PocketCenterRadius + 32, startAngle + slotMargin * 0.35, endAngle - slotMargin * 0.35);
            sliceGloss.Fill = CreateGlossOverlayBrush();
            sliceGloss.IsHitTestVisible = false;
            PocketsLayer.Children.Add(sliceGloss);


            var slot = CreateSlice(cx, cy, slotInnerRadius, slotOuterRadius, startAngle + slotMargin, endAngle - slotMargin);
            slot.Fill = SlotBrushForNumber(num);
            slot.Stroke = SlotStrokeForNumber(num);
            slot.StrokeThickness = 0.8;
            slot.Opacity = 0.9;
            PocketsLayer.Children.Add(slot);

            double highlightMargin = slotMargin * 0.6;
            var slotGloss = CreateSlice(cx, cy, slotOuterRadius - 6, slotOuterRadius, startAngle + slotMargin + highlightMargin, endAngle - slotMargin - highlightMargin);
            slotGloss.Fill = CreateGlossOverlayBrush(0.85);
            slotGloss.IsHitTestVisible = false;
            PocketsLayer.Children.Add(slotGloss);

            var divider = CreateRadialLine(cx, cy, DividerInnerRadius, PocketCenterRadius + 34, startAngle);
            divider.Stroke = new SolidColorBrush(ColorFromHex("#44000000"));
            divider.StrokeThickness = 1.6;
            divider.StrokeStartLineCap = PenLineCap.Round;
            divider.StrokeEndLineCap = PenLineCap.Round;
            PocketsLayer.Children.Add(divider);

            double midAngle = (i + 0.5) * _slotAngle - 90;
            var (tx, ty) = Polar(cx, cy, PocketCenterRadius + 10, midAngle);
            var tb = new TextBlock
            {
                Text = num.ToString(),
                Foreground = new SolidColorBrush(ColorFromHex("#ffe6c6")),
                FontWeight = FontWeights.Bold,
                FontSize = 18,
                RenderTransform = new RotateTransform(midAngle + 90, 0, 0),
                Effect = new DropShadowEffect
                {
                    Color = ColorFromHex("#88000000"),
                    BlurRadius = 8,
                    ShadowDepth = 1.6,
                    Direction = 320,
                    Opacity = 0.8
                }
            };
            tb.RenderTransformOrigin = new Point(0.5, 0.5);
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, tx - tb.DesiredSize.Width / 2);
            Canvas.SetTop(tb, ty - tb.DesiredSize.Height / 2);
            PocketsLayer.Children.Add(tb);
        }

        const int deflectorCount = 12;
        double deflectorRadius = PocketInnerRadius - 22;
        var deflectorFallback = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(ColorFromHex("#f3f6f8"), 0),
                new GradientStop(ColorFromHex("#c2c7cc"), 0.45),
                new GradientStop(ColorFromHex("#5a6065"), 1)
            }
        };
        var boltFallback = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.45, 0.4),
            RadiusX = 0.9,
            RadiusY = 0.9,
            GradientStops = new GradientStopCollection
            {
                new GradientStop(ColorFromHex("#ffffff"), 0),
                new GradientStop(ColorFromHex("#c6cbd1"), 0.55),
                new GradientStop(ColorFromHex("#6a7076"), 1)
            }
        };

        Brush deflectorBrush = CloneResourceBrush("MetallicBrush", deflectorFallback);
        Brush boltBrush = CloneResourceBrush("HubCapBrush", boltFallback);

        for (int d = 0; d < deflectorCount; d++)
        {
            double placeAngle = d * (360.0 / deflectorCount) - 90.0;

            var deflector = new Rectangle
            {
                Width = 12,
                Height = 36,
                RadiusX = 4,
                RadiusY = 4,
                Fill = deflectorBrush.CloneCurrentValue(),
                Stroke = new SolidColorBrush(ColorFromHex("#2c3033")),
                StrokeThickness = 0.8,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(placeAngle + 90)
            };

            var (px, py) = Polar(cx, cy, deflectorRadius, placeAngle);
            Canvas.SetLeft(deflector, px - deflector.Width / 2);
            Canvas.SetTop(deflector, py - deflector.Height / 2);
            PocketsLayer.Children.Add(deflector);

            var bolt = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = boltBrush.CloneCurrentValue(),
                Stroke = new SolidColorBrush(ColorFromHex("#3b3f44")),
                StrokeThickness = 0.6
            };

            var (bx, by) = Polar(cx, cy, deflectorRadius - 16, placeAngle);
            Canvas.SetLeft(bolt, bx - bolt.Width / 2);
            Canvas.SetTop(bolt, by - bolt.Height / 2);
            PocketsLayer.Children.Add(bolt);
        }


        var ring = new Ellipse
        {
            Width = PocketCenterRadius * 2 + 4,
            Height = PocketCenterRadius * 2 + 4,
            Stroke = Brushes.White,
            StrokeThickness = 1.5
        };
        Canvas.SetLeft(ring, (WheelSize - ring.Width) / 2);
        Canvas.SetTop(ring, (WheelSize - ring.Height) / 2);
        PocketsLayer.Children.Add(ring);
    }

    private static Path CreateSlice(double cx, double cy, double rInner, double rOuter, double startDeg, double endDeg)
    {
        double s = Deg2Rad(startDeg);
        double e = Deg2Rad(endDeg);
        Point p1 = new(cx + rOuter * Math.Cos(s), cy + rOuter * Math.Sin(s));
        Point p2 = new(cx + rOuter * Math.Cos(e), cy + rOuter * Math.Sin(e));
        Point p3 = new(cx + rInner * Math.Cos(e), cy + rInner * Math.Sin(e));
        Point p4 = new(cx + rInner * Math.Cos(s), cy + rInner * Math.Sin(s));

        bool largeArc = (endDeg - startDeg) > 180;

        var fig = new PathFigure { StartPoint = p1, IsClosed = true, IsFilled = true };
        fig.Segments.Add(new ArcSegment(p2, new Size(rOuter, rOuter), 0, largeArc, SweepDirection.Clockwise, true));
        fig.Segments.Add(new LineSegment(p3, true));
        fig.Segments.Add(new ArcSegment(p4, new Size(rInner, rInner), 0, largeArc, SweepDirection.Counterclockwise, true));

        return new Path { Data = new PathGeometry { Figures = { fig } } };
    }
    private static Line CreateRadialLine(double cx, double cy, double rInner, double rOuter, double angleDeg)
    {
        var (x1, y1) = Polar(cx, cy, rInner, angleDeg);
        var (x2, y2) = Polar(cx, cy, rOuter, angleDeg);
        return new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2
        };
    }
    private static (double x, double y) Polar(double cx, double cy, double r, double deg)
    {
        double rad = Deg2Rad(deg);
        return (cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }

    private void MoveBall(double angleDeg, double radius)
    {
        var (x, y) = Polar(_center.X, _center.Y, radius, angleDeg);
        Canvas.SetLeft(Ball, x - Ball.Width / 2.0);
        Canvas.SetTop(Ball, y - Ball.Height / 2.0);
    }

    private void PlaceBallAt(double x, double y)
    {
        Canvas.SetLeft(Ball, x - Ball.Width / 2.0);
        Canvas.SetTop(Ball, y - Ball.Height / 2.0);
    }

    private double PocketAngleForIndex(int index) => (index + 0.5) * _slotAngle - 90.0;

    private static double Deg2Rad(double d) => d * Math.PI / 180.0;

    private static double NormalizeDeg(double d)
    {
        d %= 360.0;
        if (d < 0) d += 360.0;
        return d;
    }

    private static double ShortestDeltaDeg(double a, double b)
    {
        double d = NormalizeDeg(b) - NormalizeDeg(a);
        if (d > 180) d -= 360;
        if (d < -180) d += 360;
        return d;
    }

    private static double Clamp(double v, double min, double max) => Math.Min(Math.Max(v, min), max);

    private static double ApproachZero(double v, double step)
    {
        if (v > 0)
        {
            v -= step;
            if (v < 0) v = 0;
        }
        else if (v < 0)
        {
            v += step;
            if (v > 0) v = 0;
        }
        return v;
    }
    private static bool IsRed(int number) => RedNumbers.Contains(number);
    private static string PocketBrushKey(int number)
    {
        if (number == 0) return "RouletteGreen";
        return IsRed(number) ? "RouletteRed" : "RouletteBlack";
    }

    private Brush FindResourceOrDefault(string key, Brush fallback)
    {
        if (TryFindResource(key) is Brush brush)
        {
            return brush;
        }

        return key switch
        {
            "RouletteRed" => new SolidColorBrush(ColorFromHex("#c62828")),
            "RouletteBlack" => new SolidColorBrush(ColorFromHex("#111111")),
            "RouletteGreen" => new SolidColorBrush(ColorFromHex("#0f9d58")),
            _ => fallback
        };
    }

    private Brush CloneResourceBrush(string key, Brush fallback)
    {
        var brush = FindResourceOrDefault(key, fallback);
        return brush.CloneCurrentValue();
    }

    private Brush SlotBrushForNumber(int number)
    {
        if (number == 0)
        {
            return CreateSlotGradient(
                ColorFromHex("#d8ffe9"),
                ColorFromHex("#1cc16a"),
                ColorFromHex("#084f2d"));
        }

        if (IsRed(number))
        {
            return CreateSlotGradient(
                ColorFromHex("#ffe3e3"),
                ColorFromHex("#d63a3a"),
                ColorFromHex("#4f0f0f"));
        }

        return CreateSlotGradient(
            ColorFromHex("#f3f3f3"),
            ColorFromHex("#3a3a3a"),
            ColorFromHex("#060606"));
    }

    private Brush SlotStrokeForNumber(int number)
    {
        if (number == 0)
        {
            return CreateStrokeGradient(
                ColorFromHex("#c9fce3"),
                ColorFromHex("#13854a"),
                ColorFromHex("#00311d"));
        }

        if (IsRed(number))
        {
            return CreateStrokeGradient(
                ColorFromHex("#ffd9d9"),
                ColorFromHex("#a72121"),
                ColorFromHex("#2c0404"));
        }

        return CreateStrokeGradient(
            ColorFromHex("#e8e8e8"),
            ColorFromHex("#1f1f1f"),
            ColorFromHex("#000000"));
    }

    private static LinearGradientBrush CreateSlotGradient(Color highlight, Color mid, Color shadow)
    {
        return new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(highlight, 0),
                new GradientStop(mid, 0.38),
                new GradientStop(shadow, 1)
            }
        };
    }

    private static LinearGradientBrush CreateStrokeGradient(Color highlight, Color mid, Color shadow)
    {
        return new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(highlight, 0),
                new GradientStop(mid, 0.45),
                new GradientStop(shadow, 1)
            }
        };
    }

    private static LinearGradientBrush CreateGlossOverlayBrush(double intensity = 0.7)
    {
        byte alpha = (byte)Math.Round(255 * intensity);
        return new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(alpha, 255, 255, 255), 0),
                new GradientStop(Color.FromArgb((byte)(alpha * 0.35), 255, 255, 255), 0.55),
                new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
            }
        };
    }
    private static Color ColorFromHex(string hex) => (Color)ColorConverter.ConvertFromString(hex);
}