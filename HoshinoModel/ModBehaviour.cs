using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Duckov.Modding;
using FMOD;
using FMODUnity;
using Newtonsoft.Json;
using SodaCraft.Localizations;
using UnityEngine;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Random = System.Random;

namespace HoshinoModel
{
    [Serializable]
    public class HoshinoModelConfig
    {
        // 目标模型
        public Int32 TargetModel = 0;
        
        // 是否默认启用
        public bool DefaultEnable = true;

        // 是否启用语音
        public bool EnableVoice = true;

        //F1语音
        public bool EnableF1Voice;
        
        //语音音量
        public float Volume = 1.0f;

        // 切换按键
        public string SwitchKey = "F6";
        
        //受击播放语音概率
        public int HurtVoiceProbability = 6;
    }
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private string bundleName = "hoshino_sd";
        private string assetName = "SD_Hoshino";
        private string targetShaderName = "SodaCraft/SodaCharacter";

        private CharacterModel characterModel;
        private Movement movement;

        string[] pathsToHide =
        {
            "CustomFaceInstance/DuckBody",
            "CustomFaceInstance/Armature/Root/Pelvis/Thigh.L",
            "CustomFaceInstance/Armature/Root/Pelvis/Thigh.R",
            "CustomFaceInstance/Armature/Root/Pelvis/Spine.001/Spine.002/Spine.003/Spine.004/Head",
            "CustomFaceInstance/Armature/Root/Pelvis/Spine.001/Spine.002/Spine.003/Spine.004/UpperArm.L/Wings.L",
            "CustomFaceInstance/Armature/Root/Pelvis/Spine.001/Spine.002/Spine.003/Spine.004/UpperArm.R/Wings.R",
            "CustomFaceInstance/Armature/Root/Pelvis/Spine.001/Spine.002/Spine.003/Spine.004/ArmorSocket",
            "CustomFaceInstance/Armature/Root/Pelvis/Spine.001/Spine.002/Spine.003/Spine.004/BackpackSocket",
            "CustomFaceInstance/Armature/Root/Pelvis/TailSocket/"
        };

        private List<GameObject> hideGameObject = new List<GameObject>();

        private Animator unityHoshinoAnimator;
        private GameObject unityHoshinoInstance;

        private bool isSetModel;
        private bool isMoving;
        private bool subscribed;
        private bool meleesubscribed;
        private bool _HandSet;
        private static string _selfPath = "";
        public static Channel _hoshinoChannel;
        private static FMOD.Sound _enterBattleVoice1;
        private static FMOD.Sound _enterBattleVoice2;
        private static FMOD.Sound _hurtVoice1;
        private static FMOD.Sound _hurtVoice2;
        private static FMOD.Sound _hurtVoice3;
        private static FMOD.Sound _azy;
        private static FMOD.Sound _azyo;
        private static FMOD.Sound _s_hurtVoice1;
        private static FMOD.Sound _s_hurtVoice2;
        private static FMOD.Sound _s_hurtVoice3;
        private static FMOD.Sound _s_hurtVoice4;
        private static FMOD.Sound _s_hurtVoice5;
        private static FMOD.Sound _s_hurtVoice6;
        private static FMOD.Sound _s_enterBattleVoice1;
        private static FMOD.Sound _s_enterBattleVoice2;
        private static FMOD.Sound _s_enterBattleVoice3;
        private static FMOD.Sound _s_enterBattleVoice4;
        private static System.Random random = new System.Random();
        private InputAction newAction = new InputAction();
        
        HoshinoModelConfig config = new HoshinoModelConfig();
        public static string MOD_NAME = "HoshinoModel";
        private static string persistentConfigPath => Path.Combine(Application.streamingAssetsPath, "HoshinoModelConfig.txt");
        
        
        protected override void OnAfterSetup()
        {
            _selfPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            LoadVoiceResources();
        }
        
