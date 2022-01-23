﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;




public class TcpLongServer
{
    public Socket ServerSocket;
    public int MaxListenAmount;
    public ServerThreading thisServerThreader;

    public int ListenFreq;
    public Thread thisThread;

    //类的构造函数
    public TcpLongServer(string ip, int port, int maxlistenamount)
    {
        //初始化成员
        ServerSocket = null;
        MaxListenAmount = maxlistenamount;
        ListenFreq = 100;
        thisServerThreader = new ServerThreading(ip, port, MaxListenAmount, ListenFreq);
        thisThread = new Thread(new ThreadStart(thisServerThreader.Threading));
    }

    ///////////////成员的Setter///////////////
    public void setServerSocket(Socket newSocket)
    {
        ServerSocket = newSocket;
    }
    public void setMaxListenAmount(int amount)
    {
        MaxListenAmount = amount;
    }
    public void setListenFreq(int freq)
    {
        ListenFreq = freq;
    }

    ////////////////////////////////////////////////

    public void Start()
    {
        thisThread.Start();
    }

    //发送数据的方法，但是会覆盖当前的缓冲区，并且会立即打包成一个数据包发走
    public void Send(int connectionIndex, byte[] buffer)
    {
        thisServerThreader.AcceptQueue[connectionIndex].SendBuffer = buffer;
    }

    //接收数据的方法，返回一个byte为单位字节数组，并清除缓冲区
    public byte[] Receive(int connectionIndex)
    {
        byte[] temp = new byte[2048];
        thisServerThreader.AcceptQueue[connectionIndex].OutBuffer.ReadBuffer(temp, 0, thisServerThreader.AcceptQueue[connectionIndex].OutBuffer.DataCount);
        thisServerThreader.AcceptQueue[connectionIndex].OutBuffer.Clear(20480);
        return temp;

    }
}










public class ServerThreading
{
    private int MaxListenAmount { get; set; }
    public AcceptedThreadObjective[] AcceptQueue;
    private int ListenFreq { get; set; }
    private string Ip { get; set; }
    private int Port { get; set; }
    public long MaxSilentTime;


    public Socket Server; //Server的对象

    public bool isConnected;        //是否链接的状态

    public ServerThreading(string ip, int port, int maxlstamt, int lstfreq)
    {
        Server = null;
        MaxListenAmount = maxlstamt;
        AcceptQueue = new AcceptedThreadObjective[maxlstamt];
        ListenFreq = lstfreq;
        Ip = ip;
        Port = port;
        isConnected = false;
    }

    public void Threading()
    {
        try     //尝试服务器连接
        {
            Server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Server.Bind(new IPEndPoint(IPAddress.Parse(Ip), Port));
            Server.Listen(MaxListenAmount);
            isConnected = true;
            Console.WriteLine("[Server Thread] Server started, wait for connection.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return;
        }

        //开启Accept的线程
        Server.BeginAccept(new AsyncCallback(AcceptCallback), Server);

        //尝试监测每个连接是否有效
        while(isConnected == true)
        {
            for (int i = 0; i < MaxListenAmount; i++)
            {
                if (AcceptQueue[i] != null && AcceptQueue[i].IsConnected == false)
                {
                    AcceptQueue[i] = null;
                    Console.WriteLine("[Accept Thread] 检测到连接[{0}]已断开，Socket将被销毁", i);
                }
            }
            Thread.Sleep(1000);
        }
    }

    public void AcceptCallback(IAsyncResult ar)
    {
        // Get the socket that handles the client request.
        Socket listener = (Socket)ar.AsyncState;

        // End the operation and display the received data on the console.

        int queueIndex = 0;
        //遍历线程池，直到遇到一个空闲的线程
        try
        {
            for (queueIndex = 0; AcceptQueue[queueIndex] != null && queueIndex <= MaxListenAmount; queueIndex++)
            {
                Console.WriteLine("[Accept Thread] Searching[{0}/{1}]: [{2}]", queueIndex, MaxListenAmount, AcceptQueue[queueIndex].ThisSocket.RemoteEndPoint);
            }
            Console.WriteLine("[Accept Thread] 当前空闲：[{0}]", queueIndex, AcceptQueue);

            AcceptQueue[queueIndex] = new AcceptedThreadObjective(listener.EndAccept(ar), ListenFreq);
            AcceptQueue[queueIndex].Begin();
            AcceptQueue[queueIndex].ConnectionId = queueIndex;
            Console.WriteLine("[Accept Thread] 建立了新的连接，id为 {0}，目标为{1}", queueIndex, AcceptQueue[queueIndex].ThisSocket.RemoteEndPoint);
        }
        catch (Exception)
        {
            Console.WriteLine("[Accept Thread] ERROR！遇到新的连接请求，但所有连接都已经被占用");
            listener.EndAccept(ar).Close();
        }

        //开启这个线程的对象


        if (isConnected == true)        //如果这个server没有被关闭，那么才继续
        {
            //继续Accept
            listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
        }
    }
}
///EOC///













public class AcceptedThreadObjective
{
    public Socket ThisSocket { get; set; }   //这个accept的socket
    public bool IsWorking { get; set; }   //是否在工作  
    public bool IsConnected { get; set; }   //是否连接存在
    public int ConnectionId { get; set; }   //连接的id
    private int ListenFreq { get; set; }    //设置连接的接收频率

    public RingBufferManager OutBuffer;

