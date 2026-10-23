using UnityEngine;
namespace IrkutskPrototype
{
    public static class Sight
    {
        public const float HeroRange=10, HeroAngle=120, InfectedRange=6, InfectedAngle=100, HumanAngle=110, ScannerRange=3.2f;
        public static bool InCone(Vector3 origin,Vector3 forward,Vector3 target,float range,float angle)
        {
            Vector3 delta=target-origin; delta.y=0; forward.y=0;
            return delta.sqrMagnitude<=range*range && (delta.sqrMagnitude<.01f || Vector3.Angle(forward,delta)<=angle*.5f);
        }
        public static bool CanSee(Transform observer,Vector3 point,float range,float angle)
        { return InCone(observer.position,observer.forward,point,range,angle) && !Physics.Linecast(observer.position+Vector3.up*1.25f,new Vector3(point.x,observer.position.y+1.25f,point.z),EscapeGame.SolidMask); }
    }
}
