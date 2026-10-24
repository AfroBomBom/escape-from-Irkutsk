using System.Collections.Generic;
using UnityEngine;
namespace IrkutskPrototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ActorPosture : MonoBehaviour
    {
        public const float StandingHeight=1.8f, CrouchingHeight=1;
        public bool IsCrouched { get; private set; }
        public bool IsIncapacitated { get; private set; }
        CharacterController body;
        Transform visuals;
        void Awake() { body=GetComponent<CharacterController>(); }
        void Start() { PrepareVisuals(); Apply(); }
        void PrepareVisuals()
        {
            if(visuals) return;
            var children=new List<Transform>();
            foreach(Transform child in transform)
                if(child.GetComponent<Renderer>() && !child.GetComponent<TextMesh>()) children.Add(child);
            visuals=new GameObject("Posture visuals").transform; visuals.SetParent(transform,false);
            foreach(var child in children) child.SetParent(visuals,false);
        }
        public bool TrySetCrouched(bool crouched)
        {
            if(IsIncapacitated) return false;
            if(!body) body=GetComponent<CharacterController>();
            if(!crouched && IsCrouched)
            {
                float radius=body.radius-.02f;
                if(Physics.CheckCapsule(transform.position+Vector3.up*body.radius,
                    transform.position+Vector3.up*(StandingHeight-body.radius),radius,EscapeGame.SolidMask,QueryTriggerInteraction.Ignore)) return false;
            }
            IsCrouched=crouched; PrepareVisuals(); Apply(); return true;
        }
        public void SetIncapacitated(bool incapacitated)
        { IsIncapacitated=incapacitated; IsCrouched=incapacitated || IsCrouched; PrepareVisuals(); Apply(); }
        void Apply()
        {
            body.height=IsIncapacitated?.64f:IsCrouched?CrouchingHeight:StandingHeight;
            body.center=Vector3.up*body.height*.5f;
            if(visuals)
            {
                visuals.localScale=IsIncapacitated?Vector3.one:new Vector3(1,body.height/StandingHeight,1);
                visuals.localRotation=IsIncapacitated?Quaternion.Euler(90,0,0):Quaternion.identity;
                visuals.localPosition=IsIncapacitated?new Vector3(0,.28f,-.8f):Vector3.zero;
            }
        }
        public static float Height(Transform actor)
        {
            var cc=actor.GetComponent<CharacterController>();
            if(cc) return cc.height;
            return EscapeGame.Instance && actor==EscapeGame.Instance.Car?1.5f:StandingHeight;
        }
        public static Vector3 Eye(Transform actor)=>actor.position+Vector3.up*(Height(actor)*.87f);
        public static Vector3 Muzzle(Transform actor)=>actor.position+Vector3.up*(Height(actor)*.72f);
        public static Vector3 Head(Transform actor)=>actor.position+Vector3.up*(Height(actor)-.12f);
        public static Vector3 Chest(Transform actor)=>actor.position+Vector3.up*(Height(actor)*.55f);
    }
}
