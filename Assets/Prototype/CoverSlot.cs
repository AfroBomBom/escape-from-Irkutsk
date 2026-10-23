using System.Collections.Generic;
using UnityEngine;
namespace IrkutskPrototype
{
    public sealed class CoverSlot : MonoBehaviour
    {
        static readonly HashSet<CoverSlot> active=new HashSet<CoverSlot>();
        public static IEnumerable<CoverSlot> Active=>active;
        public Vector3 LeftPeek=new Vector3(-2,0,0),RightPeek=new Vector3(2,0,0);
        HumanAgent owner;
        float leaseUntil;
        public CoverObstacle Cover=>GetComponentInParent<CoverObstacle>();
        public HumanAgent Owner
        {
            get
            {
                if(owner && (owner.Downed || !owner.isActiveAndEnabled || Time.time>=leaseUntil)) owner=null;
                return owner;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry()=>active.Clear();
        void OnEnable()=>active.Add(this);
        void OnDisable() { active.Remove(this); owner=null; }
        public bool TryReserve(HumanAgent actor)
        {
            if(!isActiveAndEnabled || !actor || actor.Downed || !actor.isActiveAndEnabled || (Owner && Owner!=actor)) return false;
            owner=actor; leaseUntil=Time.time+2; return true;
        }
        public void Release(HumanAgent actor) { if(owner==actor) owner=null; }
        public bool ProtectsFrom(Vector3 threat)
        {
            if(!isActiveAndEnabled || !Cover || !NpcTactics.Walkable(transform.position)) return false;
            Vector3 point=transform.position+Vector3.up*(Cover.Low?.85f:1.6f);
            return Physics.Linecast(threat+Vector3.up*1.56f,point,out var hit,EscapeGame.SolidMask) && hit.collider.GetComponentInParent<CoverObstacle>()==Cover;
        }
        public Vector3 Peek(bool left)=>transform.TransformPoint(left?LeftPeek:RightPeek);
        void OnDrawGizmosSelected()
        {
            Gizmos.color=Color.green; Gizmos.DrawWireSphere(transform.position+Vector3.up*.15f,.3f);
            Gizmos.color=Color.yellow;
            Gizmos.DrawLine(transform.position,Peek(true)); Gizmos.DrawWireSphere(Peek(true)+Vector3.up*.15f,.2f);
            Gizmos.DrawLine(transform.position,Peek(false)); Gizmos.DrawWireSphere(Peek(false)+Vector3.up*.15f,.2f);
        }
    }
}
