using UnityEngine;
namespace IrkutskPrototype
{
    public sealed partial class EscapeGame
    {
        float damageFlashUntil,lastDamage;
        string damageKind;
        public int SoldierTracers { get; private set; }
        public Vector3 LastSoldierDirection { get; private set; }
        void BuildCombat()
        {
            var hitbox=Car.gameObject.AddComponent<BoxCollider>(); hitbox.center=new Vector3(0,.8f,0);
            hitbox.size=new Vector3(1.8f,1.5f,3.5f); hitbox.isTrigger=true;
        }
        public void SoldierShot(HumanAgent soldier)
        { NpcShot(soldier,driving?Car:Hero); }
        public void NpcShot(HumanAgent soldier,Transform target)
        {
            WarnCivilians(soldier.transform.position);
            Vector3 origin=ActorPosture.Muzzle(soldier.transform);
            Vector3 aim=Sight.AimPoint(soldier.transform,target)-origin;
            Vector3 direction=Quaternion.Euler(Random.Range(-2f,2f),Random.Range(-5f,5f),0)*aim.normalized;
            LastSoldierDirection=direction; SoldierTracers++;
            var hits=Physics.RaycastAll(origin,direction,24,~0,QueryTriggerInteraction.Collide);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            Vector3 end=origin+direction*24;
            foreach(var hit in hits)
            {
                if(hit.collider.gameObject==soldier.gameObject) continue;
                end=hit.point;
                if(hit.collider.gameObject==Hero.gameObject || hit.collider.gameObject==Car.gameObject) { if(soldier.Role==HumanRole.Medic) Dart(); else Damage(8); break; }
                var z=hit.collider.GetComponent<Infected>();
                if(z) { z.Hit(soldier.Role==HumanRole.Medic?0:20,soldier.Role==HumanRole.Medic?1.2f:.2f,direction*.1f,origin); break; }
                var person=hit.collider.GetComponent<HumanAgent>();
                if(person)
                {
                    if(!soldier.IsPatrol || !person.IsPatrol)
                    { if(soldier.Role==HumanRole.Medic) person.Stun(1.2f); else person.Hit(15,false,soldier.transform); }
                    break;
                }
                if(hit.collider.gameObject.layer==8) break;
            }
            var tracer=new GameObject("Soldier tracer"); tracer.transform.position=origin;
            var line=tracer.AddComponent<LineRenderer>(); line.sharedMaterial=amber; line.startWidth=.09f; line.endWidth=.04f;
            line.generateLightingData=true;
            line.positionCount=2; line.SetPosition(0,origin); line.SetPosition(1,end);
            Fog.RegisterDynamic(tracer); Destroy(tracer,.22f); Noise(origin,12,soldier.transform,NoiseKind.Gunshot);
            sound.PlayOneShot(shotClip,.45f);
        }
        void ShowDamage(float damage,string kind)
        { if(damage<=0) return; lastDamage=damage; damageKind=kind; damageFlashUntil=Time.unscaledTime+.65f; }
        void DrawDamageFeedback(float width,float height)
        {
            float alpha=Mathf.Clamp01((damageFlashUntil-Time.unscaledTime)/.65f);
            if(alpha<=0) return;
            Color old=GUI.color; GUI.color=new Color(1,.08f,.03f,alpha*.75f);
            GUI.DrawTexture(new Rect(0,0,width,12),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0,height-12,width,12),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0,0,12,height),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width-12,0,12,height),Texture2D.whiteTexture);
            GUI.color=new Color(1,.25f,.15f,alpha);
            GUI.Label(new Rect(width/2-100,height/2+50,300,40),$"−{lastDamage:0} • {damageKind}",title); GUI.color=old;
        }
    }
}
