using UnityEngine;
namespace IrkutskPrototype
{
    public sealed class HealingDart : MonoBehaviour
    {
        HumanAgent source,patient;
        EscapeGame game;
        Vector3 direction;
        float expires;
        LineRenderer line;
        public void Initialize(EscapeGame owner,HumanAgent medic,HumanAgent target,Material material)
        {
            game=owner; source=medic; patient=target;
            transform.position=ActorPosture.Muzzle(medic.transform);
            direction=(Sight.AimPoint(medic.transform,target.transform)-transform.position).normalized;
            expires=Time.time+.5f;
            line=gameObject.AddComponent<LineRenderer>(); line.sharedMaterial=material;
            line.generateLightingData=true;
            line.startWidth=.12f; line.endWidth=.06f; line.positionCount=2; Draw();
        }
        void Draw() { line.SetPosition(0,transform.position); line.SetPosition(1,transform.position-direction*.45f); }
        void Update()
        {
            if(!game || game.IsOver || Time.time>=expires) { Destroy(gameObject); return; }
            float travel=20*Time.deltaTime;
            var hits=Physics.SphereCastAll(transform.position,.06f,direction,travel,~0,QueryTriggerInteraction.Collide);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(source && (hit.transform==source.transform || hit.transform.IsChildOf(source.transform))) continue;
                var person=hit.collider.GetComponent<HumanAgent>();
                if(person==patient && patient && patient.ReceiveHealingDart(15)) game.RecordHealingDartHit();
                Destroy(gameObject); return;
            }
            transform.position+=direction*travel; Draw();
        }
    }
    public sealed partial class EscapeGame
    {
        public int HealingDartsFired { get; private set; }
        public int HealingDartsHit { get; private set; }
        public void RecordHealingDartHit()=>HealingDartsHit++;
        public void FireHealingDart(HumanAgent medic,HumanAgent soldier)
        {
            var root=new GameObject("Лечебный дротик"); root.AddComponent<HealingDart>().Initialize(this,medic,soldier,green);
            HealingDartsFired++; if(Fog) Fog.RegisterDynamic(root);
            Noise(medic.transform.position,2,medic.transform,NoiseKind.Work);
        }
    }
}