        private void OnEnable()
        {
            Health.OnHurt += OnCharacterHurt;
            ModManager.OnModActivated += OnModActivated;
            LevelManager.OnAfterLevelInitialized += OnSceneLoaded;
            //SceneManager.sceneLoaded += OnSceneLoaded;
            // 立即检查一次，防止 ModConfig 已经加载但事件错过了
            if (ModConfigAPI.IsAvailable())
            {
                ModLog.Log("HoshinoModel: ModConfig already available!");
                SetupModConfig();
                LoadConfigFromModConfig();
            }
            //F1语音
            if (config.EnableF1Voice && config.EnableVoice)
            {
                InputAction inputAction = GameManager.MainPlayerInput.actions.FindAction("Quack", false);
                inputAction.Disable();
                newAction.AddBinding(inputAction.controls[0]);
                newAction.performed += WhenQuack;
                newAction.Enable();
            }
        }

        private void Awake()
        {
            ModLog.Log($"[HoshinoModel]Awake.");
        }
        
        private void OnModActivated(ModInfo info, Duckov.Modding.ModBehaviour behaviour)
        {
            if (info.name == ModConfigAPI.ModConfigName)
            {
                ModLog.Log("HoshinoModel: ModConfig activated!");
                SetupModConfig();
                LoadConfigFromModConfig();
            }
        }

        private void SetupModConfig()
        {
            if (!ModConfigAPI.IsAvailable())
            {
                ModLog.Log("HoshinoModel: ModConfig not available");
                return;
            }

            ModLog.Log("准备添加ModConfig配置项");

            // 添加配置变更监听
            ModConfigAPI.SafeAddOnOptionsChangedDelegate(OnModConfigOptionsChanged);

            // 根据当前语言设置描述文字
            SystemLanguage[] chineseLanguages = {
                SystemLanguage.Chinese,
                SystemLanguage.ChineseSimplified,
                SystemLanguage.ChineseTraditional
            };

            bool isChinese = chineseLanguages.Contains(LocalizationManager.CurrentLanguage);

            // 添加配置项

            var formatOptions = new SortedDictionary<string, object>
            {
                { isChinese ? "星野" : "Hoshino(Original)", 0 },
                { isChinese ? "临战星野" : "Hoshino(Strategy)", 1 },
            };

            ModConfigAPI.SafeAddDropdownList(
                MOD_NAME,
                "TargetModel",
                isChinese ? "选择目标模型(切换场景或重启后生效)" : "Choose Target Model(Need restart)",
                formatOptions,
                typeof(int),
                config.TargetModel
            );
            
            ModConfigAPI.SafeAddBoolDropdownList(
                MOD_NAME,
                "EnableVoice",
                isChinese ? "启用语音(当前版本:0.9.0)" : "EnableVoice(Version:0.9.0)",
                config.EnableVoice
            );
            
            ModConfigAPI.SafeAddBoolDropdownList(
                MOD_NAME,
                "EnableF1Voice",
                isChinese ? "启用F1语音(重启生效)" : "EnableF1Voice(Need restart)",
                config.EnableF1Voice
            );
            
            ModConfigAPI.SafeAddInputWithSlider(
                MOD_NAME,
                "HurtVoiceProbability",
                isChinese ? "受击播放受击语音概率([整数值]分之一,不要使用滑条调整,不可为0及以下)" : "HurtVoiceProbability(1/[InputValue],do not use slider,Can't be 0 or less)",
                typeof(int),
                config.HurtVoiceProbability
            );
            
            ModConfigAPI.SafeAddInputWithSlider(
                MOD_NAME,
                "Volume",
                isChinese ? "语音音量" : "Volume",
                typeof(float),
                config.Volume,
                new Vector2(0f, 1f)
            );

            ModLog.Log("DisplayItemValue: ModConfig setup completed");
        }
        
        private void LoadConfigFromModConfig()
        {
            // 使用新的 LoadConfig 方法读取所有配置
            config.EnableVoice = ModConfigAPI.SafeLoad<bool>(MOD_NAME, "EnableVoice", config.EnableVoice);
            config.TargetModel = ModConfigAPI.SafeLoad<int>(MOD_NAME, "TargetModel", config.TargetModel);
            config.HurtVoiceProbability = ModConfigAPI.SafeLoad<int>(MOD_NAME, "HurtVoiceProbability", config.HurtVoiceProbability);
            config.Volume = ModConfigAPI.SafeLoad<float>(MOD_NAME, "Volume", config.Volume);
            config.EnableF1Voice = ModConfigAPI.SafeLoad<bool>(MOD_NAME, "EnableF1Voice", config.EnableF1Voice);
        }
        
