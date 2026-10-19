using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace IrkutskPrototype
{
    public sealed class EscapeGame : MonoBehaviour
    {
        public static EscapeGame Instance;
        public const int SolidMask = 1 << 8;
        public Transform Hero, Car;
        public CharacterController Controller;
        public readonly List<Infected> Zombies = new List<Infected>();
        public readonly List<SparePart> Parts = new List<SparePart>();
        readonly List<int> bag = new List<int>();
        readonly bool[] installed = new bool[4];
        readonly string[] partNames = { "Аккумулятор", "Двигатель", "Колесо", "Канистра" };
        readonly List<Material> materials = new List<Material>();
        Camera view;
        Transform gun, arm;
        AudioSource sound;
        AudioClip shotClip, hitClip;
        float hp = 100, durability = 100, speed, attackAt, shotAt, reloadAt, footAt, started, finalTime;
        float flashUntil, noiseUntil, messageUntil;
        Vector3 noisePosition;
        float noiseRadius;
        int ammo = 6;
        bool driving, finished, dead;
        string message = "Собери детали и вернись в гараж";
        public bool IsDriving => driving;
        public bool IsOver => finished || dead;
        public Vector3 Target => driving ? Car.position : Hero.position;
        Material orange, blue, green, dark, asphalt, snow, steel, amber, red;
        GUIStyle normal, title, small;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<EscapeGame>() == null)
                new GameObject("Escape prototype").AddComponent<EscapeGame>();
        }

        void Awake()
        {
            Instance = this;
            started = Time.time;
            Application.targetFrameRate = 60;
            orange = Mat(new Color(.95f,.43f,.12f)); blue = Mat(new Color(.15f,.48f,.64f));
            green = Mat(new Color(.36f,.54f,.22f)); dark = Mat(new Color(.12f,.16f,.19f));
            asphalt = Mat(new Color(.27f,.30f,.33f)); snow = Mat(new Color(.70f,.76f,.76f));
            steel = Mat(new Color(.43f,.46f,.48f)); amber = Mat(new Color(1f,.75f,.15f));
            red = Mat(new Color(.75f,.17f,.16f));
            BuildWorld();
            sound = gameObject.AddComponent<AudioSource>(); sound.spatialBlend = 0;
            shotClip = Tone(120, .12f); hitClip = Tone(65,.08f);
        }

        Material Mat(Color color)
        {
            var surface = Resources.Load<Material>("PrototypeSurface");
            var shader = surface != null ? surface.shader : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader); m.color = color; materials.Add(m); return m;
        }

        GameObject Box(string label, Vector3 p, Vector3 size, Material m, bool solid = true, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = label;
            go.transform.SetParent(parent, false); go.transform.localPosition = p; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (solid) go.layer = 8; else { var c = go.GetComponent<Collider>(); c.enabled = false; Destroy(c); }
            return go;
        }
        void Wall(Vector3 p, Vector3 size) => Box("Wall", p, size, steel);
        void Building(float x, float z, float w, float d)
        {
            Box("Building", new Vector3(x,2,z), new Vector3(w,4,d), steel);
            Box("Roof", new Vector3(x,4.1f,z), new Vector3(w+.3f,.2f,d+.3f), snow, false);
            for (float a=-w/2+1; a<w/2; a+=2)
                Box("Window",new Vector3(x+a,2,z-d/2-.02f),new Vector3(.8f,1,.08f),amber,false);
        }
        GameObject Label(string text, Vector3 p, Color color, float size = .3f)
        {
            var go = new GameObject(text); go.transform.position=p; go.transform.rotation=Quaternion.Euler(65,0,0);
            var t=go.AddComponent<TextMesh>(); t.text=text; t.characterSize=size; t.fontSize=40;
            t.anchor=TextAnchor.MiddleCenter; t.color=color; return go;
        }

        void BuildWorld()
        {
            foreach (var old in FindObjectsByType<Camera>(FindObjectsSortMode.None)) old.gameObject.SetActive(false);
            foreach (var old in FindObjectsByType<Light>(FindObjectsSortMode.None)) old.gameObject.SetActive(false);
            RenderSettings.ambientLight = new Color(.68f,.72f,.77f);
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type=LightType.Directional;
            sun.intensity=1.5f; sun.transform.rotation=Quaternion.Euler(55,-35,0);
            Box("City ground",new Vector3(0,-.3f,0),new Vector3(64,.5f,52),snow);
            Box("Courtyard",new Vector3(0,-.02f,0),new Vector3(53,.08f,43),asphalt,false);
            Box("Highway ground",new Vector3(75,-.3f,0),new Vector3(90,.5f,22),snow);
            Box("Highway",new Vector3(75,-.02f,0),new Vector3(90,.08f,12),asphalt,false);
            for(int i=0;i<16;i++) Box("Lane marking",new Vector3(32+i*5,.03f,0),new Vector3(2,.02f,.13f),amber,false);
            Wall(new Vector3(-31,1,0),new Vector3(1,2,52));
            Wall(new Vector3(0,1,25),new Vector3(64,2,1)); Wall(new Vector3(0,1,-25),new Vector3(64,2,1));
            Wall(new Vector3(31,1,16),new Vector3(1,2,20)); Wall(new Vector3(31,1,-16),new Vector3(1,2,20));
            Wall(new Vector3(75,.6f,7),new Vector3(88,1.2f,.5f)); Wall(new Vector3(75,.6f,-7),new Vector3(88,1.2f,.5f));
            Building(-10,17,14,8); Building(12,18,12,6); Building(16,-17,13,8);
            Building(-25,7,7,11); Building(4,7,6,5);
            // Open-roof garage, wide east entrance.
            Box("Garage floor",new Vector3(-17,.02f,-12),new Vector3(14,.1f,12),dark,false);
            Wall(new Vector3(-24,1.5f,-12),new Vector3(.5f,3,12));
            Wall(new Vector3(-17,1.5f,-18),new Vector3(14,3,.5f));
            Wall(new Vector3(-17,1.5f,-6),new Vector3(14,3,.5f));
            Box("Workbench",new Vector3(-22, .6f,-15),new Vector3(2,1.2f,4),orange);
            Label("ГАРАЖ",new Vector3(-17,.15f,-5),Color.white,.4f);
            var carGo = new GameObject("Repairable car"); Car=carGo.transform; Car.position=new Vector3(-16,0,-12);
            Car.rotation=Quaternion.Euler(0,90,0);
            Box("Body",new Vector3(0,.65f,0),new Vector3(1.8f,.7f,3.5f),blue,false,Car);
            Box("Cabin",new Vector3(0,1.25f,-.2f),new Vector3(1.45f,.6f,1.8f),dark,false,Car);
            Box("Bonnet",new Vector3(0,1,1.2f),new Vector3(1.6f,.15f,1),steel,false,Car);
            for(int i=0;i<4;i++) Box("Wheel",new Vector3(i%2==0?-1:1,.4f,i<2?-1.1f:1.1f),new Vector3(.35f,.7f,.7f),dark,false,Car);
            Box("Headlight",new Vector3(-.6f,.8f,1.8f),new Vector3(.35f,.2f,.05f),amber,false,Car);
            Box("Headlight",new Vector3(.6f,.8f,1.8f),new Vector3(.35f,.2f,.05f),amber,false,Car);
            // Waist-high parked vehicles and crates provide cover.
            Box("Abandoned van",new Vector3(12,1,0),new Vector3(2.5f,2,5),red);
            Box("Crates",new Vector3(-4,.7f,-2),new Vector3(3,1.4f,2),orange);
            Box("Highway obstacle",new Vector3(52,.6f,3.8f),new Vector3(3,1.2f,3),steel);
            Box("Highway obstacle",new Vector3(76,.6f,-3.7f),new Vector3(3,1.2f,3),steel);
            Label("ВЫЕЗД →",new Vector3(24,.2f,4),Color.white,.32f);
            Box("Finish",new Vector3(106,.06f,0),new Vector3(1,.05f,12),green,false);
            Label("БЕЗОПАСНАЯ ЗОНА",new Vector3(109,.2f,0),Color.white,.3f);
            Vector3[] locations={new Vector3(-18,.5f,4),new Vector3(1,.5f,18),new Vector3(20,.5f,9),new Vector3(16,.5f,-9)};
            Material[] colors={amber,red,dark,green};
            for(int i=0;i<4;i++)
            {
                var p=Box(partNames[i],locations[i],new Vector3(.9f,.9f,.9f),colors[i],false);
                var part=p.AddComponent<SparePart>(); part.Kind=i; Parts.Add(part);
                Box("Pickup marker",new Vector3(0,1,0),new Vector3(.2f,.2f,.2f),amber,false,p.transform);
                Label(partNames[i],locations[i]+Vector3.up*1.3f,Color.white,.18f).transform.SetParent(p.transform,true);
            }
            Hero=new GameObject("Mechanic").transform; Hero.position=new Vector3(-10,0,-10);
            Controller=Hero.gameObject.AddComponent<CharacterController>(); Controller.height=1.8f; Controller.radius=.32f; Controller.center=Vector3.up*.9f;
            Box("Jacket",new Vector3(0,1,0),new Vector3(.65f,1,.4f),orange,false,Hero);
            Box("Head",new Vector3(0,1.7f,0),new Vector3(.4f,.4f,.4f),snow,false,Hero);
            Box("Backpack",new Vector3(0,1,-.32f),new Vector3(.55f,.6f,.25f),green,false,Hero);
            arm=Box("Fist",new Vector3(-.43f,1,.3f),new Vector3(.2f,.25f,.45f),snow,false,Hero).transform;
            gun=Box("Pistol",new Vector3(.4f,1,.4f),new Vector3(.15f,.2f,.55f),dark,false,Hero).transform;
            Vector3[] enemies={new Vector3(-18,0,10),new Vector3(13,0,10),new Vector3(20,0,-10),new Vector3(43,0,-3),new Vector3(59,0,2),new Vector3(70,0,-2),new Vector3(87,0,3),new Vector3(95,0,-3)};
            foreach(var p in enemies)
            {
                var z=new GameObject("Infected"); z.transform.position=p;
                var cc=z.AddComponent<CharacterController>(); cc.height=1.8f; cc.radius=.35f; cc.center=Vector3.up*.9f;
                Box("Torso",new Vector3(0,1,0),new Vector3(.65f,1,.45f),green,false,z.transform);
                Box("Head",new Vector3(0,1.7f,0),new Vector3(.45f,.4f,.45f),snow,false,z.transform);
                Box("Hands",new Vector3(0,1,.5f),new Vector3(1,.2f,.2f),green,false,z.transform);
                var enemy=z.AddComponent<Infected>(); Zombies.Add(enemy);
            }
            view=new GameObject("Overhead camera").AddComponent<Camera>(); view.orthographic=true; view.orthographicSize=15;
            view.transform.rotation=Quaternion.Euler(65,0,0); view.farClipPlane=180; view.backgroundColor=dark.color;
            view.gameObject.AddComponent<AudioListener>(); UpdateCamera(true);
            Physics.SyncTransforms();
        }

        bool Key(Key key) => Keyboard.current != null && Keyboard.current[key].isPressed;
        bool Press(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        Vector2 Movement()
        {
            return Vector2.ClampMagnitude(new Vector2((Key(UnityEngine.InputSystem.Key.D)?1:0)-(Key(UnityEngine.InputSystem.Key.A)?1:0),
                (Key(UnityEngine.InputSystem.Key.W)?1:0)-(Key(UnityEngine.InputSystem.Key.S)?1:0)),1);
        }
        void Update()
        {
            if(Press(UnityEngine.InputSystem.Key.F5)) { SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); return; }
            if(IsOver) return;
            if(driving) Drive(); else Walk();
            if(Press(UnityEngine.InputSystem.Key.E)) Interact();
            if(!driving)
            {
                if((Mouse.current!=null && Mouse.current.leftButton.wasPressedThisFrame)||Press(UnityEngine.InputSystem.Key.Space)) Punch();
                if((Mouse.current!=null && Mouse.current.rightButton.wasPressedThisFrame)||Press(UnityEngine.InputSystem.Key.Q)) Shoot();
                if(Press(UnityEngine.InputSystem.Key.R) && ammo<6 && reloadAt==0) { reloadAt=Time.time+1.5f; Say("Перезарядка…"); }
            }
            if(reloadAt>0 && Time.time>=reloadAt) { ammo=6; reloadAt=0; }
            arm.localPosition=new Vector3(-.43f,1,Time.time<attackAt-.32f?.75f:.3f);
            if(hp<=0 || durability<=0) { dead=true; finalTime=Time.time-started; }
        }
        void LateUpdate() { if(view!=null) UpdateCamera(false); }
        void UpdateCamera(bool instant)
        {
            var focus=driving?Car.position:Hero.position;
            var desired=focus+new Vector3(0,27,-12.6f);
            view.transform.position=instant?desired:Vector3.Lerp(view.transform.position,desired,1-Mathf.Exp(-6*Time.deltaTime));
            view.orthographicSize=Mathf.Lerp(view.orthographicSize,driving?18:15,Time.deltaTime*3);
        }
        void Walk()
        {
            Vector2 input=Movement();
            bool quiet=Key(UnityEngine.InputSystem.Key.LeftCtrl)||Key(UnityEngine.InputSystem.Key.RightCtrl);
            bool sprint=!quiet && Key(UnityEngine.InputSystem.Key.LeftShift);
            float pace=quiet?1.8f:sprint?6:3.6f;
            Controller.Move((new Vector3(input.x,0,input.y)*pace+Vector3.down*8)*Time.deltaTime);
            if(Mouse.current!=null)
            {
                var ray=view.ScreenPointToRay(Mouse.current.position.ReadValue());
                if(new Plane(Vector3.up,Hero.position).Raycast(ray,out float d))
                {
                    Vector3 aim=ray.GetPoint(d)-Hero.position; aim.y=0;
                    if(aim.sqrMagnitude>.1f) Hero.rotation=Quaternion.LookRotation(aim);
                }
            }
            if(input.sqrMagnitude>.1f && !quiet && Time.time>=footAt)
            { Noise(Hero.position,sprint?12:5); footAt=Time.time+(sprint?.3f:.5f); }
        }
        void Drive()
        {
            Vector2 input=Movement();
            speed=Mathf.MoveTowards(speed,input.y*(input.y<0?8:20),Time.deltaTime*(input.y==0?9:12));
            if(Mathf.Abs(speed)>.2f) Car.Rotate(0,input.x*75*Time.deltaTime*Mathf.Sign(speed),0);
            Vector3 travel=Car.forward*speed*Time.deltaTime;
            if(travel.sqrMagnitude>0 && Physics.BoxCast(Car.position+Vector3.up*.7f,new Vector3(.85f,.6f,1.65f),travel.normalized,out _,Car.rotation,travel.magnitude+.1f,SolidMask))
            { durability-=Mathf.Abs(speed)*.8f; speed=-speed*.25f; Say("Столкновение! Береги машину"); }
            else Car.position+=travel;
            if(Mathf.Abs(speed)>1) Noise(Car.position,24);
            foreach(var z in Zombies)
                if(!z.Downed && Vector3.Distance(z.transform.position,Car.position)<2.3f && Mathf.Abs(speed)>5)
                { z.Hit(60,2.5f,Car.forward*2); durability-=3; }
            if(Car.position.x>106) { finished=true; finalTime=Time.time-started; Say("Ты выбрался из Иркутска!"); }
        }
        void Interact()
        {
            if(driving) { Say("Доедь до зелёной линии в конце трассы"); return; }
            if(Vector3.Distance(Hero.position,Car.position)<4.2f)
            {
                if(bag.Count>0) { int k=bag[0]; bag.RemoveAt(0); installed[k]=true; Noise(Car.position,12); Say("Установлено: "+partNames[k]); return; }
                if(InstalledCount()==4) { driving=true; Hero.gameObject.SetActive(false); Say("W/S — газ и тормоз, A/D — поворот. На трассу!"); return; }
                Say("Не хватает деталей. Ищи цветные кубы во дворах"); return;
            }
            SparePart nearest=null; float distance=2.4f;
            foreach(var p in Parts) if(p!=null && p.gameObject.activeSelf)
            { float d=Vector3.Distance(Hero.position,p.transform.position); if(d<distance) { nearest=p; distance=d; } }
            if(nearest==null) return;
            if(bag.Count>=2) { Say("Рюкзак полон — отнеси детали в гараж"); return; }
            bag.Add(nearest.Kind); nearest.gameObject.SetActive(false);
            Say("Взято: "+partNames[nearest.Kind]);
        }
        void Punch()
        {
            if(Time.time<attackAt) return; attackAt=Time.time+.6f; Noise(Hero.position,7); sound.PlayOneShot(hitClip,.4f);
            foreach(var z in Zombies)
                if(!z.Downed && Vector3.Distance(Hero.position,z.transform.position)<2 && Vector3.Dot(Hero.forward,(z.transform.position-Hero.position).normalized)>.25f &&
                    !Physics.Linecast(Hero.position+Vector3.up,z.transform.position+Vector3.up,SolidMask))
                    z.Hit(25,.65f,Hero.forward*.45f);
        }
        void Shoot()
        {
            if(Time.time<shotAt || reloadAt>0) return;
            if(ammo==0) { Say("Нет патронов — нажми R"); return; }
            ammo--; shotAt=Time.time+.35f; flashUntil=Time.time+.08f;
            Noise(Hero.position,30); sound.PlayOneShot(shotClip,.65f);
            Vector3 origin=Hero.position+Vector3.up;
            var hits=Physics.RaycastAll(origin,Hero.forward,35);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            Vector3 end=origin+Hero.forward*35;
            foreach(var h in hits)
            {
                if(h.collider.gameObject==Hero.gameObject) continue;
                var z=h.collider.GetComponent<Infected>();
                if(z!=null && !z.Downed) { z.Hit(20,1.6f,Hero.forward*.25f); end=h.point; break; }
                if(h.collider.gameObject.layer==8) { end=h.point; break; }
            }
            var tracer=new GameObject("Shot tracer"); var line=tracer.AddComponent<LineRenderer>();
            line.sharedMaterial=amber; line.startWidth=.07f; line.endWidth=.02f; line.positionCount=2;
            line.SetPosition(0,origin+Hero.forward*.6f); line.SetPosition(1,end); Destroy(tracer,.09f);
        }
        public void Noise(Vector3 position,float radius)
        {
            noisePosition=position; noiseRadius=radius; noiseUntil=Time.time+.45f;
            foreach(var z in Zombies) if(!z.Downed && Vector3.Distance(z.transform.position,position)<radius) z.Hear(position);
        }
        public void Damage(float value)
        { if(IsOver) return; if(driving) durability=Mathf.Max(0,durability-value); else hp=Mathf.Max(0,hp-value); }
        void Say(string text) { message=text; messageUntil=Time.time+4; }
        int InstalledCount() { int count=0; foreach(bool b in installed) if(b)count++; return count; }
        AudioClip Tone(float frequency,float seconds)
        {
            int n=(int)(22050*seconds); float[] data=new float[n];
            var rng=new System.Random(17);
            for(int i=0;i<n;i++) data[i]=((float)rng.NextDouble()*.5f+Mathf.Sin(i*frequency*2*Mathf.PI/22050)*.5f)*(1f-(float)i/n);
            var clip=AudioClip.Create("Prototype sound",n,1,22050,false); clip.SetData(data,0); return clip;
        }
        void OnGUI()
        {
            if(Hero==null) return;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f); GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            float width=Screen.width/scale, height=Screen.height/scale;
            if(normal==null)
            {
                normal=new GUIStyle(GUI.skin.label){fontSize=18,wordWrap=true}; normal.normal.textColor=Color.white;
                title=new GUIStyle(normal){fontSize=27,fontStyle=FontStyle.Bold}; small=new GUIStyle(normal){fontSize=14};
            }
            Panel(new Rect(18,18,300,100)); GUI.Label(new Rect(32,28,270,30),"ПОБЕГ ИЗ ИРКУТСКА",normal);
            GUI.Label(new Rect(32,60,270,28),driving?$"Машина: {durability:0}%  •  {Mathf.Abs(speed)*3.6f:0} км/ч":$"Здоровье: {hp:0}%  •  Патроны: {ammo}/6",normal);
            GUI.Label(new Rect(32,88,270,25),$"Время: {(IsOver?finalTime:Time.time-started):0} с",small);
            Panel(new Rect(width-294,18,276,190)); GUI.Label(new Rect(width-280,28,250,30),$"ДЕТАЛИ  {InstalledCount()}/4",normal);
            for(int i=0;i<4;i++)
            { string state=installed[i]?"Установлено":bag.Contains(i)?"В рюкзаке":"Найти";
                GUI.Label(new Rect(width-280,65+i*31,250,30),partNames[i]+" — "+state,small); }
            Panel(new Rect(18,height-110,700,92));
            GUI.Label(new Rect(32,height-102,680,26),driving?"W/S — газ / тормоз / задний ход   •   A/D — руль":"WASD — движение   •   Мышь — прицел   •   E — взять / установить / сесть",small);
            GUI.Label(new Rect(32,height-76,680,26),driving?"Объезжай преграды. Зелёная линия — выход из города.":"Ctrl — тихий шаг   •   Shift — бег   •   ЛКМ / Space — удар",small);
            GUI.Label(new Rect(32,height-50,680,26),driving?"F5 — начать заново":"ПКМ / Q — пистолет (оглушение)   •   R — перезарядка   •   F5 — заново",small);
            if(!driving)
            {
                Panel(new Rect(width-294,height-110,276,92));
                GUI.Label(new Rect(width-280,height-101,252,25),$"РЮКЗАК {bag.Count}/2",normal);
                GUI.Label(new Rect(width-280,height-70,252,46),bag.Count==0?"Пусто":string.Join(" • ",bag.ConvertAll(k=>partNames[k])),small);
                if(Key(UnityEngine.InputSystem.Key.LeftCtrl)||Key(UnityEngine.InputSystem.Key.RightCtrl))
                    GUI.Label(new Rect(width/2-180,18,360,35),"ТИХИЙ ШАГ — тебя не слышно",normal);
            }
            if(Time.time<messageUntil) { Panel(new Rect(width/2-280,height-168,560,44)); GUI.Label(new Rect(width/2-265,height-160,530,35),message,normal); }
            if(!driving && Vector3.Distance(Hero.position,Car.position)<4.2f)
                GUI.Label(new Rect(width/2-220,height/2+105,440,35),bag.Count>0?"E — установить деталь":InstalledCount()==4?"E — сесть и уехать":"Машина: нужны ещё детали",normal);
            foreach(var z in Zombies) if(!z.Downed && Vector3.Distance(Target,z.transform.position)<21)
            {
                Vector3 p=view.WorldToScreenPoint(z.transform.position+Vector3.up*2.6f);
                if(p.z>0) GUI.Label(new Rect(p.x/scale-45,(Screen.height-p.y)/scale,110,25),z.Status,small);
            }
            if(IsOver)
            {
                Panel(new Rect(width/2-310,height/2-105,620,210));
                GUI.Label(new Rect(width/2-280,height/2-80,560,50),finished?"ТЫ ВЫБРАЛСЯ!":"ПОБЕГ НЕ УДАЛСЯ",title);
                GUI.Label(new Rect(width/2-280,height/2-20,560,50),finished?$"Время: {finalTime:0} с. Машина сохранена на {durability:0}%.":"Зомби или столкновения остановили тебя.",normal);
                if(GUI.Button(new Rect(width/2-130,height/2+45,260,40),"Начать заново (F5)")) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }
        void Panel(Rect rect) { var old=GUI.color; GUI.color=new Color(.06f,.09f,.12f,.92f); GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=old; }
        void OnDrawGizmos()
        {
            if(!Application.isPlaying || Time.time>noiseUntil) return;
            Gizmos.color=Color.yellow; Gizmos.DrawWireSphere(noisePosition,noiseRadius);
        }
        void OnDestroy()
        {
            foreach(var m in materials) if(m!=null) Destroy(m);
            if(shotClip!=null) Destroy(shotClip); if(hitClip!=null) Destroy(hitClip);
            if(Instance==this) Instance=null;
        }
    }

    public sealed class SparePart : MonoBehaviour { public int Kind; }
}
