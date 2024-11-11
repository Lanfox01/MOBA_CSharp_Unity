using MOBA_CSharp_Server.Library.DataReader;
using MOBA_CSharp_Server.Library.ECS;
using System.Collections.Generic;

namespace MOBA_CSharp_Server.Game
{
    //这个类包含了多种属性和方法 管理和执行与战斗相关的行为   是一个广义的模板吧；
    public class Combat : UnitComponent
    {
        public CombatType Type { get; private set; } //战斗类型
        //Attributes
        public List<CombatAttribute> attributes { get; private set; } = new List<CombatAttribute>();//一个CombatAttribute类型的列表，用于存储战斗属性
        //Display
        public float Timer { get; protected set; } // 计时器，用于冷却时间或计时。
        public int Stack { get; protected set; } // 堆叠数量，表示技能或效果的次数 表示某些可以叠加的技能或效果的数量  叠加伤害？
        public bool IsActive { get; protected set; } // 布尔值，表示战斗组件是否激活 应该是激活该本组件

        public bool StackDisplayCount { get; protected set; }  // 布尔值，指示是否显示堆叠数量。
        public bool ExecuteConsumeCount { get; protected set; } // 布尔值，指示执行时是否消耗堆叠数量。  比如充能技能，一个技能回叠加几层，另外一个操作可能会消耗叠加；
        public float Cooldown { get; protected set; }       //冷却时间。  
        public int Charge { get; protected set; }           //当前充能数。  这通常用于需要“充能”才能使用的技能或效果，如某些大招或特殊技能。
        public int MaxCharge { get; protected set; }        // 最大充能数。
        public int Count { get; protected set; }            // 技能或效果的当前次数。  药水？  
        public float Cost { get; protected set; }           // 执行成本，通常指消耗的魔法值(MP)。

        //Params   分别用于存储浮点型、布尔型和视野参数。  是不是几乎所有用到的参数都会存储到这里
        //将 FloatStatus 枚举作为字典的键是完全可行的。这样做的好处是，你可以通过枚举值来直接访问或修改字典中对应的 FloatParam 对象，
        //而不需要使用字符串或其他可能引发错误或混淆的键类型。 学习， 这样子键的意义更加明显，好找
        // 任意一个作战单位，他的数据都可以从下面三套或者4套仓库中找到 ； 其实理论上可以看成一个单位的三张表；
        // 具体的数据  根据派生的子类的具体的组件情况；这里基类只做一个扩容的万金油模板 比如有些组件可能没有一些属性数据；
        public Dictionary<FloatStatus, FloatParam> floatParams = new Dictionary<FloatStatus, FloatParam>();
        public Dictionary<BoolStatus, BoolParam> boolParams = new Dictionary<BoolStatus, BoolParam>();
        public Dictionary<Team, VisionParam> visionParams = new Dictionary<Team, VisionParam>();
        public AnimParam animParam = null;  //动画参数。

        //Execute  布尔值，表示是否正在执行。 表示技能或效果是否正在执行。这有助于控制技能的执行流程，避免重复执行或冲突。
        public bool IsExecute { get; private set; }

        //构造函数接收战斗类型、单位根节点和实体根节点作为参数，并初始化一些基础属性。
        //它还从YAML对象中读取配置数据，并根据这些数据设置战斗组件的初始状态。
        public Combat(CombatType type, Unit unitRoot, Entity root) : base(unitRoot, root)
        {
            AddInheritedType(typeof(Combat));

            Type = type;

            StackDisplayCount = GetYAMLObject().GetData<bool>("StackDisplayCount");
            ExecuteConsumeCount = GetYAMLObject().GetData<bool>("ExecuteConsumeCount");
            Cooldown = GetYAMLObject().GetData<float>("Cooldown");
            Charge = GetYAMLObject().GetData<int>("Charge");
            MaxCharge = GetYAMLObject().GetData<int>("MaxCharge");
            Count = GetYAMLObject().GetData<int>("Count");
            Cost = GetYAMLObject().GetData<float>("Cost");

            if (!StackDisplayCount && Charge < MaxCharge)
            {
                Timer = Cooldown;
            }

            SetStackAndIsActive();
        }

