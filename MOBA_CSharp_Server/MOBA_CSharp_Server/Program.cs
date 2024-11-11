using MOBA_CSharp_Server.Game; // 可能包含了游戏逻辑相关的类和接口
using System;
using System.Diagnostics; //引入诊断命名空间，这里主要用于Stopwatch类，以测量代码执行时间
using System.Threading;//引入线程命名空间，用于控制线程的创建、管理和同步，这里主要用于Thread.Sleep方法。

namespace MOBA_CSharp_Server
{
    class Program
    {
        // 控制游戏逻辑的帧率， 通过平衡下一帧的耗时来消化上一帧的超时； 主要功能是均衡时间，保持帧率稳定
        static void Main(string[] args)
        {
            RootEntity root = new RootEntity();//实例root，这个类可能是一个游戏世界的根实体，用于管理和协调游戏中的所有实体。

            int frameRate = root.GetChild<DataReaderEntity>().GetYAMLObject(@"YAML\ServerConfig.yml").GetData<int>("FrameRate"); //每秒执行的帧数。
            int frameMilliseconds = 1000 / frameRate;//计算每帧的毫秒数

            Stopwatch stopwatch = new Stopwatch(); // 使用Stopwatch类来测量游戏逻辑执行的时间
            int overTime = 0;
            while (true)
            {
                stopwatch.Restart(); // 重启Stopwatch 重新计时？

                root.Step((frameMilliseconds + overTime) * 0.001f); // 执行一帧的游戏逻辑 考虑了上一帧可能遗留的超时时间

                stopwatch.Stop();
                int stepTime = (int)stopwatch.ElapsedMilliseconds; //停止Stopwatch，获取实际执行时间（stepTime）

                if (stepTime <= frameMilliseconds) //如果实际执行时间小于或等于每帧的毫秒数，则让当前线程休眠，
                                                   //直到达到每帧应有的时间长度，并将overTime重置为0。
                {
                    Thread.Sleep(frameMilliseconds - stepTime);
                    overTime = 0;
                }
                else
                {
                    overTime = stepTime - frameMilliseconds;//如果实际执行时间大于每帧的毫秒数，则计算超时时间，以便在下一次循环中补偿。
                }
            }
        }
    }
}
