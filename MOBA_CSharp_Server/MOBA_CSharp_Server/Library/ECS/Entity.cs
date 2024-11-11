using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MOBA_CSharp_Server.Library.ECS
{
    public class Entity
    {
        public List<Type> InheritedTypes { get; private set; } = new List<Type>();  // 存储每个实体继承的类型列表  记录继承关系； 从基类到派生顶  
        public Entity Root { get; private set; }                                    // 存储实体的根实体  就是他的父对象是谁？
        public bool Destroyed { get; set; }                                         // 标记实体是否已被销毁  标记类

        //children                                                                 //  子实体字典，按类型分组 算是一种抽象的管理
        protected Dictionary<Type, List<Entity>> children = new Dictionary<Type, List<Entity>>(); // 给主干根节点和几个次级节点用；
        protected bool isDuringStep;                                               // 标记是否正在执行步骤（如更新）
        protected List<Entity> addEntities = new List<Entity>();                   // 临时存储待添加的实体       
        protected List<Entity> removeEntities = new List<Entity>();                // 临时存储待移除的实体   
        
        // 实体构造函数，接收根实体作为参数  
        public Entity(Entity root)
        {
            AddInheritedType(typeof(Entity));

            Root = root;
            Destroyed = false;

            isDuringStep = false;
        }

        protected void AddInheritedType(Type type)  // 似乎 一个 root 有调用两次这里， InheritedTypes count =2
        {
            InheritedTypes.Add(type);
        }

        public void Destroy()
        {
            List<Entity> tempRemoveEntities = new List<Entity>();
            foreach (Entity entity in GetChildren<Entity>())// 遍历并销毁所有子实体  应该是遍历 某种类型 节点下的 所有子节点？
            {
                entity.Destroy();

                if (entity.Destroyed)
                {
                    entity.ClearReference();
                    tempRemoveEntities.Add(entity);
                }
            }
            tempRemoveEntities.ForEach(x => RemoveChild(x));// 从子实体列表中移除已销毁的实体  
        }
        // 清理实体的引用（虚方法，可在子类中重写）  
        public virtual void ClearReference()
        {
            foreach (Entity entity in GetChildren<Entity>()) // 递归清理所有子实体的引用  
            {
                entity.ClearReference();
            }
        }
        // 添加子实体（考虑是否正在执行步骤）  
        public virtual void AddChild(Entity entity)
        {
            if(isDuringStep)
            {
                addEntities.Add(entity);
            }
            else
            {// 根据子实体的继承类型将其添加到对应的列表中  
                foreach (Type type in entity.InheritedTypes)
                {
                    if (!children.ContainsKey(type)) // 根据这个类实例的继承树，遍历； 他可能被 children 加到很多地方；在children的很多子项中有他；
                    {
                        children.Add(type, new List<Entity>());
                    }
                    children[type].Add(entity);
                }
            }
        }
        // 移除子实体（考虑是否正在执行步骤）  
        public virtual void RemoveChild(Entity entity)
        {
            if(isDuringStep)
            {
                removeEntities.Add(entity);
            }
            else
            {    // 从对应的列表中移除子实体，如果列表为空则移除该类型  
                foreach (Type type in entity.InheritedTypes)
                {
                    children[type].Remove(entity);
                    if (children[type].Count == 0)
                    {
                        children.Remove(type);
                    }
                }
            }
        }
        // 执行步骤（如更新），处理添加和移除的子实体  等价于 一帧？
        public virtual void Step(float deltaTime)
        {
            isDuringStep = true;
            // 递归执行所有子实体的步骤  
            foreach (Entity entity in GetChildren<Entity>())
            {
                entity.Step(deltaTime);
            }

            isDuringStep = false;
            // 处理待添加的实体  
            foreach (Entity addEntity in addEntities)
            {
                AddChild(addEntity);
            }
            addEntities.Clear();
            // 处理待移除的实体  
            foreach (Entity removeEntity in removeEntities)
            {
                RemoveChild(removeEntity);
            }
            removeEntities.Clear();
        }
        // 获取第一个指定类型的子实体  
        public T GetChild<T>() where T : Entity
        {
            if(children.ContainsKey(typeof(T)))
            {
                return (T)children[typeof(T)].First();
            }
            else
            {
                return null;
            }
        }
        // 获取所有指定类型的子实体数组  
        public T[] GetChildren<T>() where T : Entity
        {
            if (!children.ContainsKey(typeof(T)))
            {
                return new T[0];
            }
            // 将子实体列表转换为指定类型的数组  
            return children[typeof(T)].Cast<T>().ToArray();
        }
    }
}
