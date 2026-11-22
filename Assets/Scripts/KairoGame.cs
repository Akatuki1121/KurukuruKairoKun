using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// くるくる かいろくん の進行役。
// ステージのオブジェクトはすべてシーンの中に実物として配置されている前提で、
// ここでは生成も初期配置もせず、シーンの実物を名前で束ねて動かすだけにする。
public sealed class KairoGame : MonoBehaviour
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
    private Transform[] chargeParticles;
    private Renderer ballRenderer;
    private Material ballMaterial;
    private TextMeshProUGUI instruction;
    private TextMeshProUGUI status;
    private Image chargeBar;
    private Image spinBar;
    private GameObject spinBarRoot;
    private Transform cameraTransform;
    private float pickupFlashUntil;
    private Transform currentMarker;
    private Transform sliderBlock;
    private float gameStartTime;
    private int collectedCount;
    private AudioSource audioSource;
    private AudioClip spinTone;
    private AudioClip successTone;
    private Phase phase = Phase.Title;
    private int nodeIndex;
    private int spinCommandIndex;
    private float spinCommandTimer;
    private float progress;
    private float charge;
    private float jumpVelocity;
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

        chargeParticles = new Transform[GameTuning.ChargeParticlePositions.Length];
        for (int i = 0; i < chargeParticles.Length; i++)
            chargeParticles[i] = FindChild(root, "Charge particle " + (i + 1));

        currentMarker = FindChild(root, "Current marker");
        sliderBlock = FindChild(root, "Slider block");
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
            else if (image.name == "SpinBar") spinBar = image;
            else if (image.name == "SpinBarBG") spinBarRoot = image.gameObject;
        }
        SetSpinBarVisible(false);
        return instruction != null && status != null;
    }

    private void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        spinTone = CreateTone(220f, 0.11f);
        successTone = CreateTone(660f, 0.28f);
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
        UpdateStatus();
        UpdateAnimator(input);
        UpdateCamera(deltaTime);
    }

    // カメラは原ゼミちゃんの動きに少しだけついていく。
    private void UpdateCamera(float deltaTime)
    {
        if (cameraTransform == null || hara == null) return;
        Vector3 position = cameraTransform.position;
        float targetX = Mathf.Clamp(hara.position.x * 0.62f, -9.5f, 9.5f);
        float targetZ = Mathf.Clamp(hara.position.z * 0.8f - 10.6f, -16.6f, -6.4f);
        float blend = 1f - Mathf.Exp(-3f * deltaTime);
        position.x = Mathf.Lerp(position.x, targetX, blend);
        position.z = Mathf.Lerp(position.z, targetZ, blend);
        cameraTransform.position = position;
    }

    // 原ゼミちゃんのアニメーターへ、進行と入力の状態を渡す。
    private void UpdateAnimator(ControllerState input)
    {
        if (haraAnimator == null) return;
        float tiltMagnitude = Mathf.Max(Mathf.Abs(input.Tilt.x), Mathf.Abs(input.Tilt.y));
        float moveAmount = phase == Phase.Carrying
            ? Mathf.InverseLerp(GameTuning.TiltDeadZone, 1f, tiltMagnitude)
            : 0f;
        haraAnimator.SetFloat("Speed", moveAmount, 0.1f, Time.deltaTime);
        haraAnimator.SetBool("IsCarrying", phase == Phase.Carrying);
        haraAnimator.SetBool("IsSpinning", phase == Phase.Spinning);
    }

    private void UpdateCarrying(ControllerState input, float deltaTime)
    {
        if (sliderBlock != null)
        {
            Vector3 sp = sliderBlock.position;
            float slide = Mathf.Sin(Time.time * GameTuning.SliderSpeed) * GameTuning.SliderRange;
            sp.x = GameTuning.SliderCenterX + GameTuning.SliderDirX * slide;
            sp.z = GameTuning.SliderCenterZ + GameTuning.SliderDirZ * slide;
            sliderBlock.position = sp;
        }
        Vector3 position = hara.position;
        float horizontalTilt = Mathf.Abs(input.Tilt.x) >= GameTuning.TiltDeadZone ? input.Tilt.x : 0f;
        float verticalTilt = Mathf.Abs(input.Tilt.y) >= GameTuning.TiltDeadZone ? input.Tilt.y : 0f;
        Vector3 beforeMove = position;
        position.x += horizontalTilt * GameTuning.CharacterMoveSpeed * deltaTime;
        position.z += verticalTilt * GameTuning.CharacterMoveSpeed * deltaTime;
        if (!InsideWalkArea(position))
        {
            Vector3 tryX = beforeMove;
            tryX.x = position.x;
            if (InsideWalkArea(tryX)) position = tryX;
            else
            {
                Vector3 tryZ = beforeMove;
                tryZ.z = position.z;
                position = InsideWalkArea(tryZ) ? tryZ : beforeMove;
            }
        }
        ResolveObstacles(ref position);

        float groundY = GroundHeightAt(position.x, position.z);
        bool grounded = position.y <= groundY + GameTuning.InputDeadZone;
        if (input.Shaken && grounded)
        {
            jumpVelocity = GameTuning.JumpVelocity;
            grounded = false;
            if (haraAnimator != null) haraAnimator.SetTrigger("Jump");
        }
        if (!grounded)
        {
            jumpVelocity -= GameTuning.Gravity * deltaTime;
            position.y += jumpVelocity * deltaTime;
            if (position.y <= groundY)
            {
                position.y = groundY;
                jumpVelocity = 0f;
            }
        }
        else
        {
            position.y = groundY;
        }
        hara.position = position;
        // 進む向きへ少しだけ体を傾ける。
        Quaternion leanTarget = Quaternion.Euler(verticalTilt * 8f, 0f, -horizontalTilt * 10f);
        hara.rotation = Quaternion.Slerp(hara.rotation, leanTarget, 1f - Mathf.Exp(-10f * deltaTime));
        Vector3 carryTarget = hara.position + Vector3.right * 0.65f + Vector3.up * 0.7f;
        ball.position = Vector3.Lerp(ball.position, carryTarget, 1f - Mathf.Exp(-14f * deltaTime));

        bool moving = Mathf.Abs(horizontalTilt) >= GameTuning.TiltDeadZone ||
                      Mathf.Abs(verticalTilt) >= GameTuning.TiltDeadZone;
        bool onBeat = IsOnBeat();
        if (moving)
        {
            AddCharge(GameTuning.ChargeRate * deltaTime);
            if (onBeat) AddCharge(GameTuning.BeatChargeRate * deltaTime);
        }
        // 加速帯: 配線の上にいる間、その向きへ押し出される。充電も少し増える。
        for (int i = 0; i < GameTuning.BoostCenterX.Length; i++)
        {
            float bdx = position.x - GameTuning.BoostCenterX[i];
            float bdz = position.z - GameTuning.BoostCenterZ[i];
            float along = bdx * GameTuning.BoostDirX[i] + bdz * GameTuning.BoostDirZ[i];
            float side = bdx * -GameTuning.BoostDirZ[i] + bdz * GameTuning.BoostDirX[i];
            if (Mathf.Abs(along) <= GameTuning.BoostHalfLength && Mathf.Abs(side) <= GameTuning.BoostHalfWidth)
            {
                position.x += GameTuning.BoostDirX[i] * GameTuning.BoostSpeed * deltaTime;
                position.z += GameTuning.BoostDirZ[i] * GameTuning.BoostSpeed * deltaTime;
                hara.position = position;
                AddCharge(GameTuning.ChargeRate * 2f * deltaTime);
            }
        }
        CollectChargeParticles();
        if (goalOpen)
        {
            Vector2 goalDelta = new Vector2(GameTuning.GoalX - position.x, GameTuning.GoalZ - position.z);
            if (goalDelta.magnitude <= 0.75f) FinishGame();
            return;
        }

        Vector2 target = new Vector2(docks[nodeIndex].position.x + 0.85f * GameTuning.ArrivalSide[nodeIndex],
            docks[nodeIndex].position.z);
        Vector2 flat = new Vector2(hara.position.x, hara.position.z);
        if (!nodeCompleted[nodeIndex] && grounded && Vector2.Distance(flat, target) <= GameTuning.ArrivalDistance)
        {
            phase = Phase.Spinning;
            progress = 0f;
            spinCommandIndex = 0;
            spinCommandTimer = 0f;
            SetSpinInstruction();
        }
    }

    // 歩ける範囲 (レーンと区画と折り返し) の中かどうか。
    private bool InsideWalkArea(Vector3 p)
    {
        for (int i = 0; i < GameTuning.WalkAreaX.Length; i++)
        {
            if (Mathf.Abs(p.x - GameTuning.WalkAreaX[i]) <= GameTuning.WalkAreaHalfX[i] - 0.3f &&
                Mathf.Abs(p.z - GameTuning.WalkAreaZ[i]) <= GameTuning.WalkAreaHalfZ[i] - 0.3f)
                return true;
        }
        return false;
    }

    // その場所の足元の高さ (段の上では段の高さ)。
    private float GroundHeightAt(float x, float z)
    {
        float top = GameTuning.CharacterGroundHeight;
        for (int i = 0; i < GameTuning.PlatformX.Length; i++)
        {
            if (Mathf.Abs(x - GameTuning.PlatformX[i]) <= GameTuning.PlatformHalfX[i] &&
                Mathf.Abs(z - GameTuning.PlatformZ[i]) <= GameTuning.PlatformHalfZ[i])
                top = Mathf.Max(top, GameTuning.PlatformTop[i]);
        }
        return top;
    }

    // 部屋の中の障害物と、まだ閉まっているドアにめり込まないよう、浅い側へ押し出す。
    private void ResolveObstacles(ref Vector3 position)
    {
        for (int i = 0; i < GameTuning.BlockerX.Length; i++)
            PushOut(ref position, GameTuning.BlockerX[i], GameTuning.BlockerZ[i],
                GameTuning.BlockerHalf[i], GameTuning.BlockerHalf[i]);
        if (sliderBlock != null)
            PushOut(ref position, sliderBlock.position.x, sliderBlock.position.z,
                GameTuning.SliderHalf, GameTuning.SliderHalf);
        for (int i = 0; i < GameTuning.SpinNodeCount; i++)
        {
            if (nodeCompleted[i]) continue;
            PushOutDoor(ref position, i);
        }
    }

    // ドアは、抜ける向き (dir) と直角な板として塞ぐ。浅い側 (dir の正負) へ押し出す。
    private void PushOutDoor(ref Vector3 position, int i)
    {
        float dirX = GameTuning.DoorDirX[i];
        float dirZ = GameTuning.DoorDirZ[i];
        float dx = position.x - GameTuning.DoorCenterX[i];
        float dz = position.z - GameTuning.DoorCenterZ[i];
        float along = dx * dirX + dz * dirZ;
        float side = dx * -dirZ + dz * dirX;
        const float halfThick = 0.28f + 0.38f;
        const float halfWidth = 1.05f + 0.38f;
        if (Mathf.Abs(along) >= halfThick || Mathf.Abs(side) >= halfWidth) return;
        float pushed = along >= 0f ? halfThick : -halfThick;
        position.x = GameTuning.DoorCenterX[i] + dirX * pushed + -dirZ * side;
        position.z = GameTuning.DoorCenterZ[i] + dirZ * pushed + dirX * side;
    }

    private void PushOut(ref Vector3 position, float centerX, float centerZ, float halfX, float halfZ)
    {
        float ex = halfX + 0.38f;
        float ez = halfZ + 0.38f;
        float dx = position.x - centerX;
        float dz = position.z - centerZ;
        if (Mathf.Abs(dx) >= ex || Mathf.Abs(dz) >= ez) return;
        if (ex - Mathf.Abs(dx) < ez - Mathf.Abs(dz))
            position.x = centerX + (dx >= 0f ? ex : -ex);
        else
            position.z = centerZ + (dz >= 0f ? ez : -ez);
    }

    private void UpdateSpinning(ControllerState input, float deltaTime)
    {
        // かいろくんは置き場所 (ドック) の上で回る。
        Vector3 dockTarget = docks[nodeIndex].position + Vector3.up * 0.48f;
        ball.position = Vector3.Lerp(ball.position, dockTarget, 1f - Mathf.Exp(-12f * deltaTime));

        spinCommandTimer += deltaTime;
        float instructionDuration = GameTuning.NodeInstructionDurations[nodeIndex];
        while (spinCommandTimer >= instructionDuration)
        {
            spinCommandTimer -= instructionDuration;
            spinCommandIndex = (spinCommandIndex + 1) % 4;
            SetSpinInstruction();
        }

        // 逆ネジの地点では、指示の向きが通常と反対になる。
        int requiredDirection = CurrentSpinDirection() * GameTuning.NodeDirectionSign[nodeIndex];
        float speed = input.RotationSpeedDegreesPerSecond;
        bool correctSpeed = CurrentSpinRequiresFast()
            ? speed >= GameTuning.FastRotationMinimum
            : speed >= GameTuning.SlowRotationMinimum && speed <= GameTuning.SlowRotationMaximum;
        if (input.RotationDirection == requiredDirection && correctSpeed)
        {
            float completion = GameTuning.NodeCompletionDegrees[nodeIndex];
            progress = Mathf.Min(completion, progress + Mathf.Abs(input.RotationDeltaDegrees));
            float ratio = progress / completion;
            Transform screw = screws[nodeIndex];
            Vector3 screwBase = screwBasePositions[nodeIndex];
            screw.position = new Vector3(screwBase.x,
                screwBase.y + ratio * GameTuning.NodeMaxScrewRise[nodeIndex], screwBase.z);
            screw.Rotate(Vector3.up,
                input.RotationDeltaDegrees * 0.35f * GameTuning.NodeDirectionSign[nodeIndex], Space.World);
            // この地点のドアは、この地点の進み具合で上がる。開いたドアは開いたまま。
            Transform door = doors[nodeIndex];
            Vector3 doorBase = doorBasePositions[nodeIndex];
            door.position = new Vector3(doorBase.x, doorBase.y + ratio * 2.3f, doorBase.z);
            SetSpinInstruction();
            if (progress >= completion)
            {
                CompleteNode();
                return;
            }
        }

        if (input.RotationDirection != 0 && input.RotationDirection != requiredDirection)
            SetInstruction(CurrentSpinInstruction("逆向きに回そう"));
        else if (input.RotationDirection != 0 && !correctSpeed)
            SetInstruction(CurrentSpinInstruction("速さを合わせて回そう"));
    }

    private void CompleteNode()
    {
        PlaySuccess();
        nodeCompleted[nodeIndex] = true;
        SetRouteColor(nodeIndex, new Color(0.1f, 0.75f, 1f));
        if (nodeIndex == GameTuning.SpinNodeCount - 1)
        {
            // 回路が全部つながった。あとはゴールの LED まで運ぶだけ。
            goalOpen = true;
            phase = Phase.Carrying;
            progress = 0f;
            SetInstruction("回路がつながった！ ゴールの LED まで運ぼう");
            return;
        }
        nodeIndex++;
        phase = Phase.Carrying;
        progress = 0f;
        spinCommandIndex = 0;
        spinCommandTimer = 0f;
        SetCarryingInstruction();
    }

    private void FinishGame()
    {
        if (phase == Phase.Finished) return;
        phase = Phase.Finished;
        if (haraAnimator != null) haraAnimator.SetTrigger("Celebrate");
        // 開いたドア全部を明るく光らせる (ドアの素材は共通)。
        Renderer doorRenderer = doors[0].GetComponent<Renderer>();
        if (doorRenderer != null && doorRenderer.material.HasProperty("_EmissionColor"))
        {
            doorRenderer.material.EnableKeyword("_EMISSION");
            doorRenderer.material.SetColor("_EmissionColor", new Color(0.1f, 0.5f, 0.65f));
        }
        SetInstruction($"ゴール！ クリア！  タイム {Time.time - gameStartTime:0.0} 秒  •  粒 {collectedCount}/{GameTuning.ChargeParticlePositions.Length}  •  R で最初から");
    }

    private void UpdateBall(ControllerState input)
    {
        if (ball != null && Mathf.Abs(input.RotationDeltaDegrees) > GameTuning.InputDeadZone)
            ball.Rotate(Vector3.forward, input.RotationDeltaDegrees, Space.Self);
    }

    private void UpdateSound(ControllerState input)
    {
        if (audioSource == null) return;
        bool turning = input.RotationDirection != 0;
        audioSource.pitch = Mathf.Lerp(audioSource.pitch,
            0.7f + Mathf.Clamp01(input.RotationSpeedDegreesPerSecond / 480f) * 1.1f, Time.deltaTime * 8f);
        audioSource.volume = turning ? 0.18f : 0f;
        if (Time.time >= lastPitchUpdate && turning)
        {
            if (!audioSource.isPlaying) audioSource.PlayOneShot(spinTone);
            lastPitchUpdate = Time.time + 0.18f;
        }
    }

    private void PlaySuccess()
    {
        if (audioSource != null && successTone != null) audioSource.PlayOneShot(successTone);
    }

    private void SetCarryingInstruction()
    {
        SetInstruction("傾けて運ぶ  •  矢印キー / 傾け  •  振る / Space でジャンプ  •  拍に合わせると充電アップ");
    }

    private void SetSpinInstruction()
    {
        SetInstruction(CurrentSpinInstruction("スピンネジを回そう"));
    }

    private string CurrentSpinInstruction(string prefix)
    {
        string speedLabel = CurrentSpinRequiresFast() ? "速く" : "ゆっくり";
        string controlHint = "A / D・マウスボタン";
        if (!CurrentSpinRequiresFast()) controlHint += "  •  Shift + A / D / マウスボタン";
        int direction = CurrentSpinDirection() * GameTuning.NodeDirectionSign[nodeIndex];
        return $"{prefix}  •  {speedLabel} / {DirectionLabel(direction)}  •  {controlHint}";
    }

    private int CurrentSpinDirection() => spinCommandIndex < 2 ? 1 : -1;
    private bool CurrentSpinRequiresFast() => spinCommandIndex == 0 || spinCommandIndex == 3;
    private static string DirectionLabel(int direction) => direction > 0 ? "右回り" : "左回り";
    private bool IsOnBeat() => Mathf.Repeat(Time.time, GameTuning.BeatIntervalSeconds) < GameTuning.BeatWindowSeconds;

    private void AddCharge(float amount)
    {
        float oldCharge = charge;
        charge = Mathf.Clamp01(charge + amount);
        if (oldCharge < 1f && charge >= 1f) flashUntil = Time.time + GameTuning.BallFlashDuration;
    }

    private void CollectChargeParticles()
    {
        for (int i = 0; i < chargeParticles.Length; i++)
        {
            Transform particle = chargeParticles[i];
            if (particle == null) continue;
            Vector2 delta = new Vector2(particle.position.x - hara.position.x,
                particle.position.z - hara.position.z);
            if (delta.magnitude > 0.55f) continue;
            AddCharge(GameTuning.ChargePerParticle);
            collectedCount++;
            pickupFlashUntil = Time.time + 0.25f;
            Destroy(particle.gameObject);
            chargeParticles[i] = null;
        }
    }

    private void UpdateBallChargeVisual()
    {
        if (ballMaterial == null) return;
        bool fullyCharged = charge >= 1f;
        Color color = fullyCharged ? GameTuning.BallFullChargeColor : GameTuning.BallChargeColor;
        float emission = GameTuning.BallBaseEmission + charge * GameTuning.BallChargeEmission;
        if (Time.time < pickupFlashUntil) emission = Mathf.Max(emission, 1.6f);
        if (fullyCharged && Time.time < flashUntil) emission = GameTuning.BallFullChargeFlashEmission;
        // テクスチャのあるモデル (球の FBX) では地の色を塗り替えない。
        // 光り方は発光の強さと、満ちたときの発光色の切り替わりで出す。
        bool ballHasTexture = ballMaterial.mainTexture != null;
        if (!ballHasTexture)
        {
            ballMaterial.color = color;
            if (ballMaterial.HasProperty("_BaseColor")) ballMaterial.SetColor("_BaseColor", color);
        }
        if (ballMaterial.HasProperty("_EmissionColor"))
        {
            ballMaterial.EnableKeyword("_EMISSION");
            ballMaterial.SetColor("_EmissionColor", color * emission);
        }
    }

    private void SetSpinBarVisible(bool visible)
    {
        if (spinBarRoot != null) spinBarRoot.SetActive(visible);
        if (spinBar != null) spinBar.gameObject.SetActive(visible);
    }

    private void UpdateStatus()
    {
        if (chargeBar != null) chargeBar.fillAmount = charge;
        if (spinBar != null) spinBar.fillAmount = progress / GameTuning.NodeCompletionDegrees[nodeIndex];
        SetSpinBarVisible(phase == Phase.Spinning);
        if (status == null) return;
        string chargeLabel = $"チャージ {charge:P0}";
        if (phase == Phase.Carrying)
            status.text = $"スピン地点 {nodeIndex + 1}/{GameTuning.SpinNodeCount}   {chargeLabel}   {(IsOnBeat() ? "♪ BEAT" : "")}";
        else if (phase == Phase.Spinning)
            status.text = $"スピン地点 {nodeIndex + 1}/{GameTuning.SpinNodeCount}   ネジ {progress / GameTuning.NodeCompletionDegrees[nodeIndex]:P0}   {chargeLabel}";
        else
            status.text = $"CLEAR   {chargeLabel}";
    }

    private void SetInstruction(string text)
    {
        if (instruction != null) instruction.text = text;
    }

    private void SetRouteColor(int legIndex, Color color)
    {
        // URP Lit の色は _BaseColor 側にあるので、そちらを変える
        // (material.color は _Color しか触らず、見た目が変わらなかった)。
        foreach (Transform part in circuitLegs[legIndex])
        {
            Material material = part.GetComponent<Renderer>().material;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }
    }

    private static AudioClip CreateTone(float frequency, float duration)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        AudioClip clip = AudioClip.Create("Generated tone", sampleCount, 1, sampleRate, false);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate) *
                (1f - i / (float)sampleCount) * 0.35f;
        }
        clip.SetData(samples, 0);
        return clip;
    }
}
