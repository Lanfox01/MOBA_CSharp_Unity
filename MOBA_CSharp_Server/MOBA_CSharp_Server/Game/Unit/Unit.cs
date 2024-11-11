using Microsoft.Xna.Framework;
using MOBA_CSharp_Server.Library.DataReader;
using MOBA_CSharp_Server.Library.ECS;
using MOBA_CSharp_Server.Library.Physics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MOBA_CSharp_Server.Game
{
    // 游戏场景中的 基本呈现单元
    public class Unit : Entity
    {
        public UnitType Type { get; private set; }
        public int UnitID { get; private set; }
        public int OwnerUnitID { get; private set; }
        public Team Team { get; private set; }

        //Gold
        public float Gold { get; private set; }

        //HPMP
        public float HP { get; private set; }
        public float MP { get; private set; }

        List<DamageHistory> hpDamageHistories = new List<DamageHistory>();//一个记录伤害历史的列表
        List<DamageHistory> mpDamageHistories = new List<DamageHistory>();//mpDamageHistories 是一个列表（或集合），其中包含了关于MP变动的历史记录

        //Status
        public Status Status { get; private set; } = new Status();

        //Combat
        Dictionary<CombatAttribute, List<Combat>> combats = new Dictionary<CombatAttribute, List<Combat>>();

        int itemSlotNum;

        public Unit(Vector2 position, float height, float rotation, CollisionType collisionType, float radius, UnitType type, Team team, float gold, Entity root) : base(root)
        {
            AddInheritedType(typeof(Unit));

            itemSlotNum = Root.GetChild<DataReaderEntity>().GetYAMLObject(@"YAML\ServerConfig.yml").GetData<int>("ItemSlotNum");

            Type = type;
            UnitID = Root.GetChild<WorldEntity>().GenerateUnitID();
            OwnerUnitID = UnitID;
            Team = team;
            Gold = gold;
            //每个单元 最基本的 两个 数据组件
            AddChild(new UnitStatus(this, Root));
            AddChild(new Transform(position, height, rotation, collisionType, radius, this, Root));

            SetStatus(0);
            Revive();
        }

        public Unit(Vector2 position, float height, float rotation, CollisionType collisionType, float radius, UnitType type, int ownerUnitID, Team team, float gold, Entity root) : base(root)
        {
            AddInheritedType(typeof(Unit));

            Type = type;
            UnitID = Root.GetChild<WorldEntity>().GenerateUnitID();
            OwnerUnitID = ownerUnitID;
            Team = team;
            Gold = gold;

            AddChild(new UnitStatus(this, Root));
            AddChild(new Transform(position, height, rotation, collisionType, radius, this, Root));

            SetStatus(0);
            Revive();
        }

        protected YAMLObject GetYAMLObject()
        {
            var yaml = Root.GetChild<DataReaderEntity>().GetYAMLObject(Type);
            if(yaml != null)
            {
                return yaml;
            }
            else
            {
                return Root.GetChild<DataReaderEntity>().GetYAMLObject(@"YAML\Units\Default.yml");
            }
        }
        //设置角色的状态
        public void SetStatus(float deltaTime)
        {
            //Init // 这个建了很多容器 用来存储大量的各种参数？并且分类存放？  主要是对应的数据组件 Combat 也是这种设计的；  
            Dictionary<FloatStatus, List<FloatParam>> floatParams = new Dictionary<FloatStatus, List<FloatParam>>();
            foreach(FloatStatus type in Enum.GetValues(typeof(FloatStatus)))
            {
                floatParams.Add(type, new List<FloatParam>());
            }
            Dictionary<BoolStatus, List<BoolParam>> boolParams = new Dictionary<BoolStatus, List<BoolParam>>();
            foreach (BoolStatus type in Enum.GetValues(typeof(BoolStatus)))
            {
                boolParams.Add(type, new List<BoolParam>());
            }
            Dictionary<Team, List<VisionParam>> visionParams = new Dictionary<Team, List<VisionParam>>();
            foreach(Team team in Enum.GetValues(typeof(Team)))
            {
                visionParams.Add(team, new List<VisionParam>());
            }
            List<AnimParam> animParams = new List<AnimParam>();

            //Add  遍历 单位 uint 身上挂在的 数据组件 Combat；可能有多种ComBat的派生子类组件；全部收集 装入三个大仓库中 应该是引用收集不是复制
            foreach (Combat combat in GetChildren<Combat>())
            {
                foreach(FloatParam param in combat.floatParams.Values)
                {
                    floatParams[param.Type].Add(param);
                }
                foreach (BoolParam param in combat.boolParams.Values)
                {
                    boolParams[param.Type].Add(param);
                }
                foreach (VisionParam param in combat.visionParams.Values)
                {
                    visionParams[param.Team].Add(param);
                }
                if(combat.animParam != null)
                {
                    animParams.Add(combat.animParam);
                }
            }

            //Set
            foreach (FloatStatus type in Enum.GetValues(typeof(FloatStatus)))
            {
                if (floatParams[type].Count > 0)
                {
                    float addSum = floatParams[type].Where(x => x.IsAdd).Sum(x => x.Value);
                    float mulSum = floatParams[type].Where(x => !x.IsAdd).Sum(x => x.Value);
                    Status.FloatStatus[(int)type] = addSum * (1.0f + mulSum);
                }
                else
                {
                    Status.FloatStatus[(int)type] = 0;
                }
            }
            foreach (BoolStatus type in Enum.GetValues(typeof(BoolStatus)))
            {
                if (boolParams[type].Count > 0)
                {
                    int minPriority = boolParams[type].Min(x => x.Priority);
                    Status.BoolStatus[(int)type] = boolParams[type].First(x => x.Priority == minPriority).Value;
                }
                else
                {
                    Status.BoolStatus[(int)type] = false;
                }
            }
            foreach (Team team in Enum.GetValues(typeof(Team)))
            {
                if (visionParams[team].Count > 0)
                {
                    int minPriority = visionParams[team].Min(x => x.Priority);
                    Status.VisionStatus[(int)team] = visionParams[team].First(x => x.Priority == minPriority).Value;
                }
                else
                {
                    Status.VisionStatus[(int)team] = false;
                }
            }
            if(animParams.Count > 0)
            {
                int minPriority = animParams.Min(x => x.Priority);
                var animParam = animParams.First(x => x.Priority == minPriority);
                Status.SetAnimationStatus(animParam.Type, animParam.SpeedRate, deltaTime);
            }
            else
            {
                Status.SetAnimationStatus(AnimationType.Idle, 1f, deltaTime);
            }
        }

        public override void AddChild(Entity entity)
        {
            base.AddChild(entity);

            if(entity is Combat)
            {
                Combat combat = (Combat)entity;
                foreach(CombatAttribute attribute in combat.attributes)
                {
                    if(!combats.ContainsKey(attribute))
                    {
                        combats.Add(attribute, new List<Combat>());
                    }
                    combats[attribute].Add(combat);
                }
            }
        }

        public override void RemoveChild(Entity entity)
        {
            base.RemoveChild(entity);

            if(entity is Combat)
            {
                Combat combat = (Combat)entity;
                foreach (CombatAttribute attribute in combat.attributes)
                {
                    combats[attribute].Remove(combat);
                    if(combats[attribute].Count == 0)
                    {
                        combats.Remove(attribute);
                    }
                }
            }
        }

        public Combat GetCombat(CombatAttribute attribute)
        {
            if(combats.ContainsKey(attribute))
            {
                return combats[attribute][0];
            }
            else
            {
                return null;
            }
        }

        public Combat[] GetCombats(CombatAttribute attribute)
        {
            if (combats.ContainsKey(attribute))
            {
                return combats[attribute].ToArray();
            }
            else
            {
                return new Combat[0];
            }
        }

        public void Execute(CombatAttribute attribute, object args)
        {
            foreach(var combat in GetCombats(attribute))
            {
                combat.Execute(args);
            }
        }

        public void Cancel(CombatAttribute attribute)
        {
            foreach (var combat in GetCombats(attribute))
            {
                combat.Cancel();
            }
        }

        public void ConfirmDamage()
        {
            ConfirmHPDamage(); //HP伤害？ 处理生命值（HP）的伤害逻辑 物理伤害？
            ConfirmMPDamage(); // MP 伤害？魔法值（MP）的伤害逻辑  魔法伤害？
        }
        // 它首先更新角色的状态，然后计算新的生命值，并根据生命值的变化处理角色死亡的情况。以下是详细步骤：
        void ConfirmHPDamage()
        {   //根据hpDamageHistories（一个记录伤害历史的列表）中是否有任何记录标记为伤害（IsDamage为true），来设置角色是否被伤害
            Status.Damaged = hpDamageHistories.Any(x => x.IsDamage);
            Status.AttackedUnitIDs = hpDamageHistories.Select(x => x.UnitID).ToList();//收集所有造成伤害的单位ID。

            float sum = hpDamageHistories.Sum(x => x.IsDamage ? x.Amount : -x.Amount);//根据记录中的IsDamage字段来决定是加上还是减去Amount（伤害值或治疗值）。

            float nextHP = HP - sum;//计算出新的生命值nextHP，并确保它不会超过最大生命值（Status.FloatStatus[(int)FloatStatus.MaxHP]）也不会低于0。
            if (nextHP > Status.FloatStatus[(int)FloatStatus.MaxHP])
            {
                nextHP = Status.FloatStatus[(int)FloatStatus.MaxHP];
            }
            else if (nextHP < 0)
            {
                nextHP = 0;
            }

            Status.Dead = HP > 0 && nextHP <= 0;
            HP = nextHP;
            ///用于处理角色死亡时奖励的分配。它确保了当角色被多个单位同时攻击并导致死亡时，这些攻击单位能够公平地获得奖励。
            ///同时，通过去除重复的ID和检查列表是否为空，避免了不必要的计算和潜在的错误。  这里应该是很重要的代码段
            if (Status.Dead)
            { //从hpDamageHistories列表中筛选出所有标记为伤害（IsDamage为true）的记录。 提取这些记录中的UnitID（攻击单位的ID）。
                List<int> attackedUnitIDs = hpDamageHistories.Where(x => x.IsDamage).Select(x => x.UnitID).ToList();
                attackedUnitIDs = attackedUnitIDs.Distinct().ToList();//使用Distinct()方法去除重复的ID

                if (attackedUnitIDs.Count > 0)//有攻击者？ 自杀呢?应该不行  很高地方掉落呢
                {
                    int level = GetChild<UnitStatus>().Level; // 获取当前单位等级： 根据等级来计算奖励

                    float exp = Utilities.CalculateExp(Type, level) / attackedUnitIDs.Count; // attackedUnitIDs.Count 攻击单位的数量；平均分？见者有份
                    float gold = Utilities.CalculateGold(Type, level) / attackedUnitIDs.Count;

                    foreach (int attackedUnitID in attackedUnitIDs)
                    {
                        Unit unit = Root.GetChild<WorldEntity>().GetUnit(attackedUnitID); // 根据每个UnitID 找到这个对象Unit
                        if (unit != null)
                        {
                            unit.AddExp(exp);     // 刷新这个对象Unit数据；加钱加经验
                            unit.AddGold(gold);
                        }
                    }
                }
            }
            //清理伤害历史：清空hpDamageHistories列表，为下一次伤害计算做准备

            hpDamageHistories.Clear();
        }

        public void AddExp(float amount)
        {
            GetChild<UnitStatus>().AddExp(amount);
        }

        public void AddGold(float amount)
        {
            Gold += amount;
        }
        // 计算和确认魔法值（MP，Magic Points）的损耗，并更新角色的MP值
        void ConfirmMPDamage()
        {
            float sum = mpDamageHistories.Sum(x => x.IsDamage ? x.Amount : -x.Amount);

            float nextMP = MP - sum; // 不超最大，或者清零
            if (nextMP > Status.FloatStatus[(int)FloatStatus.MaxMP])
            {
                nextMP = Status.FloatStatus[(int)FloatStatus.MaxMP];
            }
            else if (nextMP < 0)
            {
                nextMP = 0;
            }

            MP = nextMP;

            mpDamageHistories.Clear();
        }

        public void Revive()
        {
            HP = Status.FloatStatus[(int)FloatStatus.MaxHP];
            MP = Status.FloatStatus[(int)FloatStatus.MaxMP];
        }

        public void Damage(int unitID, bool isDamage, float amount)
        {
            hpDamageHistories.Add(new DamageHistory(unitID, isDamage, amount));

            Unit enemyUnit = Root.GetChild<WorldEntity>().GetUnit(unitID);
            if(enemyUnit != null && enemyUnit.Team != Team)
            {
                Sight enemySight = enemyUnit.GetChild<Sight>();
                if(enemySight != null)
                {
                    enemySight.SetSight(Team);
                }

                Sight sight = GetChild<Sight>();
                if(sight != null)
                {
                    sight.SetSight(enemyUnit.Team);
                }
            }
        }

        public void DamageMP(int unitID, bool isDamage, float amount)
        {
            mpDamageHistories.Add(new DamageHistory(unitID, isDamage, amount));
        }

        public UnitObj GetUnitObj()
        {
            return new UnitObj()
            {
                UnitID = UnitID,
                Type = Type,
                Team = Team,
                Position = new Vector2Obj() { X = GetChild<Transform>().Position.X, Y = GetChild<Transform>().Position.Y },
                Rotation = GetChild<Transform>().Rotation,
                Warped = GetChild<Transform>().Warped,
                AnimationNum = (byte)Status.AnimationStatus,
                SpeedRate = Status.SpeedRate,
                PlayTime = Status.PlayTime,
                MaxHP = Status.GetValue(FloatStatus.MaxHP),
                CurHP = HP
            };
        }

        public PlayerObj GetPlayerObj()
        {
            //Effects
            List<CombatObj> effects = new List<CombatObj>();
            foreach(Effect effect in GetChildren<Effect>())
            {
                if(effect.IsSendDataToClient)
                {
                    effects.Add(effect.GetCombatObj());
                }
            }

            //Items
            List<CombatObj> items = new List<CombatObj>();
            foreach (Item item in GetChildren<Item>())
            {
                items.Add(item.GetCombatObj());
            }

            //Skills
            List<CombatObj> skills = new List<CombatObj>();
            foreach (Skill skill in GetChildren<Skill>())
            {
                skills.Add(skill.GetCombatObj());
            }

            return new PlayerObj()
            {
                UnitID = UnitID,
                Exp = GetChild<UnitStatus>().Exp,
                NextExp = GetChild<UnitStatus>().GetNextExp(),
                Gold = Gold,
                Effects = effects.ToArray(),
                Items = items.ToArray(),
                Skills = skills.ToArray()
            };
        }

        public void BuyItem(CombatType type)
        {
            if(GetChild<OnBase>() == null || type < CombatType.Potion)
            {
                return;
            }

            float buyingPrice = Root.GetChild<CSVReaderEntity>().GetItemTable(type).BuyingPrice;
            int slotNum = GetEmptySlotNum();

            if(Gold >= buyingPrice && slotNum != -1)
            {
                AddChild(ItemFactory.CreateItem(type, slotNum, this, Root));
                AddGold(-buyingPrice);
            }
        }

        int GetEmptySlotNum()
        {
            List<Item> items = GetCombats(CombatAttribute.Item).Select(x => (Item)x).ToList();
            for(int i=0; i<itemSlotNum; i++)
            {
                if(items.All(x => x.SlotNum != i))
                {
                    return i;
                }
            }

            return -1;
        }

        public void SellItem(int slotNum)
        {
            if (GetChild<OnBase>() == null || slotNum >= itemSlotNum)
            {
                return;
            }

            Item item = GetItem(slotNum);
            if (item != null)
            {
                item.ClearReference();
                RemoveChild(item);

                AddGold(Root.GetChild<CSVReaderEntity>().GetItemTable(item.Type).SellingPrice * item.Count);
            }
        }

        public void UseItem(int slotNum)
        {
            if (slotNum >= itemSlotNum)
            {
                return;
            }

            Item item = GetItem(slotNum);
            if (item != null)
            {
                item.Execute(null);
            }
        }

        Item GetItem(int slotNum)
        {
            List<Item> items = GetCombats(CombatAttribute.Item).Select(x => (Item)x).ToList();
            return items.FirstOrDefault(x => x.SlotNum == slotNum);
        }
    }
}
