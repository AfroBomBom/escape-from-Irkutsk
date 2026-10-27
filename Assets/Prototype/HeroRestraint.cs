using UnityEngine;
using UnityEngine.InputSystem;
namespace IrkutskPrototype
{
    public sealed partial class EscapeGame
    {
        HumanAgent restrainingMedic;
        bool restraintInputConsumed;
        float strugglePower,struggleUntil,restraintGraceUntil;
        public bool HeroStruggling { get; private set; }
        public float EscapePower => strugglePower;
        public string HeroCaptorName => restrainingMedic && restrainingMedic.Role==HumanRole.Soldier?"Солдат":"Врач";
        public float StruggleResistance => restrainingMedic && restrainingMedic.Role==HumanRole.Soldier?.3f:.2f;
        public float StruggleClickPower => restrainingMedic && restrainingMedic.Role==HumanRole.Soldier?.07f:.08f;
        public bool IsBindingHero(HumanAgent actor) => restrainingMedic==actor && actor;
        public bool HeroCaptureImmune => Time.time<restraintGraceUntil;
        bool HeroControlsLocked => restraintInputConsumed || HeroStunned || HeroStruggling || restrainingMedic;
        bool CanBindHero(HumanAgent medic) => medic && medic.enabled && medic.gameObject.activeInHierarchy
            && medic.IsPatrol && (medic.Role!=HumanRole.Soldier || IsBindingHero(medic) || !medic.HasCaptureMedic) && !medic.Downed && !medic.IsIncapacitated && !medic.IsDetained
            && !medic.IsStunned && !medic.RescuePatient && !driving && !IsOver && !HeroCaptureImmune
            && Vector3.Distance(medic.transform.position,Hero.position)<=1.6f
            && Sight.VisibleBody(ActorPosture.Eye(medic.transform),Hero);
        public bool TryBindHero(HumanAgent medic)
        {
            if(!CanBindHero(medic) || (restrainingMedic && restrainingMedic!=medic)) return false;
            if(!restrainingMedic) { restrainingMedic=medic; detainProgress=0; CloseInteraction(); }
            return true;
        }
        void FinishDetention()
        {
            detained=true; finalTime=Time.time-started; bag.Clear(); CloseInteraction();
            HeroStruggling=false; restrainingMedic=null; detainProgress=0;
            Change(0,0,"Задержание: добыча конфискована");
        }
        void EscapeRestraint()
        {
            var captor=restrainingMedic;
            HeroStruggling=false; restrainingMedic=null; detainProgress=0; heroStunUntil=0;
            restraintGraceUntil=Time.time+3;
            if(captor) captor.CancelDartRestraint();
            Say("Ты освободился! Убегай — 3 с защиты от повторного захвата");
        }
        void AdvanceHeroStruggle(float elapsed,bool clicked)
        {
            if(!HeroStruggling) return;
            strugglePower=Mathf.Clamp01(strugglePower-StruggleResistance*elapsed+(clicked?StruggleClickPower:0));
            if(strugglePower>=1) EscapeRestraint();
            else if(strugglePower<=0) FinishDetention();
            else if(Time.time>=struggleUntil)
            { if(strugglePower>=.5f) EscapeRestraint(); else FinishDetention(); }
        }
        void TickHeroRestraint()
        {
            restraintInputConsumed=HeroStruggling || restrainingMedic;
            if(IsOver || hp<=0) { HeroStruggling=false; restrainingMedic=null; detainProgress=0; return; }
            if(restrainingMedic)
            {
                if(!CanBindHero(restrainingMedic)) { EscapeRestraint(); return; }
                if(!HeroStruggling && !HeroStunned)
                {
                    HeroStruggling=true; strugglePower=.5f; struggleUntil=Time.time+8; CloseInteraction();
                    Say(HeroCaptorName+" связывает тебя! Часто нажимай ЛКМ, чтобы вырваться");
                }
                if(HeroStruggling)
                    AdvanceHeroStruggle(Time.deltaTime,Mouse.current!=null && Mouse.current.leftButton.wasPressedThisFrame);
                else { detainProgress+=Time.deltaTime; if(detainProgress>=3) FinishDetention(); }
                return;
            }
            bool close=!driving && Scanned && !PatrolHostile && !HeroCaptureImmune
                && (Medic && Medic.enabled && Medic.gameObject.activeInHierarchy && !Medic.Downed && !Medic.IsIncapacitated && !Medic.IsStunned && !Medic.RescuePatient && Sight.CanSee(Medic.transform,Hero,1.5f,Sight.HumanAngle));
            detainProgress=close?detainProgress+Time.deltaTime:0;
            if(detainProgress>=3) FinishDetention();
        }
        void DrawHeroRestraint(float width,float height)
        {
            if(!HeroStruggling || IsOver) return;
            float x=width/2-230,y=height/2+70;
            Panel(new Rect(x,y,460,155));
            GUI.Label(new Rect(x+18,y+10,424,30),HeroCaptorName=="Солдат"?"БОРЬБА С СОЛДАТОМ":"БОРЬБА С ВРАЧОМ",title);
            GUI.Label(new Rect(x+18,y+46,424,28),HeroCaptorName=="Солдат"?"Солдат сильнее! Нажимай ЛКМ быстрее":"Часто нажимай ЛКМ, чтобы освободиться",normal);
            Color old=GUI.color; GUI.color=new Color(.2f,.2f,.2f); GUI.DrawTexture(new Rect(x+20,y+85,420,20),Texture2D.whiteTexture);
            GUI.color=new Color(.25f,.8f,.4f); GUI.DrawTexture(new Rect(x+20,y+85,420*strugglePower,20),Texture2D.whiteTexture); GUI.color=old;
            GUI.Label(new Rect(x+18,y+114,424,30),$"Освобождение {strugglePower:P0} • осталось {Mathf.Max(0,struggleUntil-Time.time):0.0} с",normal);
        }
    }
}
