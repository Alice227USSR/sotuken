import socket
import json
import os
import numpy as np
from inference import predict_action  # 既存の推論関数
import inference  # ファイル先頭で一度importしておけばOK（predict_actionのある同モジュール）
import os, json

# === 観測ベクトル検証オプション ===
OBS_VERIFY = os.getenv("OBS_VERIFY", "0") == "1"           # 1で有効化
GOLDEN_JSON = os.getenv("GOLDEN_JSON", "golden_obs.json")  # 期待観測のパス
OBS_BLOCKS_ENV = os.getenv("OBS_BLOCKS", "")               # 例: "100,50,200,308"
_OBS_GOLDEN = None
_OBS_BLOCKS = None

def _load_golden():
    global _OBS_GOLDEN, _OBS_BLOCKS
    try:
        with open(GOLDEN_JSON, "r", encoding="utf-8") as f:
            data = json.load(f)
        _OBS_GOLDEN = {
            "observation": [int(x) for x in data["observation"]],
            "legal_actions": [float(x) for x in data["legal_actions"]],
        }
        print(f"[OBSCHK] loaded golden from {GOLDEN_JSON}: len(obs)={len(_OBS_GOLDEN['observation'])}, len(mask)={len(_OBS_GOLDEN['legal_actions'])}")
    except Exception as e:
        print(f"[OBSCHK] failed to load golden: {e}")
        _OBS_GOLDEN = None

    if OBS_BLOCKS_ENV.strip():
        try:
            _OBS_BLOCKS = [int(x) for x in OBS_BLOCKS_ENV.split(",") if x.strip()]
            print(f"[OBSCHK] block spec = {_OBS_BLOCKS}")
        except Exception as e:
            print(f"[OBSCHK] invalid OBS_BLOCKS: {e}")
            _OBS_BLOCKS = None

# HLE から idx2move（0..19 の行動ラベル）を取得（失敗時は None）
try:
    from hanabi_learning_environment import rl_env
    _env_moves = rl_env.make(environment_name="Hanabi-Full", num_players=2)  # players は学習時設定に合わせる
    IDX2MOVE = [str(_env_moves.game.get_move(i)) for i in range(_env_moves.num_moves())]
except Exception:
    IDX2MOVE = None

# （必要なら）検証メタを通常応答に一度だけ同梱するフラグ
_idx2move_sent = False


