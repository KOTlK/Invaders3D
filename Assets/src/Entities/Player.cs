using UnityEngine;

public class Player : Ship {
    public PlayerInput Input;

    public override void OnBaking() {
        OnCreate();
    }

    public override void OnCreate() {
        Input = Singleton<PlayerInput>.Instance;
    }

    public override void Execute() {
        var p = transform.position;
        var moveDir  = Input.Gameplay.MovementDirection;
        var targetAngle = Input.Gameplay.LookRotation;

        Vector3 vel;

        if(moveDir.sqrMagnitude < 0.01f) {
            vel = moveDir * Drag;
        } else {
            vel = moveDir * MaxSpeed;
        }

        var targetVelocity = vel - Velocity; 

        var targetRotation = Mathf.DeltaAngle(Orientation, targetAngle);
        var size = Mathf.Abs(targetRotation);

        if(size < RotationSlowRadius) {
            targetRotation = Mathf.Sign(targetRotation) * MaxAngularRotation * size / RotationSlowRadius;
        } else {
            targetRotation = Mathf.Sign(targetRotation) * MaxAngularRotation;
        }

        p += Velocity * Clock.Delta;

        MoveEntity(p, Quaternion.AngleAxis(Orientation, Vector3.forward));

        Velocity += targetVelocity * Clock.Delta;
        Orientation += targetRotation * Clock.Delta;

        if(Orientation > 360f) {
            Orientation -= 360f;
        } else if(Orientation < -360f) {
            Orientation += 360f;
        }
    }
}