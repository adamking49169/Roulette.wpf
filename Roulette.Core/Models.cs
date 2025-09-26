namespace Roulette.Core;

public enum BetType { Straight, RedBlack, EvenOdd, Dozen, Column, LowHigh }
public enum Color { Green, Red, Black }
public enum RedBlack { Red, Black }
public enum EvenOdd { Even, Odd }
public enum LowHigh { Low, High }

public record Bet(BetType Type, int Stake,
    int? Number = null, RedBlack? RB = null, EvenOdd? EO = null,
    int? Dozen = null, int? Column = null, LowHigh? LH = null);

public record SpinResult(int Number, Color Color);