        private void SaveConfig(HoshinoModelConfig config)
        {
            try
            {
                string json = JsonUtility.ToJson(config, true);
                File.WriteAllText(persistentConfigPath, json);
                ModLog.Log("[HoshinoModel]: Config saved");
            }
            catch (Exception e)
            {
                ModLog.Log($"[HoshinoModel]: Failed to save config: {e}");
            }
        }

        private void WhenQuack(InputAction.CallbackContext context)
        {
            _hoshinoChannel.setVolume(config.Volume);
            int num = random.Next(2);
            if (num == 0)
            { 
                PlaySound(_azy);
            }
            else if (num == 1)
            {
                PlaySound(_azyo);
            }
            AIMainBrain.MakeSound(new()
            {
                fromCharacter = characterModel.characterMainControl,
                fromObject = characterModel.characterMainControl.gameObject,
                pos = characterModel.characterMainControl.transform.position,
                fromTeam = characterModel.characterMainControl.Team,
                soundType = SoundTypes.unknowNoise,
                radius = 15f
            });
        }
        
        private async void OnSceneLoaded()
        {
            ModLog.Log($"[HoshinoModel]检测到载入场景,尝试加载模型");
            ModLog.Log($"[HoshinoModel]开始加载模型");
            await Task.Delay(100);
            if (unityHoshinoInstance != null)
            {
                DestroyImmediate(unityHoshinoInstance);
                unityHoshinoInstance = null;
                unityHoshinoAnimator = null;
                ModLog.Log($"[HoshinoModel]模型已销毁");
            }
            _HandSet = false;
            SetModel();
            subscribed = false;
            meleesubscribed = false;
            if (!LevelManager.Instance.IsBaseLevel)
            {
                if (!LevelManager.Instance.IsBaseLevel && LevelManager.Instance.IsRaidMap && config.EnableVoice)
                {
                    _hoshinoChannel.setVolume(config.Volume);
                    if (config.TargetModel == 0)
                    {
                        int num = random.Next(2);
                        if (num == 0)
                        {
                            PlaySound(_enterBattleVoice1);
                        }
                        else if (num == 1)
                        {
                            PlaySound(_enterBattleVoice2);
                        }
                    }
                    else if (config.TargetModel == 1)
                    {
                        int num = random.Next(4);
                        if (num == 0)
                        {
                            PlaySound(_s_enterBattleVoice1);
                        }
                        else if (num == 1)
                        {
                            PlaySound(_s_enterBattleVoice2);
                        }
                        else if (num == 2)
                        {
                            PlaySound(_s_enterBattleVoice3);
                        }
                        else if (num == 3)
                        {
                            PlaySound(_s_enterBattleVoice4);
                        }
                    }
                }
            }
            else
            {
                SetModel();
            }
        }
        
        public void ForceRefreshWeapon(CharacterMainControl playerControl)
        {
            if (playerControl == null) return;
            var currentAgent = playerControl.CurrentHoldItemAgent;
            if (currentAgent != null && currentAgent.Item != null)
            {
                try
                {
                    var item = currentAgent.Item;
                    // 通过快速切换空手来强制触发游戏的装备逻辑，使武器挂载到新的 socket 上
                    playerControl.ChangeHoldItem(null);
                    playerControl.ChangeHoldItem(item);
                    ModLog.Log($"SocketHandler: 已强制刷新武器 [{item.name}] 模型位置。");
                }
                catch (Exception e)
                {
                    ModLog.Error($"SocketHandler: 强制刷新武器时出错：{e.Message}");
                }
            }
        }
        
