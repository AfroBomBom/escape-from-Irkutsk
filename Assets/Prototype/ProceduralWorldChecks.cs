using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace IrkutskPrototype
{
    public sealed class ProceduralWorldChecks : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool ok,string message) { if(!ok) throw new Exception("CHECK FAILED: "+message); Debug.Log("PASS: "+message); }
        static void Call(object obj,string method)=>obj.GetType().GetMethod(method,Hidden).Invoke(obj,null);
        static string Houses(EscapeGame game)
        {
            var result=new StringBuilder();
            foreach(var h in game.GeneratedHouses) result.Append(h.Center).Append(h.Width).Append(h.Depth).Append(h.NorthEntrance);
            return result.ToString();
        }
        static string People(EscapeGame game)
        {
            var result=new StringBuilder();
            foreach(var position in game.InitialNpcPositions) result.Append(Vector3Int.RoundToInt(position));
            return result.ToString();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-procedural-world-check")<0 || UnityEngine.Object.FindAnyObjectByType<ProceduralWorldChecks>()) return;
            Application.runInBackground=true; var root=new GameObject("Procedural world verification"); DontDestroyOnLoad(root); root.AddComponent<ProceduralWorldChecks>();
        }
        IEnumerator Start()
        {
            yield return null; yield return null;
            if(Keyboard.current==null) InputSystem.AddDevice<Keyboard>();
            var game=EscapeGame.Instance; string houses=Houses(game),people=People(game); int seed=game.WorldSeed;
            try
            {
                Audit(game);
                var args=Environment.GetCommandLineArgs(); int preview=Array.IndexOf(args,"-procedural-preview");
                if(preview>=0 && preview+1<args.Length) Capture(game,args[preview+1]);
            }
            catch(Exception ex) { Debug.LogException(ex); Application.Quit(1); yield break; }
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.F5)); InputSystem.Update(); Call(game,"Update");
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState()); InputSystem.Update();
            yield return null; yield return null;
            try
            {
                game=EscapeGame.Instance; Audit(game);
                bool pinned=Array.IndexOf(Environment.GetCommandLineArgs(),"-world-seed")>=0;
                Check(pinned?(game.WorldSeed==seed && Houses(game)==houses):(game.WorldSeed!=seed && Houses(game)!=houses),"F5 regenerates randomized house layout; an explicit seed reproduces it");
                Check(pinned?People(game)==people:People(game)!=people,"F5 also regenerates NPC positions or reproduces them with an explicit seed");
                Debug.Log("IRKUTSK_PROCEDURAL_WORLD_OK"); Application.Quit(0);
            }
            catch(Exception ex) { Debug.LogException(ex); Application.Quit(1); }
        }
        static void Capture(EscapeGame game,string path)
        {
            var camera=UnityEngine.Object.FindAnyObjectByType<Camera>();
            Vector3 position=camera.transform.position; Quaternion rotation=camera.transform.rotation;
            float size=camera.orthographicSize,far=camera.farClipPlane;
            camera.transform.position=new Vector3(-18.5f,160,0); camera.transform.rotation=Quaternion.Euler(90,0,0);
            camera.orthographicSize=54; camera.farClipPlane=300; Shader.SetGlobalFloat("_ExplorationEnabled",0);
            var target=new RenderTexture(1280,720,24); target.Create();
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination=target });
            var previous=RenderTexture.active; RenderTexture.active=target;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
            System.IO.File.WriteAllBytes(path,image.EncodeToPNG());
            RenderTexture.active=previous; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image);
            camera.transform.position=position; camera.transform.rotation=rotation; camera.orthographicSize=size; camera.farClipPlane=far;
            Shader.SetGlobalFloat("_ExplorationEnabled",1);
        }
        static void Audit(EscapeGame game)
        {
            game.enabled=false;
            foreach(var h in game.Humans) h.enabled=false;
            foreach(var z in game.Zombies) z.enabled=false;
            Check(game.GeneratedHouses.Count==9 && game.HouseContainers.Count==18,"nine varied furnished houses create eighteen existing-item containers");
            Check(game.Humans.Count==4 && game.Citizen && game.Medic && game.Soldier && game.Zombies.Count==12,"all required NPC roles and city/highway infected spawn");
            Check(Vector3.Distance(game.Hero.position,game.Citizen.transform.position)<=12 && Vector3.Distance(game.Hero.position,game.Citizen.transform.position)>=3.5f,"quest civilian remains near but not on the player");
            Check(Vector3.Distance(game.Soldier.transform.position,game.Medic.transform.position)<=5.5f && Sight.VisibleBody(ActorPosture.Eye(game.Soldier.transform),game.Medic.transform),"soldier and doctor spawn as a visible nearby pair");
            Check(game.PatrolRoute!=null && game.PatrolRoute.Length==5,"patrol has a shared generated route");
            foreach(var waypoint in game.PatrolRoute)
            {
                Check(game.IsConnectedCityPoint(waypoint),"generated patrol waypoint is on reachable city ground");
                Check(Vector3.Distance(waypoint,game.InitialNpcPositions[1])<=22.01f,"patrol waypoint stays within the pair's local district");
            }
            foreach(var house in game.GeneratedHouses)
            {
                Check(game.IsConnectedCityPoint(house.OutsideDoor),house.Name+" entrance connects to the city");
                Check(house.Center.x-house.Width/2>-67 && house.Center.x+house.Width/2<30 && Mathf.Abs(house.Center.z)+house.Depth/2<47,house.Name+" remains inside enlarged city bounds");
                foreach(var other in game.GeneratedHouses)
                    if(other!=house) Check(Mathf.Abs(other.Center.x-house.Center.x)>(other.Width+house.Width)/2+1 || Mathf.Abs(other.Center.z-house.Center.z)>(other.Depth+house.Depth)/2+1,house.Name+" does not overlap another house");
            }
            var positions=new List<Vector3>();
            foreach(var h in game.Humans)
            {
                Check(game.IsConnectedCityPoint(h.transform.position),h.Role+" spawns in the reachable city");
                positions.Add(h.transform.position);
            }
            for(int i=0;i<game.Zombies.Count;i++)
            {
                var p=game.Zombies[i].transform.position;
                Check(i<7?game.IsConnectedCityPoint(p):(p.x>31 && Mathf.Abs(p.z)<6),"infected "+i+" spawns in its city or highway region");
                positions.Add(p);
            }
            for(int i=0;i<positions.Count;i++)
            {
                var p=positions[i];
                Check(!Physics.CheckBox(p+Vector3.up,new Vector3(.32f,.55f,.32f),Quaternion.identity,EscapeGame.SolidMask,QueryTriggerInteraction.Ignore),"NPC "+i+" does not spawn in solid scenery");
                Check(Vector3.Distance(p,game.Hero.position)>3,"NPC "+i+" does not spawn on top of hero");
                for(int j=0;j<i;j++) Check(Vector3.Distance(positions[j],p)>1.3f,"NPC "+i+" does not overlap NPC "+j);
            }
            Check(game.IsConnectedCityPoint(new Vector3(29,0,0)),"city exit remains connected to garage");
            foreach(var part in game.Parts) Check(game.IsConnectedCityPoint(part.transform.position),"required repair part remains on reachable city ground");
            Check(GridRoute.Find(new Vector3(29,0,0),new Vector3(105,0,0)).Count>0,"highway route reaches finish");
            Check(NpcTactics.Walkable(new Vector3(-60,0,-42)) || game.IsConnectedCityPoint(new Vector3(-60,0,-42)),"navigation covers the newly expanded west district");
            Vector3 old=game.Hero.position; var cc=game.Controller; cc.enabled=false; game.Hero.position=new Vector3(-60,0,-43); cc.enabled=true;
            game.Hero.forward=Vector3.forward; Physics.SyncTransforms(); game.Fog.Refresh();
            Check(game.Fog.IsVisible(game.Hero.position),"fog of war covers expanded city instead of hiding its far edge");
            cc.enabled=false; game.Hero.position=old; cc.enabled=true; Physics.SyncTransforms();
        }
    }
}
