using ENet;
using MessagePack;
using Microsoft.Xna.Framework;
using MOBA_CSharp_Server.Library.ECS;
using MOBA_CSharp_Server.Library.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
//好像没有改变场景的回调
namespace MOBA_CSharp_Server.Game
{
    public class NetworkEntity : Entity
    {
        enum NetworkState
        {
            Lobby,
            Countdown,
            Battle
        }
        bool testMode;
        NetworkState state;
        float timer;

        ServerNetwork server = new ServerNetwork();
        Dictionary<uint, GameClient> peers = new Dictionary<uint, GameClient>();// 字典 存放远程目标客户端；一般是有是个玩家？ 共享的数据是否考虑线程安全？

        float initGold;

        public NetworkEntity(Entity root) : base(root)
        {
            AddInheritedType(typeof(NetworkEntity));

            testMode = Root.GetChild<DataReaderEntity>().GetYAMLObject(@"YAML\ServerConfig.yml").GetData<bool>("TestMode");
            initGold = Root.GetChild<DataReaderEntity>().GetYAMLObject(@"YAML\ServerConfig.yml").GetData<float>("InitGold");
        }
        //入口，开始监听  创建服务器
        public void Listen(ushort port, int peerLimit)
        {
            SetLobby(); //设置大厅的 网络环境 配置 
            // 默认5000  限定100 创建服务器
            server.Listen(port, peerLimit);
        }

        int count = 0;
        const string DefaultName = "NONAME";
        void ConnectHandler(Peer peer, byte[] data)      
        {
            Team team = (count++ % 2) == 0 ? Team.Blue : Team.Red; //偶数是蓝色玩家，基数是红色玩家；
            if (testMode)
            {
                Champion champion = new Champion(peer.ID, Root.GetChild<WorldEntity>().GetFountainPosition(team), 0, 0.3f, UnitType.HatsuneMiku, team, initGold, Root);
                Root.GetChild<WorldEntity>().AddChild(champion);

                peers.Add(peer.ID, new GameClient(peer, DefaultName, team, UnitType.HatsuneMiku, true, champion.UnitID));
            }
            else
            {
                peers.Add(peer.ID, new GameClient(peer, DefaultName, team, UnitType.HatsuneMiku, false, -1));
                SendLobby(PacketFlags.Reliable);
            }
        }
        // 这里仅仅考虑断开的情况，不考虑重连的情况？断开就删掉一切信息？
        //  如果是匹配大厅可以，但是如果在对战地图，需要做一些处理？
        //  或者发现有玩家掉线，是不是应该直接重来？
        void DisconnectHandler(Peer peer, byte[] data)
        {
            RemoveClient(peer, data);
        }
        // 超时
        void TimeoutHandler(Peer peer, byte[] data)
        {
            RemoveClient(peer, data);
        }
        // 移除客户端
        void RemoveClient(Peer peer, byte[] data)
        {
            Unit unit = Root.GetChild<WorldEntity>().GetUnit(peers[peer.ID].UnitID);
            if(unit != null)
            {
                unit.ClearReference(); 
                Root.GetChild<WorldEntity>().RemoveChild(unit); // 从世界中移除这个角色所有信息
            }

            peers.Remove(peer.ID); // 移除网络客户端信息
        }
        // 用于处理来自网络对端（可能是玩家）的选择信息，比如玩家选择的角色、队伍等信息
        void SelectHandler(Peer peer, byte[] data)
        {
            SelectObj selectObj = MessagePackSerializer.Deserialize<SelectObj>(data);
            if(selectObj.Type < UnitType.HatsuneMiku || selectObj.Name == "" || selectObj.Team == Team.Yellow)
            {
                return;
            }

            if (state == NetworkState.Lobby) //如果当前网络状态是 Lobby（大厅），则创建一个新的 GameClient 对象，并将其添加到 peers 字典中，使用 peer.ID 作为键
            {
                peers[peer.ID]= new GameClient(peer, selectObj.Name, selectObj.Team, selectObj.Type, selectObj.Ready, -1);
            }
            //如果当前状态是 Countdown（倒计时），则更新对应 peer 的 Ready 状态。
            //如果某个玩家设置为不准备（!selectObj.Ready），则状态可能会重置为 Lobby
            else if (state == NetworkState.Countdown)
            {
                peers[peer.ID].Ready = selectObj.Ready;
                if(!selectObj.Ready)
                {
                    state = NetworkState.Lobby;
                }
            }
            else //还有其他什么情况？
            {
                peers[peer.ID] = new GameClient(peer, selectObj.Name, selectObj.Team, selectObj.Type, false, -1);
            }

            SendLobby(PacketFlags.Reliable);//可靠
        }

