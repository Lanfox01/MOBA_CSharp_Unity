using MOBA_CSharp_Server.Library.DataReader;
using MOBA_CSharp_Server.Library.ECS;
using System.Collections.Generic;
using System.Linq;

namespace MOBA_CSharp_Server.Game
{ //游戏单位（Unit）状态管理的部分 UnitStatus 类继承自 Effect 类，表示游戏单位的状态，如等级、经验等。
    // 看样子 应该是一个数据组件 
    public class UnitStatus : Effect
    {
        public int Level { get; private set; } //表示单位的等级。
        public float Exp { get; private set; } //表示单位当前的经验值。
        Dictionary<int, ExpTable> table; //一个字典，存储了不同等级对应的经验值表（ExpTable），以及该等级下的各项属性（如最大生命值、最大魔法值、攻击力等）。

        public UnitStatus(Unit unitRoot, Entity root) : base(CombatType.UnitStatus, unitRoot, root)
        {
            AddInheritedType(typeof(UnitStatus));

            Level = 0;
            Exp = 0;
            table = Root.GetChild<CSVReaderEntity>().GetExpTable(unitRoot.Type);

            SetLevel();
        }

        void SetLevel()
        {
            while(table.ContainsKey(Level + 1) && Exp >= table[Level + 1].Exp)
            {
                Exp -= table[Level + 1].Exp;
                Level++;

                SetFloatParam(FloatStatus.MaxHP, table[Level].MaxHP, true);
                SetFloatParam(FloatStatus.MaxMP, table[Level].MaxMP, true);

                SetFloatParam(FloatStatus.Attack, table[Level].Attack, true);
                SetFloatParam(FloatStatus.Defence, table[Level].Defence, true);
                SetFloatParam(FloatStatus.MagicAttack, table[Level].MagicAttack, true);
                SetFloatParam(FloatStatus.MagicDefence, table[Level].MagicDefence, true);

                SetFloatParam(FloatStatus.AttackRange, table[Level].AttackRange, true);
                SetFloatParam(FloatStatus.AttackRate, table[Level].AttackRate, true);
                SetFloatParam(FloatStatus.MovementSpeed, table[Level].MovementSpeed, true);
            }
        }
        // 接收一个浮点数 amount 作为参数，
        // 表示要增加的经验值，然后调用 SetLevel 方法更新等级。
        public void AddExp(float amount)
        {
            Exp += amount;
            SetLevel();
        }
        //查询 升级到下一个等级所需的经验值
        public float GetNextExp()
        {
            if(table.ContainsKey(Level + 1))
            {
                return table[Level + 1].Exp;
            }
            else
            {
                return Exp;
            }
        }
        //如果大于0，则取消死亡动画参数；如果为0或以下，则设置死亡动画参数。
        public override void Step(float deltaTime)
        {
            base.Step(deltaTime);

            if (unitRoot.HP > 0)
            {
                UnSetAnimationParam();
            }
            else
            {
                SetAnimationParam(AnimationType.Death, 1f, (int)AnimationStatusPriority.Death);
            }
        }
    }
}
