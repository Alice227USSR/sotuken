# legal_check.py
# Unity→Python で受け取った 0/1 マスクと obs_vec を用い、
# HLE に問い合わせた合法手（20長の 0/1）と比較ログを出すユーティリティ。
import json
import numpy as np

# 既存の HLE util を使って obs_vec(=658) から
# Qネット入力や合法手を作っている箇所があなたの環境にあります。
# ここでは既存の inference.py の「encode / mask 作成ロジック」を**そのまま**呼び出す形にします。

from inference import encode_observation_vector, legal_mask_from_hle_like  # ★後述の補足参照

def hle_legal_mask_from_obsvec(obs_vec: np.ndarray) -> np.ndarray:
    """
    重要：ここで 'legal_mask_from_hle_like' は、HLEが返す 'legal_moves_as_int'
    （整数インデックスの集合）を 20長の 0/1 に直す処理を内部でやる想定。
    既に inference.py で Q値計算前に同等の処理を使っているなら、それを再利用してください。
    """
    # obs_vec -> （HLE内部と同じ）観測構造に復元 → legal indices → 0/1 マスク
    mask01 = legal_mask_from_hle_like(obs_vec)  # shape=(20,), 値は0/1
    return mask01.astype(np.float32)

def compare_unity_vs_hle(obs_vec_list, unity_mask_list):
    """
    1手ごとに Unity送信(0/1) と HLE再計算(0/1) を比較してログ戻り値を返す。
    """
    logs = []
    for t, (obs_vec, u_mask) in enumerate(zip(obs_vec_list, unity_mask_list)):
        g_mask = hle_legal_mask_from_obsvec(np.asarray(obs_vec, dtype=np.float32))
        diff = np.where((u_mask.astype(int) - g_mask.astype(int)) != 0)[0].tolist()
        logs.append({
            "t": t,
            "unity_mask": u_mask.tolist(),
            "hle_mask": g_mask.tolist(),
            "diff_idx": diff
        })
    return logs

if __name__ == "__main__":
    # 単体テスト用：golden_obs.json を読み、1手分比較する例
    with open("golden_obs.json", "r") as f:
        g = json.load(f)
    obs = np.array(g["observation"], dtype=np.float32)
    unity_mask = np.array(g["legal_actions"], dtype=np.float32)  # 0/1 想定

    logs = compare_unity_vs_hle([obs], [unity_mask])
    print(json.dumps(logs[0], ensure_ascii=False, indent=2))
