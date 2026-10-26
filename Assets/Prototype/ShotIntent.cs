using UnityEngine;
namespace IrkutskPrototype
{
    public sealed partial class EscapeGame
    {
        public bool LastShotHadAggressiveTarget { get; private set; }
        public Transform LastShotProtectedTarget { get; private set; }
        public float ShotReactionRadius(Transform unit)
        {
            var zone=unit.GetComponent<ShotReactionZone>(); if(!zone) return 0;
            var person=unit.GetComponent<HumanAgent>(); if(!person || person.Role!=HumanRole.Soldier) return zone.Radius;
            bool Nearby(Transform enemy)
            {
                Vector3 delta=enemy.position-unit.position; delta.y=0;
                return delta.sqrMagnitude<=Sight.HumanRange*Sight.HumanRange && Sight.VisibleBody(ActorPosture.Eye(unit),enemy);
            }
            foreach(var z in Zombies) if(z && !z.Downed && z.gameObject.activeInHierarchy && Nearby(z.transform)) return zone.Radius;
            foreach(var h in Humans) if(h && h.Role==HumanRole.Marauder && !h.Downed && !h.IsDetained && h.gameObject.activeInHierarchy && Nearby(h.transform)) return zone.Radius;
            return zone.PeacefulSoldierRadius;
        }
        bool CrossesShotZone(Transform unit,Vector3 origin,Vector3 direction,float length)
        {
            var zone=unit.GetComponent<ShotReactionZone>(); if(!zone) return false;
            Vector3 toward=unit.position-origin; toward.y=0;
            // Shooting away while standing inside someone's zone is not aiming at them.
            if(Vector3.Dot(toward,direction)<=.01f) return false;
            return zone.Intersects(origin,direction,length,out _,ShotReactionRadius(unit)) && Sight.VisibleBody(origin,unit);
        }
        public void ReportGunfire(Vector3 origin)
        {
            Vector3 muzzle=ActorPosture.Muzzle(Hero),direction=Hero.forward;
            Vector3 end=muzzle+direction*35;
            HumanAgent hitPerson=null;
            var hits=Physics.RaycastAll(muzzle,direction,35); System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(hit.transform==Hero) continue;
                var person=hit.collider.GetComponent<HumanAgent>(); var infected=hit.collider.GetComponent<Infected>();
                if(hit.collider.gameObject.layer==8 || (person && !person.Downed) || (infected && !infected.Downed))
                { end=hit.point; hitPerson=person; break; }
            }
            ReportGunfire(muzzle,direction,end,hitPerson);
        }
        public void ReportGunfire(Vector3 origin,Vector3 direction,Vector3 end,HumanAgent hitPerson=null)
        {
            WarnCivilians(origin); LastShotHadAggressiveTarget=false; LastShotProtectedTarget=null;
            direction.y=0; if(direction.sqrMagnitude<.0001f) return; direction.Normalize();
            Vector3 delta=end-origin; delta.y=0; float length=delta.magnitude;
            foreach(var z in Zombies)
                if(z && !z.Downed && z.gameObject.activeInHierarchy && CrossesShotZone(z.transform,origin,direction,length))
                { LastShotHadAggressiveTarget=true; break; }
            if(!LastShotHadAggressiveTarget)
                foreach(var h in Humans)
                    if(h && h.Role==HumanRole.Marauder && !h.Downed && !h.IsDetained && h.gameObject.activeInHierarchy
                        && CrossesShotZone(h.transform,origin,direction,length)) { LastShotHadAggressiveTarget=true; break; }
            // An aggressive target justifies the shot on either side of a patrol zone.
            if(LastShotHadAggressiveTarget && (!hitPerson || hitPerson.Role==HumanRole.Marauder)) return;
            foreach(var victim in Humans)
            {
                if(!victim || victim.Role==HumanRole.Marauder || victim.Downed || !victim.gameObject.activeInHierarchy
                    || !CrossesShotZone(victim.transform,origin,direction,length)) continue;
                foreach(var witness in Humans)
                {
                    if(!witness || !witness.IsPatrol || witness.Downed || witness.IsIncapacitated || !witness.gameObject.activeInHierarchy) continue;
                    if(!Sight.CanSee(witness.transform,Hero,Sight.HumanRange,Sight.HumanAngle)) continue;
                    if(witness!=victim && !Sight.CanSee(witness.transform,victim.transform,Sight.HumanRange,Sight.HumanAngle)) continue;
                    LastShotProtectedTarget=victim.transform; PatrolAttacked(witness); return;
                }
            }
        }
    }
}
