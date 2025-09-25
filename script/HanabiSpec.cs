using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class HanabiSpec
{
    public const int NumColors = 5;
    public const int NumRanks  = 5;
    public const int BitsPerCard = NumColors * NumRanks; // 25

    // ★HLE準拠の色順（color-major：RYGWB）
    public static readonly char[] ColorOrder = new[] { 'R', 'Y', 'G', 'W', 'B' };

    // 文字列を正規化（"red","r","R"→'R' など）
    public static char NormalizeColorString(string s)
    {
        if (string.IsNullOrEmpty(s)) return '\0';
        s = s.Trim().ToLowerInvariant();
        if (s == "r" || s == "red")    return 'R';
        if (s == "y" || s == "yellow") return 'Y';
        if (s == "g" || s == "green")  return 'G';
        if (s == "w" || s == "white")  return 'W';
        if (s == "b" || s == "blue")   return 'B';
        // 既知以外は原文の先頭文字で推測（例: "R" "G" 等）
        char c = char.ToUpperInvariant(s[0]);
        return (c == 'R' || c == 'Y' || c == 'G' || c == 'W' || c == 'B') ? c : '\0';
    }

    public static int ColorToIndex(char c)
    {
        for (int i = 0; i < ColorOrder.Length; i++)
            if (ColorOrder[i] == c) return i;
        return -1; // 未知
    }

    public static int RankToIndex(int rank1to5) => rank1to5 - 1;

    public static int CardBitIndex(char colorChar, int rank1to5)
        => ColorToIndex(colorChar) * NumRanks + RankToIndex(rank1to5);
}
