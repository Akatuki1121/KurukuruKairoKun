using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class KairoProjectBuilder
{
    [MenuItem("Kairo/Build Main Scene")]
    public static void BuildMainScene()
    {
        GameObject root = new GameObject("Kairo Game");
        root.AddComponent<ControllerInput>();
        root.AddComponent<KairoGame>();
        var scene = root.scene;
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainScene.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/MainScene.unity", true) };
    }
}