        void SendLobby(PacketFlags flags)
        {
            List<SelectObj> selectObjs = new List<SelectObj>();  
            foreach(GameClient gameClient in peers.Values)
            {
                selectObjs.Add(new SelectObj()
                {
                    Type = gameClient.Type,
                    Name = gameClient.Name,
                    Team = gameClient.Team,
                    Ready = gameClient.Ready
                }); // 通信玩家数据 封装成  SelectObj 对象
            }
            LobbyObj lobbyObj = new LobbyObj() 
            {
                State = (byte)state,
                Timer = timer,
                SelectObjs = selectObjs.ToArray()  // 这个是所有 角色基本信息，
            };

            foreach (var keyValue in peers) //即时通知每一个大厅中的玩家信息
            {
                if (state == NetworkState.Battle && keyValue.Value.Ready) // 如果处于对战状态，并且该玩家处于准备状态
                {
                    continue;
                }
                // 本角色的基本信息 遍历通知每一个客户端的时候都会修改后； 
                lobbyObj.PeerSelectObj = new SelectObj()
                {
                    Type = keyValue.Value.Type,
                    Name = keyValue.Value.Name,
                    Team = keyValue.Value.Team,
                    Ready = keyValue.Value.Ready
                };
                Send(MessageType.Lobby, keyValue.Key, MessagePackSerializer.Serialize(lobbyObj), flags);// 封包发送
            }
        }

        float lobbyTimer = 1.0f;
        public override void Step(float deltaTime)
        {
            base.Step(deltaTime);

            server.Service(); // 好像跟客户端一样的驱动模式；

            lobbyTimer -= deltaTime;
            if (lobbyTimer <= 0) // 大厅 1秒通信一次？ 心跳？
            {
                lobbyTimer = 1.0f;
                SendLobby(PacketFlags.Reliable);
            }

            if (state == NetworkState.Lobby) //此时大家都在大厅中等待；
            {
                if(testMode)
                {
                    SetBattle();
                }
                else
                {
                    if(peers.Count > 0 && peers.Values.All(x => x.Ready)) //所有玩家准备好了，开始倒计时；
                    {
                        state = NetworkState.Countdown;
                        timer = 5.0f;
                    }
                }
            }
            else if(state == NetworkState.Countdown)//此时大家在倒计时 5秒
            {
                timer -= deltaTime;
                if(peers.Values.Any(x => !x.Ready))
                {
                    state = NetworkState.Lobby; // 此时只要有一个玩家取消倒计时，就重新设定大厅模式；
                }
                else
                {
                    if(timer <= 0)
                    {
                        SetBattle(); // 倒计时结束，进入战斗模式；
                    }
                }
            }
            else    //此时 应该是战斗模式（场景）
            {
                if (!testMode && peers.Where(x => x.Value.Ready).Count() == 0)//非测试模式，并且 没有一个玩家是准备好的？
                {
                    SetLobby();
                }
                else
                {
                    SendSnapshot(); //发送游戏快照 并重置所有单位的扭曲状态   // 好像游戏进入对战场景，会一直发这个？ 该方法在一个游戏服务器环境中用于向所有已就绪的游戏客户端发送当前游戏世界的快照信息

                    foreach (Unit unit in Root.GetChild<WorldEntity>().GetChildren<Unit>())
                    {
                        if (unit.GetChild<Transform>() != null)
                        {
                            unit.GetChild<Transform>().ResetWarped();  //?? 为什么 什么东西一直重置
                        }
                    }
                }
            }
        }
        //战斗、状态、网络环境设置
        void SetBattle()
        {
            state = NetworkState.Battle;//将当前网络状态设置为 Battle（战斗）。

            server.ClearMessageHandlers(); // 清除服务器上的所有消息处理器。

            server.SetMessageHandler(MessageType.Connect, ConnectHandler);
            server.SetMessageHandler(MessageType.Disconnect, DisconnectHandler);
            server.SetMessageHandler(MessageType.Timeout, TimeoutHandler);
            server.SetMessageHandler(MessageType.Move, MoveHandler); // 移动指令
            server.SetMessageHandler(MessageType.Attack, AttackHandler); // 移动攻击
            server.SetMessageHandler(MessageType.Recall, RecallHandler); //召回
            server.SetMessageHandler(MessageType.BuyItem, BuyItemHandler);//买
            server.SetMessageHandler(MessageType.SellItem, SellItemHandler);//卖
            server.SetMessageHandler(MessageType.UseItem, UseItemHandler);//使用道具
            server.SetMessageHandler(MessageType.Change, ChangeHandler);// 改变？
            server.SetMessageHandler(MessageType.Cast, CastHandler);//投掷？
            server.SetMessageHandler(MessageType.Chat, ChatHandler);//聊天
            server.SetMessageHandler(MessageType.Select, SelectHandler);//选择？

            ((RootEntity)Root).CreateWorld();//创建世界  重要入口
            //世界中创建所有英雄角色
            foreach(var keyValue in peers)
            {
                Champion champion = new Champion(keyValue.Key, Root.GetChild<WorldEntity>().GetFountainPosition(keyValue.Value.Team), 0, 0.3f, keyValue.Value.Type, keyValue.Value.Team, initGold, Root);
                Root.GetChild<WorldEntity>().AddChild(champion);

                keyValue.Value.UnitID = champion.UnitID;
            }
        }

