using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Roulette.Core;

namespace Roulette.Wpf.ViewModels;

public  class GameViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    void Raise([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    // Options
    public ObservableCollection<BetType> BetTypes { get; } = new(Enum.GetValues<BetType>());
    public ObservableCollection<int> Numbers { get; } = new(Enumerable.Range(0, 37));
    public ObservableCollection<RedBlack> RBOptions { get; } = new(Enum.GetValues<RedBlack>());
    public ObservableCollection<EvenOdd> EOOptions { get; } = new(Enum.GetValues<EvenOdd>());
    public ObservableCollection<LowHigh> LHOptions { get; } = new(Enum.GetValues<LowHigh>());
    public ObservableCollection<int> Dozens { get; } = new(new[] { 1, 2, 3 });
    public ObservableCollection<int> Columns { get; } = new(new[] { 1, 2, 3 });

    // Composer state
    BetType _selectedBetType = BetType.RedBlack;
    public BetType SelectedBetType { get => _selectedBetType; set { _selectedBetType = value; Raise(); RaiseComposerFlags(); } }
    string _stake = "10"; public string Stake { get => _stake; set { _stake = value; Raise(); Raise(nameof(CanAddBet)); } }
    int? _straightNumber; public int? StraightNumber { get => _straightNumber; set { _straightNumber = value; Raise(); Raise(nameof(CanAddBet)); } }
    RedBlack? _selectedRB = RedBlack.Red; public RedBlack? SelectedRB { get => _selectedRB; set { _selectedRB = value; Raise(); Raise(nameof(CanAddBet)); } }
    EvenOdd? _selectedEO; public EvenOdd? SelectedEO { get => _selectedEO; set { _selectedEO = value; Raise(); Raise(nameof(CanAddBet)); } }
    LowHigh? _selectedLH; public LowHigh? SelectedLH { get => _selectedLH; set { _selectedLH = value; Raise(); Raise(nameof(CanAddBet)); } }
    int? _selectedDozen; public int? SelectedDozen { get => _selectedDozen; set { _selectedDozen = value; Raise(); Raise(nameof(CanAddBet)); } }
    int? _selectedColumn; public int? SelectedColumn { get => _selectedColumn; set { _selectedColumn = value; Raise(); Raise(nameof(CanAddBet)); } }

    // Visibility flags
    public bool ShowStraight { get; private set; }
    public bool ShowRB { get; private set; }
    public bool ShowEO { get; private set; }
    public bool ShowLH { get; private set; }
    public bool ShowDozen { get; private set; }
    public bool ShowColumn { get; private set; }
    void RaiseComposerFlags()
    {
        ShowStraight = SelectedBetType == BetType.Straight;
        ShowRB = SelectedBetType == BetType.RedBlack;
        ShowEO = SelectedBetType == BetType.EvenOdd;
        ShowLH = SelectedBetType == BetType.LowHigh;
        ShowDozen = SelectedBetType == BetType.Dozen;
        ShowColumn = SelectedBetType == BetType.Column;
        Raise(nameof(ShowStraight)); Raise(nameof(ShowRB)); Raise(nameof(ShowEO));
        Raise(nameof(ShowLH)); Raise(nameof(ShowDozen)); Raise(nameof(ShowColumn));
    }

    // Bets list
    public ObservableCollection<BetView> Bets { get; } = new();
    BetView? _selectedBet; public BetView? SelectedBet { get => _selectedBet; set { _selectedBet = value; Raise(); } }

    public bool CanAddBet => int.TryParse(Stake, out var s) && s > 0 && ComposerValid();
    bool ComposerValid() => SelectedBetType switch
    {
        BetType.Straight => StraightNumber is not null,
        BetType.RedBlack => SelectedRB is not null,
        BetType.EvenOdd => SelectedEO is not null,
        BetType.LowHigh => SelectedLH is not null,
        BetType.Dozen => SelectedDozen is not null,
        BetType.Column => SelectedColumn is not null,
        _ => false
    };

    Bet BuildBet(int stake) => SelectedBetType switch
    {
        BetType.Straight => new(BetType.Straight, stake, Number: StraightNumber),
        BetType.RedBlack => new(BetType.RedBlack, stake, RB: SelectedRB),
        BetType.EvenOdd => new(BetType.EvenOdd, stake, EO: SelectedEO),
        BetType.LowHigh => new(BetType.LowHigh, stake, LH: SelectedLH),
        BetType.Dozen => new(BetType.Dozen, stake, Dozen: SelectedDozen),
        BetType.Column => new(BetType.Column, stake, Column: SelectedColumn),
        _ => throw new NotSupportedException()
    };
    string Describe(Bet b) => b.Type switch
    {
        BetType.Straight => $"#{b.Number}",
        BetType.RedBlack => $"{b.RB}",
        BetType.EvenOdd => $"{b.EO}",
        BetType.LowHigh => $"{b.LH}",
        BetType.Dozen => $"Dozen {b.Dozen}",
        BetType.Column => $"Column {b.Column}",
        _ => ""
    };

    // Result
    int _lastSpinNumber; public int LastSpinNumber { get => _lastSpinNumber; private set { _lastSpinNumber = value; Raise(); Raise(nameof(LastSpinLabel)); } }
    Roulette.Core.Color _lastSpinColor = Roulette.Core.Color.Green; public Roulette.Core.Color LastSpinColor { get => _lastSpinColor; private set { _lastSpinColor = value; Raise(); } }
    public string LastSpinLabel => $"Landed on {LastSpinNumber} ({LastSpinColor})";
    int _lastNetResult; public int LastNetResult { get => _lastNetResult; private set { _lastNetResult = value; Raise(); } }

    // Commands
    public ICommand AddBetCommand { get; }
    public ICommand ClearBetsCommand { get; }
    public ICommand SpinCommand { get; }

    public GameViewModel()
    {
        RaiseComposerFlags();

        AddBetCommand = new RelayCommand(_ =>
        {
            if (!int.TryParse(Stake, out var stake) || stake <= 0) return;
            var bet = BuildBet(stake);
            Bets.Add(new BetView(bet, Describe(bet)));
        }, _ => CanAddBet);

        ClearBetsCommand = new RelayCommand(_ => Bets.Clear(), _ => Bets.Any());

        SpinCommand = new RelayCommand(_ =>
        {
            var spin = Wheel.Spin();
            LastSpinNumber = spin.Number;
            LastSpinColor = spin.Color;
            LastNetResult = Payouts.EvaluateMany(Bets.Select(b => b.Bet), spin);
        });
    }
}

public  class BetView
{
    public Bet Bet { get; }
    public BetType Type => Bet.Type;
    public int Stake => Bet.Stake;
    public string Description { get; }
    public BetView(Bet bet, string description) { Bet = bet; Description = description; }
}

public  class RelayCommand : ICommand
{
    readonly Action<object?> _exec; readonly Predicate<object?>? _can;
    public RelayCommand(Action<object?> exec, Predicate<object?>? can = null) { _exec = exec; _can = can; }
    public bool CanExecute(object? p) => _can?.Invoke(p) ?? true;
    public void Execute(object? p) => _exec(p);
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
}
