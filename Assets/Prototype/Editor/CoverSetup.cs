using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace IrkutskPrototype
{
    public static class CoverSetup
    {
        public const string ScenePath="Assets/Scenes/CoverSandbox.unity";
        const string Folder="Assets/Resources/Cover";
        [MenuItem("Irkutsk/Create Cover Test Assets")]
        public static void EnsureAssets()
        {
            if(!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources","Cover");
            CreatePrefab("LowCover",1.15f,true,new Color(.3f,.5f,.65f));
            CreatePrefab("HighCover",2.3f,false,new Color(.4f,.45f,.5f));
            if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                var root=new GameObject("Cover sandbox only"); SceneManager.MoveGameObjectToScene(root,scene);
                root.AddComponent<CoverSandbox>();
                EditorSceneManager.SaveScene(scene,ScenePath); EditorSceneManager.CloseScene(scene,true);
            }
            AssetDatabase.SaveAssets();
        }
        static void CreatePrefab(string name,float height,bool low,Color color)
        {
            string path=Folder+"/"+name+".prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;
            string matPath=Folder+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(!material)
            {
                material=new Material(Shader.Find("Irkutsk/Exploration")); material.color=color;
                AssetDatabase.CreateAsset(material,matPath);
            }
            var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                var root=new GameObject(name); SceneManager.MoveGameObjectToScene(root,preview); root.layer=8;
                var box=root.AddComponent<BoxCollider>(); box.size=new Vector3(3,height,.7f); box.center=Vector3.up*height*.5f;
                var cover=root.AddComponent<CoverObstacle>(); cover.Low=low;
                var mesh=GameObject.CreatePrimitive(PrimitiveType.Cube); mesh.name="Cover body"; mesh.transform.SetParent(root.transform,false);
                mesh.transform.localPosition=box.center; mesh.transform.localScale=box.size;
                Object.DestroyImmediate(mesh.GetComponent<Collider>()); mesh.GetComponent<Renderer>().sharedMaterial=material;
                CreateSlots(cover); PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
        public static void CreateSlots(CoverObstacle cover)
        {
            if(cover.GetComponentsInChildren<CoverSlot>().Length>0) return;
            var box=cover.GetComponent<BoxCollider>();
            float scaleX=Mathf.Max(.01f,Mathf.Abs(cover.transform.lossyScale.x));
            float scaleZ=Mathf.Max(.01f,Mathf.Abs(cover.transform.lossyScale.z));
            foreach(int sign in new[]{-1,1})
            {
                var root=new GameObject(sign<0?"Back cover slot":"Front cover slot"); root.transform.SetParent(cover.transform,false);
                root.transform.localPosition=box.center+new Vector3(0,-box.size.y*.5f,sign*(box.size.z*.5f+.62f/scaleZ));
                root.transform.localRotation=Quaternion.Euler(0,sign<0?0:180,0);
                var slot=root.AddComponent<CoverSlot>();
                slot.LeftPeek=new Vector3(-(box.size.x*.5f+.5f/scaleX),0,0);
                slot.RightPeek=new Vector3(-slot.LeftPeek.x,0,slot.LeftPeek.z);
            }
        }
        [MenuItem("Irkutsk/Open Cover Sandbox")]
        static void OpenSandbox()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureAssets(); EditorSceneManager.OpenScene(ScenePath);
        }
    }
    [CustomEditor(typeof(CoverObstacle))]
    public sealed class CoverObstacleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Объекту нужен BoxCollider, соответствующий препятствию. Зелёные точки — места NPC, жёлтые — выходы для выглядывания. Их можно перемещать вручную.",MessageType.Info);
            if(GUILayout.Button("Создать места для NPC"))
            {
                var cover=(CoverObstacle)target;
                Undo.RegisterFullObjectHierarchyUndo(cover.gameObject,"Create cover slots");
                CoverSetup.CreateSlots(cover); EditorUtility.SetDirty(cover);
            }
        }
    }
}
