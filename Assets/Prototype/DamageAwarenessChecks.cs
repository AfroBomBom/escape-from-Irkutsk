using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
namespace IrkutskPrototype
{
    public sealed class DamageAwarenessChecks : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        EscapeGame game; HumanAgent shooter,observer; Material clothing;
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Hidden).SetValue(o,value);
        static object Get(object o,string name)=>o.GetType().GetField(name,Hidden).GetValue(o);
        static object Call(object o,string name)=>o.GetType().GetMethod(name,Hidden).Invoke(o,null);
        static void Check(bool ok,string message)
        { if(!ok) throw new Exception("CHECK FAILED: "+message); Debug.Log("PASS: "+message); }
        static void Place(Transform actor,Vector3 point)
        { var cc=actor.GetComponent<CharacterController>(); if(cc) cc.enabled=false; actor.position=point; if(cc) cc.enabled=true; actor.rotation=Quaternion.identity; Physics.SyncTransforms(); }
        void Pair(HumanRole role=HumanRole.Soldier)
        {
            foreach(var h in game.Humans.ToArray())
            {
                h.enabled=false; h.gameObject.SetActive(false);
                if(h.name.StartsWith("Belief check")) { game.Humans.Remove(h); Destroy(h.gameObject); }
            }
            shooter=game.SpawnHuman("Belief check shooter",role,new Vector3(20,0,0),clothing);
            observer=game.SpawnHuman("Belief check observer",HumanRole.Medic,new Vector3(20,0,-2),clothing);
            shooter.enabled=true; observer.enabled=false; observer.gameObject.SetActive(false);
            game.Soldier=shooter; game.Medic=observer;
            Set(shooter,"shotAt",Time.time+100); Set(observer,"shotAt",Time.time+100); Set(observer,"healAt",Time.time+100);
            Set(game,"hp",100f); Set(game,"<PatrolHostile>k__BackingField",true); Place(game.Hero,new Vector3(20,0,4));
        }
        void Watch()
        { observer.gameObject.SetActive(true); observer.enabled=true; observer.transform.forward=Vector3.forward; }
        GameObject Wall(Vector3 position)
        { var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer=8; wall.transform.position=position; wall.transform.localScale=new Vector3(6,2,.3f); Physics.SyncTransforms(); return wall; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-damage-awareness-check")<0) return;
            Application.runInBackground=true; new GameObject("Damage estimate verification").AddComponent<DamageAwarenessChecks>();
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
            Debug.Log("IRKUTSK_DAMAGE_AWARENESS_OK"); Application.Quit(0);
        }
        IEnumerator Run(EscapeGame instance)
        {
            game=instance; game.enabled=false; clothing=game.Soldier.GetComponentInChildren<Renderer>().sharedMaterial;
            foreach(var z in game.Zombies) z.gameObject.SetActive(false);
            var randomState=UnityEngine.Random.state; UnityEngine.Random.InitState(57);
            Pair(); Watch(); yield return null;
            float sum=0;
            for(int i=0;i<12;i++) { Set(game,"hp",100f); game.NpcShot(shooter,game.Hero); sum+=100-(float)Get(game,"hp"); }
            Check(sum>0 && shooter.KnownHeroDamage==Mathf.Min(100,sum) && shooter.OwnConfirmedHeroDamage==Mathf.Min(100,sum),"shooter counts actual confirmed HP loss rather than shots fired");
            Check(observer.KnownHeroDamage==shooter.KnownHeroDamage && observer.OwnConfirmedHeroDamage==0,"watching ally records visible partner hits without claiming own damage");

            Pair(); yield return null; var wall=Wall(new Vector3(20,1,2)); game.NpcShot(shooter,game.Hero);
            Check(shooter.KnownHeroDamage==0 && (float)Get(game,"hp")==100,"wall-blocked shot gives no damage belief");
            Destroy(wall); yield return null;
            Place(game.Hero,new Vector3(20,0,-4)); game.NpcShot(shooter,game.Hero);
            Check(shooter.KnownHeroDamage==0,"hit outside shooter view does not supply unseen damage knowledge");

            Pair(); Watch(); yield return null; wall=Wall(new Vector3(20,1,-1));
            for(int i=0;i<8;i++) { Set(game,"hp",100f); game.NpcShot(shooter,game.Hero); }
            Check(shooter.KnownHeroDamage>0 && observer.KnownHeroDamage==0,"ally behind wall receives no hit knowledge");
            Destroy(wall); yield return null; observer.transform.forward=Vector3.back;
            for(int i=0;i<4;i++) { Set(game,"hp",100f); game.NpcShot(shooter,game.Hero); }
            Check(observer.KnownHeroDamage==0,"ally facing away receives no offscreen hit knowledge");
            Place(observer.transform,new Vector3(22,0,2)); observer.transform.forward=(game.Hero.position-observer.transform.position).normalized;
            observer.ObserveConfirmedHeroHit(shooter,8);
            Check(observer.KnownHeroDamage==0,"seeing victim without seeing shooter does not identify partner hit");
            var outsider=game.SpawnHuman("Belief check outsider",HumanRole.Marauder,new Vector3(23,0,-1),clothing);
            outsider.transform.forward=(game.Hero.position-outsider.transform.position).normalized;
            outsider.ObserveConfirmedHeroHit(shooter,8);
            Check(outsider.KnownHeroDamage==0,"enemy faction does not receive patrol damage reports"); outsider.gameObject.SetActive(false);

            Pair(); yield return null;
            Set(game,"hp",3f); for(int i=0;i<12 && (float)Get(game,"hp")>0;i++) game.NpcShot(shooter,game.Hero);
            Check(shooter.KnownHeroDamage==3,"overkill records only actual remaining health lost");
            Pair(); yield return null;
            Place(game.Car,new Vector3(20,0,4)); Place(game.Hero,new Vector3(-28,0,-22));
            float durability=(float)Get(game,"durability");
            for(int i=0;i<5;i++) game.NpcShot(shooter,game.Car);
            Check(shooter.KnownHeroDamage==0 && (float)Get(game,"hp")==100 && (float)Get(game,"durability")<durability,"vehicle hits damage vehicle and never enter hero damage belief");
            Place(game.Car,new Vector3(-16,0,-12));

            Pair(); yield return null; Watch();
            Place(shooter.transform,new Vector3(24,0,0)); Place(observer.transform,new Vector3(20,0,0));
            for(int i=0;i<5;i++) game.NpcShot(observer,game.Hero);
            Check(observer.KnownHeroDamage==0 && (float)Get(game,"hp")==100,"tranquilizer stun contributes no fictitious health damage");

            Pair(); yield return null; shooter.ObserveConfirmedHeroHit(shooter,60); Set(game,"hp",40f);
            Check(game.HealHero(20)==20 && shooter.KnownHeroDamage==40 && shooter.OwnConfirmedHeroDamage==40,"visible completed healing reduces known damage by restored health");
            Place(game.Hero,new Vector3(-28,0,-22)); game.HealHero(20);
            Check(shooter.KnownHeroDamage==40,"unseen healing leaves observer estimate unchanged");
            Place(game.Hero,new Vector3(20,0,4)); Call(shooter,"ObserveHeroDamageMemory");
            Check(shooter.EstimatedHeroHealth==60 && (float)Get(game,"hp")==80,"reacquiring sight does not read or synchronize real hero HP");
            Set(game,"hp",40f); game.HealHero(100);
            Check(shooter.KnownHeroDamage==0 && shooter.OwnConfirmedHeroDamage==0 && shooter.EstimatedHeroHealth==100,"visible healing cannot make damage belief negative");
            shooter.ObserveConfirmedHeroHit(shooter,16); game.HealHero(10);
            Check(shooter.KnownHeroDamage==16,"zero effective healing at full HP changes no belief");
            Check(game.HealHero(-10)==0 && (float)Get(game,"hp")==100,"invalid negative healing cannot damage hero or alter belief");

            Pair(); yield return null; shooter.ObserveConfirmedHeroHit(shooter,48); Place(game.Hero,new Vector3(-28,0,-22));
            Set(shooter,"heroHealthSeenAt",Time.time-9.9f); Call(shooter,"ObserveHeroDamageMemory");
            Check(shooter.KnownHeroDamage==48,"damage memory remains before ten second unseen boundary");
            Set(shooter,"heroHealthSeenAt",Time.time-10.01f); Call(shooter,"ObserveHeroDamageMemory");
            Check(shooter.KnownHeroDamage==0 && shooter.OwnConfirmedHeroDamage==0,"ten unseen seconds reset both personal and shared damage estimates");
            Place(game.Hero,new Vector3(20,0,4)); shooter.ObserveConfirmedHeroHit(shooter,48);
            Set(shooter,"heroHealthSeenAt",Time.time-10.01f); Call(shooter,"ObserveHeroDamageMemory");
            Check(shooter.KnownHeroDamage==0,"expired belief resets before reappearing hero refreshes sight time");

            Pair(); yield return null; shooter.ObserveConfirmedHeroHit(shooter,48); Place(game.Hero,new Vector3(-28,0,-22));
            yield return new WaitForSeconds(4.3f);
            Check(shooter.KnownHeroDamage==48,"damage memory survives ordinary four second target search expiry");
            yield return new WaitForSeconds(6);
            Check(shooter.KnownHeroDamage==0,"actual ten second absence clears belief through live NPC updates");

            Pair(); yield return null; Place(game.Hero,new Vector3(20,0,9)); shooter.AlertTo(game.Hero); shooter.ObserveConfirmedHeroHit(shooter,48);
            Call(shooter,"Update");
            Check(shooter.HasHeroHealthAdvantage && shooter.TacticalTask==PatrolTask.Press,"large confirmed damage and HP advantage enable pressure task");
            Vector3 start=shooter.transform.position; yield return new WaitForSeconds(1);
            Check(Vector3.Distance(start,shooter.transform.position)>2,"confident soldier advances faster instead of holding normal range");
            Set(shooter,"<Health>k__BackingField",55f); Call(shooter,"Update");
            Check(!shooter.HasHeroHealthAdvantage && shooter.TacticalTask!=PatrolTask.Press,"loss of own health advantage immediately ends pressure");
            Set(shooter,"<Health>k__BackingField",49f); shooter.ObserveConfirmedHeroHit(shooter,40);
            Check(!shooter.HasHeroHealthAdvantage,"wounded NPC does not charge despite optimistic hero damage estimate");

            Pair(HumanRole.Marauder); yield return null; Place(game.Hero,new Vector3(20,0,8)); shooter.AlertTo(game.Hero); shooter.ObserveConfirmedHeroHit(shooter,48); Call(shooter,"Update");
            Check(shooter.TacticalTask==PatrolTask.Press,"marauder uses the same evidence based pressure rule");
            Pair(); Watch(); yield return null;
            observer.ObserveConfirmedHeroHit(shooter,48);
            Place(shooter.transform,new Vector3(24,0,0)); shooter.enabled=false;
            Set(observer,"<CurrentTarget>k__BackingField",game.Hero); Set(observer,"targetVisible",true); Set(observer,"lastSeen",game.Hero.position); Set(observer,"shotAt",0f);
            Call(observer,"FireIfSafe");
            Check(observer.HasHeroHealthAdvantage && !observer.PressingWoundedHero && (float)Get(observer,"shotAt")-Time.time>2.9f
                && (float)Get(observer,"shotAt")-Time.time<3.1f,"confident medic keeps support role and uses shorter tranquilizer interval");
            UnityEngine.Random.state=randomState;
        }
    }
}
