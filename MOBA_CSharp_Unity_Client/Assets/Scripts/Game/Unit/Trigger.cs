using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Trigger : MonoBehaviour
{
    // Start is called before the first frame update
    // 每个场景都有，每次跳转场景的时候都来一次，相当于场景初始化入口
    void Start()
    {
        GameObject.Find("Network").GetComponent<Network>().TriggeredStart();
    }
}
