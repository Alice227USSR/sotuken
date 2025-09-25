import pickle

log_path = "logs/exp1/logs/log_31"  # 必要に応じて変更

with open(log_path, "rb") as f:
    data = pickle.load(f)

for k in data:
    v = data[k]
    if isinstance(v, list):  # リストなら末尾5個を表示
        print(f"{k}: {v[-5:]}")
    else:
        print(f"{k}: {v}")  # そうでなければそのまま表示
