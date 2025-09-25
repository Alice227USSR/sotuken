// PlayManager.cs
// プレイ、捨て、ヒントのアクション処理・合法手生成・観測構築

using System;
using System.Collections.Generic;


public class PlayManager
{
    public LastAction lastAction = null;
    public class LastAction
    {
        public int actingPlayer;      // 行動を行ったプレイヤー（0 or 1）
        public string actionType;     // "play", "discard", "hint"
        public int targetPlayer;      // hint の対象（それ以外は -1）
        public int cardIndex;         // 対象カードのインデックス（0〜4）
        public string hintColor;      // "R", "G", "B", "Y", "W"（hint時）
        public int hintNumber;        // 1〜5（hint時）
        public bool success;          // 成功（play） or 失敗
        public Card actualCard;       // 実際にプレイ/破棄されたカード
    }
    public void ExecuteAction(int playerIndex, string actionType, int targetIndex, string hintInfo,
                               HandManager handManager, ScoreManager scoreManager, ShuffleManager shuffleManager, FireworkDisplayManager fireworkDisplayManager)
    {
        if (scoreManager.lives <= 0)
        {
            UnityEngine.Debug.Log($"[Action Ignored] Lives are 0. Player {playerIndex}'s action is skipped.");
            return;
        }
        var player = handManager.hands[playerIndex];

        if (actionType == "play")
        {
            var card = player.hand[targetIndex];
            string color = card.color;
            int number = card.number;

            // ← 成否を先に確定しておく
            bool wasSuccess = (scoreManager.fireworks[color] == number - 1);

            if (wasSuccess)
            {
                scoreManager.fireworks[color]++;
                scoreManager.score++;
                UnityEngine.Debug.Log($"Player {playerIndex} successfully played {color}{number}");
                UnityEngine.Debug.Log($"Firework {color} is now {scoreManager.fireworks[color]}");
                int playedNumber = number; // または card.number
                if (playedNumber == 5 && scoreManager.hints < 8)
                {
                    scoreManager.hints += 1;
                    if (scoreManager.hints > 8) scoreManager.hints = 8;
                }

            }
            else
            {
                scoreManager.lives--;
                scoreManager.discardPile.Add(card);
                UnityEngine.Debug.Log($"Player {playerIndex} failed to play {color}{number} (Lives left: {scoreManager.lives})");
            }

            DrawNewCard(player, shuffleManager, targetIndex);

            lastAction = new LastAction
            {
                actingPlayer = playerIndex,
                actionType = "play",
                targetPlayer = -1,
                cardIndex = targetIndex,
                actualCard = card,
                success = wasSuccess,   // ← ここを修正
                hintColor = null,
                hintNumber = -1
            };
        }
        else if (actionType == "discard")
        {
            var card = player.hand[targetIndex];
            scoreManager.discardPile.Add(card);
            if (scoreManager.hints < 8) scoreManager.hints++;
            DrawNewCard(player, shuffleManager, targetIndex);
            UnityEngine.Debug.Log($"Player {playerIndex} discarded {card.color}{card.number}");
            lastAction = new LastAction
            {
                actingPlayer = playerIndex,
                actionType = "discard",
                targetPlayer = -1,
                cardIndex = targetIndex,
                actualCard = card,
                success = true,
                hintColor = null,
                hintNumber = -1
            };
        }
        else if (actionType == "hint")
        {
            if (scoreManager.hints == 0)
            {
                UnityEngine.Debug.Log("No hints available");
                return;
            }

            scoreManager.hints--;
            int targetPlayer = targetIndex;
            var target = handManager.hands[targetPlayer];

            bool isColorHint = IsColorHint(hintInfo);  // 色か数字か判定
            for (int i = 0; i < target.hand.Count; i++)
            {
                var card = target.hand[i];
                var know = target.knowledge[i];

                if (isColorHint)
                {
                    char hc = HanabiSpec.NormalizeColorString(hintInfo);
                    int ci = HanabiSpec.ColorToIndex(hc);
                    if (card.color == hintInfo)
                    {
                        // 当たり：その色のみ true
                        know.possibleColors = OnlyTrueForColor(hintInfo);
                    }
                    else
                    {
                        // 外れ：その色を false にする
                        if (ci >= 0) know.possibleColors[ci] = false;
                    }
                }
                else
                {
                    int n = int.Parse(hintInfo);
                    if (card.number == n)
                    {
                        // 当たり：その数字のみ true
                        know.possibleNumbers = OnlyTrueForNumber(n);
                    }
                    else
                    {
                        // 外れ：その数字を false にする（0-based）
                        know.possibleNumbers[n - 1] = false;
                    }
                }
            }

            UnityEngine.Debug.Log($"Player {playerIndex} gave hint to Player {targetPlayer}: {hintInfo}");
            lastAction = new LastAction
            {
                actingPlayer = playerIndex,
                actionType = "hint",
                targetPlayer = targetIndex,
                cardIndex = -1,
                actualCard = null,
                success = true,
                hintColor = isColorHint ? hintInfo : null,
                hintNumber = isColorHint ? -1 : int.Parse(hintInfo)
            };
        }
    }

