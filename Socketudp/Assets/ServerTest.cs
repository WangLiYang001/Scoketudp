using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ServerTest : MonoBehaviour
{
    SocketServer Server;
    public Text 信息;
    public InputField 发送文字;

    IEnumerator 循环发送广播(string mes, float time)
    {
        while (true)
        {
            UdpBroad.Broad.发送广播(mes);
            yield return new WaitForSeconds(time);
        }
    }

    void Start()
    {
        Server = SocketServer.Server.StartServer();
        Server.连接到客户端 += 连接到客户端;
        Server.客户端断开连接 += 客户端断开连接;
        Server.收到消息 += 收到消息;
        StartCoroutine(循环发送广播(Server.IPPoint.ToString(),3));
    }

    void 连接到客户端(TcpClient t)
    {
        IPEndPoint ip = t.Client.RemoteEndPoint as IPEndPoint;
        信息.text += "\n"+ip+"连接了服务器";
    }
    void 客户端断开连接(TcpClient t,string ip)
    {
        信息.text += "\n" + ip + "断开了连接";
    }

    void 收到消息(TcpClient t,byte[] data)
    {
        string s = Encoding.UTF8.GetString(data);
        IPEndPoint ip = t.Client.RemoteEndPoint as IPEndPoint;
        信息.text += "\n" + ip + ":"+s;
    }


    public void 发送()
    {
        if (string.IsNullOrEmpty(发送文字.text) || Server.Clients.Count<=0)
        {
            信息.text += "发送失败！没有客户端进入或发送为空！";
            return;
        }
        Server.SendAllMessage(发送文字.text);
        信息.text += "\n我" + ":" + 发送文字.text;
    }

}
