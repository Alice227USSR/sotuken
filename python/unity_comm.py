import socket
import json
import numpy as np

def send_to_unity(observation, legal_actions, action):
    try:
        # NumPy 配列を Python の list に変換
        # 念のため NumPy 配列に変換しておく（重複しても問題なし）
        observation = np.array(observation)
        legal_actions = np.array(legal_actions)
        
        message = {
            "observation": observation.tolist(),
            "legal_actions": legal_actions.tolist(),
            "action": int(action)
        }

        json_message = json.dumps(message)

        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            s.connect(("192.168.0.11", 8052))  # Unity 側のポートとIPに合わせて
            s.sendall(json_message.encode('utf-8'))

            print("Unityに送信成功: action =", action)

    except Exception as e:
        print("送信エラー:", e)
