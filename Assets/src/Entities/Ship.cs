using UnityEngine;

public abstract class Ship : Entity {
    public float   MaxSpeed = 10f;
    public float   MaxAcceleration = 12f;
    public float   MaxAngularRotation = 180f;
    public float   RotationSlowRadius = 30f;
    public float   Drag = 20f;

    public Vector3 Velocity;
    public float   Orientation;
}