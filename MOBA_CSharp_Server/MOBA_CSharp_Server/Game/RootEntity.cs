using Microsoft.Xna.Framework;
using MOBA_CSharp_Server.Library.ECS;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// Enet 库；
namespace MOBA_CSharp_Server.Game
{
    //  这是游戏世界的根实体 它负责初始化游戏世界的各个方面，包括数据读取、物理模拟、路径寻找、世界管理、网络通信等。
    public class RootEntity : Entity
    {
        public RootEntity() : base(null)
        {
            AddInheritedType(typeof(RootEntity));

            AddEntities();// 启动必要的模块

            //CreateWorld();

            Listen();
        }

        /*
        这个方法创建了游戏世界所需的各种实体，并将它们作为子实体添加到RootEntity中。这些实体包括：
        DataReaderEntity：用于读取配置文件（如YAML格式的配置文件）。
        CSVReaderEntity：用于读取CSV格式的数据文件（尽管在这段代码中未直接使用）。
        PhysicsEntity：处理物理模拟，如碰撞检测和边界墙。
        PathfindingEntity：处理路径寻找，为游戏中的角色和单位找到最佳移动路径。
        WorldEntity：管理游戏世界中的所有对象，如角色、建筑、怪物等。
        NetworkEntity：处理网络通信，允许服务器与客户端之间的数据交换。
         */
        void AddEntities()
        {
            //DataReader
            DataReaderEntity dataReader = new DataReaderEntity(this);
            AddChild(dataReader);

            //TableReader
            CSVReaderEntity tableReader = new CSVReaderEntity(this);
            AddChild(tableReader);

            //Physics
            PhysicsEntity physics = new PhysicsEntity(this);
            AddChild(physics);

            //Pathfinding
            PathfindingEntity pathfinding = new PathfindingEntity(this);
            AddChild(pathfinding);

            //World
            WorldEntity world = new WorldEntity(this);
            AddChild(world);

            //Network
            NetworkEntity network = new NetworkEntity(this);
            AddChild(network);
        }
        /*
         * 这个方法根据配置文件中的信息创建游戏世界。它读取地图信息（MapInfo），
         * 然后创建并配置地图上的各种元素，如泉水（Fountain）、小兵集结点（MinionRelayPoint）、
         * 核心（Core）、塔（Tower）、怪物（Monster）等。此外，
         * 它还根据配置文件中的信息创建物理边界（如边缘墙、圆形墙和草丛）。
         */
        public void CreateWorld()
        {
            PhysicsEntity physics = GetChild<PhysicsEntity>();
            WorldEntity world = GetChild<WorldEntity>();

            //world.RemoveAllEntity();

            MapInfo mapInfo = JsonConvert.DeserializeObject<MapInfo>(File.ReadAllText(GetChild<DataReaderEntity>().GetYAMLObject(@"YAML\ServerConfig.yml").GetData<string>("Map")));

            //Fountain   如泉水
            SpawnInfo blueSpawnInfo = mapInfo.blueSpawn;
            world.AddChild(new Fountain(new Vector2(blueSpawnInfo.x, blueSpawnInfo.y), 0, blueSpawnInfo.regainRadius, Team.Blue, this));

            SpawnInfo redSpawnInfo = mapInfo.redSpawn;
            world.AddChild(new Fountain(new Vector2(redSpawnInfo.x, redSpawnInfo.y), 0, redSpawnInfo.regainRadius, Team.Red, this));

            //MinionRelayPoint      小兵传送点
            Dictionary<Team, Dictionary<int, Dictionary<int, Vector2>>> points = new Dictionary<Team, Dictionary<int, Dictionary<int, Vector2>>>();
            points.Add(Team.Blue, new Dictionary<int, Dictionary<int, Vector2>>());
            points[Team.Blue].Add(0, new Dictionary<int, Vector2>());
            points[Team.Blue].Add(1, new Dictionary<int, Vector2>());
            points[Team.Blue].Add(2, new Dictionary<int, Vector2>());
            points.Add(Team.Red, new Dictionary<int, Dictionary<int, Vector2>>());
            points[Team.Red].Add(0, new Dictionary<int, Vector2>());
            points[Team.Red].Add(1, new Dictionary<int, Vector2>());
            points[Team.Red].Add(2, new Dictionary<int, Vector2>());
            foreach (var minionRelayPoint in mapInfo.minionRelayPoints)
            {
                points[minionRelayPoint.blueTeam ? Team.Blue : Team.Red][minionRelayPoint.laneNum].Add(minionRelayPoint.index, new Vector2(minionRelayPoint.x, minionRelayPoint.y));
            }

            //Core   核心
            CoreInfo blueCoreInfo = mapInfo.blueCore;
            world.AddChild(new Core(points[Team.Blue], new Vector2(blueCoreInfo.x, blueCoreInfo.y), blueCoreInfo.angle, blueCoreInfo.radius, Team.Blue, this));

            CoreInfo redCoreInfo = mapInfo.redCore;
            world.AddChild(new Core(points[Team.Red], new Vector2(redCoreInfo.x, redCoreInfo.y), redCoreInfo.angle, redCoreInfo.radius, Team.Red, this));

            world.GetChildren<Core>().ToList().ForEach(x => x.SetGoal());

            //Tower   创建塔
            foreach (var towerInfo in mapInfo.towers)
            {
                world.AddChild(new Tower(towerInfo.height, new Vector2(towerInfo.x, towerInfo.y), towerInfo.angle, towerInfo.radius, towerInfo.blueTeam ? Team.Blue : Team.Red, this));
            }

            //Monster 创建怪物
            foreach (var monsterInfo in mapInfo.monsters)
            {
                world.AddChild(new Monster(monsterInfo.chaseRadius, monsterInfo.respawnTime, new Vector2(monsterInfo.x, monsterInfo.y), monsterInfo.angle, 0.3f, monsterInfo.type, this));
            }
            //创建物理障碍物
            foreach (var edgeInfo in mapInfo.edges)
            {
                physics.CreateEdgeWall(new Vector2(edgeInfo.x0, edgeInfo.y0), new Vector2(edgeInfo.x1, edgeInfo.y1));
            }
            // 创建圆形墙 加载寻路网格
            foreach (var circleInfo in mapInfo.circles)
            {
                physics.CreateCircleWall(circleInfo.radius, new Vector2(circleInfo.x, circleInfo.y));
            }
            // 灌木 Info
            foreach (var bushInfo in mapInfo.bushes)
            {
                List<Vector2> vertices = new List<Vector2>();
                vertices.Add(new Vector2(bushInfo.x0, bushInfo.y0));
                vertices.Add(new Vector2(bushInfo.x1, bushInfo.y1));
                vertices.Add(new Vector2(bushInfo.x2, bushInfo.y2));
                vertices.Add(new Vector2(bushInfo.x3, bushInfo.y3));
                physics.CreateBush(vertices);
            }
            // 寻路插件
            GetChild<PathfindingEntity>().Load(GetChild<DataReaderEntity>().GetYAMLObject(@"YAML\ServerConfig.yml").GetData<string>("NavMesh"));
        }

        void Listen()
        {   // 调用 某个 模块 下面的 某个函数
            //
            GetChild<NetworkEntity>().Listen((ushort)GetChild<DataReaderEntity>().GetYAMLObject(@"YAML\ServerConfig.yml").GetData<int>("Port"), 100);

        }

        bool setLobbyFlag = false;
        public void SetLobby()
        {
            setLobbyFlag = true; //方法设置一个标志，指示服务器准备进入大厅状态  一般什么时候调用、
        }

        public override void Step(float deltaTime)
        {
            base.Step(deltaTime);

            if(setLobbyFlag) 
            {
                setLobbyFlag = false; // 标志，开关， 开或者关闭
                GetChild<NetworkEntity>().SetLobby();
            }
        }
    }
}
