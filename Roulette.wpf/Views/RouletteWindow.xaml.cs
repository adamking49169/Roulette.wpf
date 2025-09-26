using System;
using System.Windows;
using System.Windows.Media.Animation;
using MahApps.Metro.Controls;

namespace Roulette.Wpf.Views
{
    public partial class RouletteWindow : MetroWindow
    {
        // Keep track of where the wheel currently is (degrees)
        private double _currentAngle;

        // European wheel order, clockwise from 0 (single-zero)
        private static readonly int[] WheelOrder =
        {
            0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23,
            10, 5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26
        };

        private const double SECTOR_DEGREES = 360.0 / 37.0;

        // If your PNG’s “0” isn’t exactly at 12 o’clock, nudge this (positive = clockwise).
        private const double OFFSET_DEGREES = 0.0;

        // How many full spins before stopping
        private const int REVOLUTIONS = 6;

        // Spin duration
        private static readonly Duration SPIN_DURATION = TimeSpan.FromSeconds(3.5);

        public RouletteWindow()
        {
            InitializeComponent();

            // DataContext might be set later by XAML — handle both cases.
            HookViewModel();
            DataContextChanged += (_, __) => HookViewModel();

            Unloaded += (_, __) => UnhookViewModel();
        }

        private ViewModels.GameViewModel? _vm;

        private void HookViewModel()
        {
            UnhookViewModel();

            _vm = DataContext as ViewModels.GameViewModel;
            if (_vm is null) return;

            _vm.SpinRequested += AnimateToNumber;
        }

        private void UnhookViewModel()
        {
            if (_vm != null)
            {
                _vm.SpinRequested -= AnimateToNumber;
                _vm = null;
            }
        }

        private void AnimateToNumber(int number)
        {
            // Find index of the number on the wheel
            int idx = Array.IndexOf(WheelOrder, number);
            if (idx < 0) idx = 0;

            // The angle where that pocket is at the top marker (0 degrees)
            double targetAngle = OFFSET_DEGREES + idx * SECTOR_DEGREES;

            // Where are we now in a 0..360 frame?
            double currentMod = Mod(_currentAngle, 360.0);

            // How far to rotate (clockwise) to reach the target?
            double deltaToTarget = targetAngle - currentMod;
            if (deltaToTarget < 0) deltaToTarget += 360.0;

            // End angle = full spins + alignment
            double endAngle = _currentAngle + REVOLUTIONS * 360.0 + deltaToTarget;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var anim = new DoubleAnimation
            {
                From = _currentAngle,
                To = endAngle,
                Duration = SPIN_DURATION,
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };

            anim.Completed += (_, __) =>
            {
                _currentAngle = endAngle;                // remember final angle
                WheelRotate.Angle = _currentAngle;       // lock transform so it doesn't snap back
                _vm?.CommitPendingSpin();                // commit logical result & payouts
            };

            WheelRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, anim);
        }

        private static double Mod(double x, double m) => (x % m + m) % m;
    }
}
