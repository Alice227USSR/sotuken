// Models/Card.cs, Player.cs, CardKnowledge.cs

using System.Collections.Generic;

[System.Serializable]
public class Card
{
    public string color;
    public int number;

    public Card(string color, int number)
    {
        this.color = color;
        this.number = number;
    }

        // ★追加：常にRYGWBの単一文字へ正規化
    public char ColorChar => HanabiSpec.NormalizeColorString(color);

    // ★追加：rank(1..5)→index(0..4)
    public int RankIndex => HanabiSpec.RankToIndex(number);
}

public class Player
{
    public List<Card> hand;
    public List<CardKnowledge> knowledge;
    

    public Player(List<Card> hand, List<CardKnowledge> knowledge)
    {
        
        this.hand = hand;
        this.knowledge = knowledge;
    }
}

public class CardKnowledge
{
    public bool[] possibleColors = new bool[5];
    public bool[] possibleNumbers = new bool[5];

    public CardKnowledge()
    {
        for (int i = 0; i < 5; i++)
        {
            possibleColors[i] = true;
            possibleNumbers[i] = true;
        }
    }
}
