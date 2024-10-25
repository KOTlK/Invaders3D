using UnityEngine;

public class Player : Ship {
    public PlayerInput Input;

    public override void OnBaking() {
        OnCreate();
    }

    public override void OnCreate() {
        base.OnCreate();
        Input = Singleton<PlayerInput>.Instance;
    }

    public override void Execute() {
        var p = transform.position;
        var moveDir  = Input.Gameplay.MovementDirection;
        var targetAngle = Input.Gameplay.LookRotation;

        var vel = moveDir * MaxSpeed;

        Vector3 targetVelocity;

        if(moveDir.sqrMagnitude < 0.01f) {
            targetVelocity = Vector3.zero - Velocity;
            var len = targetVelocity.magnitude;

            if(len < DragSlowRadius) {
                targetVelocity = targetVelocity.normalized * (Drag * len / DragSlowRadius);
            } else if (len < Drag) {
                targetVelocity = targetVelocity.normalized * Drag;
            }

        } else {
            targetVelocity = vel - Velocity;

            if(targetVelocity.magnitude > MaxAcceleration) {
                targetVelocity = targetVelocity.normalized * MaxAcceleration;
            }
        }

        var targetRotation = Mathf.DeltaAngle(Orientation, targetAngle);
        var size = Mathf.Abs(targetRotation);

        if(size < RotationSlowRadius) {
            targetRotation = Mathf.Sign(targetRotation) * MaxAngularRotation * size / RotationSlowRadius;
        } else {
            targetRotation = Mathf.Sign(targetRotation) * MaxAngularRotation;
        }

        p += Velocity * Clock.Delta;

        MoveEntity(p, Quaternion.AngleAxis(Orientation, Vector3.up));

        Velocity += targetVelocity * Clock.Delta;
        Orientation += targetRotation * Clock.Delta;

        if(Orientation > 180f) {
            Orientation -= 360f;
        } else if(Orientation < -180f) {
            Orientation += 360f;
        }

        if(Input.Gameplay.Shooting) {
            Shoot();
            WasShootingPreviousFrame = true;
        } else {
            WasShootingPreviousFrame = false;
        }
    }
}