    public byte[] ReceiveBuffer;    //接收的缓冲区
    public byte[] SendBuffer;       //发送的缓冲区

    public DateTime LastReceiveTime;

    public AcceptedThreadObjective(Socket socket, int listenfreq)
    {
        //对成员进行初始化
        ThisSocket = socket;
        IsWorking = false;
        ListenFreq = listenfreq;
        ReceiveBuffer = new byte[2048];
        SendBuffer = new byte[2048];
        OutBuffer = new RingBufferManager(2048);
        LastReceiveTime = DateTime.Now.ToUniversalTime();
    }

    /// <summary>
    /// 重新开始信息的发送和接收
    /// </summary>
    public void Begin()
    {
        if (IsWorking == false)
        {
            //将发送的状态设置为真
            IsWorking = true;
            IsConnected = true;
            Console.WriteLine("1");
            //启动接收的线程
            ThisSocket.BeginReceive(ReceiveBuffer, 0, ReceiveBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), ThisSocket);
            Console.WriteLine("2");
            //启动发送的线程
            //ThisSocket.BeginSend(SendBuffer, 0, SendBuffer.Length, SocketFlags.None, new AsyncCallback(SendCallback), ThisSocket);

            Console.WriteLine("3");
        }
        else if (ThisSocket == null)
        {
            throw new Exception("尝试启动一个空的服务端连接");
        }
        else if (IsWorking == true)
        {
            throw new Exception("尝试启动一个已经开启的服务端连接");
        }
        else
            throw new Exception("在尝试启动连接的时候遇到了未知问题");
    }

    /// <summary>
    /// 暂停信息的接收
    /// </summary>
    public void Pause()
    {
        if (IsWorking == true)
            IsWorking = false;
        else if (ThisSocket == null)
            throw new Exception("尝试关闭一个未建立的服务端连接");
        else if (IsWorking == false)
            throw new Exception("尝试关闭一个已经关闭的服务端连接");
        else
            throw new Exception("在尝试关闭一个服务端连接时遇到问题");
    }



    ////////////////// 回调方法 回调方法 回调方法 //////////////////
    ////////////////// 回调方法 回调方法 回调方法 //////////////////
    ////////////////// 回调方法 回调方法 回调方法 //////////////////

    /// <summary>
    ///接收数据的回调方法
    /// </summary>
    /// <param name="result">
    /// 由BegineReceive方法调用返回的 IAsyncResult 类型的对象
    /// </param>
    public void ReceiveCallback(IAsyncResult result)
    {
        //初始化
        Socket ts = (Socket)result.AsyncState;
        ts.EndReceive(result);
        result.AsyncWaitHandle.Close();

        //读取监听，并写入输出缓冲区，并记录
        //Console.WriteLine("[Server Thread {0}] （前）缓冲区位置：End: {1}, Start: {2}, length: {3}", ConnectionId,  OutBuffer.DataEnd, OutBuffer.DataStart, OutBuffer.DataCount);
        Console.WriteLine("[Server Thread {0}] 从{1}收到消息：{2}", ConnectionId, ThisSocket.RemoteEndPoint, System.Text.Encoding.UTF8.GetString(ReceiveBuffer));

        //获取读取的数据的长度
        int datalength = 0;
        for (datalength = 0; ReceiveBuffer[datalength] != 0; datalength++) ;
        if (datalength == 0)
        {
            IsWorking = false;
            IsConnected = false;
            Console.WriteLine("[Server Thread {0}] 连接[{0}]已断开", ConnectionId);
        }

        //写入环形buffer
        OutBuffer.WriteBuffer(ReceiveBuffer, 0, datalength);
        //Console.WriteLine("[Server Thread {0}] （后）缓冲区位置：End: {1}, Start: {2}, length: {3}",ConnectionId,  OutBuffer.DataEnd, OutBuffer.DataStart, OutBuffer.DataCount);

        /*
        if (SendBuffer[0] != 0)
        {
            ts.Send(SendBuffer);
        }
        */

        if (IsWorking == true)
        {
            //延时
            Thread.Sleep(ListenFreq);
            //清空接收缓冲，并开始新的缓冲
            ReceiveBuffer = new byte[ReceiveBuffer.Length];
            ts.BeginReceive(ReceiveBuffer, 0, ReceiveBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), ts);
            LastReceiveTime = DateTime.Now.ToUniversalTime();
        }
    }

    /// <summary>
    /// 发送数据的回调方法
    /// </summary>
    /// <param name="result">由BegineSend方法调用返回的 IAsyncResult 类型的对象</param>
    public void SendCallback(IAsyncResult result)
    {
        Socket ts = (Socket)result.AsyncState;
        ts.EndSend(result);
        result.AsyncWaitHandle.Close();
        Console.WriteLine("[Server Thread {0}] 发送信息：{1}",ConnectionId, System.Text.Encoding.UTF8.GetString(SendBuffer));

        if (IsWorking == true)  //如果状态是正在工作的话
        {
            //清空数据，重新开始异步发送
            //阻塞，直到缓冲区重新非空
            SendBuffer = new byte[SendBuffer.Length];
            
            while (SendBuffer[0] == 0)
            {
                Thread.Sleep(ListenFreq);
            }
            ts.BeginSend(SendBuffer, 0, SendBuffer.Length, SocketFlags.None, new AsyncCallback(SendCallback), ts);
        }
    }
}