        public void SetLobby()
        {
            state = NetworkState.Lobby; // 设置大厅 
            server.ClearMessageHandlers();

            server.SetMessageHandler(MessageType.Connect, ConnectHandler);
            server.SetMessageHandler(MessageType.Disconnect, DisconnectHandler);
            server.SetMessageHandler(MessageType.Timeout, TimeoutHandler);
            server.SetMessageHandler(MessageType.Chat, ChatHandler);
            server.SetMessageHandler(MessageType.Select, SelectHandler);

            Root.GetChild<WorldEntity>().RemoveAllEntity();
            foreach(var keyValue in peers)
            {
                keyValue.Value.Ready = false;
                keyValue.Value.UnitID = -1;
            }
        }
        // 每帧都发？  重要的收据收集？
        void SendSnapshot()
        {
            ClientObj[] clientObjs = GetClientMsgPackObjs();                                    //获取客户端消息包对象数组。
            ChampionObj[] blueChampionObjs = Root.GetChild<WorldEntity>().GetChampionObjs(true);// 分别获取蓝色队伍和红色队伍的冠军（英雄）对象数组
            ChampionObj[] redChampionObjs = Root.GetChild<WorldEntity>().GetChampionObjs(false);
            BuildingObj[] buildingObjs = Root.GetChild<WorldEntity>().GetBuildingObj();         //获取建筑对象数组  12塔 + 12 塔 
            ActorObj[] blueVector3NoAnimObjs = Root.GetChild<WorldEntity>().GetActorObjs(true);   // 0 没有动画对象？是什么？ 这个好像是技能物体？包括子弹，飞出去东西？特效？
            ActorObj[] redVector3NoAnimObjs = Root.GetChild<WorldEntity>().GetActorObjs(false);   // 0  
            UnitObj[] blueUnitObjs = Root.GetChild<WorldEntity>().GetUnitObjs(true);            // 15  分别获取蓝色队伍和红色队伍的单位小兵对象数组 好像不对 只有3个？
            UnitObj[] redUnitObjs = Root.GetChild<WorldEntity>().GetUnitObjs(false);            // 15 个小兵； 只有3个？  HP=150 ？

            foreach (GameClient gameClient in peers.Values)                                     // peers 是一个包含所有游戏客户端连接的集合。
            {
                if (gameClient.Ready)
                {
                    Unit unit = Root.GetChild<WorldEntity>().GetUnit(gameClient.UnitID); // ? 这是找到哪个特殊的 游戏对象？

                    SnapshotObj snapshotObj = new SnapshotObj()
                    {
                        PlayerObj = Root.GetChild<WorldEntity>().GetUnit(gameClient.UnitID).GetPlayerObj(), // 这个是重要的？方法？
                        ClientObjs = clientObjs,
                        ChampionObjs = unit.Team == Team.Blue ? blueChampionObjs : redChampionObjs, ///只发自己一方的英雄数据？
                        BuildingObjs = buildingObjs,
                        Vector3NoAnimObjs = unit.Team == Team.Blue ? blueVector3NoAnimObjs : redVector3NoAnimObjs,
                        UnitObjs = unit.Team == Team.Blue ? blueUnitObjs : redUnitObjs
                    };

                    Send(MessageType.Snapshot, gameClient.Peer.ID, MessagePackSerializer.Serialize(snapshotObj), PacketFlags.None);
                }
            }
        }

