using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using PimDeWitte.UnityMainThreadDispatcher;


public class ActionMapVerificationMode : MonoBehaviour
{
        // 送信の一回化（冪等化）フラグ
    private bool _sentMaskToggle = false;
    private bool _requestedIdx2Move = false;
    private bool _sentGolden = false;

    [Header("Verification Mode")]
    public bool enableVerification = true;     // idx2move 突合せ
    public bool enableMaskCheck = true;        // ★ 追加：合法手マスクのログON/OFFをPythonに指示
    public bool pauseTimeScale = true;
    public MonoBehaviour[] toDisableDuringVerify;

    [Header("Python server for verification")]
    public string pythonHost = "127.0.0.1";
    public int pythonPort = 9000;

    [Header("Golden Comparison")]
    public bool sendGoldenOnStart = false;
    public TextAsset goldenJson;   // dump_golden.py で保存した JSON を Unity に取り込んで割り当て

    private static ActionMapVerificationMode _instance;
    private bool _doneIdx2Move;

    [Serializable]
    private class Idx2MoveResp { public string type; public List<string> items; }

    // 期待アクション比較（オプション）
    [Header("Golden Compare Options")]
    public bool compareExpectedAction = true;

    private void Awake()
    {
        // idx2move_table 要求（1回だけ）
        if (enableVerification && !_requestedIdx2Move)
        {
            _requestedIdx2Move = true;
            Debug.Log("[U-VERIFY] ActionMap verification ENABLED. Requesting idx2move_table from " + pythonHost + ":" + pythonPort);
            new Thread(RequestIdx2MoveThread).Start();
        }

        // マスクログのON指示（1回だけ）
        if (enableMaskCheck && !_sentMaskToggle)
        {
            _sentMaskToggle = true;
            Debug.Log("[U-VERIFY] Mask check ENABLED. Sending mask_log_on to " + pythonHost + ":" + pythonPort);
            new Thread(() => SendSimpleCommand("{\"type\":\"mask_log_on\"}")).Start();
        }

        // ゴールデン比較の送信（TextAsset.text をメインスレッドで読む → 文字列をスレッドへ）
        if (sendGoldenOnStart && goldenJson != null && !_sentGolden)
        {
            _sentGolden = true;
            Debug.Log("[U-VERIFY] Golden test: sending payload to Python");

            // ★TextAsset をバックグラウンドスレッドで触らない！
            string goldenText = goldenJson.text; // ←ここはメインスレッド

            new Thread(() =>
            {
                try
                {
                    SendGoldenPayload(goldenText);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[U-VERIFY] send golden payload failed: " + e.Message);
                }
            }).Start();
        }

        // （必要なら）検証中はゲーム進行を止める
        if (pauseTimeScale) Time.timeScale = 0f;
        if (toDisableDuringVerify != null)
        {
            foreach (var mb in toDisableDuringVerify)
            {
                if (mb != null) mb.enabled = false;
            }
        }
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (pauseTimeScale) Time.timeScale = 1f;

        if (enableMaskCheck)
        {
            Debug.Log($"[U-VERIFY] Mask check DISABLE. Sending mask_log_off to {pythonHost}:{pythonPort}");
            new Thread(() => SendSimpleCommand("mask_log_off")).Start();
        }
    }

    // ===== idx2move 検証 =====

    public static void ReceiveIdx2Move(string[] items)
    {
        if (_instance == null || !_instance.enableVerification || _instance._doneIdx2Move) return;
        _instance.RunVerification(items);
    }

