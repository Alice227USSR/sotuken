using UnityEngine;
using System.Net.Sockets;
using System.Text;

public class PythonTestSender : MonoBehaviour
{
    void Start()
    {
        SendTestData();
    }

    void SendTestData()
{
    try
    {
        TcpClient client = new TcpClient("127.0.0.1", 9000);
        NetworkStream stream = client.GetStream();

        // 明示的なインスタンスで作成
        PythonMessage message = new PythonMessage();
        message.observation = new float[] { 0.1f, 0.2f, 0.3f };
        message.legal_actions = new float[] { 0f, -1f, 0f };
        message.info = "テスト送信";

        string json = JsonUtility.ToJson(message);
        byte[] data = Encoding.UTF8.GetBytes(json);
        stream.Write(data, 0, data.Length);
        Debug.Log("Unity → Python 送信成功: " + json);

        stream.Close();
        client.Close();
    }
    catch (SocketException e)
    {
        Debug.LogError("ソケットエラー: " + e.Message);
    }
}
}

public class PythonMessage
{
    public float[] observation;
    public float[] legal_actions;
    public string info;
}