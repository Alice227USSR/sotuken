import os
# os.environ['CUDA_VISIBLE_DEVICES'] = '-1'
import gin
import tensorflow as tf
import json
# === 追加：先頭付近の import の下あたりに置くと見通し良い ===
import numpy as np
DEBUG_MASK = os.getenv("MASK_LOG", "0") == "1"

def _ensure_legal_mask(legal_actions, num_actions):
    """Unityから来た 0/1 マスクを、Rainbow用の加算マスク(合法=0.0, 非合法=-inf)に正規化する。"""
    la = np.asarray(legal_actions)

    # すでに 0/-inf 形式（-inf を含む）なら、その意味を尊重して 0/-inf に整形
    if la.dtype.kind == 'f' and np.isneginf(la).any():
        mask = np.full(num_actions, -np.inf, dtype=np.float32)
        mask[la == 0.0] = 0.0
        return mask

    # float32へ
    if la.dtype != np.float32:
        la = la.astype(np.float32, copy=False)

    # 基本形：0/1（または0/正の二値）は「>0.5 を合法」として 0/-inf 化
    mask = np.full(num_actions, -np.inf, dtype=np.float32)
    if la.min() >= 0.0 and la.max() <= 1.0:
        mask[la > 0.5] = 0.0
        return mask

    # 保険：非ゼロを合法とみなす
    mask[la != 0.0] = 0.0
    return mask

from hanabi_learning_environment import rl_env
from rainbow_agent import RainbowAgent

def _normalize_legal_mask(legal):
    """
    Unityから来る legal_actions を、HLE/Rainbowが期待する
    '加算マスク'（合法=0.0 / 非合法=-inf）の1次元(20,)に正規化する。
    - 0/1 配列なら: 1→0.0, 0→-inf
    - 既に 0/-inf なら: 0→0.0, (-大値)→-inf に揃える
    - その他の値が混在しても、0.5閾値で 0/1 として扱う安全弁付き
    """
    arr = np.asarray(legal, dtype=np.float32).reshape(-1)
    if arr.size != 20:
        raise ValueError(f"legal_actions length must be 20, got {arr.size}")

    # すでに -inf/0 を含むなら、符号で解釈して再正規化
    if np.isneginf(arr).any():
        mask = np.where(np.isneginf(arr), -np.inf, 0.0).astype(np.float32)
        return mask

    # 0/1 か、0.0/1.0 近辺なら閾値で2値化
    if ((arr >= 0.0) & (arr <= 1.0)).all():
        bin01 = (arr > 0.5).astype(np.float32)
        mask = np.where(bin01 > 0.5, 0.0, -np.inf).astype(np.float32)
        return mask

    # 万一その他の実数が来ても “0以上→合法、負→非合法” とみなすフォールバック
    mask = np.where(arr > 0.0, 0.0, -np.inf).astype(np.float32)
    return mask

env = rl_env.make()
idx2move = [str(env.game.get_move(i)) for i in range(env.num_moves())]
print("\n".join(f"{i}: {m}" for i, m in enumerate(idx2move)))

# ginファイルの読み込み
gin_files = ['configs/hanabi_rainbow.gin']  # ginファイルのパス
gin.parse_config_files_and_bindings(gin_files, bindings=[])

# 環境とエージェントの初期化（gin設定で行うため明示的に書かない）
env = rl_env.make()
observation_vector_shape = env.vectorized_observation_shape()[0]
print(f"観測ベクトルサイズ: {observation_vector_shape}")

agent = RainbowAgent(
    num_actions=env.num_moves(),
    observation_size=observation_vector_shape,
    num_players=env.players
)

checkpoint_path = "results/checkpoints/tf_ckpt-2150"

# with tf.compat.v1.Session() as sess:
#     sess.run(tf.compat.v1.global_variables_initializer())
#     saver = tf.compat.v1.train.Saver()
#     saver.restore(sess, checkpoint_path)
#     print("✅ チェックポイントロード成功")

#     # 推論テスト
#     obs = env.reset()
#     observation = obs['player_observations'][0]['vectorized']
#     action = agent._select_action(observation)
#     print("✅ 推論成功, 選択されたアクション:", action)

# 以下のように書き直す：

agent._saver.restore(agent._sess, checkpoint_path)
print("チェックポイントロード成功")

# チェックポイントのロード後に評価モードへ
agent.eval_mode = True  # ← これが超重要。選択時に epsilon_eval を使う

# 念のため（ginで変えていた場合に備えて）ゼロ固定
agent.epsilon_eval = 0.0
agent.epsilon_train = 0.0  # 誤って訓練モードに戻っても探索しないよう保険

# 推論テスト
obs = env.reset()
observation = obs['player_observations'][0]['vectorized']
legal_actions = np.zeros(agent.num_actions, dtype=np.float32)  # 適当に仮置き
action = agent._select_action(observation, legal_actions)
print("推論成功, 選択されたアクション:", action)