def start_server(host='0.0.0.0', port=9000):
    if OBS_VERIFY:
        _load_golden()
    print(f"[Python] Rainbow推論サーバ 起動中 ({host}:{port})...")

    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.bind((host, port))
        s.listen(1)
        while True:
            conn, addr = s.accept()
            with conn:
                print(f"[Python] Unityと接続: {addr}")
                try:
                    data = conn.recv(8192)
                    if not data:
                        continue

                    msg = json.loads(data.decode('utf-8'))
                    if isinstance(msg, dict) and msg.get("type") == "mask_log_on":
                        inference.DEBUG_MASK = True
                        print("[Python][MASK] debug logging ENABLED")
                        continue

                    if isinstance(msg, dict) and msg.get("type") == "mask_log_off":
                        inference.DEBUG_MASK = False
                        print("[Python][MASK] debug logging DISABLED")
                        continue
                    if isinstance(msg, dict) and msg.get("type") == "idx2move_request":
                        # ★ 検証モード用の特別ハンドラ（ExternalAIController は関係なし）
                        meta = {"type": "idx2move_table", "items": (IDX2MOVE or [])}
                        conn.sendall(json.dumps(meta, ensure_ascii=False).encode('utf-8'))
                        print("[Python] sent idx2move_table to Unity (via 9000)")
                        continue  # 次の接続へ

                    # ---- ここから通常の観測→推論→応答 ----
                    print("[Python] 受信データ構造確認:", list(msg.keys()))
                    # === 観測ベクトル＆合法手の比較（ゴールデンと一致か）===
                    if OBS_VERIFY and _OBS_GOLDEN is not None and isinstance(msg, dict) and "observation" in msg and "legal_actions" in msg:
                        try:
                            obs_rx = [int(x) for x in msg["observation"]]
                            legal_rx = [float(x) for x in msg["legal_actions"]]
                            obs_g = _OBS_GOLDEN["observation"]
                            legal_g = _OBS_GOLDEN["legal_actions"]

                            if len(obs_rx) != len(obs_g) or len(legal_rx) != len(legal_g):
                                print(f"[OBSCHK] ❌ shape mismatch: obs {len(obs_rx)} vs {len(obs_g)}, legal {len(legal_rx)} vs {len(legal_g)}")
                            else:
                                diff_idx = [i for i, (a, b) in enumerate(zip(obs_rx, obs_g)) if a != b]
                                diff_legal = [i for i, (a, b) in enumerate(zip(legal_rx, legal_g)) if not (a == b or (abs(a - b) < 1e-9))]
                                if not diff_idx and not diff_legal:
                                    print("[OBSCHK] ✅ observation & legal_actions PERFECT MATCH")
                                else:
                                    print(f"[OBSCHK] ❌ mismatch: obs_diff={len(diff_idx)}, legal_diff={len(diff_legal)}")
                                    if diff_idx:
                                        head = diff_idx[:10]
                                        print(f"[OBSCHK]   first_obs_diffs_idx={head}")
                                        print(f"[OBSCHK]   sample_rx={[obs_rx[i] for i in head]}")
                                        print(f"[OBSCHK]   sample_g ={[obs_g[i]  for i in head]}")
                                        if _OBS_BLOCKS:
                                            s = 0
                                            for bi, blen in enumerate(_OBS_BLOCKS):
                                                e = s + blen
                                                sum_rx = sum(obs_rx[s:e])
                                                sum_g  = sum(obs_g[s:e])
                                                mark = "OK" if sum_rx == sum_g else "DIFF"
                                                print(f"[OBSCHK]   block#{bi} [{s}:{e}) sum_rx={sum_rx} sum_g={sum_g} -> {mark}")
                                                s = e
                                    if diff_legal:
                                        head = diff_legal[:10]
                                        print(f"[OBSCHK]   legal first diffs idx={head}")
                                        print(f"[OBSCHK]   legal_rx_sample={[legal_rx[i] for i in head]}")
                                        print(f"[OBSCHK]   legal_g_sample ={[legal_g[i]  for i in head]}")
                        except Exception as e:
                            print(f"[OBSCHK] compare failed: {e}")
                    obs = np.array(msg["observation"], dtype=np.uint8)
                    legal = np.array(msg["legal_actions"], dtype=np.float32)

                    action = predict_action(obs, legal)
                    print(f"[Python] 推論完了 → 選択アクション: {action}")

                    resp = {"action": int(action)}
                    global _idx2move_sent
                    # 任意：検証環境変数があるなら、最初だけ通常応答にも同梱（ExternalAIは無視しても壊れない）
                    if os.getenv("VERIFY_ACTIONMAP") == "1" and (not _idx2move_sent) and IDX2MOVE is not None:
                        resp["idx2move_table"] = IDX2MOVE
                        _idx2move_sent = True
                    
                    if isinstance(msg, dict) and msg.get("type") == "golden_test":
                        # Unity から投げ込まれた観測/マスクで 1 回だけ推論して結果を返す
                        try:
                            obs = np.array(msg["observation"], dtype=np.uint8)
                            legal = np.array(msg["legal_actions"], dtype=np.float32)
                            action = predict_action(obs, legal)
                            resp = {"type": "golden_result", "action": int(action)}
                            conn.sendall(json.dumps(resp, ensure_ascii=False).encode('utf-8'))
                            print("[Python] golden_test → action:", action)
                        except Exception as e:
                            err = {"type": "golden_result", "error": str(e)}
                            conn.sendall(json.dumps(err, ensure_ascii=False).encode('utf-8'))
                            print("[Python] golden_test error:", e)
                        continue

                    conn.sendall(json.dumps(resp, ensure_ascii=False).encode('utf-8'))
                    print("[Python] Unityにアクション返信")

                except Exception as e:
                    print("エラー:", e)


if __name__ == "__main__":
    start_server()
