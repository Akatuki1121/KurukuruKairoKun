using UnityEngine;

public sealed partial class KairoGame
{

    private void BeginSpinning()
    {
        phase = Phase.Spinning;
        float completion = GameTuning.NodeTotalDegrees(nodeIndex);
        float carryCharge = charge;
        // 帯電の持ち越し: 残っていた帯電の分だけ、最初の要素の積算が進んだ状態で始める。
        // 1 本目で眠りが未有効の時だけは 0 から。
        spinStepIndex = 0;
        spinStepDegrees = nodeIndex == 0 && !sleepEnabled
            ? 0f
            : completion * carryCharge * GameTuning.ChargeCarryConversion;
        spinStopTimer = 0f;
        spinStepOvershot = false;
        // 先行分だけで最初の要素が完了の範囲に達する場合は、その要素は完了した状態で始める。
        CompleteCarriedSteps();
        RefreshSpinProgress();
        ApplySpinProgressVisuals();
        if (spinStepIndex >= GameTuning.NodeCombinations[nodeIndex].Length)
        {
            // 先行分だけで組み合わせをすべて満たした場合は、そのままネジ完了へ。
            CompleteNode();
            return;
        }
        UpdateSpinInstruction();
    }

    // 帯電の持ち越しで要素が完了の範囲に達していたら、その要素を完了した状態にする。
    // 余りは次の要素へは持ち越さない (先行分は、その要素の積算に充てる分まで)。
    private void CompleteCarriedSteps()
    {
        GameTuning.SpinStep[] combination = GameTuning.NodeCombinations[nodeIndex];
        while (spinStepIndex < combination.Length &&
               spinStepDegrees >= combination[spinStepIndex].Degrees - GameTuning.SpinStepToleranceDegrees)
        {
            spinStepIndex++;
            spinStepDegrees = 0f;
        }
    }

    // ネジ全体の進捗と帯電を、組み合わせの進み具合から出し直す。
    // 帯電は進捗率と一致する (完了時に最大)。
    private void RefreshSpinProgress()
    {
        float completion = GameTuning.NodeTotalDegrees(nodeIndex);
        progress = Mathf.Clamp(CompletedCombinationDegrees() + spinStepDegrees, 0f, completion);
        charge = Mathf.Clamp01(progress / completion);
    }

    // 完了した要素の合計量。
    private float CompletedCombinationDegrees()
    {
        float total = 0f;
        GameTuning.SpinStep[] combination = GameTuning.NodeCombinations[nodeIndex];
        for (int i = 0; i < spinStepIndex && i < combination.Length; i++)
            total += combination[i].Degrees;
        return total;
    }

    private void ApplySpinProgressVisuals()
    {
        float completion = GameTuning.NodeTotalDegrees(nodeIndex);
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

        GameTuning.SpinStep step = GameTuning.NodeCombinations[nodeIndex][spinStepIndex];
        float completion = GameTuning.NodeTotalDegrees(nodeIndex);

        // 今の要素の向きに回した量だけを積算する。逆向きは積算しない。
        if (input.RotationDirection == step.Direction)
        {
            spinStepDegrees += Mathf.Abs(input.RotationDeltaDegrees);
            spinStepOvershot = false;
            RefreshSpinProgress();
            float ratio = progress / completion;
            Transform screw = screws[nodeIndex];
            Vector3 screwBase = screwBasePositions[nodeIndex];
            screw.position = new Vector3(screwBase.x,
                screwBase.y + ratio * GameTuning.NodeMaxScrewRise[nodeIndex], screwBase.z);
            // 逆ネジの地点では、ネジの見た目の回転方向が入力と反対になる。
            screw.Rotate(Vector3.up,
                input.RotationDeltaDegrees * 0.35f * GameTuning.NodeDirectionSign[nodeIndex], Space.World);
            // この地点のドアは、この地点の進み具合で上がる。開いたドアは開いたまま。
            Transform door = doors[nodeIndex];
            Vector3 doorBase = doorBasePositions[nodeIndex];
            door.position = new Vector3(doorBase.x, doorBase.y + ratio * GameTuning.DoorOpenHeight, doorBase.z);
        }

        // 目標量＋許容幅を超えて回しすぎたら、その要素の積算だけを 0 に戻してやり直しにする。
        // 完了した要素と組み合わせ全体は戻さない。
        if (spinStepDegrees > step.Degrees + GameTuning.SpinStepToleranceDegrees)
        {
            spinStepDegrees = 0f;
            spinStepOvershot = true;
            spinStopTimer = 0f;
            RefreshSpinProgress();
        }

        // 目標量から許容幅以内まで回した状態で止まったら、その要素は完了。
        if (spinStepDegrees >= step.Degrees - GameTuning.SpinStepToleranceDegrees)
        {
            if (input.RotationSpeedDegreesPerSecond < GameTuning.SpinStopSpeedThreshold)
                spinStopTimer += deltaTime;
            else
                spinStopTimer = 0f;
            if (spinStopTimer >= GameTuning.SpinStopHoldSeconds)
            {
                CompleteSpinStep();
                if (phase != Phase.Spinning) return;
            }
        }
        else
        {
            spinStopTimer = 0f;
        }

        UpdateSpinInstruction(input);
    }

    // 今の要素が完了した。次の要素へ進み、最後の要素ならネジの完了へつなぐ。
    private void CompleteSpinStep()
    {
        PlayStep();
        spinStepIndex++;
        spinStepDegrees = 0f;
        spinStopTimer = 0f;
        spinStepOvershot = false;
        RefreshSpinProgress();
        if (spinStepIndex >= GameTuning.NodeCombinations[nodeIndex].Length)
            CompleteNode();
    }

    private void CompleteNode()
    {
        PlaySuccess();
        flashUntil = Time.time + GameTuning.BallFlashDuration;
        nodeCompleted[nodeIndex] = true;
        sleepEnabled = true;
        sleepDepth = Mathf.Clamp01(GameTuning.NodeTotalDegrees(nodeIndex) / GameTuning.SleepInitialReferenceDegrees) * GameTuning.SleepMaximum;
        awake = false;
        awakeTimer = 0f;
        charge = 1f;
        spinStepIndex = 0;
        spinStepDegrees = 0f;
        spinStopTimer = 0f;
        spinStepOvershot = false;
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

    // 次に回す向きと残り回数、組み合わせ全体のうち何番目の要素かを、常に見える形にする。
    // 回しすぎた時と逆向きに回した時の注意も、ここで出す。
    private void UpdateSpinInstruction(ControllerState input = default)
    {
        GameTuning.SpinStep step = GameTuning.NodeCombinations[nodeIndex][spinStepIndex];
        float remainingRotations = Mathf.Max(0f, step.Degrees - spinStepDegrees) / GameTuning.DegreesPerRotation;
        string prefix;
        if (input.RotationDirection != 0 && input.RotationDirection != step.Direction)
            prefix = "逆向きに回そう";
        else if (spinStepOvershot)
            prefix = "回しすぎた！ もう一度";
        else if (spinStepDegrees >= step.Degrees - GameTuning.SpinStepToleranceDegrees)
            prefix = "止まって決めよう";
        else
            prefix = "スピンネジを回そう";
        SetInstruction($"{prefix}  •  {DirectionLabel(step.Direction)} あと {remainingRotations:0.0} 回転  •  {spinStepIndex + 1}/{GameTuning.NodeCombinations[nodeIndex].Length}  •  A / D・マウスボタン  •  ホイールで微調整");
    }

    private static string DirectionLabel(int direction) => direction > 0 ? "右回り" : "左回り";
}