        public string GetName(uint peerID)
        {
            return peers[peerID].Name;
        }

        ClientObj[] GetClientMsgPackObjs()
        {
            List<ClientObj> ret = new List<ClientObj>();

            foreach(var gameClient in peers.Values)
            {
                if (gameClient.Ready)
                {
                    Unit unit = Root.GetChild<WorldEntity>().GetUnit(gameClient.UnitID);
                    ret.Add(new ClientObj()
                    {
                        Name = gameClient.Name,
                        Type = unit.Type,
                        Level = (byte)unit.GetChild<UnitStatus>().Level,
                        Team = unit.Team
                    });
                }
            }

            return ret.ToArray();
        }

        public void Send(MessageType type, uint peerID, byte[] data, PacketFlags flags)
        {
            server.Send(type, peers[peerID].Peer, data, flags);
        }

        public void SendAll(MessageType type, byte[] data, PacketFlags flags)
        {
            foreach (GameClient client in peers.Values)
            {
                server.Send(type, client.Peer, data, flags);
            }
        }

        void MoveHandler(Peer peer, byte[] data)
        {
            Vector2Obj vector2Obj = MessagePackSerializer.Deserialize<Vector2Obj>(data);
            object args = new Vector2(vector2Obj.X, vector2Obj.Y);

            Unit unit = Root.GetChild<WorldEntity>().GetUnit(peers[peer.ID].UnitID);
            if (unit != null && unit.GetCombat(CombatAttribute.Move).IsExecutable(args))
            {
                unit.Cancel(CombatAttribute.Ability);
                unit.Execute(CombatAttribute.Move, args);
            }
            
        }

        void AttackHandler(Peer peer, byte[] data)
        {
            int unitID = MessagePackSerializer.Deserialize<int>(data);
            object args = unitID;

            Unit unit = Root.GetChild<WorldEntity>().GetUnit(peers[peer.ID].UnitID);
            if (unit != null && unit.GetCombat(CombatAttribute.Attack).IsExecutable(args))
            {
                unit.Cancel(CombatAttribute.Ability);
                unit.Execute(CombatAttribute.Attack, args);
            }
        }

        void RecallHandler(Peer peer, byte[] data)
        {
            Unit unit = Root.GetChild<WorldEntity>().GetUnit(peers[peer.ID].UnitID);
            if (unit != null && unit.GetCombat(CombatAttribute.Recall).IsExecutable(null))
            {
                unit.Cancel(CombatAttribute.Ability);
                unit.Execute(CombatAttribute.Recall, null);
            }
        }

        void BuyItemHandler(Peer peer, byte[] data)
        {
            CombatType type = MessagePackSerializer.Deserialize<CombatType>(data);
            Unit unit = Root.GetChild<WorldEntity>().GetUnit(peers[peer.ID].UnitID);
            if(unit != null)
            {
                unit.BuyItem(type);
            }
        }

