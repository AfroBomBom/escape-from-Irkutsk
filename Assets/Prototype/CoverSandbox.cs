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
            GUI.Box(new Rect(Screen.width/2-320,Screen.height-175,640,160),"ПЕСОЧНИЦА УКРЫТИЙ И СПАСЕНИЯ\nC — присесть; WASD — движение; F5 — начать заново\nF6 — третий солдат; F7 — бой; F8 — укрытие; F9 — врач\nF10 — ранить солдата; F11 — тяжело ранить солдата; F12 — врача\nОтойди от раненого, чтобы дать напарнику безопасно помочь.\nСолдат: "+game.Soldier.State+" / "+game.Soldier.Health.ToString("F0")+" HP\nВрач: "+(pair?game.Medic.State+" "+(game.Medic.RescueProgress*100).ToString("F0")+"%":"выключен"));
        }
    }
}