        private void OnCharacterHurt(Health health,DamageInfo damageInfo)
        {
            int num = random.Next(config.HurtVoiceProbability);
            if (config.EnableVoice && num == 0 && health.team == Teams.player && health.IsMainCharacterHealth == true)
            {
                if (config.TargetModel == 0)
                {
                    _hoshinoChannel.setVolume(config.Volume);
                    int num2 = random.Next(3);
                    if (num2 == 0)
                    {
                        PlaySound(_hurtVoice1);
                    }
                    else if (num2 == 1)
                    {
                        PlaySound(_hurtVoice2);
                    }
                    else if (num2 == 2)
                    {
                        PlaySound(_hurtVoice3);
                    }
                }
                else if (config.TargetModel == 1)
                {
                    _hoshinoChannel.setVolume(config.Volume);
                    int num2 = random.Next(6);
                    if (num2 == 0)
                    {
                        PlaySound(_s_hurtVoice1);
                    }
                    else if (num2 == 1)
                    {
                        PlaySound(_s_hurtVoice2);
                    }
                    else if (num2 == 2)
                    {
                        PlaySound(_s_hurtVoice3);
                    }
                    else if (num2 == 3)
                    {
                        PlaySound(_s_hurtVoice4);
                    }
                    else if (num2 == 4)
                    {
                        PlaySound(_s_hurtVoice5);
                    }
                    else if (num2 == 5)
                    {
                        PlaySound(_s_hurtVoice6);
                    }
                }
            }
        }
        
        
        public static void LoadVoiceResources()
        {
            UnityEngine.Debug.Log( "[HoshinoModel]Start load audio resources.");
            LoadSound("Origin", "EnterBattle1", ".ogg", out _enterBattleVoice1);
            LoadSound("Origin", "EnterBattle2", ".ogg", out _enterBattleVoice2);
            LoadSound("Origin", "Hurt1", ".ogg", out _hurtVoice1);
            LoadSound("Origin", "Hurt2", ".ogg", out _hurtVoice2);
            LoadSound("Origin", "Hurt3", ".ogg", out _hurtVoice3);
            LoadSound("Origin", "Azy", ".mp3", out _azy);
            LoadSound("Origin", "Azyo", ".mp3", out _azyo);
            LoadSound("Strategy","CH0258_Battle_Damage_1", ".ogg", out _s_hurtVoice1);
            LoadSound("Strategy","CH0258_Battle_Damage_2", ".ogg", out _s_hurtVoice2);
            LoadSound("Strategy","CH0258_Battle_Damage_3", ".ogg", out _s_hurtVoice3);
            LoadSound("Strategy","CH0258_S2_Battle_Damage_1", ".ogg", out _s_hurtVoice4);
            LoadSound("Strategy","CH0258_S2_Battle_Damage_2", ".ogg", out _s_hurtVoice5);
            LoadSound("Strategy","CH0258_S2_Battle_Damage_3", ".ogg", out _s_hurtVoice6);
            LoadSound("Strategy","CH0258_Battle_In_1", ".ogg", out _s_enterBattleVoice1);
            LoadSound("Strategy","CH0258_Battle_In_2", ".ogg", out _s_enterBattleVoice2);
            LoadSound("Strategy","CH0258_S2_Battle_In_1", ".ogg", out _s_enterBattleVoice3);
            LoadSound("Strategy","CH0258_S2_Battle_In_2", ".ogg", out _s_enterBattleVoice4);
            UnityEngine.Debug.Log( $"[HoshinoModel]Try load Voice resources.");
            UnityEngine.Debug.Log("[HoshinoModel]Load All Resources Complete.");
        }

        private static void LoadSound(string character, string name,string format, out FMOD.Sound sound)
        {
            var path = Path.Combine(_selfPath, "Voice",$"{character}", $"{name}{format}");
            FMODUnity.RuntimeManager.CoreSystem.createSound(path, MODE.LOOP_OFF, out FMOD.Sound loadedSound);
            sound = loadedSound;
        }

