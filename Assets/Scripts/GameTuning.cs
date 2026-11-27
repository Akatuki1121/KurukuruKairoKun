using UnityEngine;

public static class GameTuning
{
    public const int SpinNodeCount = 3;
    public const float CharacterMoveSpeed = 3.2f;
    public const float CharacterGroundHeight = 0.2f;
    public const float JumpVelocity = 4.2f;
    public const float Gravity = 12f;
    public const float NodeArrivalDistance = 0.12f;
    public const float TiltDeadZone = 0.12f;
    public const float InputDeadZone = 0.001f;

    // 傾け歩行
    public const float CarryMaximumSpeed = 2.0f;
    public const float CarryMaximumTilt = 1.0f;
    public const float CarryLeanDegrees = 10f;
    public const float CarryLeanResponse = 10f;
    public const float BallCarryOffsetX = 0.65f;
    public const float BallCarryOffsetY = 0.7f;
    public const float BallCarryFollowRate = 14f;
    public const float BallSleepBobFrequency = 2.2f;
    public const float BallSleepBobAmplitude = 0.035f;
    public const float BallShakeVisualDegrees = 8f;
    public const float BallShakeVisualResponse = 16f;
    public const float DockArrivalOffsetX = 0.85f;
    public const float GoalArrivalDistance = 0.75f;
    public const float DoorOpenHeight = 2.3f;

    // 眠り
    public const float SleepMaximum = 100f;
    public const float SleepInitialReferenceDegrees = 1080f;
    public const float SleepLossRate = 20f;
    public const float SleepRecoveryRate = 12f;
    public const float SleepWakeThreshold = 0f;
    public const float SleepReentryDepth = 15f;
    public const float AwakeDuration = 3f;
    public const float SleepQuietTiltThreshold = 0.1f;
    public const float SleepQuietShakeThreshold = 0.08f;
    public const float SleepTimeDecayRate = 0f;
    public const int SleepZMinCount = 1;
    public const int SleepZMaxCount = 3;
    public const float SleepZShallowInterval = 0.65f;
    public const float SleepZDeepInterval = 2.2f;
    public const float SleepZVisibleRatio = 0.7f;
    public const float SleepZShallowFontSize = 18f;
    public const float SleepZDeepFontSize = 28f;

    // 揺れ
    public const float ShakeSpeedNormalization = 2.0f;
    public const float ShakeSpeedCoefficient = 0.65f;
    public const float ShakeNormalMultiplier = 1.0f;
    public const float ShakeRoughnessMultiplier = 2.0f;
    public const float ShakeInputCoefficient = 0.15f;
    public const float ShakeSliderImpact = 0.5f;
    public const float SliderImpactHalfX = 1.0f;
    public const float SliderImpactHalfZ = 1.0f;
    public static readonly Vector4[] RoughnessAreas =
    {
        // 通路A終端の短い予告区間 (仮置き)。部屋2に近い側にだけ置く。
        new Vector4(-2f, -4f, 0.65f, 0.65f),
        // 通路B中央の本番区間 (仮置き)。通路B後半の歩行幅いっぱいに置き、まっすぐ抜けると必ず通る。
        new Vector4(11.4f, -0.7f, 1.6f, 1.0f)
    };

    // 帯電の持ち越し
    public const float ChargeWakeSpillRatio = 0.25f;
    public const float ChargeCarryConversion = 0.25f;
    public const float DebugMouseShakePixelsPerSecond = 900f;
    public const float SerialTiltChangeNormalization = 3.0f;
    public const float SerialShakeFlagIntensity = 0.9f;

    public const float BeatIntervalSeconds = 0.8f;
    public const float BeatWindowSeconds = 0.16f;
    public const float ChargeRate = 0.025f;
    public const float BeatChargeRate = 0.18f;
    public const float ChargePerParticle = 0.12f;
    public const float ParticlePickupRadius = 0.34f;

    public const float DebugRotationSpeed = 240f;
    public const float DebugSlowRotationSpeed = 60f;
    public const float MouseWheelDegreesPerNotch = 6f;
    public const float FastRotationMinimum = 180f;
    public const float SlowRotationMinimum = 30f;
    public const float SlowRotationMaximum = 120f;
    public const float SpinInstructionDuration = 1.8f;
    public const float CompletionRotationDegrees = 720f;
    public const float MaxScrewRise = 1.65f;