    private void RequestIdx2MoveThread()
    {
        try
        {
            using (var client = new TcpClient())
            {
                client.NoDelay = true;
                client.Connect(pythonHost, pythonPort);
                using (var stream = client.GetStream())
                {
                    var reqJson = "{\"type\":\"idx2move_request\"}";
                    var outBytes = Encoding.UTF8.GetBytes(reqJson);
                    stream.Write(outBytes, 0, outBytes.Length);

                    var sb = new StringBuilder();
                    var buf = new byte[4096];
                    stream.ReadTimeout = 2000;
                    int n = stream.Read(buf, 0, buf.Length);
                    if (n > 0) sb.Append(Encoding.UTF8.GetString(buf, 0, n));
                    while (stream.DataAvailable)
                    {
                        n = stream.Read(buf, 0, buf.Length);
                        if (n > 0) sb.Append(Encoding.UTF8.GetString(buf, 0, n));
                        else break;
                    }
                    var json = sb.ToString();

                    Idx2MoveResp resp = null;
                    try { resp = JsonUtility.FromJson<Idx2MoveResp>(json); }
                    catch (Exception ex) { Debug.LogWarning("[U-VERIFY] idx2move parse failed: " + ex.Message); }

                    if (resp != null && resp.type == "idx2move_table" && resp.items != null && resp.items.Count == 20)
                    {
                        var items = resp.items.ToArray();
                        UnityMainThreadDispatcher.Instance().Enqueue(() => ReceiveIdx2Move(items));
                    }
                    else
                    {
                        Debug.LogWarning("[U-VERIFY] idx2move_table not returned or invalid. Raw=" + json);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[U-VERIFY] idx2move request failed: " + e.Message);
        }
    }

    private void RunVerification(string[] pythonIdx2Move)
    {
        _doneIdx2Move = true;

        var local = BuildLocalIdx2Move();
        if (pythonIdx2Move == null || pythonIdx2Move.Length != local.Length)
        {
            Debug.LogError($"[U-VERIFY] idx2move length mismatch: python={pythonIdx2Move?.Length ?? -1}, unityLocal={local.Length}");
            return;
        }

        int mismatches = 0;
        for (int i = 0; i < local.Length; i++)
        {
            string py = pythonIdx2Move[i];
            string un = local[i];
            if (!string.Equals(py, un, StringComparison.Ordinal))
            {
                mismatches++;
                Debug.LogWarning($"[ACTMAP] mismatch at {i}: python='{py}'  unity='{un}'");
            }
        }

        if (mismatches == 0)
            Debug.Log("[U-VERIFY] ✅ Action mapping PERFECT MATCH (all indices)");
        else
            Debug.LogWarning($"[U-VERIFY] ⚠️ Action mapping had {mismatches} mismatch(es). Check color order / hand indices.");
    }

    private static string[] BuildLocalIdx2Move()
    {
        List<string> v = new List<string>(20);
        for (int i = 0; i < 5; i++) v.Add($"(Discard {i})");
        for (int i = 0; i < 5; i++) v.Add($"(Play {i})");
        string[] colors = { "R", "Y", "G", "W", "B" };
        foreach (var c in colors) v.Add($"(Reveal player +1 color {c})");
        for (int r = 1; r <= 5; r++) v.Add($"(Reveal player +1 rank {r})");
        return v.ToArray();
    }

    public static bool IsActive()
    {
        return _instance != null && (_instance.enableVerification || _instance.enableMaskCheck);
    }

    // ===== Python への簡易コマンド送信（mask_log_on/off） =====
    private void SendSimpleCommand(string type)
    {
        try
        {
            using (var client = new TcpClient())
            {
                client.NoDelay = true;
                client.Connect(pythonHost, pythonPort);
                using (var stream = client.GetStream())
                {
                    var reqJson = "{\"type\":\"" + type + "\"}";
                    var outBytes = Encoding.UTF8.GetBytes(reqJson);
                    stream.Write(outBytes, 0, outBytes.Length);
                    // 応答は不要
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[U-VERIFY] send '" + type + "' failed: " + e.Message);
        }
    }
    
    [Serializable]
    private class GoldenPayload
    {
        public int[] observation;
        public float[] legal_actions;
        public int expected_action = -1; // 無ければ -1
    }

    [Serializable]
    private class GoldenResult
    {
        public string type;
        public int action;
        public string error;
    }

    private void SendGoldenPayload(string jsonText)
    {
        // 期待アクションをローカルに保持（比較用）
        int expected = -1;
        try
        {
            var gp = JsonUtility.FromJson<GoldenPayload>(jsonText);
            if (gp != null) expected = gp.expected_action;
        }
        catch { /* JSONに expected_action が無くてもOK */ }

        try
        {
            using (var client = new TcpClient())
            {
                client.NoDelay = true;
                client.Connect(pythonHost, pythonPort);
                using (var stream = client.GetStream())
                {
                    // Pythonが期待する {"type":"golden_test", ...} 形式にする
                    string payload = "{\"type\":\"golden_test\"," + jsonText.Trim().TrimStart('{');
                    var outBytes = Encoding.UTF8.GetBytes(payload);
                    stream.Write(outBytes, 0, outBytes.Length);

                    // 返答を受信
                    var sb = new StringBuilder();
                    var buf = new byte[2048];
                    stream.ReadTimeout = 3000;
                    int n = stream.Read(buf, 0, buf.Length);
                    if (n > 0) sb.Append(Encoding.UTF8.GetString(buf, 0, n));
                    var resp = sb.ToString();

                    if (!string.IsNullOrEmpty(resp))
                    {
                        // 解析
                        GoldenResult gr = null;
                        try { gr = JsonUtility.FromJson<GoldenResult>(resp); } catch { }
                        if (gr != null && string.IsNullOrEmpty(gr.error))
                        {
                            Debug.Log("[U-VERIFY] Golden result <= action=" + gr.action +
                                      (expected >= 0 ? " (expected=" + expected + ")" : ""));

                            if (compareExpectedAction && expected >= 0)
                            {
                                if (gr.action == expected)
                                    Debug.Log("<color=#12c98f>[U-VERIFY] ✅ Golden compare PASS (action matched)</color>");
                                else
                                    Debug.LogWarning("[U-VERIFY] ❌ Golden compare MISMATCH (got " + gr.action + ", expected " + expected + ")");
                            }
                        }
                        else
                        {
                            Debug.LogWarning("[U-VERIFY] Golden result error: " + resp);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[U-VERIFY] Golden result: empty response");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[U-VERIFY] send golden payload failed: " + e.Message);
        }
    }
}
