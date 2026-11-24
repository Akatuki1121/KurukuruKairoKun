using UnityEngine;

public sealed partial class KairoGame
{
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
}
