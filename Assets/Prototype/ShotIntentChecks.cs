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
        }
    }
}
