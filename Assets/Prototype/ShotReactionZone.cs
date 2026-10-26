using UnityEngine;
namespace IrkutskPrototype
{
    [DisallowMultipleComponent]
    public sealed class ShotReactionZone : MonoBehaviour
    {
        [Min(.1f)] public float Radius=2;
        [Min(.1f)] public float PeacefulSoldierRadius=3;
        // Ground-plane intent zone, deliberately without a collider or trigger.
        public bool Intersects(Vector3 origin,Vector3 direction,float length,out float entry,float radiusOverride=-1)
        {
            entry=0; if(!isActiveAndEnabled || length<0) return false;
            direction.y=0; if(direction.sqrMagnitude<.0001f) return false; direction.Normalize();
            Vector3 delta=origin-transform.position; delta.y=0;
            float b=Vector3.Dot(delta,direction),radius=Mathf.Max(.1f,radiusOverride>=0?radiusOverride:Radius);
            float discriminant=b*b-(delta.sqrMagnitude-radius*radius);
            if(discriminant<0) return false;
            float root=Mathf.Sqrt(discriminant),near=-b-root,far=-b+root;
            if(far<0 || near>length) return false;
            entry=Mathf.Max(0,near); return true;
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color=new Color(1,.7f,.1f,.7f); Vector3 center=transform.position+Vector3.up*.05f;
            float radius=EscapeGame.Instance?EscapeGame.Instance.ShotReactionRadius(transform):Radius;
            for(int i=0;i<48;i++)
                Gizmos.DrawLine(center+Quaternion.Euler(0,i*7.5f,0)*Vector3.forward*radius,center+Quaternion.Euler(0,(i+1)*7.5f,0)*Vector3.forward*radius);
        }
    }
}
