using System;
using System.Text;
using System.Threading;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.NetworkInformation;

public class UdpBroad : MonoBehaviour
{
    private UdpClient UDPrecv ;
    int port = 9999;
    ConcurrentQueue<Action> ActionQueue = new ConcurrentQueue<Action>();
    public event Action<byte[]> ReceiveMessage;
    static UdpBroad broad;
    public static UdpBroad Broad
    {
        get
        {
            if (broad==null)
            {
                GameObject g = new GameObject("udp广播");
                g.AddComponent<UdpBroad>();
            }
            return broad;
        }
    }
    private void Awake()
    {
        if (broad != null)
        {
            Destroy(gameObject);
            return;
        }
        broad = this;
        DontDestroyOnLoad(broad);
        GetUdpClient();
    }

    public UdpClient GetUdpClient()
    {
        if (UDPrecv==null)
        {
            UDPrecv = new UdpClient();
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
            UDPrecv.Client = socket;
        }
        return UDPrecv;
    }

    public void StartBroadcast(Action<byte[]> 收到广播消息)
    {
        this.ReceiveMessage = 收到广播消息;
        UdpClient uc = GetUdpClient();
        uc.BeginReceive(ReceiveCallback, null);
    }

    public void Stop()
    {
        if (UDPrecv!=null)
        {
            UDPrecv.Close();
            UDPrecv.Dispose();
            UDPrecv = null;
        }    
    }

    private void Update()
    {
        while (ActionQueue.TryDequeue(out Action a))
        {
            a();
        }
    }

    public void 发送广播(byte[] data)
    {
        IPEndPoint endpoint = new IPEndPoint(IPAddress.Broadcast, port);
        GetUdpClient();
        UDPrecv.Send(data, data.Length, endpoint);
    }

    public void 发送广播(string mes)
    {
        byte[] data = Encoding.UTF8.GetBytes(mes);
        发送广播(data);
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        IPEndPoint endpoint = null;
        byte[] recvBuf = UDPrecv.EndReceive(ar, ref endpoint);
        ActionQueue.Enqueue(() => { ReceiveMessage?.Invoke(recvBuf);});
        UDPrecv.BeginReceive(new AsyncCallback(ReceiveCallback), null);
    }

}