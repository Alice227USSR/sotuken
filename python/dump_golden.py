import json
import gin
import numpy as np
from hanabi_learning_environment import rl_env
from rainbow_agent import RainbowAgent

# 学習時と同じ gin を読み込む（パスは環境に合わせて）
gin.parse_config_files_and_bindings(['configs/hanabi_rainbow.gin'], bindings=[])

env = rl_env.make(environment_name="Hanabi-Full", num_players=2)
observation_size = env.vectorized_observation_shape()[0]
num_actions = env.num_moves()

agent = RainbowAgent(
    num_actions=num_actions,
    observation_size=observation_size,
    num_players=env.players
)

# ★チェックポイントのパスは環境に合わせて
ckpt = "results/checkpoints/tf_ckpt-2150"
agent._saver.restore(agent._sess, ckpt)
agent.eval_mode = True
agent.epsilon_eval = 0.0

# 初手の観測と合法手（HLE仕様）
ts = env.reset()
po = ts['player_observations'][0]
obs_vec = np.array(po['vectorized'], dtype=np.float32)  # float32に
legal_ints = po['legal_moves_as_int']

# 合法=0.0, 非合法=-inf の加算マスク（※未バッチで (num_actions,) ）
legal_mask = np.full(num_actions, float('-inf'), dtype=np.float32)
for a in legal_ints:
    legal_mask[a] = 0.0

# model 期待形状に整形： (1, obs_len, 1)
state_in = obs_vec.reshape(1, observation_size, 1)

# Q値を取得して argmax
q_values = agent._sess.run(
    agent._q_values,
    feed_dict={
        agent.state_ph: state_in,           # (1, obs_len, 1)
        agent.legal_actions_ph: legal_mask  # (num_actions,)
    }
)
# q_values 形状は (1, num_actions) を想定
action = int(np.argmax(q_values[0]))

# JSON に保存（obsは int 配列に戻しておくと既存フローと親和性高い）
payload = {
    "observation": [int(x) for x in obs_vec.tolist()],
    "legal_actions": [float(x) for x in legal_mask.tolist()],  # 0.0 / -inf
    "expected_action": action
}

with open("golden_obs.json", "w", encoding="utf-8") as f:
    json.dump(payload, f, ensure_ascii=False)

print("saved: golden_obs.json")
