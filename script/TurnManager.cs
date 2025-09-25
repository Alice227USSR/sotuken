using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager
{
    private int currentPlayer;
    private int totalPlayers;

    public TurnManager(int numPlayers)
    {
        totalPlayers = numPlayers;
        currentPlayer = 0;
    }

    public int GetCurrentPlayer()
    {
        return currentPlayer;
    }

    public void AdvanceTurn()
    {
        currentPlayer = (currentPlayer + 1) % totalPlayers;
    }

    public void ResetTurn()
    {
        currentPlayer = 0;
    }
}

