using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KairoGame
{

    private void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        spinTone = CreateTone(220f, 0.11f);
        successTone = CreateTone(660f, 0.28f);
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
        float moveAmount = phase == Phase.Carrying && (!sleepEnabled || !awake || awakeTimer <= 0f)
            ? Mathf.InverseLerp(GameTuning.TiltDeadZone, GameTuning.CarryMaximumTilt, tiltMagnitude)
            : 0f;
        haraAnimator.SetFloat("Speed", moveAmount, 0.1f, Time.deltaTime);
        haraAnimator.SetBool("IsCarrying", phase == Phase.Carrying);
        haraAnimator.SetBool("IsSpinning", phase == Phase.Spinning);
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
    private void UpdateBallChargeVisual()
    {
        if (ballMaterial == null) return;
        bool fullyCharged = charge >= 1f;
        Color color = fullyCharged ? GameTuning.BallFullChargeColor : GameTuning.BallChargeColor;
        float emission = GameTuning.BallBaseEmission + charge * GameTuning.BallChargeEmission;
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

    private void UpdateSleepVisual()
    {
        if (sleepText == null) return;
        if (!sleepEnabled || phase != Phase.Carrying)
        {
            sleepText.enabled = false;
            sleepText.text = string.Empty;
            return;
        }
        if (awake)
        {
            sleepText.enabled = true;
            sleepText.text = "◎";
            return;
        }
        float depthRatio = Mathf.Clamp01(sleepDepth / GameTuning.SleepMaximum);
        int count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(GameTuning.SleepZMinCount,
            GameTuning.SleepZMaxCount, depthRatio)), GameTuning.SleepZMinCount, GameTuning.SleepZMaxCount);
        float interval = Mathf.Lerp(GameTuning.SleepZShallowInterval, GameTuning.SleepZDeepInterval, depthRatio);
        bool visible = Mathf.Repeat(Time.time, interval) < interval * GameTuning.SleepZVisibleRatio;
        sleepText.enabled = visible;
        sleepText.fontSize = Mathf.Lerp(GameTuning.SleepZShallowFontSize, GameTuning.SleepZDeepFontSize, depthRatio);
        string sleepGlyph = count == 1 ? "Z" : count == 2 ? "Zzz" : "Zzzz";
        sleepText.text = sleepGlyph + (shakeIntensity > GameTuning.SleepQuietShakeThreshold ? "…" : "");
    }

    private void SetSpinBarVisible(bool visible)
    {
        if (spinBarRoot != null) spinBarRoot.SetActive(visible);
        if (spinBar != null) spinBar.gameObject.SetActive(visible);
    }

    private void UpdateStatus()
    {
        if (chargeBar != null) chargeBar.fillAmount = charge;
        if (sleepBar != null)
        {
            bool showSleepBar = sleepEnabled && phase == Phase.Carrying;
            sleepBar.gameObject.SetActive(showSleepBar);
            sleepBar.fillAmount = showSleepBar ? sleepDepth / GameTuning.SleepMaximum : 0f;
        }
        if (spinBar != null) spinBar.fillAmount = progress / GameTuning.NodeCompletionDegrees[nodeIndex];
        SetSpinBarVisible(phase == Phase.Spinning);
        if (status == null) return;
        string chargeLabel = $"帯電 {charge:P0}";
        string sleepLabel = sleepEnabled ? (awake ? "起きている" : $"眠り {sleepDepth / GameTuning.SleepMaximum:P0}") : "起きている";
        if (phase == Phase.Carrying)
            status.text = $"スピン地点 {nodeIndex + 1}/{GameTuning.SpinNodeCount}   {chargeLabel}   {sleepLabel}   揺れ {shakeIntensity:P0}";
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
