using MahApps.Metro.Controls;
using System;
namespace Roulette.Wpf.Views;

public partial class RouletteWindow : MetroWindow
{
    private ViewModels.GameViewModel? _vm;
    public RouletteWindow()
    {
        InitializeComponent();
        WheelControl.SpinCompleted += WheelControlOnSpinCompleted;
        HookViewModel();
        DataContextChanged += (_, __) => HookViewModel();
        Unloaded += (_, __) => UnhookViewModel();
    }
    private void WheelControlOnSpinCompleted(object? sender, EventArgs e)
    {
        _vm?.CommitPendingSpin();
    }
    private void HookViewModel()
    {
        UnhookViewModel();
        _vm = DataContext as ViewModels.GameViewModel;
        if (_vm is null) return;
        _vm.SpinRequested += OnSpinRequested;
    }
    private void UnhookViewModel()
    {
        if (_vm is null) return;
        _vm.SpinRequested -= OnSpinRequested;
        _vm = null;
    }
    private void OnSpinRequested(int number)
    {
        Dispatcher.Invoke(() => WheelControl.SpinToNumber(number));
    }
}