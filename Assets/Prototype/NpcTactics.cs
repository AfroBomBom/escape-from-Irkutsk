using UnityEngine;
namespace IrkutskPrototype
{
    public static class NpcTactics
    {
        public static bool Walkable(Vector3 p)
        {
            if(p.x< -29.5f || p.x>115 || Mathf.Abs(p.z)>23.5f || (p.x>31 && Mathf.Abs(p.z)>6.1f)) return false;
            return !Physics.CheckBox(p+Vector3.up,new Vector3(.4f,.7f,.4f),Quaternion.identity,EscapeGame.SolidMask);
        }
        public static bool ClearStep(Vector3 origin,Vector3 point)
        {
            Vector3 delta=point-origin; delta.y=0;
            return Walkable(point) && !Physics.SphereCast(origin+Vector3.up,.4f,delta.normalized,out _,delta.magnitude,EscapeGame.SolidMask);
        }
        // A small, bounded sample chooses reachable escape space and favours cover.
        public static Vector3 RetreatPoint(Vector3 origin,Vector3 danger)
        {
            Vector3 away=origin-danger; away.y=0;
            if(away.sqrMagnitude<.01f) away=Vector3.back;
            away.Normalize(); Vector3 chosen=origin; float best=Vector3.Distance(origin,danger);
            for(int i=0;i<9;i++)
            {
                float angle=(i==0?0:((i+1)/2)*35*(i%2==0?-1:1));
                Vector3 candidate=origin+Quaternion.Euler(0,angle,0)*away*3.5f;
                if(!ClearStep(origin,candidate)) continue;
                float score=Vector3.Distance(candidate,danger);
                if(Physics.Linecast(candidate+Vector3.up*1.15f,danger+Vector3.up*1.15f,EscapeGame.SolidMask)) score+=2;
                if(score>best) { best=score; chosen=candidate; }
            }
            return chosen;
        }
        public static bool FiringLane(Transform shooter,Transform target)
        {
            Vector3 origin=ActorPosture.Muzzle(shooter);
            Vector3 aim=Sight.AimPoint(shooter,target)-origin;
            var hits=Physics.RaycastAll(origin,aim.normalized,aim.magnitude+.1f,~0,QueryTriggerInteraction.Collide);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(hit.transform==shooter) continue;
                if(hit.transform==target) return true;
                if(hit.collider.gameObject.layer==8 || hit.collider.GetComponent<HumanAgent>() || hit.collider.GetComponent<Infected>() || hit.collider is CharacterController) return false;
            }
            return true;
        }
        public static Vector3 FiringStep(Transform actor,Transform target)
        {
            Vector3 forward=target.position-actor.position; forward.y=0; forward.Normalize();
            Vector3 side=Vector3.Cross(Vector3.up,forward);
            if(actor.GetEntityId().GetHashCode()%2==0) side=-side;
            foreach(float sign in new[]{1f,-1f})
            {
                Vector3 point=actor.position+side*sign*1.4f;
                if(ClearStep(actor.position,point)) return point;
            }
            return RetreatPoint(actor.position,target.position);
        }
        public static Vector3 Separate(Transform actor,Vector3 desired)
        {
            var game=EscapeGame.Instance; Vector3 repel=Vector3.zero;
            void Add(Transform other)
            {
                if(other==actor || !other.gameObject.activeInHierarchy) return;
                Vector3 d=actor.position-other.position; d.y=0; float distance=d.magnitude;
                if(distance<.95f) repel+=(distance>.05f?d/distance:actor.right)*(.95f-distance);
            }
            foreach(var human in game.Humans) if(!human.Downed) Add(human.transform);
            foreach(var z in game.Zombies) if(!z.Downed) Add(z.transform);
            return (desired.normalized+repel*.8f).normalized;
        }
    }
}
