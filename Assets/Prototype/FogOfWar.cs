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
        sealed class Group { public Transform Root; public Renderer[] Renderers; public bool Bypass,Actor; }

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
            RegisterDynamic(game.Hero.gameObject);
            foreach(var human in game.Humans) RegisterDynamic(human.gameObject);
            foreach(var item in game.ExtraItems) RegisterDynamic(item.gameObject);
            foreach(var text in FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
                if(!text.GetComponentInParent<SparePart>() && !text.GetComponentInParent<HumanAgent>() && !text.GetComponentInParent<ExpeditionPickup>()) labels.Add(text);
            var camera=FindAnyObjectByType<Camera>();
            if(camera) { camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Darkness; }
            Refresh();
        }
        public void RegisterDynamic(GameObject root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>(true);
            var block=new MaterialPropertyBlock(); block.SetFloat("_FogDynamic",1);
            foreach(var r in renderers) if(!(r is MeshRenderer && r.GetComponent<TextMesh>())) r.SetPropertyBlock(block);
            groups.Add(new Group { Root=root.transform,Renderers=renderers,Actor=root.GetComponent<CharacterController>()!=null });
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
            float personal=game.IsDriving?2.2f:1.5f;
            if(Vector2.Distance(new Vector2(point.x,point.z),new Vector2(game.Target.x,game.Target.z))>personal && !Sight.InCone(game.Target,game.IsDriving?game.Car.forward:game.Hero.forward,point,game.IsDriving?14:Sight.HeroRange,Sight.HeroAngle)) return false;
            // Grid cells can straddle a wall. Check the exact point for labels/actors as well.
            Vector3 eye=ActorPosture.Eye(game.IsDriving?game.Car:game.Hero);
            Vector3 target=new Vector3(point.x,eye.y,point.z);
            return !Physics.Linecast(eye,target,EscapeGame.SolidMask);
        }
        public bool IsExplored(Vector3 point) { int i=Index(point); return i>=0 && pixels[i].r>0; }
        public bool IsVisible(Transform actor)
        {
            if(game==null || !actor || Index(actor.position)<0) return false;
            Transform viewer=game.IsDriving?game.Car:game.Hero;
            float personal=game.IsDriving?2.2f:1.5f;
            bool nearby=Vector2.Distance(new Vector2(actor.position.x,actor.position.z),new Vector2(game.Target.x,game.Target.z))<=personal;
            return (nearby || Sight.InCone(viewer.position,viewer.forward,actor.position,game.IsDriving?14:Sight.HeroRange,Sight.HeroAngle)) && Sight.VisibleBody(ActorPosture.Eye(viewer),actor);
        }
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
            Vector3 eye=ActorPosture.Eye(game.IsDriving?game.Car:game.Hero);
            float radius=game.IsDriving?14:Sight.HeroRange;
            int minX=Mathf.Max(0,Mathf.FloorToInt(eye.x-radius-Origin.x));
            int maxX=Mathf.Min(Width-1,Mathf.CeilToInt(eye.x+radius-Origin.x));
            int minZ=Mathf.Max(0,Mathf.FloorToInt(eye.z-radius-Origin.y));
            int maxZ=Mathf.Min(Height-1,Mathf.CeilToInt(eye.z+radius-Origin.y));
            for(int z=minZ;z<=maxZ;z++) for(int x=minX;x<=maxX;x++)
            {
                Vector3 destination=new Vector3(Origin.x+x+.5f,eye.y,Origin.y+z+.5f);
                Vector3 delta=destination-eye; float distance=delta.magnitude;
                if(distance>(game.IsDriving?2.2f:1.5f) && !Sight.InCone(eye,game.IsDriving?game.Car.forward:game.Hero.forward,destination,radius,Sight.HeroAngle)) continue;
                // Reveal the front surface of a wall, but not rooms behind it.
                bool blocked=distance>.01f && Physics.Raycast(eye,delta.normalized,out var hit,distance,EscapeGame.SolidMask) && hit.distance<distance-.7f;
                if(!blocked) pixels[z*Width+x]=new Color32(255,255,0,255);
            }
            int self=Index(game.Target); if(self>=0) pixels[self]=new Color32(255,255,0,255);
            map.SetPixels32(pixels); map.Apply(false);
            UpdateObjects();
        }
        void UpdateObjects()
        {
            for(int i=groups.Count-1;i>=0;i--)
            {
                var group=groups[i];
                if(!group.Root) { groups.RemoveAt(i); continue; }
                bool self=group.Root==game.Hero || (group.Root==game.Car && game.IsDriving);
                bool visible=self || (group.Actor?IsVisible(group.Root):IsVisible(group.Root.position)) || (group.Renderers.Length>0 && group.Renderers[0] is LineRenderer);
                bool bypass=self || (group.Actor && visible);
                if(bypass!=group.Bypass)
                {
                    group.Bypass=bypass; var block=new MaterialPropertyBlock(); block.SetFloat("_FogDynamic",1); block.SetFloat("_FogSelf",bypass?1:0);
                    foreach(var r in group.Renderers) if(r && !r.GetComponent<TextMesh>()) r.SetPropertyBlock(block);
                }
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
