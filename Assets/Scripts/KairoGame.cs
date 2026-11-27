using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// くるくる かいろくん の進行役。
// ステージのオブジェクトはすべてシーンの中に実物として配置されている前提で、
// ここでは生成も初期配置もせず、シーンの実物を名前で束ねて動かすだけにする。
public sealed partial class KairoGame : MonoBehaviour
{
    private enum Phase { Title, Carrying, Spinning, Finished }

    [SerializeField] private ControllerInput controllerInput;

    private const string StageRootName = "Stage";
    private Transform hara;
    private Animator haraAnimator;
    private Transform ball;
    private Transform[] screws;
    private Vector3[] screwBasePositions;
    private Transform[] doors;
    private Vector3[] doorBasePositions;
    private Transform[] docks;
    private System.Collections.Generic.List<Transform>[] circuitLegs;
    private readonly bool[] nodeCompleted = new bool[GameTuning.SpinNodeCount];
    private bool goalOpen;
    private Renderer ballRenderer;
    private Material ballMaterial;
    private TextMeshProUGUI instruction;
    private TextMeshProUGUI status;
    private Image chargeBar;
    private Image sleepBar;
    private TextMeshProUGUI sleepText;
    private Image spinBar;
    private GameObject spinBarRoot;
    private Transform cameraTransform;
    private Transform currentMarker;
    private Transform[] roughnessMarkers;
    private Transform sliderBlock;
    private float gameStartTime;
    private AudioSource audioSource;
    private AudioClip spinTone;
    private AudioClip successTone;
    private Phase phase = Phase.Title;
    private int nodeIndex;
    private int spinCommandIndex;
    private float spinCommandTimer;
    private float progress;
    private float charge;
    private float sleepDepth;
    private float awakeTimer;
    private bool sleepEnabled;
    private bool awake;
    private float shakeIntensity;
    private float flashUntil;
    private float lastPitchUpdate;


    private void Start()
    {
        if (!BindScene())
        {
            Debug.LogError("シーンに Stage のオブジェクトが配置されていません。KairoGame は無効になります。");
            enabled = false;
            return;
        }
        SetupAudio();
        if (Camera.main != null) cameraTransform = Camera.main.transform;
        SetInstruction("傾けるか、振るか、キー入力でスタート");
        UpdateBallChargeVisual();
    }

    // シーンに配置済みの実物を名前で探して束ねる。欠けがあれば false。
    private bool BindScene()
    {
        GameObject stage = GameObject.Find(StageRootName);
        if (stage == null) { Debug.LogError("束ね失敗: Stage が見つかりません"); return false; }
        Transform root = stage.transform;

        hara = FindChild(root, "原ゼミちゃん");
        if (hara != null) haraAnimator = hara.GetComponent<Animator>();
        ball = FindChild(root, "かいろくん");
        if (hara == null || ball == null) { Debug.LogError("束ね失敗: 原ゼミちゃん / かいろくん が見つかりません"); return false; }

        screws = new Transform[GameTuning.SpinNodeCount];
        screwBasePositions = new Vector3[GameTuning.SpinNodeCount];
        doors = new Transform[GameTuning.SpinNodeCount];
        doorBasePositions = new Vector3[GameTuning.SpinNodeCount];
        docks = new Transform[GameTuning.SpinNodeCount];
        for (int i = 0; i < screws.Length; i++)
        {
            screws[i] = FindChild(root, "Spin screw " + (i + 1));
            doors[i] = FindChild(root, "Door " + (i + 1));
            docks[i] = FindChild(root, "Ball dock " + (i + 1));
            if (screws[i] == null || doors[i] == null || docks[i] == null) { Debug.LogError($"束ね失敗: セット {i + 1} のネジ・ドア・ドックが見つかりません"); return false; }
            screwBasePositions[i] = screws[i].position;
            doorBasePositions[i] = doors[i].position;
        }

        circuitLegs = new System.Collections.Generic.List<Transform>[GameTuning.SpinNodeCount];
        for (int i = 0; i < circuitLegs.Length; i++)
        {
            circuitLegs[i] = new System.Collections.Generic.List<Transform>();
            for (int k = 1; ; k++)
            {
                Transform part = FindChild(root, $"Circuit {i + 1} part {k}");
                if (part == null) break;
                circuitLegs[i].Add(part);
            }
            if (circuitLegs[i].Count == 0) { Debug.LogError($"束ね失敗: Circuit {i + 1} が見つかりません"); return false; }
        }

        currentMarker = FindChild(root, "Current marker");
        sleepText = FindChild(root, "SleepText")?.GetComponent<TextMeshProUGUI>();
        if (sleepText == null)
            sleepText = GameObject.Find("SleepText")?.GetComponent<TextMeshProUGUI>();
        sliderBlock = FindChild(root, "Slider block");
        roughnessMarkers = new Transform[GameTuning.RoughnessAreas.Length];
        for (int i = 0; i < roughnessMarkers.Length; i++)
            roughnessMarkers[i] = FindChild(root, "Roughness marker " + (i + 1));
        LayoutRoughnessMarkers();
        ballRenderer = ball.GetComponentInChildren<Renderer>();
        if (ballRenderer != null)
        {
            ballMaterial = ballRenderer.material;
            ballMaterial.EnableKeyword("_EMISSION");
        }

        if (!BindHud()) { Debug.LogError("束ね失敗: HUD のラベルが見つかりません"); return false; }
        if (controllerInput == null) controllerInput = GetComponent<ControllerInput>();
        if (controllerInput == null) Debug.LogError("束ね失敗: ControllerInput がありません");
        return controllerInput != null;
    }

