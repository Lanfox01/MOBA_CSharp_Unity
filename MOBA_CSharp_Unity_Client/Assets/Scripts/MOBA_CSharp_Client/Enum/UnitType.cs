public enum UnitType
{
    //Building
    Fountain,       //喷泉//一种可以提供治疗或资源的结构
    Core,           //核心//需要保护的主要结构
    Tower,          //塔//攻击敌人的防御结构
    //Actor
    TowerBullet,   // //代表投射物或特效的演员类型
    FireBall,       // 塔弹，//由塔发射的弹丸
    FireBreath,   // 火球，//一种基于火的弹丸
    Meteor,       // 流星//一块来自天空的大石头，会造成伤害
    BigBang,       //  大爆炸//强大的爆炸效果
    EarthShatter,  // EarthShatter，//导致地面震动并造成伤害的攻击
    PoisonGas,        // 受压蒸汽，//可能造成区域损伤的蒸汽攻击
    PressurisedSteam,  // 代表不同敌人或盟友的角色类型
    //Character
    Minion,             //小兵//标准敌方单位
    Monster,            // 怪物//更强大的敌人
    SuperMonster,       // 超级怪物//一个更强大的怪物版本
    UltraMonster,
    //Character.Champion  // 是不是  各种 
    HatsuneMiku,        // 一个特定的冠军角色  
    HakureiReimu,       //  另一个独特的角色
    Yukikaze,           // 另一个独特的角色
    Serval,              // 另一个冠军角色   
    KizunaAI,       // //一个特定的冠军角色
}