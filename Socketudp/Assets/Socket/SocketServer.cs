using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class SocketServer : MonoBehaviour
{
    public TcpListener serverSocket;
    static SocketServer server;
    ConcurrentQueue<Action> ActionQueue = new ConcurrentQueue<Action>();
    public event Action<TcpClient, byte[]> 收到消息;
    public event Action<TcpClient> 连接到客户端;
    public event Action<TcpClient,string> 客户端断开连接;
    public Dictionary<TcpClient,string> Clients = new Dictionary<TcpClient, string>();
    public IPEndPoint IPPoint;
    public const int Port = 10086;
    [HideInInspector]
    public bool IsStart = false;
    public static SocketServer Server
    {
        get
        {
            if (server == null)
            {
                GameObject g = new GameObject("SocketServer");
                server=g.AddComponent<SocketServer>();
            }
            return server;
        }
    }

    public static IPAddress GetIP(bool IPv6)
    {
        if (IPv6 && !Socket.OSSupportsIPv6)
        {
            return null;
        }

        foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            NetworkInterfaceType _type1 = NetworkInterfaceType.Wireless80211;
            NetworkInterfaceType _type2 = NetworkInterfaceType.Ethernet;

            if ((item.NetworkInterfaceType == _type1 || item.NetworkInterfaceType == _type2) && item.OperationalStatus == OperationalStatus.Up)
#endif 
            {
                foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                {
                    //IPv4
                    if (!IPv6)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return ip.Address;
                            //Debug.Log("IP:" + output);
                        }
                    }
                    //IPv6
                    else
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            return ip.Address;
                        }
                    }
                }
            }
        }
        return null;
    }

   public void 断开客户端(TcpClient client)
    {
        Clients.Remove(client);
        client.Close();
        client.Dispose();
    }

    // Update is called once per frame
    private void Awake()
    {
        if (server != null)
        {
            Destroy(gameObject);
            return;
        }
        server = this;
        DontDestroyOnLoad(gameObject);
        IPPoint = new IPEndPoint(GetIP(false), Port); 
    }

    void Update()
    {
        while (ActionQueue.TryDequeue(out Action a))
        {
            a();
        }
    }

   public SocketServer StartServer()
    {
        serverSocket = new TcpListener(IPPoint);
        serverSocket.Start();
        //创建服务器Socket对象，并设置相关属性        
        serverSocket.BeginAcceptTcpClient(OnConnectRequest, null);
        IsStart = true;
        return this;
    }
     void OnConnectRequest(IAsyncResult ar)
    {
        //初始化一个SOCKET，用于其它客户端的连接
        TcpClient client = serverSocket.EndAcceptTcpClient(ar);
        Clients[client] = client.Client.RemoteEndPoint.ToString();
        ActionQueue.Enqueue(() => {
            连接到客户端?.Invoke(client);
        });
        Debug.Log("收到连接"+client.Client.RemoteEndPoint);
        byte[] bytes = new byte[1024];
        byte[] 缓存数据 = new byte[0];
        int 缓存剩余长度 = 0;
        NetworkStream stream = client.GetStream();
        AsyncCallback callback = null; ;
        callback=(IAsyncResult ar1)=>{
            int recvLenth = 0;
            try
            {
                recvLenth = stream.EndRead(ar1);
            }
            catch (Exception e)
            {
                Debug.LogWarning("客户端断开连接"+e);
            }
            if (serverSocket==null || client == null) return;
            if (recvLenth > 0)
            {
                bytes = bytes.Take(recvLenth).ToArray();//拿取真实消息长度
                byte[] 合并后缓存 = 缓存数据.Concat(bytes.Take(缓存剩余长度)).ToArray();
                if (合并后缓存 .Length>= 4)
                {                    
                    int 缓存体长度 = BitConverter.ToInt32(合并后缓存, 0);//合并后缓存包头代表的包体长度
                    if (合并后缓存.Length != 缓存体长度 + 4) //缓存剩余长度与包体缓存长度不匹配，此时是小概率事件主要是包头被分割
                    {
                        缓存剩余长度 = 缓存体长度 + 4 - 缓存数据.Length;
                        合并后缓存 = 缓存数据.Concat(bytes.Take(缓存剩余长度)).ToArray();//重新获取合并后缓存
                    }
                }
                if (recvLenth >= 缓存剩余长度)
                {
                    bytes = bytes.Skip(缓存剩余长度).ToArray();
                    if (合并后缓存.Length > 4)//接受上次没接受完的合并后的缓存消息
                    {
                        byte[] 缓存消息体 = 合并后缓存.Skip(4).ToArray();
                        缓存数据 = new byte[0]; 缓存剩余长度 = 0;//合并后缓存还置空缓存区
                        ActionQueue.Enqueue(() => {
                            收到消息?.Invoke(client, 缓存消息体);
                        });
                    }
                    while (bytes.Length >= 4)
                    {
                        int 消息长度 = BitConverter.ToInt32(bytes, 0);
                        byte[] 剩余消息 = bytes.Skip(4).ToArray();
                        if (消息长度 > 剩余消息.Length)//断包需要缓存等待下次接受
                        {
                            缓存数据 = bytes.Clone() as byte[];
                            缓存剩余长度 = 消息长度 - 剩余消息.Length;
                        }
                        else//连包需要截取剩下的缓存下来
                        {
                            byte[] 消息体 = 剩余消息.Take(消息长度).ToArray();
                            ActionQueue.Enqueue(() => {//放到主线程队列里面执行接受到的消息
                                收到消息?.Invoke(client, 消息体);
                            });
                        }
                        bytes = bytes.Skip(消息长度 + 4).ToArray();
                    }
                    if (bytes.Length > 0)//剩余消息大于0并且小于4此时剩余消息头被截断，缓存剩余长度要取满4个字节
                    {
                        缓存数据 = bytes.Clone() as byte[]; 缓存剩余长度 = 4 - bytes.Length;
                    }
                }
                else
                {
                    缓存剩余长度 -= recvLenth;
                    缓存数据 = 合并后缓存;
                }
                bytes = new byte[1024];
                stream.BeginRead(bytes, 0, bytes.Length, callback, null);
            }
            else
            {
                //IPEndPoint ip = client.Client.RemoteEndPoint as IPEndPoint;
                //stream.Dispose();
                string ip = Clients[client];
                Clients.Remove(client);
                ActionQueue.Enqueue(() => {
                    客户端断开连接?.Invoke(client, ip);
                    client.Dispose();
                    client = null;
                });
            }
        };
        stream.BeginRead(bytes, 0, bytes.Length, callback, null);
        serverSocket.BeginAcceptTcpClient(OnConnectRequest, null);
    }

    public bool SendAllMessage(byte[] data)
    {
        bool b = true;
        foreach (TcpClient item in Clients.Keys)
        {
            if (!SendClientMessage(item, data))
            {
                b = false;
            }
        }
        return b;
    }

    public bool SendAllMessage(string s)
    {
        byte[] data = Encoding.UTF8.GetBytes(s);
        return SendAllMessage(data);
    }

    public bool SendClientMessage(TcpClient client, string s)
    {
        byte[] data = Encoding.UTF8.GetBytes(s);
        return SendClientMessage(client,data);
    }

    public bool SendClientMessage(TcpClient client,byte[] data)
    {
        if (!Clients.ContainsKey(client))
        {
            Debug.Log("发送失败！客户端已经退出");
            return false;
        }
        if (!client.Connected)
        {
            string ip = Clients[client];
            Debug.LogWarning("发送失败！客户端"+ ip + "断开连接！");
            Clients.Remove(client);
            client.Dispose();
            ActionQueue.Enqueue(() => {
                客户端断开连接?.Invoke(client,ip);
            });
            return false;
        }
        NetworkStream stream = client.GetStream();
        try
        {
            byte[] 消息体 = data;
            byte[] 消息头 = BitConverter.GetBytes(消息体.Length);
            byte[] message = 消息头.Concat(消息体).ToArray();
            stream.Write(message, 0, message.Length);

        }
        catch (Exception e)
        {
            Debug.LogWarning("发送失败" + e);
            string ip = Clients[client];
            client.Dispose();
            Clients.Remove(client);
            ActionQueue.Enqueue(() => {
                客户端断开连接?.Invoke(client,ip);
            });
            return false;
        }
        return true;
    }

    public void StopServer()
    {
        if (serverSocket!=null)
        {
            serverSocket.Stop();
            serverSocket = null;
            Clients.Clear();
        }
        IsStart = false;
    }

    private void OnDestroy()
    {
        StopServer();
    }
}