try:
    from hanabi_learning_environment import rl_env
    _env_for_moves = rl_env.make()
    _IDX2MOVE = [str(_env_for_moves.game.get_move(i)) for i in range(_env_for_moves.num_moves())]
except Exception:
    _IDX2MOVE = None

# === ここから predict_action を貼り替え ===
def predict_action(observation, legal_actions):
    global agent
    if agent is None:
        raise RuntimeError("agent is not initialized")

    # 観測・合法手
    obs = np.asarray(observation, dtype=np.uint8)
    la_raw = np.asarray(legal_actions, dtype=np.float32)

    # 学習時と同じ“加算マスク”仕様へ正規化（合法=0.0 / 非合法=-inf）
    la_mask = _ensure_legal_mask(la_raw, agent.num_actions)

    if DEBUG_MASK:
        try:
            la = la_raw.reshape(-1).astype(np.float32)
            mask = la_mask.reshape(-1).astype(np.float32)
            illegal_idx = np.where(~(la > 0.5))[0].tolist()

            def short(v, n=20):
                return np.array2string(v[:n], max_line_width=120) + (" ..." if v.size > n else "")

            print(f"[MASKCHK] LEGAL_RAW={short(la)}")
            print(f"[MASKCHK] MASK_APPLIED={short(mask)}")
            print(f"[MASKCHK] ILLEGAL_IDX={illegal_idx}")
        except Exception as e:
            print("[MASKCHK] logging failed:", e)

    # 行動選択（これが最終アクション。従来と同じAPI）
    act = agent._select_action(obs, la_mask)

    # 選択“後”に読み取りだけで意図ログ（内部状態は一切変更しない）
    try:
        _log_intent_after_selection(agent, obs, la_mask, act, idx2move=_IDX2MOVE, topk=3)
    except Exception as e:
        print("Intent計算時エラー:", e)

    return act

def _log_intent_after_selection(agent, observation, legal_actions, chosen_action, idx2move=None, topk=3):
    """選択後に同一入力で Q を読むだけの純ロガー（副作用なし）。"""
    # 形状合わせ（state は [1, obs, 1]。legal は 1次元のまま）
    obs = np.asarray(observation, dtype=np.uint8).reshape(1, -1, 1)
    la  = np.asarray(legal_actions, dtype=np.float32).reshape(-1)

    # 期待Qを読み取り。legal_actions_ph は feed しない（読むだけ）
    # self._q は [1, num_actions] の期待Q（Rainbowで _reshape_networks 後）:contentReference[oaicite:2]{index=2}
    q = agent._sess.run(agent._q, {agent.state_ph: obs})[0]  # (num_actions,)

    # 選択時と同じルールでマスク加算（合法=0.0 / 非合法=-inf）
    masked_q = q + la

    # softmax で選択行動の確信度を概算
    exps = np.exp(masked_q - np.max(masked_q))
    probs = exps / np.sum(exps) if np.sum(exps) > 0 else np.zeros_like(exps)

    act = int(chosen_action)
    conf = float(probs[act])

    # 可読な行動名
    move_str = idx2move[act] if (idx2move is not None and 0 <= act < len(idx2move)) else str(act)
    su = move_str.upper()

    # 誤検出対策：REVEAL → DISCARD → PLAY の順で判定（"PLAYER" に含まれる "PLAY" を無視）
    if "REVEAL" in su or "HINT" in su:
        intent_type = "SAVE_HINT" if (" RANK 5" in su or "RANK=5" in su or " 5)" in su) else "PLAY_HINT"
    elif "DISCARD" in su:
        intent_type = "SAFE_DISCARD" if conf >= 0.80 else "RISKY_DISCARD"
    elif su.startswith("PLAY") or su.startswith("(PLAY"):
        intent_type = "SAFE_PLAY" if conf >= 0.80 else "RISKY_PLAY"
    else:
        intent_type = "UNKNOWN"

    # 上位候補（デバッグ用）
    order = np.argsort(masked_q)[::-1][:int(topk)]
    top_list = []
    for i in order:
        label = idx2move[i] if (idx2move is not None and 0 <= i < len(idx2move)) else str(i)
        top_list.append({"action": int(i), "move": label, "q": float(masked_q[i]), "p": float(exps[i]/np.sum(exps) if np.sum(exps)>0 else 0.0)})

    print(f"[INTENT] {intent_type}  conf={conf:.3f}  move={move_str}")
    print("[INTENT_JSON]", json.dumps({
        "intent_type": intent_type,
        "confidence": round(conf, 3),
        "chosen_action": act,
        "move": move_str,
        "topk": top_list
    }, ensure_ascii=False))


  # ソケット通信の設定（Unityから観測データ受信）
    #HOST = '192.168.0.11'
    #PORT = 8052