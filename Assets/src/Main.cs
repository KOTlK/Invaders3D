using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using static Globals;

public class Main : MonoBehaviour {
    public TextAsset      VarsAsset;
    public EntityManager  EntityManager;
    public TaskRunner     TaskRunner;
    public Events         Events;
    public ResourceSystem ResourceSystem;
    public SaveSystem     SaveSystem;
    public PlayerInput    Input;
    public ResourceLink   PlayerPrefab;
    public Bullets        Bullets;
    
    private void Awake() {
        Vars.ParseVars(VarsAsset);
        TaskRunner     = new TaskRunner();
        Events         = new Events();
        ResourceSystem = new ResourceSystem();
        SaveSystem     = new SaveSystem();
        Input          = new PlayerInput();

        Input.Initialize(EntityManager);

        Singleton<SaveSystem>.Create(SaveSystem);
        Singleton<ResourceSystem>.Create(ResourceSystem);
        Singleton<EntityManager>.Create(EntityManager);
        Singleton<TaskRunner>.Create(TaskRunner);
        Singleton<Events>.Create(Events);
        Singleton<PlayerInput>.Create(Input);
        Singleton<Bullets>.Create(Bullets);
        Bullets.Init();

        EntityManager.BakeEntities();
        var player = EntityManager.CreateEntity(PlayerPrefab, new Vector3(0, PlanesHeight, 0), Quaternion.identity);
        Input.Gameplay.Initialize(player);
    }

    private void OnDestroy() {
        SaveSystem.Dispose();
    }
    
    private void Start() {
        EntityManager.BakeEntities();
    }
    
    private void Update() {
        Clock.Update();
        Input.UpdateInput();
        EntityManager.Execute();
        Bullets.UpdateBehavior();
        TaskRunner.RunTaskGroup(TaskGroupType.ExecuteAlways);
    }

    [ConsoleCommand("quit", "exit")]
    public static void Quit() {
#if UNITY_EDITOR
        EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }
}
