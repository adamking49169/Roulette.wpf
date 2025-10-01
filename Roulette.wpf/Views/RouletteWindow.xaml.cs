using MahApps.Metro.Controls;
using RouletteCore = Roulette.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Roulette.Wpf.Views
{
    public partial class RouletteWindow : MetroWindow
    {
        // ----- High-level timing/feel -----
        private const double SPIN_DURATION_SECONDS = 9.25;    // total animation time
        private const double WHEEL_EXTRA_REVOLUTIONS = 6.75;   // extra clockwise laps before landing
        private const double BALL_RELATIVE_REVOLUTIONS = 11.5; // how far ahead the ball starts (counter-clockwise)
        private const double SECTOR_DEGREES = 360.0 / 37.0;
        private const double OFFSET_DEGREES = 0.0;             // PNG alignment tweak if required
        private const double WHEEL_CENTER = 180.0;             // half the rendered wheel diameter
        private const double NUMBER_RING_RADIUS = 146.0;       // where we place the number pips around the wheel
        private const double NUMBER_LABEL_SIZE = 34.0;

        // European wheel ordering, clockwise from 0 (at the marker)
        private static readonly int[] WheelOrder =
        {
            0,32,15,19,4,21,2,25,17,34,6,27,13,36,11,30,8,23,
            10,5,24,16,33,1,20,14,31,9,22,18,29,7,28,12,35,3,26
        };

        private static readonly SolidColorBrush WheelRedBrush = CreateWheelBrush(177, 44, 44);
        private static readonly SolidColorBrush WheelBlackBrush = CreateWheelBrush(34, 34, 34);
        private static readonly SolidColorBrush WheelGreenBrush = CreateWheelBrush(14, 122, 58);

        public static IReadOnlyList<WheelLabel> WheelLabels { get; } = BuildWheelLabels();

        // View-model + sounds
        private ViewModels.GameViewModel? _vm;
        private SoundPlayer? _clickA, _clickB, _drop;
        private bool _useA = true;

        // Spin state
        private SpinAnimationPlan? _activeSpin;
        private DateTime _spinStartUtc;
        private double _currentWheelAngle; // persists between spins so consecutive spins feel continuous
        private DispatcherTimer? _tickTimer;

        public RouletteWindow()
        {
            InitializeComponent();

            HookViewModel();
            DataContextChanged += (_, __) => HookViewModel();
            Unloaded += (_, __) => UnhookViewModel();

            TryLoadSounds();
        }

        private void HookViewModel()
        {
            UnhookViewModel();
            _vm = DataContext as ViewModels.GameViewModel;
            if (_vm is null) return;
            _vm.SpinRequested += AnimateToNumber;
        }

        private void UnhookViewModel()
        {
            if (_vm is null) return;
            _vm.SpinRequested -= AnimateToNumber;
            _vm = null;
        }

        // ----- Spin orchestration -----
        private void AnimateToNumber(int number)
        {
            _activeSpin = BuildPlan(number);
            _spinStartUtc = DateTime.UtcNow;

            StartTickTimer();

            CompositionTarget.Rendering -= OnRenderSpin;
            CompositionTarget.Rendering += OnRenderSpin;
        }

        private void OnRenderSpin(object? sender, EventArgs e)
        {
            if (_activeSpin is not SpinAnimationPlan plan) return;

            double elapsed = (DateTime.UtcNow - _spinStartUtc).TotalSeconds;
            double duration = plan.DurationSeconds;
            double rawProgress = Math.Clamp(elapsed / duration, 0.0, 1.0);

            // The wheel eases out over time (fast start, gentle stop).
            double wheelProgress = EaseOutQuint(rawProgress);
            double wheelAngle = Lerp(plan.WheelStartAngle, plan.WheelEndAngle, wheelProgress);
            WheelRotate.Angle = wheelAngle;

            // The ball starts far ahead (counter-rotating), gradually loses speed and syncs with the wheel.
            double ballProgress = EaseInOutQuint(rawProgress);
            double relativeAngle = Lerp(plan.BallRelativeStartAngle, plan.BallRelativeEndAngle, ballProgress);
            double ballAngle = wheelAngle + relativeAngle;
            BallRotate.Angle = ballAngle;

            if (rawProgress >= 1.0)
            {
                FinishSpin(plan);
            }
        }

        private void FinishSpin(SpinAnimationPlan plan)
        {
            CompositionTarget.Rendering -= OnRenderSpin;
            StopTickTimer();

            _currentWheelAngle = NormalizeDegrees(plan.WheelEndAngle);
            WheelRotate.Angle = _currentWheelAngle;
            BallRotate.Angle = 0; // ball rests at the marker

            PlayDrop();
            BallBounce();

            _activeSpin = null;
            _vm?.CommitPendingSpin();
        }

        private SpinAnimationPlan BuildPlan(int number)
        {
            double startWheel = _currentWheelAngle;

            int index = Array.IndexOf(WheelOrder, number);
            if (index < 0) index = 0;
            double targetAngleAtMarker = OFFSET_DEGREES + index * SECTOR_DEGREES;

            double currentMod = Mod(startWheel, 360.0);
            double deltaToTarget = targetAngleAtMarker - currentMod;
            if (deltaToTarget <= 0) deltaToTarget += 360.0;

            double wheelEnd = startWheel + WHEEL_EXTRA_REVOLUTIONS * 360.0 + deltaToTarget;

            // The ball is animated relative to the wheel.  It begins many laps ahead (counter-clockwise) and
            // bleeds energy until it lines up with the marker exactly when the wheel reaches its final pose.
            double relativeStart = -startWheel - BALL_RELATIVE_REVOLUTIONS * 360.0;
            double relativeEnd = -wheelEnd;

            return new SpinAnimationPlan(startWheel, wheelEnd, relativeStart, relativeEnd, SPIN_DURATION_SECONDS);
        }

        private static IReadOnlyList<WheelLabel> BuildWheelLabels()
        {
            var labels = new List<WheelLabel>(WheelOrder.Length);
            double halfLabel = NUMBER_LABEL_SIZE / 2.0;

            for (int i = 0; i < WheelOrder.Length; i++)
            {
                int number = WheelOrder[i];
                double angle = OFFSET_DEGREES + i * SECTOR_DEGREES;
                double radians = angle * Math.PI / 180.0;

                double centerX = WHEEL_CENTER + Math.Sin(radians) * NUMBER_RING_RADIUS;
                double centerY = WHEEL_CENTER - Math.Cos(radians) * NUMBER_RING_RADIUS;

                Brush background = number == 0
                    ? WheelGreenBrush
                    : (RouletteCore.Wheel.GetColor(number) == RouletteCore.Color.Red ? WheelRedBrush : WheelBlackBrush);

                labels.Add(new WheelLabel(number,
                    centerX - halfLabel,
                    centerY - halfLabel,
                    angle,
                    background));
            }

            return labels;
        }

        private static SolidColorBrush CreateWheelBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        // Small bounce when the ball "drops" into the pocket.
        private void BallBounce()
        {
            var start = Ball.Margin;
            var down = new Thickness(start.Left, start.Top + 3, start.Right, start.Bottom);

            var animation = new System.Windows.Media.Animation.ThicknessAnimation
            {
                From = start,
                To = down,
                Duration = TimeSpan.FromMilliseconds(110),
                AutoReverse = true,
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                }
            };

            Ball.BeginAnimation(MarginProperty, animation);
        }

        // ----- Sound ticking -----
        private void StartTickTimer()
        {
            _tickTimer?.Stop();
            _tickTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(90)
            };
            _tickTimer.Tick += TickTimerOnTick;
            _tickTimer.Start();
        }

        private void StopTickTimer()
        {
            if (_tickTimer == null) return;
            _tickTimer.Stop();
            _tickTimer.Tick -= TickTimerOnTick;
            _tickTimer = null;
        }

        private void TickTimerOnTick(object? sender, EventArgs e)
        {
            if (_activeSpin is not SpinAnimationPlan plan)
            {
                StopTickTimer();
                return;
            }

            double elapsed = (DateTime.UtcNow - _spinStartUtc).TotalSeconds;
            double progress = Math.Clamp(elapsed / plan.DurationSeconds, 0.0, 1.0);

            // Ease the tick spacing so the impacts slow down as we approach the end of the spin.
            double eased = EaseOutCubic(progress);
            double intervalMs = 85 + eased * 150; // from ~85ms up to ~235ms
            if (_tickTimer != null)
            {
                _tickTimer.Interval = TimeSpan.FromMilliseconds(intervalMs);
            }

            PlayClick();

            if (progress >= 1.0)
            {
                StopTickTimer();
            }
        }

        // ----- Sounds -----
        private void TryLoadSounds()
        {
            try
            {
                _clickA = LoadWav("pack://application:,,,/Roulette.Wpf;component/Assets/Sounds/click.wav");
                _clickB = LoadWav("pack://application:,,,/Roulette.Wpf;component/Assets/Sounds/click.wav");
                _drop = LoadWav("pack://application:,,,/Roulette.Wpf;component/Assets/Sounds/drop.wav");
            }
            catch
            {
                _clickA = _clickB = _drop = null;
            }
        }

        private static SoundPlayer LoadWav(string packUri)
        {
            var sri = Application.GetResourceStream(new Uri(packUri));
            if (sri == null) throw new FileNotFoundException(packUri);
            using var stream = sri.Stream;
            var memory = new MemoryStream();
            stream.CopyTo(memory);
            memory.Position = 0;
            return new SoundPlayer(memory);
        }

        private void PlayClick()
        {
            var player = _useA ? _clickA : _clickB;
            _useA = !_useA;
            try { player?.Play(); }
            catch { SystemSounds.Asterisk.Play(); }
        }

        private void PlayDrop()
        {
            try { (_drop ?? SystemSounds.Exclamation).Play(); }
            catch { SystemSounds.Exclamation.Play(); }
        }

        // ----- Helpers -----
        private static double Mod(double value, double modulus) => (value % modulus + modulus) % modulus;
        private static double NormalizeDegrees(double value)
        {
            value %= 360.0;
            if (value < 0) value += 360.0;
            return value;
        }
        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private static double EaseOutQuint(double x)
        {
            x = Math.Clamp(x, 0, 1);
            return 1 - Math.Pow(1 - x, 5);
        }

        private static double EaseOutCubic(double x)
        {
            x = Math.Clamp(x, 0, 1);
            return 1 - Math.Pow(1 - x, 3);
        }

        private static double EaseInOutQuint(double x)
        {
            x = Math.Clamp(x, 0, 1);
            return x < 0.5
                ? 16 * Math.Pow(x, 5)
                : 1 - Math.Pow(-2 * x + 2, 5) / 2;
        }

        private readonly struct SpinAnimationPlan
        {
            public SpinAnimationPlan(double wheelStart, double wheelEnd, double relativeStart, double relativeEnd, double durationSeconds)
            {
                WheelStartAngle = wheelStart;
                WheelEndAngle = wheelEnd;
                BallRelativeStartAngle = relativeStart;
                BallRelativeEndAngle = relativeEnd;
                DurationSeconds = durationSeconds;
            }

            public double WheelStartAngle { get; }
            public double WheelEndAngle { get; }
            public double BallRelativeStartAngle { get; }
            public double BallRelativeEndAngle { get; }
            public double DurationSeconds { get; }
        }

        public sealed class WheelLabel
        {
            public WheelLabel(int number, double left, double top, double angle, Brush background)
            {
                Number = number;
                Left = left;
                Top = top;
                Angle = angle;
                Background = background;
            }

            public int Number { get; }
            public double Left { get; }
            public double Top { get; }
            public double Angle { get; }
            public double TextAngle => -Angle;
            public Brush Background { get; }
        }
    }
}