        private void PlaySound(Sound soundName)
        {
            FMOD.Sound sound = soundName;
            RuntimeManager.CoreSystem.playSound(sound, new ChannelGroup(), false, out _hoshinoChannel);
            _hoshinoChannel.setVolume(config.Volume);
        }
        
        
        private KeyCode GetKeyCodeFromConfig()
        {
            string keyName;
            if (config.SwitchKey != null)
            { 
                keyName = config.SwitchKey;
            }
            else
            { 
                keyName = "F6"; 
            }
            return (KeyCode)System.Enum.Parse(typeof(KeyCode), keyName, true);
        }
        
        
        private void Update()
        {
            //切换功能
            
            if (characterModel == null || characterModel.characterMainControl == null)
            {
                return;
            }
            if (Input.GetKeyDown(GetKeyCodeFromConfig()))
            {
                ModLog.Log($"[HoshinoModel]检测到切换按键,开始切换");
            }
            if (isSetModel)
            {
                UpdateAnimation();
            }
            
            // 持续更新 isMoving 状态
            if (movement != null)
            {
                isMoving = movement.Moving;
            }
            
            //订阅开火状态
            if (characterModel != null && characterModel.characterMainControl != null && !subscribed)
            {
                try
                {
                    characterModel.characterMainControl.OnTriggerInputUpdateEvent += HandleTriggerInput;
                    ModLog.Log($"[HoshinoModel]Subscribed to OnTriggerInputUpdateEvent.");
                    subscribed = true;
                }
                catch (Exception e)
                {
                    ModLog.Warning($"[HoshinoModel]Error subscribing to event: {e.Message}");
                }
            }
            
            //订阅近战状态
            if (characterModel != null && characterModel.characterMainControl != null && !meleesubscribed)
            {
                try
                {
                    characterModel.characterMainControl.OnAttackEvent += WhenAttack;
                    ModLog.Log($"[HoshinoModel]Subscribed to OnAttackEvent.");
                    meleesubscribed = true;
                }
                catch (Exception e)
                {
                    ModLog.Warning($"[HoshinoModel]Error subscribing to event: {e.Message}");
                }
            }
            
            //检查是否持近战武器
            if (characterModel != null && characterModel.characterMainControl != null)
            {
                var _meleeWeapon = characterModel.characterMainControl.GetMeleeWeapon();
                if (unityHoshinoAnimator != null)
                {
                    if (_meleeWeapon != null)
                    {
                        unityHoshinoAnimator.SetBool("HoldingMelee", true);
                    }
                    else
                    {
                        unityHoshinoAnimator.SetBool("HoldingMelee", false);
                    }
                }
            }
        }
        
        
        private void ToggleModel()
        {
            isSetModel = !isSetModel;

            if (isSetModel)
            {
                ModLog.Log($"{config.SwitchKey} pressed - Loading UnityHoshino...");
                SetModel();
            }
            else
            {
                ModLog.Log($"{config.SwitchKey} pressed - Restoring original model...");
                RestoreModel();
            }
        }
        

        private void SetModel()
        {
            var levelManager = LevelManager.Instance;
            if (levelManager == null)
            {
                ModLog.Error("[HoshinoModel]LevelManager instance is null");
                return;
            }
            if (levelManager.MainCharacter == null)
            {
                ModLog.Error("[HoshinoModel]MainCharacter is null");
                return;
            }
            characterModel = levelManager.MainCharacter.characterModel;
            if (characterModel == null)
            {
                ModLog.Error("[HoshinoModel]CharacterModel is null");
                return;
            }
            if (characterModel.characterMainControl == null)
            {
                ModLog.Error("[HoshinoModel]CharacterMainControl is null");
                return;
            }
            movement = characterModel.characterMainControl.movementControl;
            if (movement == null)
            {
                ModLog.Error("[HoshinoModel]Movement control is null");
                return;
            }
            HideDuck();
            StartCoroutine(LoadUnityHoshinoCoroutine());
            isSetModel = true;
            ModLog.Log($"[HoshinoModel]模型加载完成.");
        }

        private void RestoreModel()
        {
            RestoreDuck();
            UnloadUnityHoshino();
            RestoreHoshinoRighthandSocket();
            ModLog.Log($"[HoshinoModel] 模型已恢复默认.");
        }

        private void HideDuck()
        {
            Transform rootTransform = characterModel.transform;
            hideGameObject.Clear();

            foreach (string path in pathsToHide)
            {
                Transform target = rootTransform.Find(path);

                if (target != null)
                {
                    hideGameObject.Add(target.gameObject);
                    target.gameObject.SetActive(false);
                }
            }
        }

        private void RestoreDuck()
        {
            foreach (var one in hideGameObject)
            {
                if (one != null)
                {
                    one.SetActive(true);
                    ModLog.Log($"[HoshinoModel]已恢复 Duck.");
                }
            }
            hideGameObject.Clear();
        }

