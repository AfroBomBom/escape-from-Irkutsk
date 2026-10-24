using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
namespace IrkutskPrototype
{
    // Exists only in the optional sandbox scene, never in EscapePrototype.
    public sealed class CoverSandbox : MonoBehaviour
    {
        bool combat,high;
        bool pair;
        HumanAgent guard;
        HumanAgent looter;
        bool lootMode,lootSamples;
        EscapeGame game;
        void Awake()
        {
            var existing=Object.FindAnyObjectByType<EscapeGame>();
            if(!existing || existing.gameObject.scene!=gameObject.scene)
                new GameObject("Cover sandbox game").AddComponent<EscapeGame>();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OpenFromCommandLine()
        {
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-cover-sandbox")>=0 && SceneManager.GetActiveScene().name!="CoverSandbox") SceneManager.LoadScene("CoverSandbox");
        }
        void Start()
        {
            game=EscapeGame.Instance;
            foreach(var z in game.Zombies) z.gameObject.SetActive(false);
            foreach(var h in game.Humans) h.gameObject.SetActive(false);
            Object.Instantiate(Resources.Load<GameObject>("Cover/LowCover"),new Vector3(20,0,0),Quaternion.identity);
            Object.Instantiate(Resources.Load<GameObject>("Cover/HighCover"),Vector3.zero,Quaternion.identity);
            PlaceHero();
            game.Soldier.gameObject.SetActive(true); game.Soldier.enabled=false;
            var cc=game.Soldier.GetComponent<CharacterController>(); cc.enabled=false;
            game.Soldier.transform.position=new Vector3(20,0,-.97f); cc.enabled=true;
            game.Fog.Refresh();
        }
        void PlaceHero()
        {
            game.Hero.GetComponent<ActorPosture>().TrySetCrouched(false);
            game.Controller.enabled=false; game.Hero.position=new Vector3(high?0:20,0,4); game.Controller.enabled=true;
            game.Hero.forward=Vector3.back; Physics.SyncTransforms(); game.Fog.Refresh();
        }
        void Update()
        {
            if(Keyboard.current==null) return;
            if(Keyboard.current.f2Key.wasPressedThisFrame) ToggleLootTest();
            if(lootMode)
            {
                if(Keyboard.current.f3Key.wasPressedThisFrame && looter && !looter.Downed) looter.Hit(200,false);
                return;
            }
            if(Keyboard.current.f7Key.wasPressedThisFrame)
            { combat=!combat; game.Soldier.enabled=combat; if(combat) game.PatrolAttacked(game.Soldier); }
            if(Keyboard.current.f8Key.wasPressedThisFrame) { high=!high; PlaceHero(); }
            if(Keyboard.current.f9Key.wasPressedThisFrame)
            {
                pair=!pair; game.Medic.gameObject.SetActive(pair);
                if(pair)
                {
                    if(!game.Medic.Downed && !game.Medic.IsIncapacitated)
                    {
                        var cc=game.Medic.GetComponent<CharacterController>(); cc.enabled=false;
                        game.Medic.transform.position=new Vector3(23,0,-2); cc.enabled=true;
                    }
                    game.Medic.enabled=combat;
                    if(combat) game.Medic.AlertTo(game.Hero);
                }
            }
            if(pair) game.Medic.enabled=combat;
            if(guard) guard.enabled=combat;
            if(Keyboard.current.f6Key.wasPressedThisFrame)
            {
                if(!guard)
                {
                    guard=game.SpawnHuman("Прикрытие",HumanRole.Soldier,new Vector3(26,0,-2),game.Soldier.GetComponentInChildren<Renderer>().sharedMaterial);
                    guard.enabled=combat; game.Fog.RegisterDynamic(guard.gameObject);
                }
                else guard.gameObject.SetActive(!guard.gameObject.activeSelf);
            }
            if(Keyboard.current.f10Key.wasPressedThisFrame && !game.Soldier.Downed && game.Soldier.Health>40)
                game.Soldier.Hit((game.Soldier.Health-40)*2,false);
            if(Keyboard.current.f11Key.wasPressedThisFrame && !game.Soldier.Downed && !game.Soldier.IsIncapacitated)
                game.Soldier.Hit(game.Soldier.Health*2+2,false);
            if(Keyboard.current.f12Key.wasPressedThisFrame && pair && !game.Medic.Downed && !game.Medic.IsIncapacitated)
                game.Medic.Hit(game.Medic.Health/.8f+1,false);
        }
        void OnGUI()
        {
            if(lootMode)
            {
                GUI.Box(new Rect(Screen.width/2-320,Screen.height-130,640,115),"ТЕСТ МАРОДЁРА И ДОБЫЧИ\nF2 — вернуться к укрытиям; F3 — убить мародёра и высыпать добычу\nE — подобрать выпавшее; F5 — начать заново\nМародёр: "+looter.State+"; рюкзак "+looter.LootInventory.Count+" / 3");
                return;
            }
            GUI.Box(new Rect(Screen.width/2-320,Screen.height-175,640,160),"ПЕСОЧНИЦА УКРЫТИЙ И СПАСЕНИЯ\nC — присесть; WASD — движение; F5 — начать заново\nF6 — третий солдат; F7 — бой; F8 — укрытие; F9 — врач\nF10 — ранить солдата; F11 — тяжело ранить солдата; F12 — врача\nОтойди от раненого, чтобы дать напарнику безопасно помочь.\nСолдат: "+game.Soldier.State+" / "+game.Soldier.Health.ToString("F0")+" HP\nВрач: "+(pair?game.Medic.State+" "+(game.Medic.RescueProgress*100).ToString("F0")+"%":"выключен"));
            GUI.Label(new Rect(15,Screen.height-40,350,30),"F2 — тест мародёра и выпадающей добычи");
        }
        void ToggleLootTest()
        {
            lootMode=!lootMode; combat=false; pair=false;
            game.Medic.gameObject.SetActive(false); if(guard) guard.gameObject.SetActive(false);
            game.Soldier.enabled=false; game.Soldier.gameObject.SetActive(!lootMode);
            if(!looter) looter=game.Humans.Find(h=>h.Role==HumanRole.Marauder);
            looter.gameObject.SetActive(lootMode);
            if(!lootMode) return;
            if(!lootSamples)
            {
                lootSamples=true;
                var cc=looter.GetComponent<CharacterController>(); cc.enabled=false; looter.transform.position=new Vector3(20,0,-7); cc.enabled=true;
                looter.transform.forward=Vector3.forward;
                game.SpawnLoosePart(0,new Vector3(20,.45f,-3));
                game.SpawnExtra("Тест: лекарство",4,new Vector3(21,.4f,-2),game.Soldier.GetComponentInChildren<Renderer>().sharedMaterial,false);
                game.SpawnExtra("Тест: бронепластина",5,new Vector3(19,.4f,-1),game.Soldier.GetComponentInChildren<Renderer>().sharedMaterial,false);
            }
            game.Controller.enabled=false; game.Hero.position=new Vector3(23,0,-11); game.Controller.enabled=true;
            game.Hero.forward=Vector3.forward; Physics.SyncTransforms(); game.Fog.Refresh();
        }
    }
}
