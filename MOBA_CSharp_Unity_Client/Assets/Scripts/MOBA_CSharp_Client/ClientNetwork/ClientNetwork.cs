using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ENet;
using UnityEngine;
using Event = ENet.Event;
using EventType = ENet.EventType;

namespace MOBA_CSharp_Client.ClientNetwork
{
    // 定义消息处理器委托，用于处理收到的消息数据
    public delegate void MessageHandler(byte[] data);

    // 客户端网络管理类
    public class ClientNetwork
    {
        // 用于存储不同类型消息的处理器数组
        MessageHandler[] handlers;

        Host client;  // 客户端主机
        Address address;  // 服务器地址
        Peer peer;  // 与服务器通信的Peer  这是一种什么概念？ Pear?  是一种封装？

        // 构造函数，初始化消息处理器数组，长度为MessageType枚举的数量
        public ClientNetwork()
        {
            handlers = new MessageHandler[Enum.GetNames(typeof(MessageType)).Length];
        }

        // 设置特定消息类型的处理器
        public void SetMessageHandler(MessageType type, MessageHandler handler)
        {
            handlers[(int)type] = handler;
        }

        // 清除所有消息处理器
        public void ClearMessageHandlers()
        {
            for(int i = 0; i < handlers.Length; i++)
            {
                handlers[i] = null;
            }
        }

        // 连接到指定的服务器主机和端口   就是这么一个写法
        public void Connect(string hostName, ushort port)
        {
            Library.Initialize();  // 初始化ENet库

            client = new Host();  // 创建ENet客户端主机

            address = new Address();  // 设置服务器地址
            address.SetHost(hostName);  // 设置服务器主机名
            address.Port = port;  // 设置端口号

            client.Create();  // 创建客户端

            peer = client.Connect(address);  // 连接到服务器
        }

        // 关闭客户端连接
        public void Shutdown()
        {
            if (client != null)
            {
                client.Flush();  // 刷新客户端缓冲区
                peer.DisconnectNow(0);  // 立即断开连接
                Library.Deinitialize();  // 释放ENet库
            }
        }

        // 处理网络事件  通信处理模块
        public void Service()
        {
            if(client == null)
            {
                return;
            }
            
            Event netEvent;
            // 处理所有网络事件  根据设定好的间隔解析信号？好像是半秒一次？能改？  似乎client.Service()返回值大于0 表示有网络信号需要处理
            // 似乎只有这么几件 EventType 事件？ 能否修改？
            while (client.Service(0, out netEvent) > 0)
            {
                switch (netEvent.Type)
                {
                    case EventType.None:
                        Debug.Log(" 没有网络事件？？ EventType.None");
                        break;

                    case EventType.Connect:
                        Debug.Log("客户端已连接到服务器 - ID: " + peer.ID); 
                        Invoke(MessageType.Connect, new byte[0]);  // 调用连接消息处理器
                        break;

                    case EventType.Disconnect:
                        Debug.Log("客户端已断开与服务器的连接");
                        Invoke(MessageType.Disconnect, new byte[0]);  // 调用断开消息处理器
                        break;

                    case EventType.Timeout:
                        Debug.Log("客户端连接超时");
                        Invoke(MessageType.Timeout, new byte[0]);  // 调用超时消息处理器
                        break;

                    case EventType.Receive: // 这里不断的收到数据，心跳数据，或其他实时数据?
                        Debug.Log("从服务器收到数据包 - 通道ID: " + netEvent.ChannelID + ", 数据长度: " + netEvent.Packet.Length);
                        Receive(netEvent);  // 处理收到的数据
                        netEvent.Packet.Dispose();  // 释放数据包资源
                        break;
                }
            }
        }

        // 调用指定类型的消息处理器
        void  Invoke(MessageType type, byte[] data)
        {
            handlers[(int)type]?.Invoke(data);
        }

        // 处理收到的数据包
        void Receive(Event netEvent)
        {
            if(netEvent.Packet.Length < MessageConfig.MESSAGE_LEN)//小于一个预定义的最小长度 最小长度通常包含了消息类型和其他必要头部信息的大小
            {
                return;
            }

            byte[] buffer = new byte[netEvent.Packet.Length];
            netEvent.Packet.CopyTo(buffer);  // 将数据包内容复制到缓冲区

            // 从数据包中提取消息类型   这行代码从buffer的前两个字节中提取一个整数（使用BitConverter.ToInt16），
            // 并将其转换为MessageType枚举值。这通常表示接收到的数据包属于哪种消息类型，
            // MessageType是一个枚举，定义了应用程序支持的不同消息类型。
            MessageType type = (MessageType)BitConverter.ToInt16(buffer, 0);

            // 提取数据部分 减去数据类型的2个字节
            byte[] data = new byte[netEvent.Packet.Length - MessageConfig.MESSAGE_LEN];
            Array.Copy(buffer, 2, data, 0, data.Length);

            Invoke(type, data);  // 调用对应的消息处理器
        }

        // 向服务器发送消息
        public void Send(MessageType type, byte[] data, PacketFlags flags)
        {
            Packet packet = default(Packet);
            byte[] buffer = new byte[MessageConfig.MESSAGE_LEN + data.Length];

            // 将消息类型转换为字节并复制到缓冲区
            byte[] byteType = BitConverter.GetBytes((ushort)type);
            Array.Copy(byteType, buffer, MessageConfig.MESSAGE_LEN);

            // 复制消息数据到缓冲区
            Array.Copy(data, 0, buffer, MessageConfig.MESSAGE_LEN, data.Length);

            packet.Create(buffer, flags);  // 创建数据包
            peer.Send(0, ref packet);  // 通过通道0发送数据包
        }

        // 获取当前连接的Peer的ID
        public uint GetPeerID()
        {
            return peer.ID;
        }
    }
}
