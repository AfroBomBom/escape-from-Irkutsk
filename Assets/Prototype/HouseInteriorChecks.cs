using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
namespace IrkutskPrototype
{
    public sealed class HouseInteriorChecks : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool ok,string text) { if(!ok) throw new Exception("CHECK FAILED: "+text); Debug.Log("PASS: "+text); }
        static object Get(object obj,string field)=>obj.GetType().GetField(field,Hidden).GetValue(obj);
        static object Call(object obj,string name)=>obj.GetType().GetMethod(name,Hidden).Invoke(obj,null);
        static void Place(EscapeGame game,Vector3 point)
        { game.Controller.enabled=false; game.Hero.position=point; game.Controller.enabled=true; Physics.SyncTransforms(); }
        static void PressE(EscapeGame game)
        {
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.E)); InputSystem.Update(); Call(game,"Update");
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState()); InputSystem.Update();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-house-interior-check")<0) return;
            if(UnityEngine.Object.FindAnyObjectByType<HouseInteriorChecks>()) return;
            Application.runInBackground=true;
            var root=new GameObject("House interior verification"); DontDestroyOnLoad(root); root.AddComponent<HouseInteriorChecks>();
        }
        IEnumerator Start()
        {
            yield return null; yield return null;
            if(Keyboard.current==null) InputSystem.AddDevice<Keyboard>();
            var game=EscapeGame.Instance;
            try
            {
                Run(game);
                var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"-interior-preview");
                if(index>=0 && index+1<args.Length) Capture(game,args[index+1]);
            }
            catch(Exception ex) { Debug.LogException(ex); Application.Quit(1); yield break; }
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            yield return null; yield return null;
            try
            {
                game=EscapeGame.Instance;
                Check(game.HouseContainers.Count==game.GeneratedHouses.Count*2 && game.HouseContainers.TrueForAll(c=>!c.Empty),"new run restores every searchable container");
                Check(((List<int>)Get(game,"bag")).Count==0,"new run restores the empty starter backpack");
                Debug.Log("IRKUTSK_HOUSE_INTERIORS_OK"); Application.Quit(0);
            }
            catch(Exception ex) { Debug.LogException(ex); Application.Quit(1); }
        }
        static void Capture(EscapeGame game,string path)
        {
            var house=game.GeneratedHouses.Find(h=>h.Name=="Large house");
            Place(game,house.Center); game.Hero.forward=Vector3.forward*house.BackSign;
            Call(game,"UpdateHouseWalls"); game.Fog.Refresh();
            var camera=UnityEngine.Object.FindAnyObjectByType<Camera>();
            camera.transform.position=game.Hero.position+new Vector3(0,22,-10.3f); camera.orthographicSize=11;
            var target=new RenderTexture(1280,720,24); target.Create();
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination=target });
            var previous=RenderTexture.active; RenderTexture.active=target;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
            System.IO.File.WriteAllBytes(path,image.EncodeToPNG());
            RenderTexture.active=previous; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image);
        }
        static void Run(EscapeGame game)
        {
            game.enabled=false;
            foreach(var h in game.Humans) { h.enabled=false; h.gameObject.SetActive(false); }
            foreach(var z in game.Zombies) { z.enabled=false; z.gameObject.SetActive(false); }
            var bag=(List<int>)Get(game,"bag"); bag.Clear(); Physics.SyncTransforms();
            Check(game.HouseContainers.Count==game.GeneratedHouses.Count*2,"every generated house has two searchable containers");
            foreach(var container in game.HouseContainers)
                Check(container && (container.ItemKind==4 || container.ItemKind==5) && !container.Empty,"container uses an existing medicine or armor item");
            string[] furniture={"table","chair","bed","sofa","wardrobe","nightstand"};
            for(int room=0;room<game.GeneratedHouses.Count;room++)
            {
                var house=game.GeneratedHouses[room]; string name=house.Name;
                foreach(var kind in furniture) Check(GameObject.Find(name+" "+kind),name+" contains "+kind);
                float side=house.Width/2-.65f;
                Vector3 outside=house.OutsideDoor;
                for(int shelf=0;shelf<2;shelf++)
                {
                    Place(game,outside); Vector3 forward=Vector3.forward*house.BackSign;
                    for(int i=0;i<44;i++) game.Controller.Move(forward*.125f+Vector3.down*.04f);
                    float expectedZ=house.Center.z+house.BackSign*(house.Depth==6?1f:1.5f);
                    // For the larger room the entrance begins 4.5 m away; 44 steps end at +1 m.
                    Check(Mathf.Abs(game.Hero.position.z-expectedZ)<.2f,name+" central passage remains walkable to rear of room");
                    // Move a little farther back in the large room, past the bed and sofa.
                    if(house.Depth==6) for(int i=0;i<4;i++) game.Controller.Move(forward*.125f+Vector3.down*.04f);
                    float direction=shelf==0?-1:1, lateral=side-.9f;
                    for(float moved=0;moved<lateral-.02f;moved+=.1f) game.Controller.Move(Vector3.right*direction*Mathf.Min(.1f,lateral-moved));
                    var target=game.HouseContainers[room*2+shelf];
                    Check(target.Contains(game.Hero.position) && Vector3.Distance(game.Hero.position,target.InteractionPoint)<1.4f,name+" "+target.DisplayName+" can be physically approached");
                    if(room==0 && shelf==0)
                    {
                        bag.Add(0); bag.Add(1); PressE(game);
                        Check(!target.Empty && bag.Count==2,"full backpack leaves container contents intact");
                        bag.Clear();
                    }
                    int item=target.ItemKind; PressE(game);
                    Check(target.Empty && bag.Count==1 && bag[0]==item && target.Caption.text.Contains("ПУСТО"),name+" "+target.DisplayName+" gives existing item with E and shows empty");
                    PressE(game); Check(bag.Count==1,"searched "+target.DisplayName+" cannot give the item twice"); bag.Clear();
                }
            }
            var last=game.HouseContainers[game.HouseContainers.Count-1];
            Place(game,new Vector3(last.RoomCenter.x+last.RoomSize.x/2+.3f,0,last.InteractionPoint.z));
            Check(!(bool)Call(game,"TryHouseInteraction") && bag.Count==0,"outside player cannot loot furniture through exterior wall");
            Place(game,new Vector3(-9,0,-10));
            Check(!(bool)Call(game,"TryHouseInteraction"),"no container can be looted remotely");
            var large=game.GeneratedHouses.Find(h=>h.Name=="Large house");
            var southWall=GameObject.Find(large.Name+(large.NorthEntrance?" back":" front left")); var southRenderer=southWall.GetComponent<Renderer>();
            Place(game,large.Center); Call(game,"UpdateHouseWalls");
            Check(!southRenderer.enabled && southWall.GetComponent<Collider>().enabled,"camera-facing wall becomes invisible inside while its collider remains solid");
            float wallX=large.NorthEntrance?large.Center.x:large.Center.x-(large.Width+2.6f)/4;
            float southZ=large.Center.z-large.Depth/2;
            Check(Physics.Linecast(new Vector3(wallX,1,southZ+.8f),new Vector3(wallX,1,southZ-.8f),EscapeGame.SolidMask),"invisible wall still blocks physical sight");
            Place(game,new Vector3(-9,0,-10)); Call(game,"UpdateHouseWalls");
            Check(southRenderer.enabled,"leaving house restores camera-facing wall rendering");
        }
    }
}
