using System.Collections.Generic;
using UnityEngine;

public class HandManager
{
    public List<Player> hands;

    public HandManager(int numPlayers, int handSize, ShuffleManager shuffleManager)
    {
        hands = new List<Player>();

        if (shuffleManager == null)
        {
            Debug.LogError("[HandManager] shuffleManager is null!");
            return;
        }

        if (shuffleManager.Remaining() == 0) shuffleManager.BuildAndShuffle();

        for (int i = 0; i < numPlayers; i++)
        {
            var hand = new List<Card>();
            var knowledge = new List<CardKnowledge>();

            for (int j = 0; j < handSize; j++)
            {
                var card = shuffleManager.Draw();
                if (card == null)
                {
                    Debug.LogError($"[HandManager] Draw() returned null at P{i}, idx{j}. Remaining={shuffleManager.Remaining()}");
                    continue;
                }

                hand.Add(card);

                // 自己手札の知識は「完全に未知」で初期化（色・数字すべて true）
                var k = new CardKnowledge
                {
                    possibleColors  = new bool[5],
                    possibleNumbers = new bool[5]
                };
                for (int t = 0; t < 5; t++)
                {
                    k.possibleColors[t]  = true; // R,Y,G,W,B
                    k.possibleNumbers[t] = true; // 1..5 (indexは0..4)
                }
                knowledge.Add(k);

                Debug.Log($"[Deal] card={card.color}{card.number}");
            }


            hands.Add(new Player(hand, knowledge));
        }
    }

    public void RemoveCard(int playerIndex, int cardIndex)
    {
        if (playerIndex >= 0 && playerIndex < hands.Count)
        {
            var player = hands[playerIndex];
            if (cardIndex >= 0 && cardIndex < player.hand.Count)
            {
                player.hand.RemoveAt(cardIndex);
                player.knowledge.RemoveAt(cardIndex);
            }
            else
            {
                Debug.LogWarning($"Invalid card index {cardIndex} for player {playerIndex}");
            }
        }
        else
        {
            Debug.LogWarning($"Invalid player index: {playerIndex}");
        }
    }
}
