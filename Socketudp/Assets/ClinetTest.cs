using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ClinetTest : MonoBehaviour
{
    public Transform ItmeCopy;
    public Transform roomContent;
    HashSet<string> rooms = new HashSet<string>();
    SocketClient Client;
    public Text message;
    public InputField SendText;
    UdpBroad Broad;
    // Start is called before the first frame update
    void Start()
    {
        ItmeCopy.gameObject.SetActive(false);
        Broad = UdpBroad.Broad;
        Broad.StartBroadcast(收到广播);
        Client = SocketClient.Client;
        Client.收到消息 += 收到消息;
        Client.与服务器断开 += Disconnect;
    }
    void Disconnect()
    {
        message.text += "\n与服务器断开";
    }
    void 收到消息(byte[] data)
    {
        string s = Encoding.UTF8.GetString(data);
        message.text += "\n服务器:"+s;
    }

    void 收到广播(byte[] data)
    {
        string s = Encoding.UTF8.GetString(data);
        rooms.Add(s);
        SetContent();
        //Debug.Log("收到广播"+s);
    }

    void SetContent()
    {
        for (int i = 0; i < roomContent.childCount; i++)
        {
           Transform child = roomContent.GetChild(i);
            if (child.gameObject.activeSelf)
            {
                Destroy(child.gameObject);
            }
        }
        foreach (string ipPort in rooms)
        {
            Transform t = Instantiate<Transform>(ItmeCopy, roomContent);
            t.gameObject.SetActive(true);
            t.Find("Text").GetComponent<Text>().text = ipPort;
            string[] ss = ipPort.Split(':');
            string ip = ss[0];
            int port = int.Parse(ss[1]);
            t.GetComponentInChildren<Button>().onClick.AddListener(()=>{
                Client.ConnectServer(ip, port, (bool success) => {
                    if (success)
                    {
                        message.text += "连接服务器成功！";
                    }
                    else
                    {
                        message.text += "连接服务器失败！";
                    }
                });               
            });
        }
    }

    private void OnDestroy()
    {
        Client.收到消息 -= 收到消息;
        Client.与服务器断开 -= Disconnect;
        Broad.ReceiveMessage -= 收到广播;
    }

    public void 发送()
    {
        if (string.IsNullOrEmpty(SendText.text) || !Client.Connected)
        {
            message.text += "发送失败！未连接服务器或发送数据为空";
            return;
        }
        if (Client.SendServerMessage(SendText.text))
        {
            message.text += "\n我" + ":" + SendText.text;
        }
        else
        {
            message.text += "发送失败！";
        }        
    }

}
