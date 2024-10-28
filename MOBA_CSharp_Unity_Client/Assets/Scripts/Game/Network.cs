using ENet;
using MessagePack;
using MessagePack.Resolvers;
using MOBA_CSharp_Client.ClientNetwork;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
// 网络管理器；
public class Network : MonoBehaviour
{
  // 定义场景状态的枚举
     enum SceneState
      {
          Title,  // 标题场景
          Lobby,  // 大厅场景
          Main    // 主场景
      }
      
      
    //State
    SceneState state;
    bool testMode;   // 测试模式开关

    //Reader   // YAML 和 CSV 读取器实例
    YAMLReader yamlReader = new YAMLReader();
    CSVReader csvReader = new CSVReader();

    //Network   // 网络客户端实例
    ClientNetwork client = new ClientNetwork();

    //Title  // 标题场景相关
    TMP_InputField hostInputField, portInputField;// 输入框，用于输入主机和端口
    GameObject successImage;      // 成功连接后的图像
    float successTimer;     // 成功连接计时器
    Image blackScreen;      // 黑色屏幕

    //Lobby&Main   // 大厅和主场景的聊天 UI
    ChatUI chatUI;

    //Lobby
    [SerializeField] Text lobbyTitle;  // 大厅标题文本
    [SerializeField] GameObject championSelectIconPrefab;   // 选手选择图标预制体
    [SerializeField] GameObject playerNodePrefab;  // 玩家节点预制体
    RectTransform blueContentRectTransform, redContentRectTransform;  // 蓝方和红方的内容区域
    List<GameObject> bluePlayerNodeInstances = new List<GameObject>(); // 蓝方玩家节点实例列表
    List<GameObject> redPlayerNodeInstances = new List<GameObject>();  // 红方玩家节点实例列表
    Image selectedChampionIconImage;   // 当前选择的英雄图标；就是代表不同的英雄，比如后裔或者项羽等
    InputField nameInputField;        // 玩家名字输入框
    Button setNameButton, teamButton, readyButton;  // 设置名字、队伍和准备按钮

    SelectObj playerSelectObj = null;   //// 本机玩家信息？ 选中的对象
    bool applyPlayerSelectObj = false;    //默认是false？  是否应用选中的对象
    UnitType type;  // 单位类型
    string _name;  // 玩家名字
    Team _team;  // 玩家队伍
    bool _ready;  // 是否准备好

    //Main
    UnitManager unitManager;  // 单位管理器
    
    public static Network Instance
    {
        get; private set;
    }

    void Awake()
    { 
        // 确保只存在一个实例，避免重复创建
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);// 在场景切换时不销毁此对象

        Init();
    }

    void Init()
    {  
        // 从YAML配置文件读取测试模式设置
        testMode = GetYAMLObject(@"YAML\ClientConfig.yml").GetData<bool>("TestMode"); //默认配置是false 是否想要尝试可以开启？
        Enum.TryParse(SceneManager.GetActiveScene().name, out state); // 获取当前场景状态
        // UI  // 从配置文件中设置UI和游戏参数
        MinimapUI.MapWidth = GetYAMLObject(@"YAML\ClientConfig.yml").GetData<float>("MapWidth");
        MinimapUI.MapHeight = GetYAMLObject(@"YAML\ClientConfig.yml").GetData<float>("MapHeight");
        Movement.SetFrameRate(GetYAMLObject(@"YAML\ClientConfig.yml").GetData<int>("FrameRate"));
        RTSCamera.IsScreenEdgeMovement = GetYAMLObject(@"YAML\ClientConfig.yml").GetData<bool>("IsScreenEdgeMovement");
        RecallUI.RecallTime = GetYAMLObject(CombatType.Recall).GetData<float>("RecallTime");
        // 设置网络消息处理程序  注册事件，或者订阅；
        client.SetMessageHandler(MessageType.Connect, ConnectHandler);
        client.SetMessageHandler(MessageType.Disconnect, DisconnectHandler);
        client.SetMessageHandler(MessageType.Timeout, TimeoutHandler);
        client.SetMessageHandler(MessageType.Lobby, LobbyHandler);
        client.SetMessageHandler(MessageType.Broadcast, BroadcastHandler);
        client.SetMessageHandler(MessageType.Snapshot, SnapshotHandler);
        // 根据测试模式连接到服务器
        if (testMode)
        {
            string host = GetYAMLObject(@"YAML\ClientConfig.yml").GetData<string>("Host");
            ushort port = (ushort)GetYAMLObject(@"YAML\ClientConfig.yml").GetData<int>("Port");

            client.Connect(host, port);// 连接到服务器
        }
        else
        {   // 如果不是在标题场景，则连接到服务器
            if (state != SceneState.Title)
            {
                string host = GetYAMLObject(@"YAML\ClientConfig.yml").GetData<string>("Host");
                ushort port = (ushort)GetYAMLObject(@"YAML\ClientConfig.yml").GetData<int>("Port");

                client.Connect(host, port);  // 连接到服务器
            }
        }
    }

    /// <summary>
    ///  这里根据每个场景都会初始化一次； 抓取更新 每个场景的内容设置，事件；
    /// </summary>
