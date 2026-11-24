using UnityEngine;

public sealed partial class KairoGame
{

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

        Vector2 tilt = input.Tilt;
        float tiltMagnitude = tilt.magnitude;
        Vector2 moveDirection = tiltMagnitude > GameTuning.TiltDeadZone ? tilt.normalized : Vector2.zero;
        float moveScale = tiltMagnitude <= GameTuning.TiltDeadZone
            ? 0f
            : Mathf.Clamp01((tiltMagnitude - GameTuning.TiltDeadZone) /
                (GameTuning.CarryMaximumTilt - GameTuning.TiltDeadZone));
        bool canMove = !sleepEnabled || !awake || awakeTimer <= 0f;
        if (sleepEnabled && awake && awakeTimer > 0f)
        {
            awakeTimer = Mathf.Max(0f, awakeTimer - deltaTime);
            canMove = false;
        }
        if (!canMove) moveScale = 0f;

        Vector3 position = hara.position;
        Vector3 beforeMove = position;
        float moveSpeed = GameTuning.CarryMaximumSpeed * moveScale;
        position.x += moveDirection.x * moveSpeed * deltaTime;
        position.z += moveDirection.y * moveSpeed * deltaTime;
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
        position.y = groundY;
        hara.position = position;
        Quaternion leanTarget = Quaternion.Euler(moveDirection.y * GameTuning.CarryLeanDegrees,
            0f, -moveDirection.x * GameTuning.CarryLeanDegrees);
        hara.rotation = Quaternion.Slerp(hara.rotation, leanTarget,
            1f - Mathf.Exp(-GameTuning.CarryLeanResponse * deltaTime));

        shakeIntensity = CalculateShakeIntensity(moveSpeed, position, input);
        float sleepRatio = sleepEnabled && !awake ? sleepDepth / GameTuning.SleepMaximum : 0f;
        Vector3 carryTarget = hara.position + Vector3.right * GameTuning.BallCarryOffsetX +
            Vector3.up * (GameTuning.BallCarryOffsetY + Mathf.Sin(Time.time * GameTuning.BallSleepBobFrequency) *
                GameTuning.BallSleepBobAmplitude * sleepRatio);
        ball.position = Vector3.Lerp(ball.position, carryTarget,
            1f - Mathf.Exp(-GameTuning.BallCarryFollowRate * deltaTime));
        if (sleepEnabled)
        {
            float wobble = shakeIntensity * GameTuning.BallShakeVisualDegrees;
            ball.rotation = Quaternion.Slerp(ball.rotation,
                Quaternion.Euler(wobble, 0f, wobble), 1f - Mathf.Exp(-GameTuning.BallShakeVisualResponse * deltaTime));
        }

        UpdateSleep(input, deltaTime, shakeIntensity);

        // 加速帯と粒は残すが、運搬の自動移動および帯電加算源としては無効化する。

        if (goalOpen)
        {
            Vector2 goalDelta = new Vector2(GameTuning.GoalX - position.x, GameTuning.GoalZ - position.z);
            if (goalDelta.magnitude <= GameTuning.GoalArrivalDistance) FinishGame();
            return;
        }

        Vector2 target = new Vector2(docks[nodeIndex].position.x + GameTuning.DockArrivalOffsetX * GameTuning.ArrivalSide[nodeIndex],
            docks[nodeIndex].position.z);
        Vector2 flat = new Vector2(hara.position.x, hara.position.z);
        if (!nodeCompleted[nodeIndex] && Vector2.Distance(flat, target) <= GameTuning.NodeArrivalDistance)
            BeginSpinning();
    }

    private float CalculateShakeIntensity(float speed, Vector3 position, ControllerState input)
    {
        float speedContribution = Mathf.Clamp01(speed / GameTuning.ShakeSpeedNormalization) *
            GameTuning.ShakeSpeedCoefficient;
        float roughnessMultiplier = GameTuning.ShakeNormalMultiplier;
        for (int i = 0; i < GameTuning.RoughnessAreas.Length; i++)
        {
            Vector4 area = GameTuning.RoughnessAreas[i];
            if (Mathf.Abs(position.x - area.x) <= area.z && Mathf.Abs(position.z - area.y) <= area.w)
            {
                roughnessMultiplier = GameTuning.ShakeRoughnessMultiplier;
                break;
            }
        }
        float inputContribution = Mathf.Clamp01(input.ShakeIntensity) * GameTuning.ShakeInputCoefficient;
        float impactContribution = 0f;
        if (sliderBlock != null)
        {
            Vector3 sliderPosition = sliderBlock.position;
            if (Mathf.Abs(position.x - sliderPosition.x) <= GameTuning.SliderImpactHalfX &&
                Mathf.Abs(position.z - sliderPosition.z) <= GameTuning.SliderImpactHalfZ)
                impactContribution = GameTuning.ShakeSliderImpact;
        }
        return Mathf.Clamp01(speedContribution * roughnessMultiplier + inputContribution + impactContribution);
    }

    private void UpdateSleep(ControllerState input, float deltaTime, float currentShake)
    {
        if (!sleepEnabled) return;
        bool quiet = input.Tilt.magnitude < GameTuning.SleepQuietTiltThreshold &&
            currentShake < GameTuning.SleepQuietShakeThreshold;
        if (awake)
        {
            if (awakeTimer > 0f) return;
            if (quiet)
            {
                sleepDepth = Mathf.Min(GameTuning.SleepMaximum,
                    sleepDepth + GameTuning.SleepRecoveryRate * deltaTime);
                if (sleepDepth >= GameTuning.SleepReentryDepth) awake = false;
            }
            return;
        }

        if (quiet)
            sleepDepth = Mathf.Min(GameTuning.SleepMaximum,
                sleepDepth + GameTuning.SleepRecoveryRate * deltaTime);
        else
            sleepDepth = Mathf.Max(0f, sleepDepth - currentShake * GameTuning.SleepLossRate * deltaTime);
        if (GameTuning.SleepTimeDecayRate > 0f)
            sleepDepth = Mathf.Max(0f, sleepDepth - GameTuning.SleepTimeDecayRate * deltaTime);
        if (sleepDepth <= GameTuning.SleepWakeThreshold)
        {
            awake = true;
            awakeTimer = GameTuning.AwakeDuration;
            charge *= 1f - GameTuning.ChargeWakeSpillRatio;
        }
    }
}
