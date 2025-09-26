using System;
using System.Collections.Generic;
using System.Linq;

namespace Roulette.Core;

public static class Payouts
{
    public static int Evaluate(Bet bet, SpinResult spin) => bet.Type switch
    {
        BetType.Straight => (bet.Number == spin.Number) ? bet.Stake * 35 : -bet.Stake,
        BetType.RedBlack => spin.Number == 0 ? -bet.Stake :
            ((bet.RB == RedBlack.Red && spin.Color == Color.Red) ||
             (bet.RB == RedBlack.Black && spin.Color == Color.Black)) ? bet.Stake : -bet.Stake,
        BetType.EvenOdd => spin.Number == 0 ? -bet.Stake :
            ((bet.EO == EvenOdd.Even && Wheel.IsEven(spin.Number)) ||
             (bet.EO == EvenOdd.Odd && !Wheel.IsEven(spin.Number))) ? bet.Stake : -bet.Stake,
        BetType.LowHigh => spin.Number == 0 ? -bet.Stake :
            ((bet.LH == LowHigh.Low && Wheel.IsLow(spin.Number)) ||
             (bet.LH == LowHigh.High && !Wheel.IsLow(spin.Number))) ? bet.Stake : -bet.Stake,
        BetType.Dozen => (bet.Dozen == Wheel.GetDozen(spin.Number)) ? bet.Stake * 2 : -bet.Stake,
        BetType.Column => (bet.Column == Wheel.GetColumn(spin.Number)) ? bet.Stake * 2 : -bet.Stake,
        _ => throw new NotSupportedException()
    };

    public static int EvaluateMany(IEnumerable<Bet> bets, SpinResult spin) =>
        bets.Sum(b => Evaluate(b, spin));
}