public void TriggeredStart()
{
    // 设置鼠标光标为默认状态
    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

    // 尝试解析当前场景的名称为 SceneState 枚举类型
    Enum.TryParse(SceneManager.GetActiveScene().name, out state);

    // 如果当前场景是标题场景   在大厅
    if (state == SceneState.Title)
    {
        // 查找并获取 "HostInputField" 和 "PortInputField" 的 TMP_InputField 组件  获取主机 ip 端口
        hostInputField = GameObject.Find("HostInputField").GetComponent<TMP_InputField>(); //  
        portInputField = GameObject.Find("PortInputField").GetComponent<TMP_InputField>();

        // 从 YAML 文件中获取主机和端口配置并设置输入框文本
        hostInputField.text = GetYAMLObject(@"YAML\ClientConfig.yml").GetData<string>("Host");
        portInputField.text = GetYAMLObject(@"YAML\ClientConfig.yml").GetData<int>("Port").ToString();

        // 为连接按钮添加点击事件监听器
        GameObject.Find("Button").GetComponent<Button>().onClick.AddListener(ConnectClick);
        // 查找成功图像
        successImage = GameObject.Find("Button").transform.Find("Background").Find("SuccessImage").gameObject;
        successTimer = 0; // 初始化成功计时器

        // 获取黑屏的 Image 组件
        blackScreen = GameObject.Find("BlackScreen").GetComponent<Image>();

        // 初始化玩家选择对象相关变量  
        applyPlayerSelectObj = false;
        playerSelectObj = null;
    }
    // 如果当前场景是大厅场景   应该是 匹配间
    else if (state == SceneState.Lobby)
    {
        // 获取图标根的 RectTransform
        RectTransform iconRoot = GameObject.Find("IconRoot").GetComponent<RectTransform>();
        int count = 0; // 初始化计数器
        Vector2 initPos = new Vector2(32f, -32f); // 初始位置

        // 遍历 UnitType 枚举的所有值
        foreach (UnitType type in Enum.GetValues(typeof(UnitType)))
        {
            // 如果类型是 HatsuneMiku 或更高
            if (type >= UnitType.HatsuneMiku)
            {
                // 计算图标的位置   一行8人？
                int x = count % 8; // 横向位置
                int y = count / 8; // 纵向位置

                // 实例化一个新的英雄选择图标
                GameObject instance = Instantiate(championSelectIconPrefab, iconRoot);
                // 设置图标的锚点和位置
                instance.GetComponent<RectTransform>().anchorMin = new Vector2(0, 1);
                instance.GetComponent<RectTransform>().anchorMax = new Vector2(0, 1);
                instance.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
                instance.GetComponent<RectTransform>().anchoredPosition = initPos + new Vector2(x * 100, y * -100);

                // 设置图标数据
                instance.GetComponent<ChampionSelectIcon>().SetData(type, this);

                count++; // 增加计数器   为什么一开始就有 5个人玩家？ 这里的表示一开始英雄池有5个英雄；
            }
        }

        // 查找并获取大厅相关的 UI 组件
        lobbyTitle = GameObject.Find("LobbyTitle").GetComponent<Text>();
        blueContentRectTransform = GameObject.Find("BlueContent").GetComponent<RectTransform>();
        redContentRectTransform = GameObject.Find("RedContent").GetComponent<RectTransform>();
        selectedChampionIconImage = GameObject.Find("SelectedChampionIconImage").GetComponent<Image>();
        nameInputField = GameObject.Find("NameInputField").GetComponent<InputField>();
        setNameButton = GameObject.Find("SetNameButton").GetComponent<Button>();
        
        // 为设置名称按钮添加点击事件监听器
        GameObject.Find("SetNameButton").GetComponent<Button>().onClick.AddListener(SetNameButtonPressed);
        
        teamButton = GameObject.Find("TeamButton").GetComponent<Button>();
        // 为队伍按钮添加点击事件监听器
        GameObject.Find("TeamButton").GetComponent<Button>().onClick.AddListener(SetTeamButtonPressed);
        
        readyButton = GameObject.Find("ReadyButton").GetComponent<Button>();
        // 为准备按钮添加点击事件监听器
        GameObject.Find("ReadyButton").GetComponent<Button>().onClick.AddListener(ReadyButtonPressed);
        
        // 获取聊天 UI 组件并设置网络客户端
        chatUI = GameObject.Find("ChatUI").GetComponent<ChatUI>();
        chatUI.SetClientNetwork(client);
        // 为发送按钮添加点击事件监听器    这个好像是 聊天发送信息？
        GameObject.Find("SendButton").GetComponent<Button>().onClick.AddListener(chatUI.Send);

        // 如果没有应用玩家选择对象且玩家选择对象不为空
        if (!applyPlayerSelectObj && playerSelectObj != null)
        {
            // 获取玩家选择对象的类型并设置图标
            type = playerSelectObj.Type;
            SetUnitTypeIcon(type);

            // 设置输入框文本为玩家名称
            _name = playerSelectObj.Name;
            nameInputField.text = playerSelectObj.Name;

            // 设置队伍按钮颜色
            _team = playerSelectObj.Team;
            SetTeamButtonColor(_team);

            // 设置准备按钮颜色
            _ready = playerSelectObj.Ready;
            SetReadyButtonColor(_ready);

            // 标记已应用玩家选择对象
            applyPlayerSelectObj = true;
            playerSelectObj = null; // 清空玩家选择对象
        }
    } 
    // 如果不是标题或大厅场景   这里应该是 对战场景；
    else
    {
        // 获取 UnitManager 和 ChatUI 组件，并设置网络客户端
        unitManager = GameObject.Find("UnitManager").GetComponent<UnitManager>();
        chatUI = GameObject.Find("ChatUI").GetComponent<ChatUI>();
        chatUI.SetClientNetwork(client); 

        // 重置玩家选择对象相关变量
        applyPlayerSelectObj = false;
        playerSelectObj = null;
    }
}
     // 连接服务器   
    void ConnectClick()
    {
        string host = hostInputField.text;
        ushort port = ushort.Parse(portInputField.text);

        client.Connect(host, port);
    }

    //Reader
    public YAMLObject GetYAMLObject(string path)
    {
        return yamlReader.GetYAMLObject(path);
    }

    public YAMLObject GetYAMLObject(UnitType type)
    {
        return yamlReader.GetYAMLObject(@"YAML\Units\" + type.ToString() + ".yml");
    }

    public YAMLObject GetYAMLObject(CombatType type)
    {
        return yamlReader.GetYAMLObject(@"YAML\Combats\" + type.ToString() + ".yml");
    }

    public ItemTable GetItemTable(CombatType type)
    {
        return csvReader.GetItemTable(type);
    }

    //Network
    void OnDestroy()
    {
        client.Shutdown();
    }
    // 连接上服务器，跳转到 匹配间 场景；
    void ConnectHandler(byte[] data)
    {
        if (testMode && state != SceneState.Main)
        {

            SceneManager.LoadScene("Main");
        }
        else if(state == SceneState.Title)
        {
            successImage.SetActive(true);
            successTimer = 1.0f;
            successImage.GetComponent<AudioSource>().Play();
        }
    }
   // 断开后 跳转到 开始界面 
    void DisconnectHandler(byte[] data)
    {
        client.Shutdown();
        SceneManager.LoadScene("Title");
    }
    // 超时处理
    void TimeoutHandler(byte[] data)
    {
        client.Shutdown();
        SceneManager.LoadScene("Title");
    }

    void Update()
    {
        if(successTimer > 0)
        {
            successTimer -= Time.deltaTime;
            if (successTimer <= 0)
            {
                SceneManager.LoadScene("Lobby");
            }
            if (successTimer <= 0.75f)
            {
                blackScreen.color = new Color(0, 0, 0, 0.75f - successTimer);
            }
        }

        client.Service();
    }

    public uint GetPeerID()
    {
        return client.GetPeerID();
    }

    void SnapshotHandler(byte[] data)
    {
        if (state != SceneState.Main)
        {
            SceneManager.LoadScene("Main");
        }
        else
        {
            SnapshotObj snapshotObj = MessagePackSerializer.Deserialize<SnapshotObj>(data);
            unitManager.SetSnapshot(snapshotObj);
        }
    }

    void BroadcastHandler(byte[] data)
    {
        if(state != SceneState.Title)
        {
            MsgObj msgObj = MessagePackSerializer.Deserialize<MsgObj>(data);
            if (chatUI != null)
            {
                chatUI.Log(msgObj.Team, msgObj.Msg);
            }
        }
    }

    public void Send(MessageType type, byte[] data, PacketFlags flags)
    {
        client.Send(type, data, flags);
    }
    ///解析大厅信息、更新UI状态以及处理玩家选择
    void LobbyHandler(byte[] data)
    {
        LobbyObj lobbyObj = MessagePackSerializer.Deserialize<LobbyObj>(data);

        if (!applyPlayerSelectObj && playerSelectObj == null)
        {
            playerSelectObj = lobbyObj.PeerSelectObj;
            if(state == SceneState.Lobby)
            {
                type = playerSelectObj.Type;
                SetUnitTypeIcon(type);

                _name = playerSelectObj.Name;
                nameInputField.text = playerSelectObj.Name;

                _team = playerSelectObj.Team;
                SetTeamButtonColor(_team);

                _ready = playerSelectObj.Ready;
                SetReadyButtonColor(_ready);

                applyPlayerSelectObj = true;
                playerSelectObj = null;
            }
        }

        if (state == SceneState.Lobby)
        {
            if (lobbyObj.State == 0)
            {
                lobbyTitle.text = "Before Battle";
            }
            else if (lobbyObj.State == 1)
            {
                lobbyTitle.text = lobbyObj.Timer.ToString("F0");
            }
            else
            {
                lobbyTitle.text = "In Battle";
            }

            bluePlayerNodeInstances.ForEach(x => Destroy(x));
            bluePlayerNodeInstances.Clear();
            redPlayerNodeInstances.ForEach(x => Destroy(x));
            redPlayerNodeInstances.Clear();

            foreach (SelectObj selectObj in lobbyObj.SelectObjs)
            {
                if (selectObj.Team == Team.Blue)
                {
                    GameObject instance = Instantiate(playerNodePrefab, blueContentRectTransform);
                    instance.GetComponent<PlayerNode>().SetData(selectObj.Type, selectObj.Team, selectObj.Name, selectObj.Ready);
                    bluePlayerNodeInstances.Add(instance);
                }
                else
                {
                    GameObject instance = Instantiate(playerNodePrefab, redContentRectTransform);
                    instance.GetComponent<PlayerNode>().SetData(selectObj.Type, selectObj.Team, selectObj.Name, selectObj.Ready);
                    redPlayerNodeInstances.Add(instance);
                }
            }
        }
        else if(state == SceneState.Main)
        {
            SceneManager.LoadScene("Lobby");
        }
    }

    public void Set()
    {   // 匹配大厅，主要是四个参数； 类型？名称，队伍，准备状态？
        SelectObj selectObj = new SelectObj()
        {
            Type = type,
            Name = nameInputField.text,
            Team = _team,
            Ready = _ready
        };

        Send(MessageType.Select, MessagePackSerializer.Serialize(selectObj), PacketFlags.Reliable);  //监控值变化，就发送网络通知；
    }

    public void SetUnitType(UnitType type)
    {
        this.type = type;

        SetUnitTypeIcon(type);

        Set();
    }
    // 设置 点击准备按钮，并且通知
    public void ReadyButtonPressed()
    {
        _ready = !_ready;

        SetReadyButtonColor(_ready);

        Set();

        if(_ready)
        {
            readyButton.GetComponent<AudioSource>().Play();
        }
    }
    // 设置 名字，并且通知
    public void SetNameButtonPressed()
    {
        if(nameInputField.text != "")
        {
            Set();
        }
    }
   // 监控 队伍变化设置值
    public void SetTeamButtonPressed()
    {
        if(_team == Team.Blue)
        {
            _team = Team.Red;
        }
        else
        {
            _team = Team.Blue;
        }

        SetTeamButtonColor(_team);

        Set();
    }

    void SetUnitTypeIcon(UnitType type)
    {
        selectedChampionIconImage.sprite = UnitTable.Instance.GetUnitModel(type).Icon; // 这是什么工具类？
    }

    void SetTeamButtonColor(Team team)
    {
        if(team == Team.Blue)
        {
            teamButton.transform.Find("Background").GetComponent<Image>().color = Color.blue;
            teamButton.transform.Find("Background").Find("Label").GetComponent<Text>().text = "Blue";
        }
        else
        {
            teamButton.transform.Find("Background").GetComponent<Image>().color = Color.red;
            teamButton.transform.Find("Background").Find("Label").GetComponent<Text>().text = "Red";
        }
    }

    void SetReadyButtonColor(bool ready)
    {
        if (!ready)
        {
            readyButton.transform.Find("Background").GetComponent<Image>().color = Color.green;
            readyButton.transform.Find("Background").Find("Label").GetComponent<Text>().text = "Ready";
        }
        else
        {
            readyButton.transform.Find("Background").GetComponent<Image>().color = Color.red;
            readyButton.transform.Find("Background").Find("Label").GetComponent<Text>().text = "Cancel";
        }
    }
}
