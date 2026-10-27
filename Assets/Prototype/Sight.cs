using UnityEngine;
namespace IrkutskPrototype
{
    public static class Sight
    {
        public const float HeroRange=float.PositiveInfinity, HeroAngle=120, InfectedRange=6, InfectedAngle=100, HumanRange=8, HumanAngle=110, ScannerRange=3.2f;
        public static bool InCone(Vector3 origin,Vector3 forward,Vector3 target,float range,float angle)
        {
            Vector3 delta=target-origin; delta.y=0; forward.y=0;
            return delta.sqrMagnitude<=range*range && (delta.sqrMagnitude<.01f || Vector3.Angle(forward,delta)<=angle*.5f);
        }
        public static bool CanSee(Transform observer,Vector3 point,float range,float angle)
        {
            Vector3 eye=ActorPosture.Eye(observer);
            return InCone(observer.position,observer.forward,point,range,angle) && Clear(eye,new Vector3(point.x,eye.y,point.z));
        }
        public static bool Clear(Vector3 origin,Vector3 point)=>!Physics.Linecast(origin,point,EscapeGame.SolidMask);
        public static bool CanSee(Transform observer,Transform target,float range,float angle)
            =>InCone(observer.position,observer.forward,target.position,range,angle) && VisibleBody(ActorPosture.Eye(observer),target);
        public static bool VisibleBody(Vector3 eye,Transform target)=>Clear(eye,ActorPosture.Head(target)) || Clear(eye,ActorPosture.Chest(target));
        public static Vector3 AimPoint(Transform shooter,Transform target)
        {
            Vector3 chest=ActorPosture.Chest(target),head=ActorPosture.Head(target),muzzle=ActorPosture.Muzzle(shooter);
            return Clear(muzzle,chest)?chest:Clear(muzzle,head)?head:chest;
        }
    }
}
