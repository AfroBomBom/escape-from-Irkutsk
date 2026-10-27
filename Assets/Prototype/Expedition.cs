using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IrkutskPrototype
{
    public sealed partial class EscapeGame
    {
        public readonly List<HumanAgent> Humans=new List<HumanAgent>();
        public readonly List<ExpeditionPickup> ExtraItems=new List<ExpeditionPickup>();
        public int Money { get; private set; }=60;
        public int Reputation { get; private set; }
        public bool PatrolHostile { get; private set; }
        public bool Scanned { get; private set; }
        public bool Sick => infection>=40;
        public bool ArmorInstalled { get; private set; }
        public float ArmorPoints { get; private set; }
        public bool InteractionOpen => shopOpen || citizenOpen || journalOpen;
        float infection,heroStunUntil,scanProgress,detainProgress,breakProgress,breakNoiseAt;
        bool shopUnlocked,shopLooted;
        GameObject shopShutter;
        TextMesh shopCaption;
        bool BreakingIn => breakProgress>0;
        bool detained,shopOpen,citizenOpen,journalOpen,questAccepted,questDone,settled;
        Vector2 journalScroll;
        public HumanAgent Citizen, Medic, Soldier;
        readonly List<string> ledger=new List<string>();
        Vector3 shopPosition=new Vector3(-8,0,-1.7f);
        string ItemName(int k) => k<4?partNames[k]:k==4?"Лекарство":"Бронепластина";
        void BuildExpedition()
        {
            Citizen=SpawnHuman("Житель",HumanRole.Civilian,new Vector3(-6,0,-13),blue);
            Medic=SpawnHuman("Врач",HumanRole.Medic,new Vector3(24,0,4),snow);
            Soldier=SpawnHuman("Солдат",HumanRole.Soldier,new Vector3(23,0,6),dark);
            SpawnHuman("Мародёр",HumanRole.Marauder,new Vector3(18,0,-8),red);
            Label("КАРАНТИН",new Vector3(27,.2f,17),Color.white,.22f);
            Box("Shop floor",new Vector3(-8,.02f,2),new Vector3(5,.08f,5),dark,false);
            Wall(new Vector3(-10.5f,1.5f,2),new Vector3(.3f,3,5));
            Wall(new Vector3(-5.5f,1.5f,2),new Vector3(.3f,3,5));
            Wall(new Vector3(-8,1.5f,4.5f),new Vector3(5,3,.3f));
            shopShutter=Box("Locked shop shutter",new Vector3(-8,1.5f,-.5f),new Vector3(5,3,.3f),steel);
            Box("Abandoned shelves",new Vector3(-8,.5f,3.5f),new Vector3(4,1,1),orange);
            shopCaption=Label("ЗАКРЫТЫЙ МАГАЗИН • E",new Vector3(-8,3.2f,-.6f),Color.white,.2f).GetComponent<TextMesh>();
            SpawnExtra("Лекарство",4,new Vector3(-15,.4f,6),red,false);
            SpawnExtra("ЧУЖОЕ • ЛЕКАРСТВО",4,new Vector3(-8,.4f,2),red,true);
            ExtraItems[ExtraItems.Count-1].gameObject.SetActive(false);
            SpawnExtra("Бронепластина",5,new Vector3(-1,.4f,-16),steel,false);
            Change(0,0,"Начало теста: 60 ₽. Счётчики действуют в пределах забега.");
        }
        public HumanAgent SpawnHuman(string name,HumanRole role,Vector3 position,Material clothing)
        {
            var root=new GameObject(name); root.transform.position=position;
            var cc=root.AddComponent<CharacterController>(); cc.height=1.8f; cc.radius=.32f; cc.center=Vector3.up*.9f;
            Box("Body",new Vector3(0,1,0),new Vector3(.65f,1,.45f),clothing,false,root.transform);
            Box("Head",new Vector3(0,1.7f,0),new Vector3(.4f,.4f,.4f),snow,false,root.transform);
            if(role==HumanRole.Medic) { Box("Red cross",new Vector3(0,1.3f,.25f),new Vector3(.35f,.12f,.06f),red,false,root.transform); Box("Scanner",new Vector3(.4f,1.3f,.5f),new Vector3(.3f,.3f,.3f),blue,false,root.transform); }
            if(role==HumanRole.Soldier || role==HumanRole.Marauder) Box("Rifle",new Vector3(.4f,1.3f,.5f),new Vector3(.2f,.25f,.9f),dark,false,root.transform);
            var person=root.AddComponent<HumanAgent>(); person.Role=role; person.Game=this;
            root.AddComponent<ActorPosture>();
            person.Caption=Label(name,position+Vector3.up*2.6f,Color.white,.18f).GetComponent<TextMesh>();
            person.Caption.transform.SetParent(root.transform,true);
            Humans.Add(person); return person;
        }
        public ExpeditionPickup SpawnExtra(string name,int kind,Vector3 pos,Material m,bool owned)
        {
            var root=Box(name,pos,new Vector3(.8f,.6f,.8f),m,false);
            var item=root.AddComponent<ExpeditionPickup>(); item.Kind=kind; item.Owned=owned;
            Label(name,pos+Vector3.up*1.2f,owned?new Color(1,.5f,.2f):Color.white,.18f).transform.SetParent(root.transform,true);
            ExtraItems.Add(item);
            if(Fog) Fog.RegisterDynamic(root);
            return item;
        }
        bool Near(Vector3 pos,float range=2.6f) => Vector3.Distance(Hero.position,pos)<range && !Physics.Linecast(Hero.position+Vector3.up,pos+Vector3.up,SolidMask);
        bool TryExpeditionInteraction()
        {
            if(!Citizen.Downed && Near(Citizen.transform.position)) { citizenOpen=true; return true; }
            if(!shopUnlocked && Near(shopPosition)) { shopOpen=true; return true; }
            foreach(var item in ExtraItems)
                if(item.gameObject.activeSelf && (!item.Owned || shopUnlocked) && Near(item.transform.position,2.1f))
                {
                    if(item.Owned) { LootStore(); return true; }
                    if(bag.Count>=2) { Say("Рюкзак полон"); return true; }
                    if(!item.GetComponent<WorldLoot>().TakeForHero()) return true;
                    bag.Add(item.Kind); item.gameObject.SetActive(false); Say("Взято: "+ItemName(item.Kind)); return true;
                }
            return false;
        }
        void CloseInteraction() { shopOpen=false; citizenOpen=false; journalOpen=false; }
        void Change(int cash,int rep,string reason)
        {
            int beforeMoney=Money,beforeRep=Reputation;
            Money=Mathf.Max(0,Money+cash); Reputation=Mathf.Clamp(Reputation+rep,-100,100);
            ledger.Insert(0,$"{reason}  [{Money-beforeMoney:+0;-0;0} ₽; {Reputation-beforeRep:+0;-0;0} реп.]");
            if(ledger.Count>30) ledger.RemoveAt(ledger.Count-1);
        }
        public void AcceptQuest() { if(questDone) return; questAccepted=true; Say("Житель просит лекарство. Найди коробку во дворе или вскрой магазин."); }
        public bool DeliverMedicine()
        {
            if(!questAccepted || questDone || Citizen.Downed || !Near(Citizen.transform.position) || !bag.Contains(4)) return false;
            bag.Remove(4); questDone=true; Change(35,4,"Помощь жителю"); Say("Спасибо! +35 ₽, +4 репутации"); return true;
        }
        public bool UnlockStore()
        {
            if(shopUnlocked || !Near(shopPosition) || breakProgress<3) return false;
            shopUnlocked=true; shopShutter.SetActive(false);
            shopCaption.text="МАГАЗИН ВСКРЫТ";
            ExtraItems.Find(p=>p.Owned).gameObject.SetActive(true); Physics.SyncTransforms();
            Change(0,0,"Магазин вскрыт"); Say("Рольставни открыты. Внутри остался чужой товар."); return true;
        }
        public bool LootStore()
        {
            if(!shopUnlocked || shopLooted || bag.Count>=2) { Say("Нет места или магазин ещё закрыт"); return false; }
            var stock=ExtraItems.Find(p=>p.Owned && p.gameObject.activeSelf);
            if(stock==null || !Near(stock.transform.position,2.1f)) return false;
            if(!stock.GetComponent<WorldLoot>().TakeForHero()) return false;
            stock.gameObject.SetActive(false); bag.Add(4); shopLooted=true;
            Change(0,-10,"Кража из вскрытого магазина"); Say("Украдено лекарство: −10 репутации, деньги не списываются"); return true;
        }
        void InstallArmor()
        {
            if(ArmorInstalled || !bag.Contains(5)) return;
            bag.Remove(5); ArmorInstalled=true; ArmorPoints=50;
            Box("Installed armor",new Vector3(-1.02f,.9f,0),new Vector3(.12f,.85f,2),steel,false,Car);
            Box("Installed armor",new Vector3(1.02f,.9f,0),new Vector3(.12f,.85f,2),steel,false,Car);
            Fog.RegisterDynamic(Car.gameObject); Noise(Car.position,6,Hero,NoiseKind.Work); Say("Броня установлена: +50 защиты машине");
        }
        void DamageCar(float damage)
        {
            float absorbed=Mathf.Min(ArmorPoints,damage); ArmorPoints-=absorbed;
            durability=Mathf.Max(0,durability-(damage-absorbed));
            ShowDamage(damage,absorbed>0?"БРОНЯ / МАШИНА":"МАШИНА");
        }
        void SettleExtraction()
        { if(settled) return; settled=true; Change(100+Mathf.RoundToInt(durability),8,"Успешный выезд"); }
        public void HumanKilled(HumanAgent person)
        {
            if(person.Role==HumanRole.Marauder) { Change(0,0,"Мародёр обезврежен"); return; }
            Change(person.Role==HumanRole.Soldier?-40:-60,person.Role==HumanRole.Soldier?-15:-25,"Убит: "+person.name);
            if(person.Role==HumanRole.Medic || person.Role==HumanRole.Soldier) PatrolHostile=true;
            CloseInteraction();
        }
        public void PatrolAttacked(HumanAgent witness=null)
        {
            PatrolHostile=true;
            if(witness==null) return;
            Transform attacker=driving?Car:Hero;
            foreach(var person in Humans)
                if(person.IsPatrol && Vector3.Distance(person.transform.position,witness.transform.position)<=12) person.AlertTo(attacker);
        }
        public void Dart(HumanAgent medic=null)
        { if((heroStunUntil>0 && Time.time<heroStunUntil+2) || IsOver || HeroStruggling || HeroCaptureImmune || restrainingMedic) return; heroStunUntil=Time.time+1.2f; if(medic) medic.TrackDartPatient(Hero); Say("Усыпляющий дротик! Оглушение 1,2 с"); }
        void TickExpedition()
        {
            if(Press(UnityEngine.InputSystem.Key.Escape)) CloseInteraction();
            if(Press(UnityEngine.InputSystem.Key.J)) { bool open=!journalOpen; CloseInteraction(); journalOpen=open; }
            if(InteractionOpen && (HeroControlsLocked || (shopOpen && !Near(shopPosition)) || (citizenOpen && (Citizen.Downed || !Near(Citizen.transform.position))))) CloseInteraction();
            if(!driving && !HeroControlsLocked && !shopUnlocked && !InteractionOpen && Key(UnityEngine.InputSystem.Key.F) && Near(shopPosition))
            {
                breakProgress+=Time.deltaTime;
                if(Time.time>=breakNoiseAt) { Noise(shopPosition,8,Hero,NoiseKind.Work); breakNoiseAt=Time.time+.7f; }
                if(breakProgress>=3) UnlockStore();
            }
            else breakProgress=0;
            if(!driving && !Scanned && !Medic.Downed && !Medic.IsIncapacitated && !Medic.RescuePatient && (Medic.CurrentTarget==null || IsHero(Medic.CurrentTarget)) && Sight.CanSee(Medic.transform,Hero,Sight.ScannerRange,Sight.HumanAngle))
            {
                scanProgress+=Time.deltaTime;
                if(scanProgress>=2) { Scanned=true; Change(0,0,Sick?"Сканирование: заражён, патруль открывает огонь":"Сканирование: здоров, попытка задержания"); if(Sick) PatrolAttacked(Medic); }
            }
            else if(!Scanned) scanProgress=0;
            TickHeroRestraint();
        }
        bool HeroStunned => Time.time<heroStunUntil;
        void DrawExpedition(float width,float height)
        {
            Panel(new Rect(18,126,330,90));
            GUI.Label(new Rect(30,134,310,25),$"Деньги: {Money} ₽   Репутация: {Reputation}",normal);
            GUI.Label(new Rect(30,162,310,25),$"Инфекция {infection:0}% • Броня {ArmorPoints:0}",small);
            GUI.Label(new Rect(30,188,310,25),"E — общение • J — журнал • Esc — выход",small);
            Panel(new Rect(18,224,380,142)); GUI.Label(new Rect(30,230,355,25),"Журнал событий",normal);
            for(int i=0;i<Mathf.Min(3,ledger.Count);i++) GUI.Label(new Rect(30,259+i*34,355,34),ledger[i],small);
            if(!questDone) GUI.Label(new Rect(18,375,380,50),questAccepted?"Просьба: принеси жителю лекарство.\nКрасная коробка находится севернее гаража.":"У гаража есть житель. Подойди и нажми E.",small);
            string warning=PatrolHostile?"ПАТРУЛЬ ОТКРЫЛ ОГОНЬ":Scanned?"СКАНИРОВАН: ЗДОРОВ — НЕ ДАЙ СЕБЯ ЗАДЕРЖАТЬ":scanProgress>0?$"СКАНИРОВАНИЕ {scanProgress/2:P0}":"";
            if(HeroStunned) warning="ОГЛУШЕНИЕ ДРОТИКОМ";
            if(detainProgress>0) warning+=$" • ЗАДЕРЖАНИЕ {detainProgress/3:P0}";
            GUI.Label(new Rect(width/2-250,60,500,55),warning,normal);
            if(BreakingIn) GUI.Label(new Rect(width/2-190,115,420,40),$"ВСКРЫТИЕ {breakProgress/3:P0} • удерживай F",normal);
            DrawHeroRestraint(width,height);
            if(!InteractionOpen || IsOver) return;
            if(journalOpen)
            {
                float left=width/2-310,top=height/2-190;
                Panel(new Rect(left,top,620,380)); GUI.Label(new Rect(left+18,top+14,580,35),"ЖУРНАЛ ИЗМЕНЕНИЙ",title);
                journalScroll=GUI.BeginScrollView(new Rect(left+16,top+60,588,250),journalScroll,new Rect(0,0,560,Mathf.Max(250,ledger.Count*54)));
                for(int i=0;i<ledger.Count;i++) GUI.Label(new Rect(0,i*54,550,52),ledger[i],small);
                GUI.EndScrollView(); if(GUI.Button(new Rect(left+200,top+325,220,38),"Закрыть (Esc / J)")) CloseInteraction();
                return;
            }
            float x=width/2-250,y=height/2-145;
            Panel(new Rect(x,y,500,290));
            GUI.Label(new Rect(x+18,y+15,460,35),shopOpen?"МАГАЗИН":"ПРОСЬБА ЖИТЕЛЯ",title);
            if(citizenOpen)
            {
                GUI.Label(new Rect(x+18,y+60,460,80),questDone?"Спасибо за лекарство. Ты мне очень помог.":"Мне нужно лекарство. Принесёшь?\nНаграда: 35 ₽ и 4 репутации.",normal);
                if(!questDone && GUI.Button(new Rect(x+18,y+150,220,42),questAccepted?"Передать лекарство":"Помочь"))
                { if(!questAccepted) AcceptQuest(); else if(!DeliverMedicine()) Say("Нужно лекарство в рюкзаке"); }
            }
            else
            {
                GUI.Label(new Rect(x+18,y+60,460,110),"Магазин закрыт, людей внутри нет.\nЗакрой окно и удерживай F у рольставней 3 секунды. Вскрытие шумное.\nКража: −10 репутации. Денежного штрафа нет.",normal);
            }
            if(GUI.Button(new Rect(x+140,y+228,220,40),"Закрыть (Esc)")) CloseInteraction();
        }
    }
    public sealed class ExpeditionPickup : MonoBehaviour
    {
        public int Kind; public bool Owned;
        void Awake() { if(!GetComponent<WorldLoot>()) gameObject.AddComponent<WorldLoot>(); }
    }
}
