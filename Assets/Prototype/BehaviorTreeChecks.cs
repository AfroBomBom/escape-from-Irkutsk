using System;
using UnityEngine;
using static IrkutskPrototype.Behaviors;

namespace IrkutskPrototype
{
    public static class BehaviorTreeChecks
    {
        static void Check(bool ok,string text)
        { if(!ok) throw new Exception("CHECK FAILED: "+text); Debug.Log("PASS: "+text); }
        public static void Verify()
        {
            bool danger=false; int patrol=0,combat=0,fallback=0;
            var tree=Select("Priority",
                When("Danger",()=>danger,Run("Combat",()=>combat++)),
                Run("Patrol",()=>patrol++),Do("Fallback",()=>fallback++));
            Check(tree.Tick()==BehaviorStatus.Running && patrol==1 && combat==0 && fallback==0,"selector stops at first running branch");
            danger=true; tree.Tick();
            Check(combat==1 && patrol==1,"higher priority danger interrupts running patrol next tick");
            danger=false; tree.Tick();
            Check(patrol==2 && combat==1,"patrol resumes when danger condition clears");
            bool ready=false; int approach=0,fire=0;
            var sequence=Steps("Scan",Test("Visible",()=>ready),Run("Approach",()=>approach++),Do("Fire",()=>fire++));
            Check(sequence.Tick()==BehaviorStatus.Failure && approach==0 && fire==0,"failed sequence guard blocks subsequent actions");
            ready=true;
            Check(sequence.Tick()==BehaviorStatus.Running && approach==1 && fire==0,"running sequence action blocks later actions");
            ready=false;
            Check(sequence.Tick()==BehaviorStatus.Failure && approach==1,"sequence rechecks conditions instead of remembering stale success");
            var complete=Steps("Complete",Do("First",()=>{}),Do("Second",()=>{}));
            Check(complete.Tick()==BehaviorStatus.Success && complete.Children[1].LastTickFrame==Time.frameCount,"completed sequence succeeds and records debug trace");
        }
    }
}
