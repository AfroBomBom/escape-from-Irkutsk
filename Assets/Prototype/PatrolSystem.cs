using System.Collections.Generic;
using UnityEngine;
namespace IrkutskPrototype
{
    public sealed partial class EscapeGame
    {
        readonly HashSet<Transform> knownThreats=new HashSet<Transform>();
        readonly HashSet<Transform> checkedNpcs=new HashSet<Transform>();
        public bool IsHero(Transform target)=>target==Hero || target==Car;
        public bool Threat(Transform target)=>IsHero(target)?PatrolHostile:knownThreats.Contains(target);
        public bool Checked(Transform target)=>IsHero(target)?Scanned:checkedNpcs.Contains(target);
        public bool Alive(Transform target)
        {
            if(!target || !target.gameObject.activeInHierarchy) return false;
            var human=target.GetComponent<HumanAgent>(); if(human) return !human.Downed && !human.IsIncapacitated && !human.IsDetained;
            var z=target.GetComponent<Infected>(); if(z) return !z.Downed;
            return IsHero(target) && !IsOver;
        }
        public void RegisterThreat(Transform target)
        { if(!target) return; if(IsHero(target)) PatrolAttacked(); else knownThreats.Add(target); }
        public void ObserveAggression(Transform source)
        {
            foreach(var human in Humans)
                if(human.IsPatrol && human.gameObject.activeInHierarchy && !human.Downed && !human.IsIncapacitated && Sight.CanSee(human.transform,source,12,Sight.HumanAngle)) { ConfirmThreat(human,source); break; }
        }
        public void ConfirmThreat(HumanAgent witness,Transform target)
        {
            RegisterThreat(target);
            foreach(var person in Humans)
                if(person.IsPatrol && !person.IsIncapacitated && person.gameObject.activeInHierarchy && Vector3.Distance(person.transform.position,witness.transform.position)<=12) person.AlertTo(target);
        }
        public void ShareSighting(HumanAgent witness,Transform target,Vector3 position)
        {
            foreach(var person in Humans)
                if(person!=witness && person.IsPatrol && person.enabled && person.gameObject.activeInHierarchy && Vector3.Distance(person.transform.position,witness.transform.position)<=12)
                    person.ReceiveReport(target,position);
        }
        public void WarnCivilians(Vector3 origin)
        { foreach(var human in Humans) human.HearGunfire(origin); }
        public void CompleteNpcScan(Transform target)
        {
            if(!Alive(target) || IsHero(target) || Medic.Downed || Medic.IsIncapacitated || !Sight.CanSee(Medic.transform,target,Sight.ScannerRange,Sight.HumanAngle)) return;
            checkedNpcs.Add(target);
            bool witnessed=Fog!=null && (Fog.IsVisible(Medic.transform.position) || Fog.IsVisible(target.position));
            if(target.GetComponent<Infected>()) { ConfirmThreat(Medic,target); if(witnessed) Change(0,0,"Патруль: сканер подтвердил заражение"); }
            else if(witnessed) Change(0,0,"Патруль: проверен здоровый человек");
        }
        public Transform SelectPatrolTarget(HumanAgent observer)
        {
            Transform chosen=null; float best=float.MaxValue;
            void Consider(Transform target)
            {
                if(target==observer.transform || (observer.Ignores(target) && !Threat(target)) || !Alive(target) || !Sight.CanSee(observer.transform,target,12,Sight.HumanAngle)) return;
                var human=target.GetComponent<HumanAgent>(); if(human && human.IsPatrol) return;
                // Keep a visible engagement instead of restarting it for every nearer passer-by.
                float score=Vector3.Distance(observer.transform.position,target.position)-(Threat(target)?20:0)-(target==observer.CurrentTarget?12:0);
                if(score<best) { best=score; chosen=target; }
            }
            Consider(driving?Car:Hero);
            foreach(var z in Zombies) Consider(z.transform);
            foreach(var human in Humans) Consider(human.transform);
            return chosen;
        }
    }
}
