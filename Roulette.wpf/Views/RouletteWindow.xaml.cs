using MahApps.Metro.Controls;
using System;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Roulette.Wpf.Views
{
    public partial class RouletteWindow : MetroWindow
    {
        // ----- Wheel / ball parameters -----
        private const double SPIN_SECONDS = 8.0;      // total spin time
        private const int WHEEL_REVS = 9;        // visual full turns (clockwise)
        private const int BALL_REVS = 6;        // visual full turns CCW before settling
        private const double SECTOR_DEGREES = 360.0 / 37.0;
        private const double OFFSET_DEGREES = 0.0;      // nudge if your PNG's "0" isn't at the marker

        // European wheel order, clockwise from 0
        private static readonly int[] WheelOrder =
        {
            0,32,15,19,4,21,2,25,17,34,6,27,13,36,11,30,8,23,
            10,5,24,16,33,1,20,14,31,9,22,18,29,7,28,12,35,3,26
        };

        // state
        private double _startAngle;   // wheel angle at spin start
        private double _endAngle;     // wheel angle at spin end
        private double _currentAngle; // wheel angle we keep after spin
        private DateTime _spinStart;
        private bool _spinning;

        // VM + sounds
        private ViewModels.GameViewModel? _vm;
        private SoundPlayer? _clickA, _clickB, _drop;
        private bool _useA = true;
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

        // ----- Spin orchestration (render loop) -----
        private void AnimateToNumber(int number)
        {
            // compute target wheel angle so 'number' lands under the top marker
            int idx = Array.IndexOf(WheelOrder, number);
            if (idx < 0) idx = 0;

            double targetAngleAtMarker = OFFSET_DEGREES + idx * SECTOR_DEGREES;

            // align from our current angle to that target with several full clockwise turns
            _startAngle = _currentAngle;
            // how far clockwise from currentMod to target in [0,360)
            double currentMod = Mod(_startAngle, 360.0);
            double delta = targetAngleAtMarker - currentMod;
            if (delta < 0) delta += 360.0;

            _endAngle = _startAngle + WHEEL_REVS * 360.0 + delta;

            // start timing
            _spinStart = DateTime.UtcNow;
            _spinning = true;

            // start SFX ticking
            StartTickTimer();

            // drive both wheel and ball with the same render tick
            CompositionTarget.Rendering -= OnRenderSpin;
            CompositionTarget.Rendering += OnRenderSpin;
        }

        private void OnRenderSpin(object? sender, EventArgs e)
        {
            if (!_spinning) return;

            double t = (DateTime.UtcNow - _spinStart).TotalSeconds;
            double p = Math.Clamp(t / SPIN_SECONDS, 0.0, 1.0);

            // Easing for the wheel (quintic ease out)
            double ew = EaseOutQuint(p);
            double wheelAngle = Lerp(_startAngle, _endAngle, ew);
            WheelRotate.Angle = wheelAngle;

            // Ball angle: start with BALL_REVS CCW turns, decelerate and land at marker (angle 0)
            // Use a slightly different curve so the ball feels lighter (cubic ease out towards ~6s, then in)
            // We'll combine two phases via one smooth curve: fast at start, gently to 0.
            double eb = EaseOutCubic(p);            // decelerate early
            double ballTurns = (1.0 - eb) * BALL_REVS;   // remaining turns to go
            double ballAngle = -ballTurns * 360.0;       // negative = CCW from the top marker
            BallRotate.Angle = ballAngle;

            // end of spin
            if (p >= 1.0)
            {
                _spinning = false;
                CompositionTarget.Rendering -= OnRenderSpin;

                // lock final angles
                _currentAngle = _endAngle;
                WheelRotate.Angle = _currentAngle;
                BallRotate.Angle = 0; // ball at the marker

                StopTickTimer();
                PlayDrop();
                BallBounce();

                // commit logical spin result/payout
                _vm?.CommitPendingSpin();
            }
        }

        // Small visual bounce on "drop"
        private void BallBounce()
        {
            // A tiny scale/translate using a transient animation (doesn't affect the circular path during spin)
            // We momentarily nudge the Ball element down and back up by adjusting Margin.Top.
            var start = Ball.Margin;
            var down = new Thickness(start.Left, start.Top + 3, start.Right, start.Bottom);

            var a = new System.Windows.Media.Animation.ThicknessAnimation
            {
                From = start,
                To = down,
                Duration = TimeSpan.FromMilliseconds(100),
                AutoReverse = true,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Ball.BeginAnimation(MarginProperty, a);
        }

        // ----- Sounds -----
        private void StartTickTimer()
        {
            _tickTimer?.Stop();
            _tickTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(85) // fast at start
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
            // slow the ticking as we approach the end
            var elapsed = (DateTime.UtcNow - _spinStart).TotalSeconds;
            double p = Math.Clamp(elapsed / SPIN_SECONDS, 0.0, 1.0);

            // ease interval from ~85ms to ~220ms
            double ms = 85 + 135 * p;
            if (_tickTimer != null) _tickTimer.Interval = TimeSpan.FromMilliseconds(ms);

            PlayClick();

            if (p >= 1.0) StopTickTimer();
        }

        private void TryLoadSounds()
        {
            // Optional WAVs (Build Action: Resource):
            //  /Roulette.Wpf;component/Assets/Sounds/click.wav
            //  /Roulette.Wpf;component/Assets/Sounds/drop.wav
            try
            {
                _clickA = LoadWav("pack://application:,,,/Roulette.Wpf;component/Assets/Sounds/click.wav");
                _clickB = LoadWav("pack://application:,,,/Roulette.Wpf;component/Assets/Sounds/click.wav");
                _drop = LoadWav("pack://application:,,,/Roulette.Wpf;component/Assets/Sounds/drop.wav");
            }
            catch
            {
                _clickA = _clickB = _drop = null; // run silent if missing
            }
        }

        private static SoundPlayer LoadWav(string packUri)
        {
            var sri = Application.GetResourceStream(new Uri(packUri));
            if (sri == null) throw new FileNotFoundException(packUri);
            using var s = sri.Stream;
            var ms = new MemoryStream();
            s.CopyTo(ms);
            ms.Position = 0;
            return new SoundPlayer(ms);
        }
        private void PlayClick() => System.Media.SystemSounds.Asterisk.Play();
        private void PlayDrop() => System.Media.SystemSounds.Exclamation.Play();

        //private void PlayClick()
        //{
        //    var p = (_useA ? _clickA : _clickB);
        //    _useA = !_useA;
        //    try { p?.Play(); } catch { /* ignore */ }
        //}

        //private void PlayDrop()
        //{
        //    try { _drop?.Play(); } catch { /* ignore */ }
        //}

        // ----- helpers -----
        private static double Mod(double x, double m) => (x % m + m) % m;
        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        // Easing functions (0..1 -> 0..1)
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
    }
}
