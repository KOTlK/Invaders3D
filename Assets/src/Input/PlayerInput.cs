using UnityEngine;

public class KeyBinds {
    //Gameplay
    public KeyCode MoveForward = KeyCode.W;
    public KeyCode MoveBack = KeyCode.S;
    public KeyCode MoveRight = KeyCode.D;
    public KeyCode MoveLeft = KeyCode.A;
}

public enum Keymaps {
    Gameplay,

}

public abstract class Keymap {
    public EntityManager EntityManager;
    public KeyBinds      Binds = new();

    public void SetupBinds(KeyBinds binds) {
        Binds = binds;
    }

    public abstract void Reset();
    public abstract void UpdateInput();
}

public class GameplayKeymap : Keymap {
    public EntityHandle Player;
    public Vector3 MovementDirection;
    public float   LookRotation;

    public void Initialize(EntityHandle player) {
        Player = player;
    }

    public override void UpdateInput() {
        Reset();
        var f = 0f;
        var r = 0f;

        if(Input.GetKey(Binds.MoveForward)) {
            f += 1f;
        }

        if(Input.GetKey(Binds.MoveBack)) {
            f -= 1f;
        }

        if(Input.GetKey(Binds.MoveRight)) {
            r += 1f;
        }

        if(Input.GetKey(Binds.MoveLeft)) {
            r -= 1f;
        }

        MovementDirection = new Vector3(r, f).normalized;

        if(EntityManager.GetEntity<Player>(Player, out var player)) {
            var p      = Camera.main.WorldToScreenPoint(player.transform.position);
            var target = Input.mousePosition;
            var x      = target.x - p.x;
            var y      = target.y - p.y;

            LookRotation = Mathf.Atan2(-x, y) * Mathf.Rad2Deg;
        } else {
            LookRotation =  0f;
        }
    }

    public override void Reset() {
        MovementDirection = Vector3.zero;
        LookRotation = 0f;
    }
}

public class PlayerInput {
    public EntityManager  EntityManager;
    public GameplayKeymap Gameplay;

    public Keymaps CurrentKeymapType;
    public Keymap  CurrentKeymap;

    public void Initialize(EntityManager em) {
        Gameplay = new GameplayKeymap();

        Gameplay.EntityManager = em;
        CurrentKeymap = Gameplay;
        CurrentKeymapType = Keymaps.Gameplay;
    }

    public void SwitchActiveKeymap(Keymaps keymap) {
        CurrentKeymapType = keymap;

        switch(keymap) {
            case Keymaps.Gameplay : {
                CurrentKeymap.Reset();
                CurrentKeymap = Gameplay;
            }
            break;

            default: {
                Debug.LogError($"Can't switch to {keymap}");
            }
            break;
        }
    }

    public void UpdateInput() {
        CurrentKeymap.UpdateInput();
    }
}