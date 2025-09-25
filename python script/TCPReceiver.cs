// Assets/hanabi/python script/TCPReceiver.cs
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Linq;
using System.Collections.Generic;

[Serializable]
public class UnityMessage
{
    public float[] observation;
    public float[] legal_actions;
    public int action;
}

public class TCPReceiver : MonoBehaviour
{
    private TcpListener listener;
    private Thread listenerThread;

    void Start()
    {
        listenerThread = new Thread(new ThreadStart(ListenForMessages));
        listenerThread.IsBackground = true;
        listenerThread.Start();
    }

    void ListenForMessages()
    {
        try
        {
            listener = new TcpListener(IPAddress.Any, 8052);
            listener.Start();
            Debug.Log("Waiting for incoming connection...");

            while (true)
            {
                using (TcpClient client = listener.AcceptTcpClient())
                using (NetworkStream stream = client.GetStream())
                {
                    var sb = new StringBuilder();
                    byte[] buffer = new byte[4096];
                    int bytesRead;
                    do
                    {
                        bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                            sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                    } while (stream.DataAvailable);

                    string received = sb.ToString();
                    if (string.IsNullOrWhiteSpace(received))
                    {
                        Debug.Log("Empty payload");
                        continue;
                    }

                    var messages = received.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var raw in messages)
                    {
                        string jsonStr = raw.Trim();
                        if (string.IsNullOrEmpty(jsonStr)) continue;

                        // メインスレッドでゲーム進行
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            UnityMessage message = null;
                            try
                            {
                                message = JsonUtility.FromJson<UnityMessage>(jsonStr);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[AI] JSON parse failed: {ex.Message}");
                                return;
                            }

                            if (message == null)
                            {
                                Debug.LogWarning("[AI] Empty message");
                                return;
                            }

                            var hanabiManager = FindObjectOfType<HanabiManager>();
                            if (hanabiManager == null) return;

                            // 終局なら何もしない（安全弁）
                            if (hanabiManager.IsTerminalLikeHLE())
                                return;

                            int actionId = message.action;
                            if (actionId < 0 || actionId > 19)
                            {
                                Debug.LogWarning($"[AI] Invalid action id: {actionId}");
                                return;
                            }

                            int currentPlayer = hanabiManager.turnManager.GetCurrentPlayer();

                            // HLE順 → Unity実行
                            if (actionId >= 0 && actionId <= 4)
                            {
                                int cardIndex = actionId; // Discard
                                hanabiManager.playManager.ExecuteAction(
                                    currentPlayer, "discard", cardIndex, null,
                                    hanabiManager.handManager, hanabiManager.scoreManager,
                                    hanabiManager.shuffleManager, hanabiManager.fireworkDisplayManager);
                            }
                            else if (actionId >= 5 && actionId <= 9)
                            {
                                int cardIndex = actionId - 5; // Play
                                hanabiManager.playManager.ExecuteAction(
                                    currentPlayer, "play", cardIndex, null,
                                    hanabiManager.handManager, hanabiManager.scoreManager,
                                    hanabiManager.shuffleManager, hanabiManager.fireworkDisplayManager);
                            }
                            else if (actionId >= 10 && actionId <= 14)
                            {
                                char colorChar = HanabiSpec.ColorOrder[actionId - 10];
                                int targetPlayer = (currentPlayer + 1) % 2;
                                hanabiManager.playManager.ExecuteAction(
                                    currentPlayer, "hint", targetPlayer, colorChar.ToString(),
                                    hanabiManager.handManager, hanabiManager.scoreManager,
                                    hanabiManager.shuffleManager, hanabiManager.fireworkDisplayManager);
                            }
                            else if (actionId >= 15 && actionId <= 19)
                            {
                                int number = actionId - 14; // 1..5
                                int targetPlayer = (currentPlayer + 1) % 2;
                                hanabiManager.playManager.ExecuteAction(
                                    currentPlayer, "hint", targetPlayer, number.ToString(),
                                    hanabiManager.handManager, hanabiManager.scoreManager,
                                    hanabiManager.shuffleManager, hanabiManager.fireworkDisplayManager);
                            }

                            // ★ここで「返信を受けて適用した」ことを通知（次の手を送れるようにする）
                            hanabiManager.NotifyActionAppliedFromTCP();

                            // ターン完了（内部で終局チェック＆停止）
                            hanabiManager.CompletePlayerTurn();
                        });
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error: " + e.Message);
        }
    }

    void OnApplicationQuit()
    {
        if (listener != null) listener.Stop();
        if (listenerThread != null && listenerThread.IsAlive) listenerThread.Abort();
    }
}
