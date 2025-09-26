using System.Linq;
using System.Security.Cryptography;

namespace Roulette.Core;

public static class Wheel
{
    public static SpinResult Spin()
    {
        int n = RandomNumberGenerator.GetInt32(0, 37); // 0..36 inclusive
        return new SpinResult(n, GetColor(n));
    }

    public static Color GetColor(int n)
    {
        if (n == 0) return Color.Green;
        int[] reds = { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };
        return reds.Contains(n) ? Color.Red : Color.Black;
    }

    public static int GetDozen(int n) => (n >= 1 && n <= 12) ? 1 : (n <= 24 ? 2 : (n <= 36 ? 3 : 0));
    public static int GetColumn(int n)
    {
        if (n == 0) return 0;
        int mod = n % 3;
        return mod == 1 ? 1 : mod == 2 ? 2 : 3;
    }

    public static bool IsEven(int n) => n != 0 && (n % 2 == 0);
    public static bool IsLow(int n) => n >= 1 && n <= 18;
}
