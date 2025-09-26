using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Media = System.Windows.Media;          // alias for WPF media types
using Core = Roulette.Core;                  // alias for your core namespace

namespace Roulette.Wpf.ViewModels;

public sealed class GameViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    void Raise([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    // ===== Chips =====
    public ObservableCollection<int> ChipValues { get; } = new(new[] { 1, 2, 5, 10, 20, 50 });
    int _selectedChipValue = 5;
    public int SelectedChipValue { get => _selectedChipValue; set { _selectedChipValue = value; Raise(); } }

    // ===== Cells =====
    public ObservableCollection<CellView> ZeroCell { get; } = new();
    public ObservableCollection<CellView> NumberCells { get; } = new();
    public ObservableCollection<CellView> OutsideEvenMoney { get; } = new(); // Red/Black, Even/Odd, Low/High
    public ObservableCollection<CellView> OutsideDozens { get; } = new();     // Dozen 1/2/3
    public ObservableCollection<CellView> OutsideColumns { get; } = new();    // Column 1/2/3

    // ===== Bets summary for the right list =====
    public ObservableCollection<BetView> Bets { get; } = new();

    // ===== Result =====
    int _lastSpinNumber;
    public int LastSpinNumber { get => _lastSpinNumber; private set { _lastSpinNumber = value; Raise(); Raise(nameof(LastSpinLabel)); } }

    Core.Color _lastSpinColor = Core.Color.Green;
    public Core.Color LastSpinColor { get => _lastSpinColor; private set { _lastSpinColor = value; Raise(); } }

    public string LastSpinLabel => $"Landed on {LastSpinNumber} ({LastSpinColor})";

    int _lastNetResult;
    public int LastNetResult { get => _lastNetResult; private set { _lastNetResult = value; Raise(); } }

    // ===== Commands =====
    public ICommand AddStakeCommand { get; }
    public ICommand ClearBetsCommand { get; }
    public ICommand SpinCommand { get; }

    public GameViewModel()
    {
        BuildCells();

        // respond to stake changes
        Subscribe(NumberCells);
        Subscribe(ZeroCell);
        Subscribe(OutsideEvenMoney);
        Subscribe(OutsideDozens);
        Subscribe(OutsideColumns);
        RefreshBets();

        AddStakeCommand = new RelayCommand(keyObj =>
        {
            if (keyObj is not string key) return;
            var cell = FindCell(key);
            if (cell is null) return;
            cell.Stake += SelectedChipValue;
        });

        ClearBetsCommand = new RelayCommand(_ => ClearAllStakes(), _ => Bets.Any());

        SpinCommand = new RelayCommand(_ =>
        {
            var compiled = CompileBetsFromCells();
            var spin = Core.Wheel.Spin();
            LastSpinNumber = spin.Number;
            LastSpinColor = spin.Color;
            LastNetResult = Core.Payouts.EvaluateMany(compiled.Select(bv => bv.Bet), spin);
        });
    }

    void BuildCells()
    {
        // Zero
        ZeroCell.Add(new CellView("N:0", "0", new Media.SolidColorBrush(Media.Colors.Green)));

        // Numbers 1..36 arranged 3 rows x 12 columns (top row = 3,6,... bottom = 1,4,...)
        var ordered = Enumerable.Range(1, 36)
            .OrderBy(n => (n - 1) % 3) // row
            .ThenBy(n => (n - 1) / 3)  // column
            .ToList();

        foreach (var n in ordered)
        {
            var isRed = Core.Wheel.GetColor(n) == Core.Color.Red;
            var bg = new Media.SolidColorBrush(isRed ? ColorFromHex("#d9534f") : ColorFromHex("#343a40"));
            NumberCells.Add(new CellView($"N:{n}", n.ToString(), bg));
        }

        // Even-money
        OutsideEvenMoney.Add(new CellView("RB:Red", "RED", new Media.SolidColorBrush(ColorFromHex("#d9534f"))));
        OutsideEvenMoney.Add(new CellView("RB:Black", "BLACK", new Media.SolidColorBrush(ColorFromHex("#343a40"))));
        OutsideEvenMoney.Add(new CellView("EO:Even", "EVEN", new Media.SolidColorBrush(ColorFromHex("#2563eb"))));
        OutsideEvenMoney.Add(new CellView("EO:Odd", "ODD", new Media.SolidColorBrush(ColorFromHex("#2563eb"))));
        OutsideEvenMoney.Add(new CellView("LH:Low", "1–18", new Media.SolidColorBrush(ColorFromHex("#198754"))));
        OutsideEvenMoney.Add(new CellView("LH:High", "19–36", new Media.SolidColorBrush(ColorFromHex("#198754"))));

        // Dozens
        OutsideDozens.Add(new CellView("DZ:1", "1st 12", new Media.SolidColorBrush(ColorFromHex("#6c757d"))));
        OutsideDozens.Add(new CellView("DZ:2", "2nd 12", new Media.SolidColorBrush(ColorFromHex("#6c757d"))));
        OutsideDozens.Add(new CellView("DZ:3", "3rd 12", new Media.SolidColorBrush(ColorFromHex("#6c757d"))));

        // Columns
        OutsideColumns.Add(new CellView("CL:1", "Column 1", new Media.SolidColorBrush(ColorFromHex("#6c757d"))));
        OutsideColumns.Add(new CellView("CL:2", "Column 2", new Media.SolidColorBrush(ColorFromHex("#6c757d"))));
        OutsideColumns.Add(new CellView("CL:3", "Column 3", new Media.SolidColorBrush(ColorFromHex("#6c757d"))));
    }

    static Media.Color ColorFromHex(string hex) =>
        (Media.Color)Media.ColorConverter.ConvertFromString(hex);

    void Subscribe(ObservableCollection<CellView> list)
    {
        foreach (var c in list) c.PropertyChanged += OnCellChanged;
        list.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null) foreach (CellView c in e.NewItems) c.PropertyChanged += OnCellChanged;
            if (e.OldItems != null) foreach (CellView c in e.OldItems) c.PropertyChanged -= OnCellChanged;
        };
    }

    void OnCellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CellView.Stake))
            RefreshBets();
    }

    void RefreshBets()
    {
        Bets.Clear();

        // zero & numbers
        foreach (var cell in ZeroCell.Concat(NumberCells).Where(c => c.Stake > 0))
        {
            if (cell.Key == "N:0")
                Bets.Add(new BetView(new Core.Bet(Core.BetType.Straight, cell.Stake, Number: 0), "#0", "Straight"));
            else
            {
                var n = int.Parse(cell.Label);
                Bets.Add(new BetView(new Core.Bet(Core.BetType.Straight, cell.Stake, Number: n), $"#{n}", "Straight"));
            }
        }

        // even-money
        foreach (var c in OutsideEvenMoney.Where(c => c.Stake > 0))
        {
            switch (c.Key)
            {
                case "RB:Red": Bets.Add(new BetView(new Core.Bet(Core.BetType.RedBlack, c.Stake, RB: Core.RedBlack.Red), "Red", "Red/Black")); break;
                case "RB:Black": Bets.Add(new BetView(new Core.Bet(Core.BetType.RedBlack, c.Stake, RB: Core.RedBlack.Black), "Black", "Red/Black")); break;
                case "EO:Even": Bets.Add(new BetView(new Core.Bet(Core.BetType.EvenOdd, c.Stake, EO: Core.EvenOdd.Even), "Even", "Even/Odd")); break;
                case "EO:Odd": Bets.Add(new BetView(new Core.Bet(Core.BetType.EvenOdd, c.Stake, EO: Core.EvenOdd.Odd), "Odd", "Even/Odd")); break;
                case "LH:Low": Bets.Add(new BetView(new Core.Bet(Core.BetType.LowHigh, c.Stake, LH: Core.LowHigh.Low), "1–18", "Low/High")); break;
                case "LH:High": Bets.Add(new BetView(new Core.Bet(Core.BetType.LowHigh, c.Stake, LH: Core.LowHigh.High), "19–36", "Low/High")); break;
            }
        }

        // dozens
        foreach (var c in OutsideDozens.Where(c => c.Stake > 0))
        {
            int dz = int.Parse(c.Key.Split(':')[1]);
            Bets.Add(new BetView(new Core.Bet(Core.BetType.Dozen, c.Stake, Dozen: dz), $"Dozen {dz}", "Dozen"));
        }

        // columns
        foreach (var c in OutsideColumns.Where(c => c.Stake > 0))
        {
            int cl = int.Parse(c.Key.Split(':')[1]);
            Bets.Add(new BetView(new Core.Bet(Core.BetType.Column, c.Stake, Column: cl), $"Column {cl}", "Column"));
        }

        Raise(nameof(Bets));
    }

    void ClearAllStakes()
    {
        foreach (var c in ZeroCell) c.Stake = 0;
        foreach (var c in NumberCells) c.Stake = 0;
        foreach (var c in OutsideEvenMoney) c.Stake = 0;
        foreach (var c in OutsideDozens) c.Stake = 0;
        foreach (var c in OutsideColumns) c.Stake = 0;
        RefreshBets();
    }

    CellView? FindCell(string key) =>
        ZeroCell.Concat(NumberCells).Concat(OutsideEvenMoney).Concat(OutsideDozens).Concat(OutsideColumns)
                .FirstOrDefault(c => c.Key == key);

    // Compile once at spin time
    ObservableCollection<BetView> CompileBetsFromCells()
    {
        RefreshBets();
        return Bets;
    }
}

// ===== Cell and helpers =====
public sealed class CellView : INotifyPropertyChanged
{
    public string Key { get; }
    public string Label { get; }
    public Media.Brush Background { get; }

    int _stake;
    public int Stake { get => _stake; set { _stake = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Stake))); } }

    public CellView(string key, string label, Media.Brush background)
    {
        Key = key; Label = label; Background = background;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class BetView
{
    public Core.Bet Bet { get; }
    public string Description { get; }
    public string Type { get; }
    public int Stake => Bet.Stake;

    public BetView(Core.Bet bet, string description, string type)
    {
        Bet = bet; Description = description; Type = type;
    }
}

public sealed class RelayCommand : ICommand
{
    readonly Action<object?> _exec; readonly Predicate<object?>? _can;
    public RelayCommand(Action<object?> exec, Predicate<object?>? can = null) { _exec = exec; _can = can; }
    public bool CanExecute(object? p) => _can?.Invoke(p) ?? true;
    public void Execute(object? p) => _exec(p);
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
}
