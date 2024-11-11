using Microsoft.Xna.Framework;  // 引入 MonoGame 框架，主要用于处理矢量和图形
using System.Collections.Generic;  // 引入集合类型（如 List、Dictionary）
using VelcroPhysics.Dynamics; // 引入 VelcroPhysics 引擎的动力学模块
using VelcroPhysics.Factories; // 引入 VelcroPhysics 工厂类，用于创建物理物体
using VelcroPhysics.Shared; // 引入 VelcroPhysics 的共享模块

namespace MOBA_CSharp_Server.Library.Physics
{
    // VelcroPhysics 是一个基于 Box2D 的 2D 物理模拟库（Box2D 是一个流行的开源 2D 物理引擎）
    // 它提供了物理模拟的功能，如刚体、碰撞检测、碰撞响应、关节等。
    public enum CollisionType
    {
        Static, // 静态物体
        Dynamic, // 动态物体
        None // 无碰撞体
    }

    public class VPhysics
    {
        // 创建两个物理世界，一个用于碰撞检测，一个用于视野检测
        World collisionWorld = new World(new Vector2()); // 碰撞世界
        World visionWorld = new World(new Vector2()); // 视野世界
        // 存储单位物体的字典
        Dictionary<int, BodyWrapper> unitBodies = new Dictionary<int, BodyWrapper>();
        // 存储没有碰撞检测的单位物体的字典
        Dictionary<int, BodyWrapper> noCollisionUnitBodies = new Dictionary<int, BodyWrapper>();
        // 存储灌木丛物体的字典
        Dictionary<Body, Body> bushBodies = new Dictionary<Body, Body>();

        // 创建一条边界墙
        public void CreateEdgeWall(Vector2 start, Vector2 end)
        {
            Body body0 = BodyFactory.CreateEdge(collisionWorld, start, end); // 在碰撞世界中创建边界墙
            body0.BodyType = BodyType.Static; // 设置为静态物体

            Body body1 = BodyFactory.CreateEdge(visionWorld, start, end); // 在视野世界中创建边界墙
            body1.BodyType = BodyType.Static; // 设置为静态物体
        }

        // 创建一个圆形的墙
        public void CreateCircleWall(float radius, Vector2 position)
        {
            Body body0 = BodyFactory.CreateCircle(collisionWorld, radius, 1f, position, BodyType.Static); // 在碰撞世界中创建圆形墙
            Body body1 = BodyFactory.CreateCircle(visionWorld, radius, 1f, position, BodyType.Static); // 在视野世界中创建圆形墙
        }

        // 创建一个单位（可以是动态或静态）
        public void CreateUnit(int unitID, float radius, Vector2 position, CollisionType type)
        {
            if (type != CollisionType.None)
            {
                Body body = BodyFactory.CreateCircle(collisionWorld, radius, 1f, position, type == CollisionType.Dynamic ? BodyType.Dynamic : BodyType.Static, unitID); // 根据碰撞类型创建物体

                unitBodies.Add(unitID, new BodyWrapper(body)); // 将物体添加到字典中
            }
            else
            {
                NoCollisionBody noCollisionBody = new NoCollisionBody(position, radius); // 创建无碰撞物体
                BodyWrapper bodyWrapper = new BodyWrapper(noCollisionBody); // 包装无碰撞物体

                unitBodies.Add(unitID, bodyWrapper); // 添加到单位字典
                noCollisionUnitBodies.Add(unitID, bodyWrapper); // 添加到无碰撞单位字典
            }
        }

        // 移除单位
        public void RemoveUnit(int unitID)
        {
            if (unitBodies.ContainsKey(unitID))
            {
                unitBodies[unitID].RemoveBody(collisionWorld); // 从碰撞世界中移除物体
                unitBodies.Remove(unitID); // 从字典中移除
            }

            if (noCollisionUnitBodies.ContainsKey(unitID))
            {
                noCollisionUnitBodies.Remove(unitID); // 从无碰撞字典中移除
            }
        }

        // 设置单位的速度
        public void SetUnitVelocity(int unitID, Vector2 velocity)
        {
            if (unitBodies.ContainsKey(unitID))
            {
                unitBodies[unitID].SetUnitVelocity(velocity); // 设置单位速度
            }
        }

        // 设置单位的位置
        public void SetUnitPosition(int unitID, Vector2 position)
        {
            if (unitBodies.ContainsKey(unitID))
            {
                unitBodies[unitID].SetUnitPosition(position); // 设置单位位置
            }
        }

        // 更新物理世界状态
        public void Step(float deltaTime)
        {
            collisionWorld.Step(deltaTime); // 在碰撞世界中进行物理更新

            foreach (var body in unitBodies.Values)
            {
                body.ResetVelocity(); // 重置单位速度
            }
        }

        // 获取单位的位置
        public Vector2 GetPosition(int unitID)
        {
            return unitBodies[unitID].GetPosition(); // 返回单位的位置
        }

        // 获取单位周围的物体（例如，半径内的单位）
        public List<int> GetUnit(float radius, Vector2 position)
        {
            List<int> ret = new List<int>();

            AABB aabb = new AABB(position, radius * 2f, radius * 2f); // 创建一个包围盒
            var fixtures = collisionWorld.QueryAABB(ref aabb); // 查询包围盒内的物体

            foreach (var fixture in fixtures)
            {
                Body body = fixture.Body;
                if (body.UserData != null)
                {
                    Vector2 unitPosition = body.Position;
                    float unitRadius = fixture.Shape.Radius;

                    // 如果单位在指定半径内，则添加到列表中
                    if ((position - unitPosition).Length() <= (radius - unitRadius))
                    {
                        ret.Add((int)body.UserData); // 添加单位ID
                    }
                }
            }

            // 处理没有碰撞的单位
            foreach (var keyValue in noCollisionUnitBodies)
            {
                Vector2 unitPosition = keyValue.Value.NoCollisionBody.Position;
                float unitRadius = keyValue.Value.NoCollisionBody.Radius;

                if ((position - unitPosition).Length() <= (radius - unitRadius))
                {
                    ret.Add(keyValue.Key); // 添加单位ID
                }
            }

            return ret;
        }

        // 检查两单位之间的视线是否畅通
        public bool CheckLineOfSight(int unitID_0, int unitID_1)
        {
            Vector2 point0 = unitBodies[unitID_0].GetPosition();
            Vector2 point1 = unitBodies[unitID_1].GetPosition();

            if (point0 == point1)
            {
                return true; // 如果两个单位位置相同，视线畅通
            }
            else
            {
                return visionWorld.RayCast(point0, point1).Count == 0; // 在视野世界中进行射线检测
            }
        }

        // 创建一个灌木丛
        public void CreateBush(IEnumerable<Vector2> vertices)
        {
            Body body = BodyFactory.CreatePolygon(visionWorld, new Vertices(vertices), 1f); // 根据顶点创建多边形物体
            bushBodies.Add(body, body); // 将灌木物体添加到字典中
        }

        // 获取灌木物体
        public Body GetBushBody(Vector2 position)
        {
            AABB aabb = new AABB(position, 0.01f, 0.01f); // 创建一个非常小的包围盒
            var fixtures = visionWorld.QueryAABB(ref aabb); // 查询包围盒内的物体

            foreach (var fixture in fixtures)
            {
                Body body = fixture.Body;
                if (bushBodies.ContainsKey(body))
                {
                    return body; // 返回找到的灌木物体
                }
            }

            return null; // 如果没有找到，返回 null
        }
    }
}
