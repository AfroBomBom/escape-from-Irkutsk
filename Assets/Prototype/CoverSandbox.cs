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
                    var cc=game.Medic.GetComponent<CharacterController>(); cc.enabled=false;
                    game.Medic.transform.position=new Vector3(23,0,-2); cc.enabled=true;
                    game.Medic.enabled=combat;
                    if(combat) game.Medic.AlertTo(game.Hero);
                }
            }
            if(pair) game.Medic.enabled=combat;
            if(Keyboard.current.f10Key.wasPressedThisFrame && !game.Soldier.Downed && game.Soldier.Health>40)
                game.Soldier.Hit((game.Soldier.Health-40)*2,false);
        }
        void OnGUI()
        {
            GUI.Box(new Rect(Screen.width/2-300,Screen.height-135,600,120),"ПЕСОЧНИЦА УКРЫТИЙ И ТАКТИКИ\nC — присесть / встать; WASD — движение\nF7 — бой; F8 — низкое / высокое; F9 — добавить / убрать врача\nF10 — ранить солдата до 40 HP; F5 — начать заново\nСолдат: "+game.Soldier.TacticalTask+" / "+game.Soldier.Health.ToString("F0")+" HP; врач: "+(pair?game.Medic.TacticalTask.ToString():"выключен"));
        }
    }
}
