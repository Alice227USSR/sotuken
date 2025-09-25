using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System;

[Serializable]
public class HanabiMessage
{
    public float[] observation;
    public float[] legal_actions;
}

[Serializable]
public class HanabiResponse
{
    public int action;
}

public class SendHanabiObservation : MonoBehaviour
{
    // この関数をボタンに割り当てる
    public void SendDataToPython()
    {
        try
        {
            TcpClient client = new TcpClient("127.0.0.1", 9000); // Python側のポート
            NetworkStream stream = client.GetStream();

            // 仮の観測ベクトル（例：Hanabi-Full, 658次元）
            float[] observation = new float[658];
            observation[0] = 1f;     // Fireworks赤1など
            observation[38] = 1f;    // Blue1
            observation[100] = 1f;   // 手札位置

            float[] legalActions = new float[20];
            for (int i = 0; i < legalActions.Length; i++) legalActions[i] = 0.0f;

            HanabiMessage msg = new HanabiMessage
            {
                observation = observation,
                legal_actions = legalActions
            };

            string json = JsonUtility.ToJson(msg);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            stream.Write(jsonBytes, 0, jsonBytes.Length);
            Debug.Log("Pythonにデータ送信済み: " + json);

            // 応答受信
            byte[] buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            HanabiResponse response = JsonUtility.FromJson<HanabiResponse>(responseJson);

            Debug.Log("Pythonからのアクション受信: " + response.action);

            stream.Close();
            client.Close();
        }
        catch (Exception e)
        {
            Debug.LogError("通信エラー: " + e.Message);
        }
    }
}
