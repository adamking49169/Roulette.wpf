using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Media = System.Windows.Media;
using Core = Roulette.Core;

namespace Roulette.Wpf.ViewModels;

public sealed class GameViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    void Raise([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    // ===== Chips (common casino denominations/colours) =====
    // 1=White, 2=Blue, 5=Red, 10=Black, 20=Green, 50=Purple
    public ObservableCollection<int> ChipValues { get; } = new(new[] { 1, 2, 5, 10, 20, 50,100,500 });
    int _selectedChipValue = 5;
    public int SelectedChipValue { get => _selectedChipValue; set { _selectedChipValue = value; Raise(); } }

    // ===== Cells =====
    public ObservableCollection<CellView> ZeroCell { get; } = new();
    public ObservableCollection<CellView> NumberCells { get; } = new();
    public ObservableCollection<CellView> OutsideEvenMoney { get; } = new(); // Red/Black, Even/Odd, Low/High
    public ObservableCollection<CellView> OutsideDozens { get; } = new();     // Dozen 1/2/3
    public ObservableCollection<CellView> OutsideColumns { get; } = new();    // Column 1/2/3

    // ===== Bets summary =====
    public ObservableCollection<BetView> Bets { get; } = new();

    // ===== Result =====
    int _lastSpinNumber;
    public int LastSpinNumber { get => _lastSpinNumber; private set { _lastSpinNumber = value; Raise(); Raise(nameof(LastSpinLabel)); } }

    Core.Color _lastSpinColor = Core.Color.Green;
    public Core.Color LastSpinColor { get => _lastSpinColor; private set { _lastSpinColor = value; Raise(); } }

    public string LastSpinLabel => $"Landed on {LastSpinNumber} ({LastSpinColor})";

    int _lastNetResult;
    public int LastNetResult { get => _lastNetResult; private set { _lastNetResult = value; Raise(); } }

    const int MaxRecentSpins = 12;
    public ObservableCollection<SpinHistoryEntry> RecentSpins { get; } = new();

    bool _useSeed;
    public bool UseSeed { get => _useSeed; set { if (_useSeed != value) { _useSeed = value; Raise(); } } }

    string? _seedText;
    public string? SeedText { get => _seedText; set { if (_seedText != value) { _seedText = value; Raise(); } } }

    bool _isSpinInProgress;
    public bool IsSpinInProgress
    {
        get => _isSpinInProgress;
        private set
        {
            if (_isSpinInProgress == value) return;
            _isSpinInProgress = value;
            Raise();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    // ===== Commands =====
    public ICommand AddStakeCommand { get; }
    public ICommand ClearBetsCommand { get; }
    public ICommand SpinCommand { get; }

    public GameViewModel()
    {
        BuildCells();

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

            // Assign chip brush based on currently selected chip
            cell.ChipBrush = GetChipBrushForValue(SelectedChipValue);
            cell.Stake += SelectedChipValue;
        });

        ClearBetsCommand = new RelayCommand(_ => ClearAllStakes(), _ => Bets.Any());

        SpinCommand = new RelayCommand(_ =>
        {
            if (IsSpinInProgress) return;
            RefreshBets();
            var spin = GenerateSpin();
            _pendingSpin = spin;
            IsSpinInProgress = true;
            SpinRequested?.Invoke(spin.Number);
        }, _ => !IsSpinInProgress);

    }
    // Visual spin plumbing
    public event Action<int>? SpinRequested;  // number to land on
    private Core.SpinResult? _pendingSpin;
    public Core.SpinResult? PendingSpin => _pendingSpin;

    // Called by the view when animation completes
    public void CommitPendingSpin()
    {
        if (_pendingSpin is Core.SpinResult s)
        {
            LastSpinNumber = s.Number;
            LastSpinColor = s.Color;
            LastNetResult = Core.Payouts.EvaluateMany(Bets.Select(b => b.Bet), s);
            AddRecentSpin(s.Number, s.Color);
            _pendingSpin = null;
        }

        IsSpinInProgress = false;
    }
    public sealed class SpinHistoryEntry
    {
        public int Number { get; }
        public Core.Color Color { get; }
        public string ColorLabel => Color.ToString();

        public SpinHistoryEntry(int number, Core.Color color)
        {
            Number = number;
            Color = color;
        }
    }

    void AddRecentSpin(int number, Core.Color color)
    {
        RecentSpins.Insert(0, new SpinHistoryEntry(number, color));
        while (RecentSpins.Count > MaxRecentSpins)
            RecentSpins.RemoveAt(RecentSpins.Count - 1);
    }

    Core.SpinResult GenerateSpin()
    {
        if (UseSeed && int.TryParse(SeedText, out int seed))
        {
            var rng = new Random(seed);
            int number = rng.Next(0, 37);
            return new Core.SpinResult(number, Core.Wheel.GetColor(number));
        }

        return Core.Wheel.Spin();
    }

    void BuildCells()
    {
        // Zero cell (single tall)
        ZeroCell.Add(new CellView("N:0", "0", new Media.SolidColorBrush(Media.Color.FromRgb(14, 122, 58))));

        // Numbers 1..36 laid out to visually match real table:
        // bottom row: 1,4,7,...,34
        // middle:     2,5,8,...,35
        // top:        3,6,9,...,36
        var bottom = Enumerable.Range(1, 36).Where(n => n % 3 == 1);
        var middle = Enumerable.Range(1, 36).Where(n => n % 3 == 2);
        var top = Enumerable.Range(1, 36).Where(n => n % 3 == 0);
        var ordered = top.Concat(middle).Concat(bottom)    // UniformGrid fills rows first (top→bottom)
                         .ToList();

        foreach (var n in ordered)
        {
            bool isRed = Core.Wheel.GetColor(n) == Core.Color.Red;
            var bg = new Media.SolidColorBrush(isRed ? Media.Color.FromRgb(177, 44, 44)
                                                     : Media.Color.FromRgb(34, 34, 34));
            NumberCells.Add(new CellView($"N:{n}", n.ToString(), bg));
        }

        // Even-money
        OutsideEvenMoney.Add(new CellView("LH:Low", "1 - 18", FeltDark()));
        OutsideEvenMoney.Add(new CellView("EO:Even", "Even", FeltDark()));
        OutsideEvenMoney.Add(new CellView("RB:Red", "RED", FeltDark()));
        OutsideEvenMoney.Add(new CellView("RB:Black", "BLACK", FeltDark()));
        OutsideEvenMoney.Add(new CellView("EO:Odd", "Odd", FeltDark()));
        OutsideEvenMoney.Add(new CellView("LH:High", "19 - 36", FeltDark()));

        // Dozens
        OutsideDozens.Add(new CellView("DZ:1", "1st 12", FeltDark()));
        OutsideDozens.Add(new CellView("DZ:2", "2nd 12", FeltDark()));
        OutsideDozens.Add(new CellView("DZ:3", "3rd 12", FeltDark()));

        // Columns (for “2 to 1”)
        OutsideColumns.Add(new CellView("CL:3", "2 to 1", FeltDark())); // top row corresponds to column 3
        OutsideColumns.Add(new CellView("CL:2", "2 to 1", FeltDark())); // middle column 2
        OutsideColumns.Add(new CellView("CL:1", "2 to 1", FeltDark())); // bottom column 1
    }

    Media.Brush FeltDark() => new Media.SolidColorBrush(Media.Color.FromRgb(15, 71, 33));

    // Map chip denomination to classic colours
    Media.Brush GetChipBrushForValue(int v) => v switch
    {
        1 => new Media.SolidColorBrush(Media.Colors.White),
        2 => new Media.SolidColorBrush(Media.Color.FromRgb(25, 118, 210)), // blue
        5 => new Media.SolidColorBrush(Media.Color.FromRgb(193, 18, 31)),  // red
        10 => new Media.SolidColorBrush(Media.Color.FromRgb(30, 30, 30)),   // black
        20 => new Media.SolidColorBrush(Media.Color.FromRgb(25, 135, 84)),  // green
        50 => new Media.SolidColorBrush(Media.Color.FromRgb(111, 66, 193)), // purple
        _ => new Media.SolidColorBrush(Media.Color.FromRgb(210, 180, 140)) // tan as fallback
    };

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

        // columns (from right “2 to 1” or separate list)
        // Note: we’re not directly binding these right buttons to OutsideColumns; the buttons call AddStake with CL:x
        // so we just read stakes from any cells carrying CL:x if you decide to add visual chips there later.
        // For now, column bets come from the command clicks.
        foreach (var c in OutsideColumns.Where(c => c.Stake > 0))
        {
            int column = int.Parse(c.Key.Split(':')[1]);
            Bets.Add(new BetView(new Core.Bet(Core.BetType.Column, c.Stake, Column: column), $"Column {column}", "Column"));
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
        ZeroCell.Concat(NumberCells).Concat(OutsideEvenMoney)
                .Concat(OutsideDozens).Concat(OutsideColumns)
                .FirstOrDefault(c => c.Key == key);

    // Compile once at spin time
    ObservableCollection<BetView> CompileBetsFromCells()
    {
        // Ensure column/2to1 and dozens etc are reflected
        RefreshBets();
        return Bets;
    }
}

// ===== Helper view models =====

// A cell on the table
public sealed class CellView : INotifyPropertyChanged
{
    public string Key { get; }          // e.g., "N:17", "EO:Even", "RB:Red", "DZ:1", "CL:3"
    public string Label { get; }        // UI label
    public Media.Brush Background { get; } // cell background (felt/red/black)

    Media.Brush _chipBrush = new Media.SolidColorBrush(Media.Colors.White);
    public Media.Brush ChipBrush { get => _chipBrush; set { _chipBrush = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChipBrush))); } }

    int _stake;
    public int Stake { get => _stake; set { _stake = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Stake))); } }

    public CellView(string key, string label, Media.Brush background)
    {
        Key = key; Label = label; Background = background;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

// A single compiled bet row for the summary list
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

// Chip preview used in the picker (only for visual)
public sealed class ChipPreview
{
    public int Value { get; set; }
    public int Stake => Value;
    public Media.Brush ChipBrush => Value switch
    {
        1 => new Media.SolidColorBrush(Media.Colors.White),
        2 => new Media.SolidColorBrush(Media.Color.FromRgb(25, 118, 210)),
        5 => new Media.SolidColorBrush(Media.Color.FromRgb(193, 18, 31)),
        10 => new Media.SolidColorBrush(Media.Color.FromRgb(30, 30, 30)),
        20 => new Media.SolidColorBrush(Media.Color.FromRgb(25, 135, 84)),
        50 => new Media.SolidColorBrush(Media.Color.FromRgb(111, 66, 193)),
        _ => new Media.SolidColorBrush(Media.Color.FromRgb(210, 180, 140))
    };
    public ChipPreview() { }
    public ChipPreview(int value) { Value = value; }
}

// Simple ICommand
public sealed class RelayCommand : ICommand
{
    readonly Action<object?> _exec; readonly Predicate<object?>? _can;
    public RelayCommand(Action<object?> exec, Predicate<object?>? can = null) { _exec = exec; _can = can; }
    public bool CanExecute(object? p) => _can?.Invoke(p) ?? true;
    public void Execute(object? p) => _exec(p);
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
}
