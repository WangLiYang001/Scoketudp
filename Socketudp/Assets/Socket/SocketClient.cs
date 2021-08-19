using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class SocketClient : MonoBehaviour
{

    static SocketClient client;
    public TcpClient clientSocket;
    public event Action<byte[]> 收到消息;
    public event Action 与服务器断开;
    public string IPName
    {
        get
        {
            if (Connected)
            {
                return clientSocket.Client.LocalEndPoint.ToString();
            }
            else
            {
                return null;
            }            
        }
    }
    public string ConnectedServerIP
    {
        get
        {
            if (Connected)
            {
                IPEndPoint ip = clientSocket.Client.RemoteEndPoint as IPEndPoint;
                return ip.Address.ToString();
            }
            else
            {
                return null;
            }
        }
    }
    ConcurrentQueue<Action> ActionQueue = new ConcurrentQueue<Action>();
    public static SocketClient Client
    {
        get
        {
            if (client == null)
            {
                GameObject g = new GameObject("SocketClient");
                client = g.AddComponent<SocketClient>();
            }
            return client;
        }
    }

    public bool Connected{
        get
        {
            return clientSocket != null && clientSocket.Connected;
        }
    }


    private void Awake()
    {
        if (client != null)
        {
            Destroy(gameObject);
            return;
        }
        client = this;
        DontDestroyOnLoad(gameObject);
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        while (ActionQueue.TryDequeue(out Action a))
        {
            a();
        }
    }

    public void ConnectServer(string ip, int port, Action<bool> callback = null)
    {
        if (Connected)
        {
            Debug.LogWarning("已经连接过了不能再连接！");
            return;
        }
        clientSocket = new TcpClient();
        clientSocket.BeginConnect(ip, port, (IAsyncResult result)=>{
            if (Connected)
            {
                Receive();
                Debug.Log("连接成功" + ip + ":" + port);
            }
            ActionQueue.Enqueue(() => {
                callback?.Invoke(Connected);
            });          
        }, null);
    }

    public IEnumerator ConnectServerAsync(string ip, int port,Action<bool> callback=null)
    {
        if (Connected)
        {
            Debug.LogWarning("已经连接过了不能再连接！");
            yield break;
        }
        clientSocket = new TcpClient();
        IAsyncResult result = clientSocket.BeginConnect(ip, port, null, null);
        yield return new WaitUntil(()=>result.IsCompleted);
        if (Connected)
        {
            Receive();
            Debug.Log("连接成功"+ip+":"+port);
        }
        callback?.Invoke(Connected);
    }

    public void 断开()
    {
        if (Connected)
        {
            clientSocket.Close();
            clientSocket.Dispose();
            clientSocket = null;
        }
    }

    private void Receive()
    {
        byte[] bytes = new byte[1024];
        byte[] 缓存数据 = new byte[0];
        int 缓存剩余长度 = 0;
        NetworkStream stream = clientSocket.GetStream();
        AsyncCallback callback = null; ;
        callback = (IAsyncResult ar1) => {
            int recvLenth = 0;
            try
            {
                recvLenth = stream.EndRead(ar1);
            }
            catch (Exception e)
            {
                Debug.LogWarning("与服务器断开" + e);
            }
            if (client == null) return;           
            if (recvLenth > 0)
            {
                bytes = bytes.Take(recvLenth).ToArray();//拿取真实消息长度
                byte[] 合并后缓存 = 缓存数据.Concat(bytes.Take(缓存剩余长度)).ToArray();
                if (合并后缓存.Length >= 4)
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
                            收到消息?.Invoke(缓存消息体);
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
                                收到消息?.Invoke(消息体);
                            });
                        }
                        bytes = bytes.Skip(消息长度 + 4).ToArray();
                    }
                    if (bytes.Length>0)//剩余消息大于0并且小于4此时剩余消息头被截断，缓存剩余长度要取满4个字节
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
                stream.Dispose();
                if (clientSocket != null)
                {
                    clientSocket.Dispose();
                    clientSocket = null;
                }              
                ActionQueue.Enqueue(() => {
                    与服务器断开?.Invoke();
                });
                Debug.LogWarning("与服务器断开！");
            }
        };
        stream.BeginRead(bytes, 0, bytes.Length, callback, null);
    }

    public bool SendServerMessage(byte[] data)
    {
        if (!Connected)
        {
            Debug.LogWarning("未连接服务器发送失败！");
            return false;
        }         
        NetworkStream stream = clientSocket.GetStream();
        try
        {
            byte[] 消息体 = data;
            byte[] 消息头 = BitConverter.GetBytes(消息体.Length);
            byte[] message = 消息头.Concat(消息体).ToArray();
            stream.Write(message, 0, message.Length);
        }
        catch (Exception e)
        {
            Debug.LogWarning("发松失败" + e);
            clientSocket.Dispose();
            client = null;
            return false;
        }
        return true;
    }

    public bool SendServerMessage(string s)
    {
        byte[] data = Encoding.UTF8.GetBytes(s);
        return SendServerMessage(data);
    }

    private void OnDestroy()
    {
        if (clientSocket!=null)
        {
            clientSocket.Dispose();
        }        
    }

}