        private void UnloadUnityHoshino()
        {
            if (unityHoshinoInstance != null)
            {
                Destroy(unityHoshinoInstance);
                unityHoshinoInstance = null;
                ModLog.Log($"[HoshinoModel]已卸载 UnityHoshinoInstance.");
            }
            unityHoshinoAnimator = null;
            ModLog.Log($"[HoshinoModel]已卸载 UnityHoshino.");
        }
        
        
        private void UpdateAnimation()
        {
            if (unityHoshinoAnimator == null) return;
            float currentSpeed = movement.GetMoveAnimationValue();
            
            //检测是否移动
            unityHoshinoAnimator.SetBool("Moving", isMoving);
            
            //检测当前速度
            unityHoshinoAnimator.SetFloat("Speed",currentSpeed,0.2f,Time.deltaTime);
            
            //检测是否翻滚
            unityHoshinoAnimator.SetBool("Dash", characterModel.characterMainControl.Dashing);
            
            //检测是否瞄准
            unityHoshinoAnimator.SetBool("Aim", characterModel.characterMainControl.IsInAdsInput);
        }
        
        private void HandleTriggerInput(bool isTriggerHeld, bool isTriggerPressed, bool isTriggerReleased)
        {
            if (unityHoshinoAnimator != null)
            {
                if (isTriggerHeld)
                {
                    unityHoshinoAnimator.SetBool("Shoot", true);
                }
                else
                {
                    unityHoshinoAnimator.SetBool("Shoot", false);
                }
            }
        }
        
        private void WhenAttack(DuckovItemAgent agent)
        {
            if (unityHoshinoAnimator != null)
            {
                unityHoshinoAnimator.SetTrigger("Attack");
                ModLog.Log($"[HoshinoModel]Melee attack triggered.");
            }
            else
            {
                ModLog.Warning($"[HoshinoModel]Animator is null, cannot trigger attack animation.");
            }
        }
        
        private IEnumerator LoadUnityHoshinoCoroutine()
        {
            if (config.TargetModel == 0)
            {
                bundleName = "hoshino_sd";
                assetName = "SD_Hoshino";
            }
            else if (config.TargetModel == 1)
            {
                bundleName = "strategy_hoshino";
                assetName = "Strategy_Hoshino";
            }
            
            string bundlePath = Path.Combine(info.path, bundleName);

            if (!File.Exists(bundlePath))
            {
                ModLog.Error($"AssetBundle not found: {bundlePath}");
                yield break;
            }

            var bundleRequest = AssetBundle.LoadFromFileAsync(bundlePath);
            yield return bundleRequest;

            AssetBundle bundle = bundleRequest.assetBundle;
            if (bundle == null)
            {
                ModLog.Error($"Failed to load AssetBundle: {bundleName}");
                yield break;
            }

            var assetRequest = bundle.LoadAssetAsync<GameObject>(assetName);
            yield return assetRequest;

            if (assetRequest.asset is GameObject unityHoshino && characterModel != null)
            {
                InitializeCharacter(unityHoshino);
                GetHoshinoRighthandSocket();
                ForceRefreshWeapon(characterModel.characterMainControl);
            }
            else
            {
                ModLog.Error("Failed to instantiate: model or character is null");
            }
            bundle.Unload(false);
        }

        
        
        private void InitializeCharacter(GameObject unityHoshino)
        {
            ReplaceAllShaders(unityHoshino, targetShaderName);
            unityHoshino.layer = LayerMask.NameToLayer("Default");
            unityHoshinoInstance = Instantiate(unityHoshino, characterModel.transform);
            unityHoshinoInstance.transform.localScale = Vector3.one * 1.3f;
            unityHoshinoInstance.transform.position += Vector3.forward * 0.1f;
            unityHoshinoAnimator = unityHoshinoInstance.GetComponent<Animator>();
        }

