using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor.Build.Reporting;

namespace IrkutskPrototype
{
    public static class PrototypeBuilder
    {
        const string ScenePath="Assets/Scenes/EscapePrototype.unity";
        [MenuItem("Irkutsk/Create prototype scene")]
        public static void CreateScene()
        {
            if(!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets","Resources");
            if(AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/PrototypeSurface.mat")==null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Lit")),"Assets/Resources/PrototypeSurface.mat");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Escape prototype").AddComponent<EscapeGame>();
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            PlayerSettings.productName="Escape from Irkutsk";
            PlayerSettings.SplashScreen.show=false;
            PlayerSettings.defaultScreenWidth=1280; PlayerSettings.defaultScreenHeight=720;
            AssetDatabase.SaveAssets();
            Debug.Log("IRKUTSK_SCENE_READY");
        }
        public static void BuildWindows()
        {
            CreateScene();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{ScenePath},locationPathName="Builds/Windows/EscapeFromIrkutsk.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development });
            if(report.summary.result!=BuildResult.Succeeded) throw new System.Exception("Build failed: "+report.summary.result);
            Debug.Log("IRKUTSK_BUILD_OK");
        }
    }
}
