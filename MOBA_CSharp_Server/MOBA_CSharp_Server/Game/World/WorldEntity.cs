using Microsoft.Xna.Framework;
using MOBA_CSharp_Server.Library.ECS;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MOBA_CSharp_Server.Game
{
    public class WorldEntity : Entity
    {
        int unitID = 0;

        Dictionary<int, Unit> units = new Dictionary<int, Unit>(); //单元？单位？ 非英雄单位？小兵？塔？ 映射出在世界单位的管理体系；

        public WorldEntity(Entity root) : base(root)
        {
            AddInheritedType(typeof(WorldEntity));
        }
        //添加实体 到树形结构节点上 
        public override void AddChild(Entity entity)
        {
            if (isDuringStep)
            {
                addEntities.Add(entity);
            }
            else
            {
                foreach (Type type in entity.InheritedTypes)
                {
                    if (!children.ContainsKey(type))
                    {
                        children.Add(type, new List<Entity>());
                    }
                    children[type].Add(entity); // 同种类型的 实例 挂到一起；
                }

                if (entity is Unit) // 除了派生这个节点外，还得记录下根节点是 Unit 的所有活动实例
                {
                    Unit unit = (Unit)entity;
                    units.Add(unit.UnitID, unit);
                }
            }
        }

        public override void RemoveChild(Entity entity)
        {
            if (isDuringStep)
            {
                removeEntities.Add(entity);
            }
            else
            {
                foreach (Type type in entity.InheritedTypes)
                {
                    children[type].Remove(entity);  // 移除盖类型下的实例；
                    if (children[type].Count == 0) //当这个实例=0的情况下 清掉对应的类型
                    {
                        children.Remove(type);  
                    }
                }

                if (entity is Unit)
                {
                    Unit unit = (Unit)entity;
                    units.Remove(unit.UnitID);
                }
            }
        }

        public Unit GetUnit(int unitID)
        {
            
            if(units.ContainsKey(unitID))
            {
                return units[unitID];
            }
            else
            {
                return addEntities.FirstOrDefault(x => x is Unit && ((Unit)x).UnitID == unitID) as Unit;
            }
        }

        public int GenerateUnitID()
        {
            return unitID++;
        }
        // 重要的入口；服务端中 游戏世界单位 管理入口；
        public override void Step(float deltaTime)
        {
            SetStatus(deltaTime); //更新所有单位的状态  主要是1这个 deltaTime作为因子，而可能会检测都操作数据变化？

            ConfirmDamage();     // 确认伤害

            base.Step(deltaTime);

            Destroy();
        }

        void SetStatus(float deltaTime)
        {
            foreach (Unit unit in GetChildren<Unit>()) /// 得到场景中所有的 场上单位 Unit 
            {
                unit.SetStatus(deltaTime);  // 
            }
        }

        void ConfirmDamage()
        {
            foreach (Unit unit in GetChildren<Unit>()) //得到场景中所有的 场上单位 Unit  确认所有单位的伤害
            {
                unit.ConfirmDamage();
            }
        }
        // 获取英雄数据
        public ChampionObj[] GetChampionObjs(bool blueTeam)
        {
            List<ChampionObj> ret = new List<ChampionObj>();

            foreach(var unit in GetChildren<Champion>())
            {
                if ((blueTeam && unit.Status.GetValue(Team.Blue)) || (!blueTeam && unit.Status.GetValue(Team.Red)))
                {
                    ret.Add(unit.GetChampionObj());
                }
            }

            return ret.ToArray();
        }
        // 获取塔数据
        public BuildingObj[] GetBuildingObj()
        {
            List<BuildingObj> ret = new List<BuildingObj>();

            foreach (var unit in GetChildren<Building>())
            {
                if(unit.Type != UnitType.Fountain)
                {
                    ret.Add(unit.GetBuildingObj());
                }
            }

            return ret.ToArray();
        }
        //技能物体。
        public ActorObj[] GetActorObjs(bool blueTeam)
        {
            List<ActorObj> ret = new List<ActorObj>();

            foreach (var unit in GetChildren<Actor>())
            {
                if((blueTeam && unit.Status.GetValue(Team.Blue)) || (!blueTeam && unit.Status.GetValue(Team.Red)))
                {
                    ret.Add(unit.GetActorObj());
                }
            }

            return ret.ToArray();
        }
        // 获取每队的小兵数据
        public UnitObj[] GetUnitObjs(bool blueTeam)
        {
            List<UnitObj> ret = new List<UnitObj>();

            foreach (var unit in GetChildren<Unit>())
            {
                if (UnitType.Minion <= unit.Type && unit.Type <= UnitType.UltraMonster && ((blueTeam && unit.Status.GetValue(Team.Blue)) || (!blueTeam && unit.Status.GetValue(Team.Red))))
                {
                    ret.Add(unit.GetUnitObj());
                }
            }

            return ret.ToArray();
        }
        // 泉水位置？？？
        public Vector2 GetFountainPosition(Team team)
        {
            return GetChildren<Fountain>().ToList().First(x => x.Team == team).GetChild<Transform>().Position;
        }

        public void RemoveAllEntity()
        {
            List<Entity> allEntities = GetChildren<Entity>().ToList();
            allEntities.ForEach(x => x.ClearReference());
            allEntities.ForEach(x => RemoveChild(x));
        }
    }
}
