using System.Collections.Generic;
using UnityEngine;

public class ShuffleManager : MonoBehaviour
{
    private List<Card> deck = new List<Card>();
    private System.Random rng = new System.Random();

    void Awake()
    {
    }

    public void BuildAndShuffle()
    {
        deck = new List<Card>();
        foreach (var c in HanabiSpec.ColorOrder) // 'R','Y','G','W','B'
        {
            string color = c.ToString();
            deck.Add(new Card(color, 1));
            deck.Add(new Card(color, 1));
            deck.Add(new Card(color, 1));
            deck.Add(new Card(color, 2));
            deck.Add(new Card(color, 2));
            deck.Add(new Card(color, 3));
            deck.Add(new Card(color, 3));
            deck.Add(new Card(color, 4));
            deck.Add(new Card(color, 4));
            deck.Add(new Card(color, 5));
        }
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
        for (int k = 0; k < Mathf.Min(5, deck.Count); k++)
            Debug.Log($"[Deck] {k}: {deck[k].color}{deck[k].number}");
    }

    public Card Draw()
    {
        if (deck.Count == 0) return null;
        var top = deck[deck.Count - 1];
        deck.RemoveAt(deck.Count - 1);
        return top;
    }

    public int Remaining()
    {
        return deck != null ? deck.Count : 0;
    }
}