        void SellItemHandler(Peer peer, byte[] data)
        {
            int slotNum = (int)MessagePackSerializer.Deserialize<byte>(data);
            Unit unit = Root.GetChild<WorldEntity>().GetUnit(peers[peer.ID].UnitID);
            if (unit != null)
            {
                unit.SellItem(slotNum);
            }
        }

        void UseItemHandler(Peer peer, byte[] data)
        {
            int slotNum = (int)MessagePackSerializer.Deserialize<byte>(data);
            Unit unit = Root.GetChild<WorldEntity>().GetUnit(peers[peer.ID].UnitID);
            if (unit != null)
            {
                unit.UseItem(slotNum);
            }
        }

        void ChangeHandler(Peer peer, byte[] data)
        {
            if(!testMode)
            {
                return;
            }

            ChangeObj changeObj = MessagePackSerializer.Deserialize<ChangeObj>(data);
            peers[peer.ID].Name = changeObj.Name;

            Unit unit = Root.GetChild<WorldEntity>().GetUnit(peers[peer.ID].UnitID);
            if(unit != null)
            {
                if(unit.Team != changeObj.Team || unit.Type != changeObj.Type)
                {
                    unit.ClearReference();
                    Root.GetChild<WorldEntity>().RemoveChild(unit);

                    Champion champion = new Champion(peer.ID, Root.GetChild<WorldEntity>().GetFountainPosition(changeObj.Team), 0, 0.3f, changeObj.Type, changeObj.Team, initGold, Root);
                    Root.GetChild<WorldEntity>().AddChild(champion);

                    peers[peer.ID].UnitID = champion.UnitID;
                    peers[peer.ID].Team = changeObj.Team;
                    peers[peer.ID].Type = changeObj.Type;
                }
            }
        }

        void CastHandler(Peer peer, byte[] data)
        {
            CastObj castObj = MessagePackSerializer.Deserialize<CastObj>(data);
            Unit unit = Root.GetChild<WorldEntity>().GetUnit(peers[peer.ID].UnitID);
            if (unit != null)
            {
                if(castObj.SkillSlotNum == 0)
                {
                    unit.Execute(CombatAttribute.QSkill, castObj);
                }
                else if(castObj.SkillSlotNum == 1)
                {
                    unit.Execute(CombatAttribute.WSkill, castObj);
                }
                else if(castObj.SkillSlotNum == 2)
                {
                    unit.Execute(CombatAttribute.ESkill, castObj);
                }
                else
                {
                    unit.Execute(CombatAttribute.RSkill, castObj);
                }
            }
        }

        void ChatHandler(Peer peer, byte[] data)
        {
            string msg = MessagePackSerializer.Deserialize<string>(data);

            Team team = peers[peer.ID].Team;
            string name = peers[peer.ID].Name;
            if (msg.StartsWith("/all "))
            {
                string spaceMsg = msg.Substring(5);
                string trimMsg = spaceMsg.TrimStart();
                string finalMsg = "[" + DateTime.Now.ToString("HH:mm:ss") + " " + name + "] " + trimMsg;

                SendAll(MessageType.Broadcast, MessagePackSerializer.Serialize(new MsgObj() { Team = Team.Yellow, Msg = finalMsg}), PacketFlags.Reliable);
            }
            else if(msg.StartsWith("/team "))
            {
                string spaceMsg = msg.Substring(6);
                string trimMsg = spaceMsg.TrimStart();
                string finalMsg = "[" + DateTime.Now.ToString("HH:mm:ss") + " " + name + "] " + trimMsg;

                foreach(var keyValue in peers)
                {
                    if(keyValue.Value.Team == team)
                    {
                        Send(MessageType.Broadcast, keyValue.Key, MessagePackSerializer.Serialize(new MsgObj() { Team = team, Msg = finalMsg }), PacketFlags.Reliable);
                    }
                }
            }
            else
            {
                string finalMsg = "[" + DateTime.Now.ToString("HH:mm:ss") + " " + name + "] " + msg;

                SendAll(MessageType.Broadcast, MessagePackSerializer.Serialize(new MsgObj() { Team = Team.Yellow, Msg = finalMsg }), PacketFlags.Reliable);
            }
        }
    }
}
