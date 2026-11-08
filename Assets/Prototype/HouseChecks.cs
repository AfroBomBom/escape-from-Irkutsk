using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
namespace IrkutskPrototype
{
    public sealed class HouseChecks : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool ok,string text) { if(!ok) throw new Exception("CHECK FAILED: "+text); Debug.Log("PASS: "+text); }
        static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Hidden).Invoke(obj,args);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-house-check")<0) return;
            Application.runInBackground=true; new GameObject("Enterable house verification").AddComponent<HouseChecks>();
        }
        IEnumerator Start()
        {
            yield return null; yield return null;
            try
            {
                var game=EscapeGame.Instance; Run(game);
                string[] args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"-house-preview");
                if(index>=0 && index+1<args.Length) Capture(game,args[index+1]);
                Debug.Log("IRKUTSK_HOUSES_OK"); Application.Quit(0);
            }
            catch(Exception ex) { Debug.LogException(ex); Application.Quit(1); }
        }
        static void Capture(EscapeGame game,string path)
        {
            game.Noise(game.Hero.position,2.5f,game.Hero,NoiseKind.Movement);
            var camera=UnityEngine.Object.FindAnyObjectByType<Camera>();
            camera.transform.position=game.Hero.position+new Vector3(0,22,-10.3f); camera.orthographicSize=11;
            var target=new RenderTexture(1280,720,24); target.Create();
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination=target });
            var previous=RenderTexture.active; RenderTexture.active=target;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
            System.IO.File.WriteAllBytes(path,image.EncodeToPNG());
            RenderTexture.active=previous; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image);
        }
        static void Run(EscapeGame game)
        {
            game.enabled=false;
            foreach(var h in game.Humans) { h.enabled=false; h.gameObject.SetActive(false); }
            foreach(var z in game.Zombies) { z.enabled=false; z.gameObject.SetActive(false); }
            Physics.SyncTransforms();
            var names=new[]{"Small house","Medium house","Large house"};
            var outside=new[]{new Vector3(-22,0,17.5f),new Vector3(-2,0,-17),new Vector3(26,0,-16.5f)};
            var inside=new[]{new Vector3(-22,0,21),new Vector3(-2,0,-21),new Vector3(26,0,-21)};
            for(int i=0;i<names.Length;i++)
            {
                var floor=GameObject.Find(names[i]+" floor"); var back=GameObject.Find(names[i]+" back");
                Check(floor && back && back.layer==8,names[i]+" has interior floor and solid walls");
                Check(GridRoute.Find(new Vector3(-9,0,-10),outside[i]).Count>0,names[i]+" entrance is reachable from garage area");
                var cc=game.Controller; cc.enabled=false; game.Hero.position=outside[i]; game.Hero.forward=(inside[i]-outside[i]).normalized; cc.enabled=true; Physics.SyncTransforms();
                Vector3 direction=(inside[i]-outside[i]).normalized;
                for(int step=0;step<32;step++) cc.Move(direction*.125f+Vector3.down*.05f);
                Check(Vector3.Distance(game.Hero.position,inside[i])<1.1f,names[i]+" doorway physically allows player into the room");
                Check(!Physics.Linecast(outside[i]+Vector3.up,inside[i]+Vector3.up,EscapeGame.SolidMask),names[i]+" open door does not block vision or sound");
                game.Fog.Refresh(); Check(game.Fog.IsVisible(inside[i]),names[i]+" interior is revealed from inside");
            }
            var small=GameObject.Find("Small house floor").transform.localScale;
            var medium=GameObject.Find("Medium house floor").transform.localScale;
            var large=GameObject.Find("Large house floor").transform.localScale;
            Check(small.x*small.z<medium.x*medium.z && medium.x*medium.z<large.x*large.z,"houses provide three distinct floor areas");
            Check(GameObject.Find("Small house back").transform.localScale.y<GameObject.Find("Large house back").transform.localScale.y,"houses also differ in wall height");
            Vector3 inSmall=new Vector3(-22,0,21),eastOutside=new Vector3(-18,0,21);
            Check(Physics.Linecast(inSmall+Vector3.up,eastOutside+Vector3.up,EscapeGame.SolidMask),"house walls block direct sight");
            Check(!(bool)Call(game,"PlayerNoiseReaches",inSmall,eastOutside,8f),"house wall attenuates player noise across it");
            Check((bool)Call(game,"PlayerNoiseReaches",new Vector3(-22,0,20),new Vector3(-22,0,17),8f),"noise passes through an open doorway without attenuation");
            foreach(var part in game.Parts)
                Check(GridRoute.Find(new Vector3(-9,0,-10),part.transform.position).Count>0,"repair part "+part.Kind+" remains reachable from garage area");
            Check(GridRoute.Find(new Vector3(-9,0,-10),new Vector3(29,0,0)).Count>0,"route to city exit remains reachable");
        }
    }
}
