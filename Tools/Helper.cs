using System;
using UnityEngine;

namespace AmmoCount.Tools;

public class Helper
{
    internal static string FormatNumberSimple(float number)
    {
        var mag = (int)(Math.Floor(Math.Log10(number)) / 3); // Truncates to 6, divides to 2
        var divisor = Math.Pow(10, mag * 3);

        var shortNumber = number / divisor;

        string suffix;
        switch (mag)
        {
            default:
                return number.ToString();
            case 1:
                suffix = "k";
                break;
            case 2:
                suffix = "m";
                break;
            case 3:
                suffix = "b";
                break;
        }

        return shortNumber.ToString("N1") + suffix;
    }

    internal static string ColorString(string text, Color color)
    {
        var colorString = ColorUtility.ToHtmlStringRGB(color);
        return $"<color=#{colorString}>{text}</color>";
    }
}