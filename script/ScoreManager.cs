// ScoreManager.cs
// スコア・ヒント・ライフ・花火の進捗・捨て札管理

using System.Collections.Generic;

public class ScoreManager
{
    public Dictionary<string, int> fireworks;
    public List<Card> discardPile;
    public int score;
    public int hints;
    public int lives;

    public ScoreManager()
    {
        // HanabiSpec.ColorOrder = {'R','Y','G','W','B'} を唯一の真実として使用
        fireworks = new Dictionary<string, int>();
        foreach (var c in HanabiSpec.ColorOrder)
        {
            fireworks[c.ToString()] = 0;
        }
        discardPile = new List<Card>();
        score = 0;
        hints = 8;
        lives = 3;
    }

    public bool IsAllFireworksCompleted()
    {
        foreach (var kvp in fireworks)
        {
            if (kvp.Value < 5) return false;
        }
        return true;
    }
}

