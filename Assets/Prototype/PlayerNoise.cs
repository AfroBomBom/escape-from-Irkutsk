using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace IrkutskPrototype
{
    public sealed partial class EscapeGame
    {
        sealed class NoisePulse
        {
            public float Radius, Until;
            public Transform Source;
            public NoiseKind Kind;
            public readonly HashSet<HumanAgent> Humans=new HashSet<HumanAgent>();
            public readonly HashSet<Infected> Infected=new HashSet<Infected>();
        }
        readonly List<NoisePulse> playerNoisePulses=new List<NoisePulse>();
        LineRenderer playerNoiseCircle;
        public float PlayerNoiseRadius
        {
            get
            {
                float radius=0;
                foreach(var pulse in playerNoisePulses)
                    if(Time.time<pulse.Until) radius=Mathf.Max(radius,pulse.Radius);
                return radius;
            }
        }
        void BuildPlayerNoiseIndicator()
        {
            var root=new GameObject("Player noise radius"); root.transform.SetParent(transform,false);
            playerNoiseCircle=root.AddComponent<LineRenderer>();
            var shader=Resources.Load<Shader>("NoiseRing");
            var ringMaterial=shader?new Material(shader):new Material(amber);
            ringMaterial.color=new Color(1,.75f,.15f,.9f); materials.Add(ringMaterial);
            playerNoiseCircle.sharedMaterial=ringMaterial; playerNoiseCircle.useWorldSpace=true;
            playerNoiseCircle.positionCount=64; playerNoiseCircle.loop=true;
            playerNoiseCircle.startWidth=playerNoiseCircle.endWidth=.06f;
            playerNoiseCircle.alignment=LineAlignment.View; playerNoiseCircle.generateLightingData=true;
            playerNoiseCircle.shadowCastingMode=ShadowCastingMode.Off; playerNoiseCircle.receiveShadows=false;
            playerNoiseCircle.enabled=false;
        }
        bool PlayerNoiseReaches(Vector3 origin,Vector3 listener,float radius)
        {
            Vector3 delta=listener-origin; delta.y=0;
            if(delta.sqrMagnitude>radius*radius) return false;
            float effective=radius;
            if(Physics.Linecast(origin+Vector3.up,listener+Vector3.up,SolidMask)) effective*=.4f;
            return delta.sqrMagnitude<=effective*effective;
        }
        void EmitPlayerNoise(float radius,Transform source,NoiseKind kind)
        {
            if(radius<=0 || !source || !Hero || !Car) return;
            // The source is the controlled actor, including work performed beside it.
            Vector3 origin=source==Car?Car.position:Hero.position;
            noisePosition=origin; noiseRadius=radius; noiseUntil=Time.time+.45f;
            float duration=kind==NoiseKind.Movement?.55f:kind==NoiseKind.Gunshot?1.2f:kind==NoiseKind.Engine?.18f:.8f;
            playerNoisePulses.RemoveAll(p=>Time.time>=p.Until);
            playerNoisePulses.Add(new NoisePulse { Radius=radius,Until=Time.time+duration,Source=source,Kind=kind });
            RefreshPlayerNoiseIndicator();
        }
        void PropagatePlayerNoise(NoisePulse pulse,Vector3 origin)
        {
            foreach(var z in Zombies)
                if(z && !pulse.Infected.Contains(z) && z.gameObject.activeInHierarchy && !z.Downed && !z.IsDetained && PlayerNoiseReaches(origin,z.transform.position,pulse.Radius))
                { pulse.Infected.Add(z); z.Hear(origin,pulse.Radius,true); }
            foreach(var human in Humans)
                if(human && !pulse.Humans.Contains(human) && human.enabled && human.gameObject.activeInHierarchy && PlayerNoiseReaches(origin,human.transform.position,pulse.Radius))
                { pulse.Humans.Add(human); human.HearNoise(origin,pulse.Radius,pulse.Source,pulse.Kind,true); }
        }
        void RefreshPlayerNoiseIndicator()
        {
            if(!playerNoiseCircle || !Hero || !Car) return;
            playerNoisePulses.RemoveAll(p=>Time.time>=p.Until);
            float radius=PlayerNoiseRadius;
            playerNoiseCircle.enabled=radius>0 && !IsOver;
            if(!playerNoiseCircle.enabled) return;
            Vector3 origin=driving?Car.position:Hero.position;
            foreach(var pulse in playerNoisePulses) PropagatePlayerNoise(pulse,origin);
            Vector3 center=origin+Vector3.up*.12f;
            for(int i=0;i<playerNoiseCircle.positionCount;i++)
            {
                float angle=i*Mathf.PI*2/playerNoiseCircle.positionCount;
                playerNoiseCircle.SetPosition(i,center+new Vector3(Mathf.Sin(angle)*radius,0,Mathf.Cos(angle)*radius));
            }
        }
        void DrawPlayerNoise(float width)
        {
            float radius=PlayerNoiseRadius;
            GUI.Label(new Rect(width/2-125,16,250,26),radius>0?$"ШУМ: {radius:0.#} м":"ШУМ: нет",normal);
        }
    }
}
