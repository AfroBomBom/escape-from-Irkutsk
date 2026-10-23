using UnityEngine;
using System;
using System.Reflection;
namespace IrkutskPrototype
{
    public static class CombatChecks
    {
        static void Check(bool ok,string message) { if(!ok) throw new Exception("CHECK FAILED: "+message); Debug.Log("PASS: "+message); }
        static void Move(EscapeGame g,Vector3 p) { g.Controller.enabled=false; g.Hero.position=p; g.Controller.enabled=true; Physics.SyncTransforms(); }
        public static void Run(EscapeGame g)
        {
            Check(Sight.InfectedRange<Sight.HeroRange && Sight.InfectedAngle<Sight.HeroAngle,"infected view is smaller than hero view");
            Check(Sight.InCone(Vector3.zero,Vector3.forward,new Vector3(0,0,5),6,100),"forward target is in cone");
            Check(!Sight.InCone(Vector3.zero,Vector3.forward,new Vector3(0,0,-1),6,100),"rear target is outside cone even nearby");
            Check(!Sight.InCone(Vector3.zero,Vector3.forward,new Vector3(0,0,7),6,100),"infected cannot see beyond six metres");
            Move(g,new Vector3(0,0,-10)); g.Hero.rotation=Quaternion.identity; g.Fog.Refresh();
            Check(g.Fog.IsVisible(new Vector3(0,0,-6)) && !g.Fog.IsVisible(new Vector3(0,0,-14)),"hero fog reveals a sector rather than a circle");
            Check(!Sight.CanSee(g.Medic.transform,g.Medic.transform.position+g.Medic.transform.forward*4,Sight.ScannerRange,Sight.HumanAngle),"scanner cannot reach a target four metres away");
            var soldier=g.Soldier; Vector3 original=soldier.transform.position;
            var cc=soldier.GetComponent<CharacterController>(); cc.enabled=false; soldier.transform.position=new Vector3(0,0,0); cc.enabled=true;
            Move(g,new Vector3(0,0,4));
            var saved=UnityEngine.Random.state; UnityEngine.Random.InitState(32);
            g.SoldierShot(soldier); Vector3 first=g.LastSoldierDirection;
            g.SoldierShot(soldier); Vector3 second=g.LastSoldierDirection; UnityEngine.Random.state=saved;
            Check(g.SoldierTracers>=2 && Vector3.Angle(first,second)>.01f,"soldier emits tracers with nonidentical spread");
            cc.enabled=false; soldier.transform.position=new Vector3(4,0,3); cc.enabled=true;
            Move(g,new Vector3(4,0,11));
            var hpField=typeof(EscapeGame).GetField("hp",BindingFlags.Instance|BindingFlags.NonPublic);
            float before=(float)hpField.GetValue(g); g.SoldierShot(soldier);
            Check((float)hpField.GetValue(g)==before,"soldier bullets stop at walls without phantom damage");
            g.Damage(3);
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            Check((float)typeof(EscapeGame).GetField("damageFlashUntil",flags).GetValue(g)>Time.unscaledTime,"damage activates visible feedback");
            typeof(EscapeGame).GetField("hp",flags).SetValue(g,100f);
            cc.enabled=false; soldier.transform.position=original; cc.enabled=true;
            Move(g,new Vector3(-10,0,-10));
        }
    }
}
