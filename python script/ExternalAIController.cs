// Assets/hanabi/python script/ExternalAIController.cs
using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class ExternalAIController : MonoBehaviour
{
    [SerializeField] private HanabiManager manager;

    private const string HOST = "127.0.0.1";
    private const int PORT_SEND = 9000;

    private bool awaiting = false; // 返信待ちの間は再送しない

    // Python(C++) 側に合わせた色順: R, Y, G, W, B
    private static readonly char[] COLOR_ORDER = { 'R','Y','G','W','B' };

    [Serializable]
    private struct HanabiMessage
    {
        public float[] observation;
        public float[] legal_actions; // 1.0=合法 / 0.0=違法
    }

    // TCPReceiver が行動を適用したら呼ぶ（次手送信を許可）
    public void NotifyActionApplied() => awaiting = false;

    public async void SendObservationToPython(int playerIndex)
    {
        if (manager == null)
        {
            Debug.LogError("[ExternalAI] HanabiManager 未設定");
            return;
        }
        if (awaiting) return;                      // 多重送信防止
        if (manager.IsTerminalLikeHLE()) return;   // 終局は送らない

        // 観測（既存実装）
        float[] obs = manager.playManager.GetObservation(
            playerIndex, manager.handManager, manager.scoreManager, manager.shuffleManager);

        // ★マスク（Python順 0/1）
        float[] legal01 = BuildLegalMaskPythonOrder(manager, playerIndex);

        var msg = new HanabiMessage { observation = obs, legal_actions = legal01 };
        string json = JsonUtility.ToJson(msg) + "\n";

        try
        {
            awaiting = true;
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(HOST, PORT_SEND);
                using (var stream = client.GetStream())
                {
                    byte[] data = Encoding.UTF8.GetBytes(json);
                    await stream.WriteAsync(data, 0, data.Length);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ExternalAI] 送信失敗: {ex.Message}");
            awaiting = false; // 送れていないので解除
        }
    }

    // --- ここから下：マスク生成（仕様合わせのみ・安全策なし） ---

    // 20長 0/1 マスクを Python（HLE）の並びに厳密一致で構築
    // 並び: 0-4 Discard[i], 5-9 Play[i], 10-14 Color(R,Y,G,W,B), 15-19 Rank(1..5)
    private static float[] BuildLegalMaskPythonOrder(HanabiManager gm, int selfIndex)
    {
        var mask = new float[20]; // 既定 0.0 = 違法

        // 手札は handManager.hands 配下にある
        var hands = gm.handManager.hands;
        int playersCount = hands.Count;
        int target = (selfIndex + 1) % playersCount; // 相対 +1（2人戦）

        var my = hands[selfIndex].hand;  // List<Card>
        var op = hands[target].hand;     // List<Card>

        int hints = gm.scoreManager.hints; // 情報トークン（0..8）
        int myCount  = my.Count;
        int oppCount = op.Count;

        // ---- 0–4: Discard[i]（カードがあり かつ hints<8）----
        bool canDiscard = (hints < 8);
        for (int i = 0; i < 5; i++)
            mask[i] = (i < myCount && my[i] != null && canDiscard) ? 1f : 0f;

        // ---- 5–9: Play[i]（カードがあれば合法）----
        for (int i = 0; i < 5; i++)
            mask[5 + i] = (i < myCount && my[i] != null) ? 1f : 0f;

        // ---- 10–19: ヒント（info>0 かつ 相手に該当がある）----
        if (hints > 0 && oppCount > 0)
        {
            // 相手手札の存在フラグ
            bool hasR=false, hasY=false, hasG=false, hasW=false, hasB=false;
            bool[] hasRank = new bool[6]; // 1..5

            for (int j = 0; j < oppCount; j++)
            {
                var c = op[j];
                if (c == null) continue;

                // Card のプロパティ名はプロジェクトに合わせる
                char col = c.ColorChar;    // 例: 'R','Y','G','W','B'
                int  rnk = c.number;       // 1..5

                switch (col)
                {
                    case 'R': hasR = true; break;
                    case 'Y': hasY = true; break;
                    case 'G': hasG = true; break;
                    case 'W': hasW = true; break;
                    case 'B': hasB = true; break;
                }
                if (1 <= rnk && rnk <= 5) hasRank[rnk] = true;
            }

            // 10–14: Color(R,Y,G,W,B) の順
            mask[10] = hasR ? 1f : 0f;
            mask[11] = hasY ? 1f : 0f;
            mask[12] = hasG ? 1f : 0f;
            mask[13] = hasW ? 1f : 0f;
            mask[14] = hasB ? 1f : 0f;

            // 15–19: Rank(1..5)
            for (int r = 1; r <= 5; r++)
                mask[14 + r] = hasRank[r] ? 1f : 0f;
        }

        return mask;
    }
}
