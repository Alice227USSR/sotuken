# eval_selfplay.py（HLE 自己対戦・意図ログ併用／スコア計算を堅牢化）
import argparse
import time
import json
import numpy as np

# 既存の推論モジュール（Unity 連携のない純推論）を利用
import inference
from hanabi_learning_environment import rl_env


# 0..19 の行動ID → 可読名（学習時の並びに合わせる）
IDX2MOVE = [
    "(Discard 0)", "(Discard 1)", "(Discard 2)", "(Discard 3)", "(Discard 4)",
    "(Play 0)", "(Play 1)", "(Play 2)", "(Play 3)", "(Play 4)",
    "(Reveal player +1 color R)", "(Reveal player +1 color Y)",
    "(Reveal player +1 color G)", "(Reveal player +1 color W)",
    "(Reveal player +1 color B)",
    "(Reveal player +1 rank 1)", "(Reveal player +1 rank 2)",
    "(Reveal player +1 rank 3)", "(Reveal player +1 rank 4)",
    "(Reveal player +1 rank 5)",
]


def _legal_mask_from_obs(env, player_obs):
    """HLEの合法手を Rainbow 用マスク（合法=0.0 / 非合法=-inf）に変換。"""
    num_actions = env.num_moves()
    mask = np.full((num_actions,), -np.inf, dtype=np.float32)
    legal = player_obs.get("legal_moves_as_int", [])
    for a in legal:
        if 0 <= a < num_actions:
            mask[a] = 0.0
    return mask


def _obs_vector_from_player_obs(player_obs):
    """推論に渡す観測ベクトルを取り出す。HLE 設定によりキー名が異なることがある。"""
    # Unity 側と同じ入力（平坦化済みベクトル）を想定
    if "vectorized" in player_obs:
        v = np.asarray(player_obs["vectorized"], dtype=np.float32)
        return v
    # もし用意されていなければ、inference 側にエンコード関数がある前提で委譲（無い場合はエラー）
    if hasattr(inference, "encode_observation"):
        return np.asarray(inference.encode_observation(player_obs), dtype=np.float32)
    raise KeyError("player_obs に 'vectorized' がありません。環境設定かエンコーダをご確認ください。")


def _score_from_player_obs(player_obs):
    """観測からスコアを安全に計算。'score' が無ければ fireworks から合計。"""
    if "score" in player_obs:
        return int(player_obs["score"])
    fw = player_obs.get("fireworks", None)
    if fw is None:
        # これでも無い場合は 0 扱い（古い/特殊設定）
        return 0
    if isinstance(fw, dict):
        return int(sum(int(v) for v in fw.values()))
    # list / np.array でも合計
    return int(np.asarray(fw, dtype=np.int32).sum())


def play_one_game(env, verbose=False):
    obs = env.reset()
    done = False
    total_steps = 0

    while not done:
        cur = obs["current_player"]
        pobs = obs["player_observations"][cur]

        # 観測・合法手マスク
        obs_vec = _obs_vector_from_player_obs(pobs)
        legal_mask = _legal_mask_from_obs(env, pobs)

        # 推論（※ inference 側の行動選択ロジックを変更しない）
        action_id = int(inference.predict_action(obs_vec, legal_mask))

        # HLE へ適用
        obs, reward, done, _ = env.step(action_id)
        total_steps += 1

        if verbose:
            print(f"[STEP {total_steps}] a={action_id} {IDX2MOVE[action_id]}  r={reward}")

    # エピローグ：安全に最終スコアを計算
    final_player0 = obs["player_observations"][0]
    final_score = _score_from_player_obs(final_player0)
    return final_score


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--episodes", type=int, default=100, help="評価ゲーム数")
    ap.add_argument("--players", type=int, default=2, help="プレイヤー人数（学習時と一致）")
    ap.add_argument("--verbose", action="store_true", help="各手のログを出す")
    args = ap.parse_args()

    # 学習時の設定に合わせる（フルルール / プレイヤー数）
    env = rl_env.make(environment_name="Hanabi-Full", num_players=args.players)

    scores = []
    t0 = time.time()
    for ep in range(1, args.episodes + 1):
        sc = play_one_game(env, verbose=args.verbose)
        scores.append(sc)
        print(f"[EVAL] episode {ep:3d}/{args.episodes}  score={sc}  avg={np.mean(scores):.2f}")

    dt = time.time() - t0
    print(json.dumps({
        "episodes": args.episodes,
        "mean_score": float(np.mean(scores) if scores else 0.0),
        "std_score": float(np.std(scores) if scores else 0.0),
        "elapsed_sec": round(dt, 1)
    }, ensure_ascii=False))


if __name__ == "__main__":
    main()