        void SetStackAndIsActive()
        {
            Stack = StackDisplayCount ? Count : Charge;
            IsActive = StackDisplayCount ? Count > 0 && unitRoot.MP >= Cost : Charge > 0 && unitRoot.MP >= Cost;
        }
        //向attributes列表中添加一个战斗属性。
        public void AddAttribute(CombatAttribute attribute)
        {
            attributes.Add(attribute);
        }
        //执行战斗行为。如果满足执行条件，将IsExecute设置为true
        public virtual void Execute(object args)
        {
            if(IsExecutable(args))
            {
                IsExecute = true;
            }
        }
        //取消执行战斗行为，将IsExecute设置为false
        public virtual void Cancel()
        {
            IsExecute = false;
        }
        //检查是否满足执行战斗行为的条件。  主要考虑魔法够不够，冷却值 等 
        public virtual bool IsExecutable(object args)
        {
            if(StackDisplayCount)
            {
                return unitRoot.HP > 0 && unitRoot.MP >= Cost && Count > 0 && (Cooldown == 0 || (Cooldown > 0 && Timer <= 0));
            }
            else
            {
                return unitRoot.HP > 0 && unitRoot.MP >= Cost && Charge > 0;
            }
        }
        //消耗魔法值并减少堆叠数量或充能数。
        protected virtual void ConsumeMPAndReduceStack()
        {
            unitRoot.DamageMP(unitRoot.UnitID, true, Cost);
            if(StackDisplayCount)
            {
                Count--;
                if(Cooldown > 0)
                {
                    Timer = Cooldown;
                }
            }
            else
            {
                Charge--;
                if(Timer <= 0)
                {
                    Timer = Cooldown;
                }
            }
        }
        // 一个虚方法，用于在每一步中检查是否继续执行战斗行为
        protected virtual bool ContinueExecution()
        {
            return true;
        }
        //一个虚方法，用于执行具体的战斗行为逻辑。
        protected virtual void ExecuteProcess(float deltaTime)
        {

        }
        // 在每一帧中调用，用于更新计时器、堆叠数量和激活状态，并根据需要执行战斗行为
        public override void Step(float deltaTime)
        {
            base.Step(deltaTime);

            //Timer
            if(StackDisplayCount)
            {
                if(Cooldown > 0 && Timer > 0)
                {
                    Timer -= deltaTime;
                    if(Timer < 0)
                    {
                        Timer = 0;
                    }
                }
            }
            else
            {
                if (Charge < MaxCharge)
                {
                    Timer -= deltaTime;
                    if (Timer <= 0)
                    {
                        Charge++;

                        if (Charge < MaxCharge)
                        {
                            Timer = Cooldown;
                        }
                        else
                        {
                            Timer = 0;
                        }
                    }
                }
            }
            //Stack&&IsActive
            SetStackAndIsActive();

            if (IsExecute)
            {
                if(ContinueExecution())
                {
                    ExecuteProcess(deltaTime);
                }
                else
                {
                    Cancel();
                }
            }
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
                return Root.GetChild<DataReaderEntity>().GetYAMLObject(@"YAML\Combats\Default.yml");
            }
        }

        protected void SetFloatParam(FloatStatus type, float value, bool isAdd)
        {
            if(!floatParams.ContainsKey(type))
            {
                floatParams.Add(type, new FloatParam(type, value, isAdd));
            }
            else
            {
                floatParams[type] = new FloatParam(type, value, isAdd);
            }
        }

        protected void UnSetFloatParam(FloatStatus type)
        {
            if(floatParams.ContainsKey(type))
            {
                floatParams.Remove(type);
            }
        }

        protected void SetBoolParam(BoolStatus type, bool value, int priority)
        {
            if (!boolParams.ContainsKey(type))
            {
                boolParams.Add(type, new BoolParam(type, value, priority));
            }
            else
            {
                boolParams[type] = new BoolParam(type, value, priority);
            }
        }

        protected void UnSetBoolParam(BoolStatus type)
        {
            if (boolParams.ContainsKey(type))
            {
                boolParams.Remove(type);
            }
        }

        protected void SetVisionParam(Team team, bool value, int priority)
        {
            if (!visionParams.ContainsKey(team))
            {
                visionParams.Add(team, new VisionParam(team, value, priority));
            }
            else
            {
                visionParams[team] = new VisionParam(team, value, priority);
            }
        }

        protected void UnSetVisionParam(Team team)
        {
            if (visionParams.ContainsKey(team))
            {
                visionParams.Remove(team);
            }
        }

        protected void SetAnimationParam(AnimationType type, float speedRate, int priority)
        {
            animParam = new AnimParam(type, speedRate, priority);
        }

        protected void UnSetAnimationParam()
        {
            animParam = null;
        }
        //方法创建并返回一个 CombatObj 对象，包含类型、插槽编号、计时器、堆叠数和激活状态。
        public CombatObj GetCombatObj()
        {
            return new CombatObj()
            {
                Type = Type,
                SlotNum = (byte)GetSlotNum(),
                Timer = Timer,
                Stack = (byte)Stack,
                IsActive = IsActive
            };
        }

        protected virtual int GetSlotNum()
        {
            return 0;
        }
    }
}