    public const float BallRadius = 0.42f;
    public const float BallBaseEmission = 0.08f;
    public const float BallChargeEmission = 1.8f;
    public const float BallFullChargeFlashEmission = 4.5f;
    public const float BallFlashDuration = 0.12f;
    public static readonly Color BallChargeColor = new Color(0.05f, 0.42f, 0.95f);
    public static readonly Color BallFullChargeColor = new Color(0.35f, 0.9f, 1f);
    // ネジごとの挙動: 必要な回転量、向きの反転 (逆ネジ)、上がる高さ、指示の切り替わり間隔
    public static readonly float[] NodeCompletionDegrees = { 720f, 1080f, 720f };
    public static readonly int[] NodeDirectionSign = { 1, 1, -1 };
    public static readonly float[] NodeMaxScrewRise = { 1.65f, 2.1f, 1.65f };
    public static readonly float[] NodeInstructionDurations = { 1.8f, 1.3f, 1.8f };
    public static readonly float[] ArrivalSide = { 1f, 1f, 1f };
    // 舞台は回路図の形: 部品 (ネジ部屋) を配線 (通路) でつなぐ。
    // 歩ける範囲は、部屋 4 つ (3 部屋とゴールの小部屋) と配線の矩形。
    // 並び: 0-3 部屋1・部屋2・部屋3・ゴール小部屋、4-5 通路A前半・後半、
    // 6-7 通路B前半・後半、8 通路C (部屋3とゴールの小部屋の間)、9-11 通路Bの迂回路 (南の連絡・東の脚・北の連絡)。
    // InsideWalkArea は各矩形を四方 0.3 ずつ狭めて判定するため、端が接するだけの矩形同士は
    // 0.6 の隙間ができて通れなくなる。通路側の矩形は部屋側へ 0.7 だけ食い込ませてあり、
    // 狭めた後も 0.1 重なる (2026-11-27 の検証で、食い込ませ前は部屋1を出られなかった)。
    public static readonly float[] WalkAreaX = { -11.5f, 3.4f, 12.2f, 17.1f, -8f, -4.05f, 9.15f, 11.4f, 16.2f, 12.55f, 14.05f, 12.55f };
    public static readonly float[] WalkAreaZ = { 4.8f, -5.4f, 5.6f, 5.6f, -1.25f, -4f, -3.4f, -0.35f, 5.6f, -2.45f, -0.6f, 1.15f };
    public static readonly float[] WalkAreaHalfX = { 4.5f, 4.2f, 4f, 0.9f, 1.6f, 3.95f, 2.25f, 1.6f, 0.7f, 2.45f, 0.95f, 2.45f };
    public static readonly float[] WalkAreaHalfZ = { 4f, 3.8f, 3.6f, 1.3f, 2.75f, 1.6f, 1.6f, 3.05f, 1.3f, 0.65f, 2.5f, 0.75f };
    // ドア (各部屋の出口)。中心と、抜ける向き (ドアはその向きを塞ぐ)。
    public static readonly float[] DoorCenterX = { -8f, 7.6f, 16.2f };
    public static readonly float[] DoorCenterZ = { 0.8f, -3.4f, 5.6f };
    public static readonly float[] DoorDirX = { 0f, 1f, 1f };
    public static readonly float[] DoorDirZ = { -1f, 0f, 0f };
    // 動かない障害物: 天蓋の柱 12 本と、部屋の隅の背の高い部品 3 つ。
    public static readonly float[] BlockerX = { -14.45f, -14.45f, -10.35f, -10.35f, 1.35f, 1.35f, 5.45f, 5.45f, 9.75f, 9.75f, 13.85f, 13.85f, -7.9f, -0.1f, 8.9f };
    public static readonly float[] BlockerZ = { 2.75f, 6.85f, 2.75f, 6.85f, -7.45f, -3.35f, -7.45f, -3.35f, 3.55f, 7.65f, 3.55f, 7.65f, 8f, -8.5f, 8.5f };
    public static readonly float[] BlockerHalf = { 0.14f, 0.14f, 0.14f, 0.14f, 0.14f, 0.14f, 0.14f, 0.14f, 0.14f, 0.14f, 0.14f, 0.14f, 0.42f, 0.42f, 0.42f };
    // ネジ台 (上に乗れる低い台)。
    public static readonly float[] PlatformX = { -12.4f, 3.4f, 11.8f };
    public static readonly float[] PlatformZ = { 4.8f, -5.4f, 5.6f };
    public static readonly float[] PlatformHalfX = { 2.3f, 2.3f, 2.3f };
    public static readonly float[] PlatformHalfZ = { 2.3f, 2.3f, 2.3f };
    public static readonly float[] PlatformTop = { 0.38f, 0.38f, 0.38f };
    // 配線 2 の上を横切る往復部品 (コンデンサ)。通路B後半の南側、でこぼこ地帯の手前 (部屋2側) に置く。
    // 手前で待つ場所: 通路B前半の西側 (9, -3.4) あたりと、通路B前半の南縁 (11.4, -4.6) あたりは、
    // コンデンサの往復範囲と衝突範囲に常時入らない。
    public const float SliderCenterX = 11.4f;
    public const float SliderCenterZ = -2.5f;
    public const float SliderDirX = 1f;
    public const float SliderDirZ = 0f;
    public const float SliderRange = 1.0f;
    public const float SliderSpeed = 0.9f;
    public const float SliderHalf = 0.55f;
    // ゴールの小部屋の中心 (LED の場所)。
    public const float GoalX = 17.1f;
    public const float GoalZ = 5.6f;
    // 粒の数えに使う配列 (長さが粒の総数)。
    public static readonly float[] ChargeParticlePositions = { -8f, -7.4f, -5.8f, -3f, 0.2f, 6.8f, 9.5f, 10.6f, 12.2f, 9.4f, 13.4f, 15f, -15f, -8.2f, 17f, 17.3f };
}
