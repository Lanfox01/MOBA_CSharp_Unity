using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using MOBA_CSharp_Server.Library.DataReader;
using MOBA_CSharp_Server.Library.ECS;
using MOBA_CSharp_Server.Library.Physics;
/*
 ECS架构是Entity-Component-System（实体-组件-系统）的缩写，
 实体（Entity）  游戏对象  只是一个标识符  就是一个有编号的空壳；
 组件（Component）  逻辑的基本单位  每个组件几乎只实现一个对一的功能 如移动、攻击、技能等；组件可以被添加、删除和修改，也可以在不同的实体之间共享
 系统（System） 根据上面唯一复杂的可能是组件，所以是对组件进行管理和处理的模块 ；每个系统都会根据需要订阅一些组件，然后对这些组件进行处理；相当于部门负责人；
  难点应该在 system
 */
namespace MOBA_CSharp_Server.Game
{
    public class Character : Unit
    {
        public Character(Vector2 position, float rotation, float radius, UnitType type, Team team, float gold, Entity root) : base(position, 0, rotation, CollisionType.Dynamic, radius, type, team, gold, root)
        {
            AddInheritedType(typeof(Character));
            //根据 ecs 的理解， e 应该是实例 entity, 需要管理他的内存， 作为一个人载体； 下面附加很多的方法行为；
            AddChild(new Sight(false, this, Root));// 视力？ 看能力
            AddChild(new Eye(GetYAMLObject().GetData<float>("VisionRadius"), this, Root));//耳？

            AddChild(new Move(this, Root));//移动
            AddChild(new Attack(this, Root)); //攻击 
        }
    }
}
