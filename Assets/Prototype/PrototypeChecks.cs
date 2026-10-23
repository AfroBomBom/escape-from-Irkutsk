using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IrkutskPrototype
{
    // Opt-in checks run only when the player receives -prototype-smoke.
    public sealed class PrototypeChecks : MonoBehaviour
    {
        static readonly BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"-prototype-smoke")>=0 || Array.IndexOf(args,"-prototype-preview")>=0)
            {
                Application.runInBackground=true;
                new GameObject("Prototype verification").AddComponent<PrototypeChecks>();
            }
        }
        static object Get(object o,string name) => o.GetType().GetField(name,Hidden).GetValue(o);
        static void Set(object o,string name,object value) => o.GetType().GetField(name,Hidden).SetValue(o,value);
        static void Call(object o,string name) => o.GetType().GetMethod(name,Hidden).Invoke(o,null);
        static void Check(bool condition,string message)
        { if(!condition) throw new Exception("CHECK FAILED: "+message); Debug.Log("PASS: "+message); }
        static void Teleport(EscapeGame game,Vector3 p)
        { game.Controller.enabled=false; game.Hero.position=p; game.Controller.enabled=true; Physics.SyncTransforms(); }
        IEnumerator Start()
        {
            yield return null; yield return null;
            var game=EscapeGame.Instance;
            string[] args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"-prototype-preview")>=0)
            {
                if(Array.IndexOf(args,"-social-tour")>=0)
                {
                    foreach(var z in game.Zombies) z.enabled=false;
                    foreach(var human in game.Humans) human.enabled=false;
                    Teleport(game,new Vector3(-8,0,-2)); game.Fog.Refresh();
                }
                if(Array.IndexOf(args,"-patrol-tour")>=0)
                {
                    foreach(var z in game.Zombies) z.enabled=false;
                    foreach(var human in game.Humans) human.enabled=false;
                    Teleport(game,new Vector3(22,0,1)); game.Fog.Refresh();
                }
                if(Array.IndexOf(args,"-fog-tour")>=0)
                {
                    foreach(var z in game.Zombies) z.enabled=false;
                    Teleport(game,new Vector3(-10,0,-10)); game.Fog.Refresh();
                    Teleport(game,new Vector3(5,0,-10)); game.Fog.Refresh();
                }
                yield return new WaitUntil(()=>UnityEngine.Rendering.SplashScreen.isFinished);
                yield return new WaitForSeconds(1);
                int index=Array.IndexOf(args,"-capture-path");
                string path=index>=0 && index+1<args.Length?args[index+1]:"prototype-preview.png";
                if(Array.IndexOf(args,"-combat-tour")>=0)
                {
                    foreach(var enemy in game.Zombies) enemy.enabled=false;
                    foreach(var human in game.Humans) human.enabled=false;
                    Teleport(game,new Vector3(22,0,1));
                    game.Hero.forward=(game.Soldier.transform.position-game.Hero.position).normalized;
                    game.enabled=false; game.Fog.Refresh(); game.SoldierShot(game.Soldier);
                }
                else { game.Hero.rotation=Quaternion.identity; game.enabled=false; game.Fog.Refresh(); }
                // Explicit offscreen render also works when Windows hides the test window.
                var camera=FindAnyObjectByType<Camera>();
                camera.transform.position=game.Target+new Vector3(0,27,-12.6f);
                var target=new RenderTexture(1280,720,24);
                target.Create();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                    new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination=target });
                var previous=RenderTexture.active; RenderTexture.active=target;
                var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
                System.IO.File.WriteAllBytes(path,image.EncodeToPNG());
                RenderTexture.active=previous; target.Release(); Destroy(target); Destroy(image);
                yield return new WaitForSeconds(2);
                Debug.Log("IRKUTSK_PREVIEW_OK"); Application.Quit(0); yield break;
            }
            try { Run(game); Debug.Log("IRKUTSK_SMOKE_OK"); Application.Quit(0); }
            catch(Exception e) { Debug.LogException(e); Application.Quit(1); }
        }
        void Run(EscapeGame game)
        {
            Check(game.Parts.Count==4 && game.Zombies.Count==8,"world and actors spawned");
            foreach(var enemy in game.Zombies) enemy.enabled=false;
            foreach(var human in game.Humans) human.enabled=false;
            CombatChecks.Run(game);
            ExpeditionChecks.Run(game);
            Check(game.Fog!=null,"fog initialized");
            Teleport(game,new Vector3(0,0,-10)); game.Fog.Refresh();
            Vector3 remembered=new Vector3(0,0,-10);
            Check(game.Fog.IsVisible(remembered) && game.Fog.IsExplored(remembered),"current view is revealed");
            Check(!game.Fog.IsExplored(new Vector3(90,0,0)),"unvisited highway is unknown");
            Teleport(game,new Vector3(20,0,-10)); game.Fog.Refresh();
            Check(game.Fog.IsExplored(remembered) && !game.Fog.IsVisible(remembered),"departed ground remains explored but not visible");
            Check(!game.Zombies[0].GetComponentInChildren<Renderer>().enabled,"offscreen infected hidden");
            Check(!game.Parts[0].GetComponent<Renderer>().enabled,"offscreen loot hidden");
            Teleport(game,new Vector3(4,0,3)); game.Fog.Refresh();
            Check(!game.Fog.IsVisible(new Vector3(4,0,11)),"fog view does not pass through building");
            Teleport(game,new Vector3(4,0,11)); game.Fog.Refresh();
            Check(!game.Fog.IsVisible(new Vector3(4,0,9.3f)),"exact visibility rejects objects just behind a wall");
            Teleport(game,remembered); game.Fog.Refresh();
            Check(game.Fog.IsVisible(remembered),"returning restores current view");
            var z=game.Zombies[0]; var cc=z.GetComponent<CharacterController>();
            cc.enabled=false; z.transform.position=Vector3.zero; z.transform.rotation=Quaternion.identity; cc.enabled=true;
            Teleport(game,new Vector3(0,0,3));
            Set(z,"alertUntil",0f); Set(game,"footAt",0f);
            if(Keyboard.current==null) InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W,Key.LeftCtrl)); InputSystem.Update();
            Call(game,"Walk");
            Check((float)Get(z,"alertUntil")==0,"Ctrl movement makes no audible footsteps");
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W)); InputSystem.Update();
            Set(game,"footAt",0f); Call(game,"Walk");
            Check((float)Get(z,"alertUntil")>Time.time,"ordinary footsteps alert nearby infected");
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState()); InputSystem.Update();
            Teleport(game,new Vector3(0,0,5.5f)); Set(z,"alertUntil",0f); z.transform.rotation=Quaternion.identity;
            Call(z,"Update"); Check((float)Get(z,"alertUntil")>Time.time,"infected sees player in view cone");
            cc.enabled=false; z.transform.position=new Vector3(4,0,3); cc.enabled=true;
            Teleport(game,new Vector3(4,0,11)); z.transform.rotation=Quaternion.identity; Set(z,"alertUntil",0f); Physics.SyncTransforms();
            Call(z,"Update"); Check((float)Get(z,"alertUntil")==0,"building blocks line of sight");
            cc.enabled=false; z.transform.position=new Vector3(0,0,0); cc.enabled=true;
            Teleport(game,new Vector3(0,0,-4)); game.Hero.rotation=Quaternion.identity; Physics.SyncTransforms();
            Set(game,"shotAt",0f); Call(game,"Shoot");
            Check((float)Get(z,"stunnedUntil")>Time.time && (float)Get(z,"health")==80,"pistol damages and stuns target");
            Teleport(game,new Vector3(0,0,-1)); Set(game,"attackAt",0f); Call(game,"Punch");
            Check((float)Get(z,"health")==55,"punch reaches nearby target");
            z.Hit(100,1,Vector3.zero); Check(z.Downed && !cc.enabled,"defeated infected stops blocking movement");
            var bag=(List<int>)Get(game,"bag");
            for(int i=0;i<2;i++) { Teleport(game,game.Parts[i].transform.position); Call(game,"Interact"); }
            Check(bag.Count==2,"two parts fit backpack");
            Teleport(game,game.Parts[2].transform.position); Call(game,"Interact");
            Check(bag.Count==2 && game.Parts[2].gameObject.activeSelf,"full backpack rejects third part");
            Teleport(game,game.Car.position+Vector3.right*3); Call(game,"Interact"); Call(game,"Interact");
            Check(bag.Count==0 && (int)game.GetType().GetMethod("InstalledCount",Hidden).Invoke(game,null)==2,"garage installs carried parts");
            for(int i=2;i<4;i++) { Teleport(game,game.Parts[i].transform.position); Call(game,"Interact"); }
            Teleport(game,game.Car.position+Vector3.right*3); Call(game,"Interact"); Call(game,"Interact"); Call(game,"Interact");
            Check(game.IsDriving && !game.Hero.gameObject.activeSelf,"completed car can be entered");
            game.Car.position=new Vector3(107,0,0); Call(game,"Drive");
            Check((bool)Get(game,"finished"),"crossing highway finish wins run");
        }
    }
}
