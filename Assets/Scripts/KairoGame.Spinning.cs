using UnityEngine;

public sealed partial class KairoGame
{

    private void BeginSpinning()
    {
        phase = Phase.Spinning;
        float completion = GameTuning.NodeCompletionDegrees[nodeIndex];
        float carryCharge = charge;
        progress = nodeIndex == 0 && !sleepEnabled
            ? 0f
            : completion * carryCharge * GameTuning.ChargeCarryConversion;
        charge = 0f;
        progress = Mathf.Clamp(progress, 0f, completion);
        ApplySpinProgressVisuals();
        spinCommandIndex = 0;
        spinCommandTimer = 0f;
        SetSpinInstruction();
    }

    private void ApplySpinProgressVisuals()
    {
        float completion = GameTuning.NodeCompletionDegrees[nodeIndex];
        float ratio = Mathf.Clamp01(progress / completion);
        Transform screw = screws[nodeIndex];
        Vector3 screwBase = screwBasePositions[nodeIndex];
        screw.position = new Vector3(screwBase.x,
            screwBase.y + ratio * GameTuning.NodeMaxScrewRise[nodeIndex], screwBase.z);
        Transform door = doors[nodeIndex];
        Vector3 doorBase = doorBasePositions[nodeIndex];
        door.position = new Vector3(doorBase.x, doorBase.y + ratio * GameTuning.DoorOpenHeight, doorBase.z);
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
            float previousCharge = charge;
            progress = Mathf.Min(completion, progress + Mathf.Abs(input.RotationDeltaDegrees));
            charge = Mathf.Clamp01(progress / completion);
            if (previousCharge < 1f && charge >= 1f)
                flashUntil = Time.time + GameTuning.BallFlashDuration;
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
            door.position = new Vector3(doorBase.x, doorBase.y + ratio * GameTuning.DoorOpenHeight, doorBase.z);
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
        sleepEnabled = true;
        sleepDepth = Mathf.Clamp01(GameTuning.NodeCompletionDegrees[nodeIndex] / GameTuning.SleepInitialReferenceDegrees) * GameTuning.SleepMaximum;
        awake = false;
        awakeTimer = 0f;
        charge = 1f;
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
        SetInstruction($"ゴール！ クリア！  タイム {Time.time - gameStartTime:0.0} 秒  •  R で最初から");
    }

    private void SetCarryingInstruction()
    {
        SetInstruction("傾けてそっと運ぶ  •  矢印キー / 傾け  •  A / D・マウスでネジ回転");
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
}