    private static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name) return child;
        }
        return null;
    }

    // でこぼこ区間の予告表示の受け口。シーンに "Roughness marker N" があれば、
    // 対応する区間の中心と半幅に合わせて置き直す。マーカーが無くても動く (null 許容)。
    // マーカーは X・Z が 1 単位の平らな物として置く想定で、半幅の 2 倍を大きさにする。高さはシーン側の置き方を残す。
    private void LayoutRoughnessMarkers()
    {
        if (roughnessMarkers == null) return;
        for (int i = 0; i < roughnessMarkers.Length && i < GameTuning.RoughnessAreas.Length; i++)
        {
            Transform marker = roughnessMarkers[i];
            if (marker == null) continue;
            Vector4 area = GameTuning.RoughnessAreas[i];
            Vector3 position = marker.position;
            position.x = area.x;
            position.z = area.y;
            marker.position = position;
            Vector3 scale = marker.localScale;
            scale.x = area.z * 2f;
            scale.z = area.w * 2f;
            marker.localScale = scale;
        }
    }

    private bool BindHud()
    {
        GameObject hud = GameObject.Find("HUD");
        if (hud == null) return false;
        foreach (TextMeshProUGUI label in hud.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (label.name == "Instruction") instruction = label;
            else if (label.name == "Progress") status = label;
        }
        foreach (Image image in hud.GetComponentsInChildren<Image>(true))
        {
            if (image.name == "ChargeBar") chargeBar = image;
            else if (image.name == "SleepBar") sleepBar = image;
            else if (image.name == "SpinBar") spinBar = image;
            else if (image.name == "SpinBarBG") spinBarRoot = image.gameObject;
        }
        SetSpinBarVisible(false);
        return instruction != null && status != null;
    }

    private void Update()
    {
        ControllerState input = controllerInput != null ? controllerInput.State : default;
        UpdateBall(input);
        UpdateSound(input);

        float deltaTime = Time.deltaTime;
        if (phase == Phase.Title)
        {
            bool anyInput = Mathf.Abs(input.Tilt.x) >= GameTuning.TiltDeadZone ||
                            Mathf.Abs(input.Tilt.y) >= GameTuning.TiltDeadZone || input.Shaken;
            if (anyInput)
            {
                phase = Phase.Carrying;
                gameStartTime = Time.time;
                SetCarryingInstruction();
            }
        }
        else if (phase == Phase.Carrying) UpdateCarrying(input, deltaTime);
        else if (phase == Phase.Spinning) UpdateSpinning(input, deltaTime);
        else if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);

        if (currentMarker != null)
        {
            bool show = phase == Phase.Carrying || phase == Phase.Spinning;
            currentMarker.gameObject.SetActive(show);
            if (show)
            {
                if (goalOpen)
                    currentMarker.position = new Vector3(GameTuning.GoalX, -0.08f, GameTuning.GoalZ);
                else
                {
                    Vector3 dockPos = docks[nodeIndex].position;
                    currentMarker.position = new Vector3(dockPos.x, dockPos.y + 0.04f, dockPos.z);
                }
            }
        }
        UpdateBallChargeVisual();
        UpdateSleepVisual();
        UpdateStatus();
        UpdateAnimator(input);
        UpdateCamera(deltaTime);
    }

}