    private void DrawNewCard(Player player, ShuffleManager shuffleManager, int replaceIndex)
    {
        var newCard = shuffleManager.Draw();
        if (newCard != null)
        {
            player.hand[replaceIndex] = newCard;
            player.knowledge[replaceIndex] = new CardKnowledge();
        }
        else
        {
            player.hand.RemoveAt(replaceIndex);
            player.knowledge.RemoveAt(replaceIndex);
        }
    }

    private bool[] OnlyTrueForColor(string color)
    {
        var result = new bool[5];
        char c = HanabiSpec.NormalizeColorString(color);
        int ci = HanabiSpec.ColorToIndex(c);
        if (ci >= 0) result[ci] = true;
        return result;
    }

    private bool[] OnlyTrueForNumber(int number)
    {
        var result = new bool[5];
        for (int i = 0; i < 5; i++)
            result[i] = ((i + 1) == number);
        return result;
    }

    public List<HanabiAction> GetLegalActions(int playerIndex, HandManager handManager, ScoreManager scoreManager)
    {
        var legalActions = new List<HanabiAction>();
        var player = handManager.hands[playerIndex];
        int handSize = player.hand.Count;

        for (int i = 0; i < handSize; i++)
        {
            legalActions.Add(new HanabiAction("play", i));
            legalActions.Add(new HanabiAction("discard", i));
        }

        if (scoreManager.hints > 0)
        {
            for (int target = 0; target < handManager.hands.Count; target++)
            {
                if (target == playerIndex) continue;
                foreach (var c in HanabiSpec.ColorOrder) // R,Y,G,W,B
                    legalActions.Add(new HanabiAction("hint", target, c.ToString()));
                for (int n = 1; n <= 5; n++)
                    legalActions.Add(new HanabiAction("hint", target, n.ToString()));
            }
        }

        return legalActions;
    }
    public float[] GetObservation(int playerIndex, HandManager handManager, ScoreManager scoreManager, ShuffleManager shuffleManager)
    {

        float[] obs = new float[658];
        int offset = 0;
        int N  = handManager.hands.Count; // プレイヤー数（2人戦なら2）
        int me = playerIndex;             // 観測者（自分）のインデックス
        // 色順は常に HLE 準拠（RYGWB）
        var colorOrder = HanabiSpec.ColorOrder; // char[] = {'R','Y','G','W','B'}
        void Check(string label, int expectMax)
        {
            if (offset > expectMax)
                UnityEngine.Debug.LogError($"[GetObservation] {label}: offset={offset} > {expectMax}");
        }

        // === Step 1: 他プレイヤーの手札（25bit×枚数） ===
        for (int p = 0; p < handManager.hands.Count; p++)
        {
            if (p == playerIndex) continue;
            foreach (var card in handManager.hands[p].hand)
            {
                int ci = HanabiSpec.ColorToIndex(HanabiSpec.NormalizeColorString(card.color));
                int ri = card.number - 1;
                if (ci >= 0 && ri >= 0)
                    obs[offset + (ri * 5 + ci)] = 1f;   // ← ここを rank-major に
                offset += 25;
            }
        }
        Check("after Step1", 25*5);
        // 手札不足分はスキップ
        offset += (5 - handManager.hands[(playerIndex + 1) % 2].hand.Count) * 25;


        // === Step 2: 各プレイヤーの手札枚数不足フラグ ===
        for (int p = 0; p < handManager.hands.Count; p++)
        {
            obs[offset++] = (handManager.hands[p].hand.Count < 5) ? 1f : 0f;
        }
        Check("after Step2", 25 * 5 + handManager.hands.Count);

        // === Step 3: ボード情報 ===
        // 山札 (thermometer形式) 40bit固定
        int deckSize = shuffleManager.Remaining();
        int fill = Math.Min(deckSize, 40);   // ← ここが重要
        for (int i = 0; i < fill; i++) obs[offset + i] = 1f;
        offset += 40;

        // 花火進捗 (各色ランクをone-hot)
        foreach (var c in colorOrder) // R,Y,G,W,B の順で固定
        {
            string key = c.ToString();
            if (!scoreManager.fireworks.TryGetValue(key, out int num))
            {
                UnityEngine.Debug.LogWarning($"[GetObservation] fireworks missing key: '{key}'. Auto-inserting 0.");
                scoreManager.fireworks[key] = 0; // その場で補正
                num = 0;
            }
            if (num > 0) obs[offset + num - 1] = 1f;
            offset += 5;
        }

        // hints
        int hfill = Math.Min(scoreManager.hints, 8);
        for (int i = 0; i < hfill; i++) obs[offset + i] = 1f;
        offset += 8;

        // lives
        int lfill = Math.Min(scoreManager.lives, 3);
        for (int i = 0; i < lfill; i++) obs[offset + i] = 1f;
        offset += 3;

        Check("after Step3", 25 * 5 + handManager.hands.Count + 40 + 25 + 8 + 3);

        // === Step 4: 捨て札 (Canonicalと完全一致、色ごと数字ごとthermometer) ===
        foreach (var c in colorOrder)
        {
            for (int number = 1; number <= 5; number++)
            {
                int count = scoreManager.discardPile
                    .FindAll(card => card.color[0] == c && card.number == number).Count;
                int maxCards = (number == 1) ? 3 : (number == 5 ? 1 : 2);
                for (int i = 0; i < maxCards; i++) obs[offset++] = (i < count) ? 1f : 0f;
            }
        }
        Check("after Step4", 25 * 5 + handManager.hands.Count + 40 + 25 + 8 + 3 + 50);

        // === Step 5: LastAction (HLE準拠：観測者相対 + ヒント結果) ===
        if (lastAction != null)
        {

            // 5-1) 行動者（観測者相対）: HLEは4bit確保だが2人戦でも4bit領域を使う
            int relActor = (lastAction.actingPlayer - me + N) % N;
            obs[offset + relActor] = 1f;
            offset += 4;

            // 5-2) 行動タイプ: play/discard/hint-color/hint-number など 4bit 確保（既存踏襲）
            int actionTypeIdx =
                (lastAction.actionType == "play") ? 0 :
                (lastAction.actionType == "discard") ? 1 :
                (lastAction.hintColor != null) ? 2 : 3;
            obs[offset + actionTypeIdx] = 1f;
            offset += 4;

            // 5-3) ヒント対象（観測者相対）
            if (lastAction.actionType == "hint")
            {
                int relTargetFromActor = (lastAction.targetPlayer - lastAction.actingPlayer + N) % N;
                int relTarget = (relActor + relTargetFromActor) % N;
                obs[offset + relTarget] = 1f;
            }
            offset += 2; // 2人戦想定のまま

            // 5-4) ヒント色
            if (lastAction.hintColor != null)
            {
                char hc = HanabiSpec.NormalizeColorString(lastAction.hintColor);
                int ci = HanabiSpec.ColorToIndex(hc);
                if (ci >= 0) obs[offset + ci] = 1f;
            }
            offset += 5;

            // 5-5) ヒント数字
            if (lastAction.hintNumber >= 1)
                obs[offset + (lastAction.hintNumber - 1)] = 1f;
            offset += 5;

            // 5-6) ヒント結果（hand_size=5 分：当たった手札位置に1）
            if (lastAction.actionType == "hint")
            {
                var tgt = handManager.hands[lastAction.targetPlayer];
                for (int i = 0; i < 5; i++)
                {
                    bool hit = false;
                    if (i < tgt.hand.Count)
                    {
                        var c = tgt.hand[i];
                        if (lastAction.hintColor != null)
                        {
                            int ciC = HanabiSpec.ColorToIndex(HanabiSpec.NormalizeColorString(c.color));
                            int ciH = HanabiSpec.ColorToIndex(HanabiSpec.NormalizeColorString(lastAction.hintColor));
                            hit = (ciC == ciH);
                        }
                        else if (lastAction.hintNumber >= 1)
                        {
                            hit = (c.number == lastAction.hintNumber);
                        }
                    }
                    obs[offset + i] = hit ? 1f : 0f;
                }
            }
            offset += 5;

            // 5-7) カード位置 (play/discard)
            if (lastAction.cardIndex >= 0)
                obs[offset + lastAction.cardIndex] = 1f;
            offset += 5;

            // 5-8) カード内容 (25bit)
            if (lastAction.actualCard != null)
            {
                int ci = HanabiSpec.ColorToIndex(HanabiSpec.NormalizeColorString(lastAction.actualCard.color));
                int ri = lastAction.actualCard.number - 1;
                if (ci >= 0 && ri >= 0) obs[offset + (ri * 5 + ci)] = 1f;  // ← rank-major
            }
            offset += 25;
        }
        else
        {
            offset += 55; // LastAction無し
        }
        Check("after Step5", 25 * 5 + handManager.hands.Count + 40 + 25 + 8 + 3 + 50 + 55);

        // === Step 6: カード知識（canonical準拠） ===
        // 仕様（2人戦・手札最大5枚を想定）:
        // 自分の手: 各スロット [25bit 候補(rank-major)] + [5bit 明示色] + [5bit 明示ランク]
        //   - 25bitは knowledge の AND（possibleColors × possibleNumbers）で立てる
        //   - 明示色/明示ランクは single-true のときのみ該当ビットを1
        // 相手の手: 各スロット [25bit 実カード(rank-major)] + [5bit 明示色=実カード色] + [5bit 明示ランク=実カードランク]

        // --- 自分の手 ---
        {
            var meHand = handManager.hands[me];
            for (int i = 0; i < 5; i++)
            {
                if (i < meHand.hand.Count)
                {
                    var know = meHand.knowledge[i];

                    // 25bit: knowledge AND（rank-major）
                    for (int k = 0; k < 25; k++) obs[offset + k] = 0f;
                    for (int ri = 0; ri < 5; ri++)
                    {
                        if (!know.possibleNumbers[ri]) continue;
                        for (int ci = 0; ci < 5; ci++)
                        {
                            if (!know.possibleColors[ci]) continue;
                            int bit = ri * 5 + ci;
                            obs[offset + bit] = 1f;
                        }
                    }
                    offset += 25;

                    // 明示色5bit: single-true のときのみ
                    for (int k = 0; k < 5; k++) obs[offset + k] = 0f;
                    {
                        int cnt = 0, last = -1;
                        for (int ci = 0; ci < 5; ci++) { if (know.possibleColors[ci]) { cnt++; last = ci; } }
                        if (cnt == 1 && last >= 0) obs[offset + last] = 1f;
                    }
                    offset += 5;

                    // 明示ランク5bit: single-true のときのみ
                    for (int k = 0; k < 5; k++) obs[offset + k] = 0f;
                    {
                        int cnt = 0, last = -1;
                        for (int ri = 0; ri < 5; ri++) { if (know.possibleNumbers[ri]) { cnt++; last = ri; } }
                        if (cnt == 1 && last >= 0) obs[offset + last] = 1f;
                    }
                    offset += 5;
                }
                else
                {
                    // スロット空: 25+5+5 = 35bit をゼロ埋め
                    offset += 35;
                }
            }
        }

        // --- 相手の手（2人戦: 相手は1人）---
        {
            int opp = (me + 1) % N;
            var other = handManager.hands[opp];
            for (int i = 0; i < 5; i++)
            {
                if (i < other.hand.Count)
                {
                    var c = other.hand[i];
                    // 25bit: 実カード rank-major
                    for (int k = 0; k < 25; k++) obs[offset + k] = 0f;
                    int ci = HanabiSpec.ColorToIndex(HanabiSpec.NormalizeColorString(c.color)); // R,Y,G,W,B -> 0..4
                    int ri = c.number - 1; // 1..5 -> 0..4
                    if (ci >= 0 && ri >= 0) obs[offset + (ri * 5 + ci)] = 1f;
                    offset += 25;

                    // 明示色5bit：実カードの色を1点立て
                    for (int k = 0; k < 5; k++) obs[offset + k] = 0f;
                    if (ci >= 0) obs[offset + ci] = 1f;
                    offset += 5;

                    // 明示ランク5bit：実カードのランクを1点立て
                    for (int k = 0; k < 5; k++) obs[offset + k] = 0f;
                    if (ri >= 0) obs[offset + ri] = 1f;
                    offset += 5;
                }
                else
                {
                    // スロット空: 25+5+5 = 35bit をゼロ埋め
                    offset += 35;
                }
            }
        }

        UnityEngine.Debug.Assert(offset == 658, $"[GetObservation] total length mismatch: {offset}");
        return obs;
    }

private int GetColorIndex(string color)
{
    switch (color)
    {
        case "R": return 0;
        case "G": return 1;
        case "B": return 2;
        case "Y": return 3;
        case "W": return 4;
        default: return 0;
    }
}

private bool IsColorHint(string hint)
{
    char c = HanabiSpec.NormalizeColorString(hint);
    return HanabiSpec.ColorToIndex(c) >= 0;
}

// 25bit + 5bit + 5bit を埋める（自分の手の knowledge 用）
// HanabiSpec.ColorOrder = {'R','Y','G','W','B'} 前提
private void EncodeKnowledgeBits(float[] obs, ref int offset, CardKnowledge know)
{
    // --- 25bit: 色×ランク（AND） Rank-major に統一
    for (int k = 0; k < 25; k++) obs[offset + k] = 0f;
    for (int ri = 0; ri < 5; ri++)
    {
        if (!know.possibleNumbers[ri]) continue;
        for (int ci = 0; ci < 5; ci++)
        {
            if (!know.possibleColors[ci]) continue;
            int bit = ri * 5 + ci;   // rank-major（HLEの canonical と整合）
            obs[offset + bit] = 1f;
        }
    }
    offset += 25;

    // --- 明示色5bit：possibleColors が 1つだけ true のとき
    for (int k = 0; k < 5; k++) obs[offset + k] = 0f;
    {
        int cnt = 0, last = -1;
        for (int ci = 0; ci < 5; ci++) if (know.possibleColors[ci]) { cnt++; last = ci; }
        if (cnt == 1 && last >= 0) obs[offset + last] = 1f;
    }
    offset += 5;

    // --- 明示ランク5bit：possibleNumbers が 1つだけ true のとき
    for (int k = 0; k < 5; k++) obs[offset + k] = 0f;
    {
        int cnt = 0, last = -1;
        for (int ri = 0; ri < 5; ri++) if (know.possibleNumbers[ri]) { cnt++; last = ri; }
        if (cnt == 1 && last >= 0) obs[offset + last] = 1f;
    }
    offset += 5;
}


private int IsSingleTrue(bool[] arr)
{
    if (arr == null || arr.Length != 5) return -1;
    int idx = -1; int cnt = 0;
    for (int i = 0; i < 5; i++)
    {
        if (arr[i]) { idx = i; cnt++; }
    }
    return (cnt == 1) ? idx : -1;
}
    
}

public class HanabiAction
{
    public string actionType;
    public int targetIndex;
    public string hintInfo;

    public HanabiAction(string actionType, int targetIndex = -1, string hintInfo = null)
    {
        this.actionType = actionType;
        this.targetIndex = targetIndex;
        this.hintInfo = hintInfo;
    }

    
}

