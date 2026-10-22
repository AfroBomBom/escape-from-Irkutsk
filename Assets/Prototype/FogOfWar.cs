using System.Collections.Generic;
using UnityEngine;

namespace IrkutskPrototype
{
    // A new component/texture is created on every run, so exploration never leaks across restarts.
    public sealed class FogOfWar : MonoBehaviour
    {
        const int Width=156, Height=56;
        static readonly Vector2 Origin=new Vector2(-34,-28);
        static readonly Color Darkness=new Color(.025f,.035f,.05f,1);
        readonly Color32[] pixels=new Color32[Width*Height];
        readonly List<Group> groups=new List<Group>();
        readonly List<TextMesh> labels=new List<TextMesh>();
        Texture2D map;
        EscapeGame game;
        float nextRefresh;
        sealed class Group { public Transform Root; public Renderer[] Renderers; }

        public void Initialize(EscapeGame owner)
        {
            game=owner;
            map=new Texture2D(Width,Height,TextureFormat.RGBA32,false,true);
            map.name="Explored and visible cells"; map.filterMode=FilterMode.Point; map.wrapMode=TextureWrapMode.Clamp;
            Shader.SetGlobalTexture("_ExplorationMap",map);
            Shader.SetGlobalVector("_ExplorationBounds",new Vector4(Origin.x,Origin.y,Width,Height));
            Shader.SetGlobalFloat("_ExplorationEnabled",1);
            Color dark=QualitySettings.activeColorSpace==ColorSpace.Linear?Darkness.linear:Darkness;
            Shader.SetGlobalVector("_ExplorationDarkness",new Vector4(dark.r,dark.g,dark.b,1));
            foreach(var z in game.Zombies) RegisterDynamic(z.gameObject);
            foreach(var p in game.Parts) RegisterDynamic(p.gameObject);
            RegisterDynamic(game.Car.gameObject);
            foreach(var text in FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
                if(!text.GetComponentInParent<SparePart>()) labels.Add(text);
            var camera=FindAnyObjectByType<Camera>();
            if(camera) { camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Darkness; }
            Refresh();
        }
        public void RegisterDynamic(GameObject root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>(true);
            var block=new MaterialPropertyBlock(); block.SetFloat("_FogDynamic",1);
            foreach(var r in renderers) if(!(r is MeshRenderer && r.GetComponent<TextMesh>())) r.SetPropertyBlock(block);
            groups.Add(new Group { Root=root.transform,Renderers=renderers });
        }
        int Index(Vector3 point)
        {
            int x=Mathf.FloorToInt(point.x-Origin.x), z=Mathf.FloorToInt(point.z-Origin.y);
            return x<0 || z<0 || x>=Width || z>=Height?-1:z*Width+x;
        }
        public bool IsVisible(Vector3 point)
        {
            int i=Index(point);
            if(i<0 || pixels[i].g==0 || game==null) return false;
            // Grid cells can straddle a wall. Check the exact point for labels/actors as well.
            Vector3 eye=game.Target+Vector3.up*1.25f;
            Vector3 target=new Vector3(point.x,eye.y,point.z);
            return !Physics.Linecast(eye,target,EscapeGame.SolidMask);
        }
        public bool IsExplored(Vector3 point) { int i=Index(point); return i>=0 && pixels[i].r>0; }
        void LateUpdate()
        {
            if(game==null) return;
            if(Time.unscaledTime>=nextRefresh) { Refresh(); nextRefresh=Time.unscaledTime+.1f; }
            UpdateObjects();
        }
        public void Refresh()
        {
            if(game==null || map==null) return;
            for(int i=0;i<pixels.Length;i++) pixels[i].g=0;
            Vector3 eye=game.Target+Vector3.up*1.25f;
            float radius=game.IsDriving?14:10;
            int minX=Mathf.Max(0,Mathf.FloorToInt(eye.x-radius-Origin.x));
            int maxX=Mathf.Min(Width-1,Mathf.CeilToInt(eye.x+radius-Origin.x));
            int minZ=Mathf.Max(0,Mathf.FloorToInt(eye.z-radius-Origin.y));
            int maxZ=Mathf.Min(Height-1,Mathf.CeilToInt(eye.z+radius-Origin.y));
            for(int z=minZ;z<=maxZ;z++) for(int x=minX;x<=maxX;x++)
            {
                Vector3 destination=new Vector3(Origin.x+x+.5f,eye.y,Origin.y+z+.5f);
                Vector3 delta=destination-eye; float distance=delta.magnitude;
                if(distance>radius) continue;
                // Reveal the front surface of a wall, but not rooms behind it.
                bool blocked=distance>.01f && Physics.Raycast(eye,delta.normalized,out var hit,distance,EscapeGame.SolidMask) && hit.distance<distance-.7f;
                if(!blocked) pixels[z*Width+x]=new Color32(255,255,0,255);
            }
            map.SetPixels32(pixels); map.Apply(false);
            UpdateObjects();
        }
        void UpdateObjects()
        {
            for(int i=groups.Count-1;i>=0;i--)
            {
                var group=groups[i];
                if(!group.Root) { groups.RemoveAt(i); continue; }
                bool visible=IsVisible(group.Root.position);
                foreach(var r in group.Renderers) if(r) r.enabled=visible;
            }
            foreach(var text in labels) if(text)
            {
                bool explored=IsExplored(text.transform.position);
                text.GetComponent<Renderer>().enabled=explored;
                text.color=IsVisible(text.transform.position)?Color.white:new Color(.35f,.35f,.35f);
            }
        }
        void OnDestroy()
        {
            Shader.SetGlobalFloat("_ExplorationEnabled",0);
            if(map) Destroy(map);
        }
    }
}