        private void GetHoshinoRighthandSocket()
        {
            if (unityHoshinoInstance == null)
            {
                ModLog.Error("[HoshinoModel]UnityHoshinoInstance为空,无法设置挂点");
                return;
            }

            Transform unityHoshinoRightHand = null;
            if (config.TargetModel == 0)
            { 
                unityHoshinoRightHand = unityHoshinoInstance.transform.Find("bone_root/Bip001/Bip001 Pelvis/Bip001 Spine/Bip001 Spine1/Bip001 R Clavicle/Bip001 R UpperArm/Bip001 R Forearm/Bip001 R Hand/Hand_Anchor");
            }
            else if (config.TargetModel == 1)
            {
                unityHoshinoRightHand = unityHoshinoInstance.transform.Find("bone_root/Bip001/Bip001 Pelvis/Bip001 Spine/Bip001 Spine1/Bip001 R Clavicle/Bip001 R UpperArm/Bip001 R Forearm/Bip001 R Hand/RightHand_Anchor");
            }
            if (unityHoshinoRightHand != null && !_HandSet)
            {
                if (characterModel == null)
                {
                    ModLog.Error("[HoshinoModel]CharacterModel为空,无法设置挂点");
                    return;
                }
                var field = typeof(CharacterModel).GetField("rightHandSocket", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(characterModel, unityHoshinoRightHand);
                    _HandSet = true;
                    ModLog.Log("[HoshinoModel]已设置右手挂点");
                }
                else
                {
                    ModLog.Error("[HoshinoModel]无法在CharacterModel中找到rightHandSocket字段");
                }
            }
            else
            {
                ModLog.Error("[HoshinoModel]在UnityHoshino模型中找不到右手骨骼或者已设置");
            }
        }
        
        private void RestoreHoshinoRighthandSocket()
        {
            if (characterModel == null)
            {
                ModLog.Error("[HoshinoModel]原始实例为空，无法获取右手挂点");
                return;
            }
            Transform unityOriginalRightHand = characterModel.transform.Find("CustomFaceInstance/Armature/Root/Pelvis/Spine.001/Spine.002/Spine.003/Spine.004/UpperArm.R/Elbow.R/ForeArm.R/Hand.R/Hand.Soket.R/RightHandSocket");
            if (unityOriginalRightHand != null && _HandSet)
            {
                var field = typeof(CharacterModel).GetField("rightHandSocket", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(characterModel, unityOriginalRightHand);
                    _HandSet = false;
                    ModLog.Log("[HoshinoModel]已重置右手挂点");
                }
                else
                {
                    ModLog.Error("[HoshinoModel]无法在角色模型中找到rightHandSocket字段");
                }
            }
            else
            {
                ModLog.Error("[HoshinoModel]在主模型中找不到右手骨骼或者未设置");
            }
        }
        
        private void ReplaceAllShaders(GameObject target, string shaderName)
        {
            Shader targetShader = Shader.Find(shaderName);
            if (targetShader == null)
            {
                ModLog.Error($"Shader not found: {shaderName}");
                return;
            }

            ProcessRenderersRecursive(target, targetShader);
        }

        private void ProcessRenderersRecursive(GameObject obj, Shader targetShader)
        {
            if (obj.TryGetComponent<Renderer>(out var renderer))
            {
                ReplaceRendererShaders(renderer, targetShader);
            }

            foreach (Transform child in obj.transform)
            {
                ProcessRenderersRecursive(child.gameObject, targetShader);
            }
        }

        private void ReplaceRendererShaders(Renderer renderer, Shader targetShader)
        {
            foreach (var material in renderer.materials)
            {
                if (material != null)
                {
                    material.shader = targetShader;
                }
            }
        }
        
        private void OnModConfigOptionsChanged(string key)
        {
            if (!key.StartsWith(MOD_NAME + "_"))
                return;

            // 使用新的 LoadConfig 方法读取配置
            LoadConfigFromModConfig();

            // 保存到本地配置文件
            SaveConfig(config);
            
            ModLog.Log($"DisplayItemValue: ModConfig updated - {key}");
        }
        
        private void OnDestroy()
        {
            ModLog.Log("OnDestroy!");
        }
        void OnDisable()
        {
            ModManager.OnModActivated -= OnModActivated;
            LevelManager.OnAfterLevelInitialized -= OnSceneLoaded;
            //SceneManager.sceneLoaded -= OnSceneLoaded;
            characterModel.characterMainControl.OnAttackEvent -= WhenAttack;
            newAction.performed -= WhenQuack;
            ModConfigAPI.SafeRemoveOnOptionsChangedDelegate(OnModConfigOptionsChanged);
        }
    }
}