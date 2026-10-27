using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
namespace IrkutskPrototype
{
    public sealed class ShotIntentChecks : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        EscapeGame game; HumanAgent soldier; Material clothing;
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Hidden).SetValue(o,value);
        static object Get(object o,string name)=>o.GetType().GetField(name,Hidden).GetValue(o);
        static void Call(object o,string name)=>o.GetType().GetMethod(name,Hidden).Invoke(o,null);
        static void Check(bool ok,string message)
        { if(!ok) throw new Exception("CHECK FAILED: "+message); Debug.Log("PASS: "+message); }
        static void Place(Transform actor,Vector3 point)
        { var cc=actor.GetComponent<CharacterController>(); if(cc) cc.enabled=false; actor.position=point; if(cc) cc.enabled=true; Physics.SyncTransforms(); }
        void Reset(Vector3 soldierPosition)
        {
            foreach(var h in game.Humans.ToArray())
            {
                h.enabled=false; h.gameObject.SetActive(false);
                if(h.name.StartsWith("Intent check")) { game.Humans.Remove(h); Destroy(h.gameObject); }
            }
            foreach(var z in game.Zombies.ToArray())
            {
                z.enabled=false; z.gameObject.SetActive(false);
                if(z.name.StartsWith("Intent check")) { game.Zombies.Remove(z); Destroy(z.gameObject); }
            }
            Place(game.Hero,new Vector3(20,0,0)); game.Hero.forward=Vector3.forward;
            Set(game,"hp",100f); Set(game,"<PatrolHostile>k__BackingField",false); Set(game,"<Scanned>k__BackingField",false);
            soldier=game.SpawnHuman("Intent check soldier",HumanRole.Soldier,soldierPosition,clothing);
            soldier.enabled=false; soldier.transform.forward=(game.Hero.position-soldier.transform.position).normalized;
            game.Soldier=soldier; Set(soldier,"shotAt",Time.time+100);
        }
        Infected Zombie(Vector3 position)
        {
            var root=new GameObject("Intent check infected"); root.transform.position=position;
            var cc=root.AddComponent<CharacterController>(); cc.height=1.8f; cc.radius=.35f; cc.center=Vector3.up*.9f;
            var z=root.AddComponent<Infected>(); z.enabled=false; game.Zombies.Add(z); Physics.SyncTransforms(); return z;
        }
        void Fire()
        {
            soldier.enabled=true; Set(game,"shotAt",0f); Set(game,"reloadAt",0f); Set(game,"ammo",6);
            Physics.SyncTransforms(); Call(game,"Shoot");
        }
        GameObject Wall(float z)
        { var root=GameObject.CreatePrimitive(PrimitiveType.Cube); root.layer=8; root.transform.position=new Vector3(20,1,z); root.transform.localScale=new Vector3(8,2,.3f); Physics.SyncTransforms(); return root; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-shot-intent-check")<0) return;
            Application.runInBackground=true; new GameObject("Shot direction verification").AddComponent<ShotIntentChecks>();
        }
        IEnumerator Start()
        {
            yield return null; yield return null;
            var tests=Run(EscapeGame.Instance);
            while(true)
            {
                object wait;
                try { if(!tests.MoveNext()) break; wait=tests.Current; }
                catch(Exception e) { Debug.LogException(e); Application.Quit(1); yield break; }
                yield return wait;
            }
            Debug.Log("IRKUTSK_SHOT_INTENT_OK"); Application.Quit(0);
        }
        IEnumerator Run(EscapeGame instance)
        {
            game=instance; game.enabled=false; clothing=game.Soldier.GetComponentInChildren<Renderer>().sharedMaterial;
            Reset(new Vector3(21.2f,0,4)); var z=Zombie(new Vector3(20.8f,0,7));
            var zone=soldier.GetComponent<ShotReactionZone>();
            Check(zone && z.GetComponent<ShotReactionZone>() && soldier.GetComponents<Collider>().Length==1,"units receive intent zones without added physical colliders");
            Check(game.ShotReactionRadius(soldier.transform)==2,"nearby aggressive unit keeps soldier zone at two metres");
            yield return null; Fire();
            Check(game.LastShotHadAggressiveTarget && !game.PatrolHostile && soldier.Health==100,"soldier zone before infected zone permits defensive fire without body hit");

            Reset(new Vector3(21.2f,0,6)); Zombie(new Vector3(20.8f,0,3)); yield return null; Fire();
            Check(game.LastShotHadAggressiveTarget && !game.PatrolHostile,"infected zone before soldier zone also permits defensive fire");

            Reset(new Vector3(21.2f,0,4));
            var marauder=game.SpawnHuman("Intent check marauder",HumanRole.Marauder,new Vector3(20,0,7),clothing); marauder.enabled=false;
            yield return null; Fire();
            Check(game.LastShotHadAggressiveTarget && !game.PatrolHostile && marauder.Health==80,"actual shot at hostile marauder behind patrol does not condemn player: aggressive="+game.LastShotHadAggressiveTarget+" hostile="+game.PatrolHostile+" hp="+marauder.Health);

            Reset(new Vector3(22.5f,0,4));
            Check(game.ShotReactionRadius(soldier.transform)==3,"soldier zone expands to three metres without nearby aggressors");
            yield return null; Fire(); Check(game.PatrolHostile && game.LastShotProtectedTarget==soldier.transform,"empty scene near miss inside expanded soldier zone is threatening");

            Reset(new Vector3(22.5f,0,4)); z=Zombie(new Vector3(26,0,5)); yield return null; Fire();
            Check(!game.LastShotHadAggressiveTarget && !game.PatrolHostile && game.ShotReactionRadius(soldier.transform)==2,"nearby aggressor shrinks soldier zone even when outside shot corridor");
            z.gameObject.SetActive(false); Set(game,"<PatrolHostile>k__BackingField",false); yield return null; Fire();
            Check(game.PatrolHostile && game.ShotReactionRadius(soldier.transform)==3,"removing nearby aggressor restores expanded near miss zone");

            Reset(new Vector3(24.5f,0,4)); yield return null; Fire();
            Check(!game.PatrolHostile && !game.LastShotProtectedTarget && soldier.HasSoundClue,"shot missing all zones creates only a sound investigation");
            Check(soldier.Health==100,"intent zones do not intercept bullets or apply damage");

            Reset(new Vector3(24.5f,0,4)); soldier.transform.forward=Vector3.forward; Set(soldier,"shotAt",0f); int shots=game.SoldierTracers; yield return null; Fire();
            yield return new WaitForSeconds(2.2f);
            Check(soldier.CurrentTarget==game.Hero && !game.PatrolHostile && game.SoldierTracers==shots,"finding shooter after harmless noise does not enable lethal aggression");

            Reset(new Vector3(20,0,4)); z=Zombie(new Vector3(20,0,5.5f)); soldier.transform.forward=Vector3.forward; yield return null; Fire();
            Check(game.PatrolHostile && soldier.Health==90 && soldier.CurrentTarget==game.Hero,"actual soldier hit always provokes response even when infected zone overlaps ray");

            Reset(new Vector3(21.2f,0,3.5f)); z=Zombie(new Vector3(20,0,3)); Set(z,"health",10f); yield return null; Fire();
            Check(z.Downed && game.LastShotHadAggressiveTarget && !game.PatrolHostile,"killing infected is classified before death and does not cause retroactive aggression");

            Reset(new Vector3(21.2f,0,4)); Zombie(new Vector3(20.8f,0,7)); var wall=Wall(5.5f); yield return null; Fire();
            Check(!game.LastShotHadAggressiveTarget && game.PatrolHostile,"aggressor hidden behind wall cannot justify a threatening shot at patrol zone");
            wall.SetActive(false); Destroy(wall); Physics.SyncTransforms();

            Reset(new Vector3(21.2f,0,4)); wall=Wall(2); yield return null; Fire();
            Check(!game.PatrolHostile,"wall before protected unit prevents invented direct threat");
            wall.SetActive(false); Destroy(wall); Physics.SyncTransforms();

            Reset(new Vector3(21.2f,0,4)); z=Zombie(new Vector3(20.8f,0,-3)); yield return null; Fire();
            Check(!game.LastShotHadAggressiveTarget && game.PatrolHostile,"aggressive unit behind shooter does not justify forward threat");

            Reset(new Vector3(21.2f,0,4)); z=Zombie(new Vector3(20.8f,0,7)); Set(z,"<Downed>k__BackingField",true); yield return null; Fire();
            Check(!game.LastShotHadAggressiveTarget && game.PatrolHostile,"dead infected does not excuse fire toward soldier zone");

            Reset(new Vector3(20,0,-1)); yield return null; Fire();
            Check(!game.PatrolHostile,"shooting away while standing within soldier zone is not aimed aggression");

            Reset(new Vector3(21.2f,0,4)); Zombie(new Vector3(20.8f,0,7)); Set(game,"<PatrolHostile>k__BackingField",true); yield return null; Fire();
            Check(game.PatrolHostile,"defensive looking shot does not erase an already established hostility");

            Reset(new Vector3(27,0,7)); var medic=game.SpawnHuman("Intent check medic",HumanRole.Medic,new Vector3(23,0,3),clothing); medic.enabled=false;
            Check(game.ShotReactionRadius(medic.transform)==2,"expanded calm zone belongs to soldier while medic keeps default radius");
            Set(game,"<PatrolHostile>k__BackingField",false); Place(game.Hero,new Vector3(20,0,0)); game.Hero.forward=Vector3.forward;
            var civilian=game.SpawnHuman("Intent check civilian",HumanRole.Civilian,new Vector3(20,0,3),clothing); civilian.enabled=false;
            medic.transform.forward=(game.Hero.position-medic.transform.position).normalized; yield return null; Fire();
            Check(game.PatrolHostile && civilian.Health==80,"witnessed direct civilian hit remains assault rather than harmless noise");
            Reset(new Vector3(21.2f,0,24)); yield return null; Fire();
            Check(soldier.HasSoundClue && !game.PatrolHostile && !soldier.CurrentTarget,"passing bullet alerts distant patrol without identifying unseen shooter");
            Check(Vector3.Distance(soldier.LastHeardPosition,game.Hero.position)<2 && (float)Get(soldier,"soundUntil")-Time.time>19,"passing shot stores source snapshot and twenty second search");
            Place(game.Hero,new Vector3(-20,0,-20)); Vector3 snapshot=soldier.LastHeardPosition; float before=soldier.transform.position.z;
            yield return new WaitForSeconds(1.2f);
            Check(soldier.transform.position.z<before-.4f && soldier.State=="Проверяю шум","distant soldier moves toward the shot source rather than continuing patrol");
            Check(soldier.LastHeardPosition==snapshot,"hidden shooter movement does not update bullet sound snapshot");
            yield return new WaitForSeconds(8.2f);
            Check(soldier.HasSoundClue && !game.PatrolHostile,"gunfire search remains active after the previous eight second limit");
            soldier.HearNoise(soldier.transform.position,2.5f,null,NoiseKind.Movement);
            Check(soldier.LastHeardPosition==snapshot,"quiet movement cannot replace active gunfire investigation");
            Set(soldier,"soundUntil",Time.time-.1f); Set(soldier,"gunfireSearchUntil",Time.time-.1f); Set(soldier,"heardAt",Time.time-21);
            yield return null;
            Check(!soldier.HasSoundClue && soldier.State=="Обход","expired gunfire investigation returns to ordinary patrol");

            Reset(new Vector3(25,0,24)); yield return null; Fire();
            Check(!soldier.HasSoundClue && !game.PatrolHostile,"bullet more than four metres away does not create distant hearing");
            Reset(new Vector3(21.2f,0,24)); wall=Wall(12); yield return null; Fire();
            Check(!soldier.HasSoundClue,"wall stopping bullet before listener prevents passing shot reaction");
            wall.SetActive(false); Destroy(wall);
            Reset(new Vector3(21.2f,0,24)); wall=Wall(24); wall.transform.position=new Vector3(20.6f,1.5f,24); wall.transform.localScale=new Vector3(.3f,3,6); Physics.SyncTransforms(); yield return null; Fire();
            Check(!soldier.HasSoundClue,"wall between flight path and listener blocks local bullet cue");
            wall.SetActive(false); Destroy(wall);

            Reset(new Vector3(27,0,20)); medic=game.SpawnHuman("Intent check distant medic",HumanRole.Medic,new Vector3(21.2f,0,24),clothing); medic.enabled=true; yield return null; Fire();
            Check(medic.HasSoundClue && !medic.CurrentTarget && !game.PatrolHostile,"medic also investigates a passing bullet beyond ordinary shot hearing");
            Reset(new Vector3(20,0,-24)); yield return null; Fire();
            Check(!soldier.HasSoundClue,"listener behind muzzle does not hear an invented passing bullet");
            Reset(new Vector3(24.5f,0,4)); yield return null; Fire();
            Check((float)Get(soldier,"soundUntil")-Time.time>19,"ordinary audible gunshot also starts twenty second investigation");
            Reset(new Vector3(21.2f,0,24)); yield return null; Fire();
            yield return new WaitForSeconds(.6f); Fire();
            Check((float)Get(soldier,"soundUntil")-Time.time>19,"fresh gunfire extends the search from the latest shot");
            soldier.HearNoise(soldier.transform.position,14,null,NoiseKind.Gunshot);
            yield return new WaitForSeconds(3);
            Check(soldier.HasSoundClue && soldier.State=="Осматриваю источник шума" && (float)Get(soldier,"soundUntil")-Time.time>16,"arrival does not end gunfire search after the former two second inspection");
        }
    }
}
