using UnityEngine;

public abstract class Ship : Entity {
    public float            MaxSpeed                    = 10f;
    public float            MaxAcceleration             = 12f;
    public float            MaxAngularRotation          = 180f;
    public float            RotationSlowRadius          = 30f;
    public float            Drag                        = 20f;
    public float            DragSlowRadius              = 2f;

    public float            Bps;
    public float            MaxDeviation;
    public bool             WasShootingPreviousFrame;

    public Vector3          Velocity;
    public float            Orientation;

    public Transform        Muzzle;
    public BulletConfig     Bullet;
    public ResourceLink     BulletPrefab;
    public Bullets          Bullets;

    private float _lastTimeShot;

    public override void OnCreate() {
        Bullets = Singleton<Bullets>.Instance;
    }

    public override void Destroy() {
        base.Destroy();
    }

    public void Shoot() {
        var shootDelay = 1 / Bps;
        var time = Clock.Time;
        if(time - _lastTimeShot > shootDelay) {
            int bulletsPerShot;

            if(!WasShootingPreviousFrame) {
                bulletsPerShot = 1;
            } else {
                bulletsPerShot = Mathf.RoundToInt((time - _lastTimeShot) / shootDelay);
            }

            for(var i = 0; i < bulletsPerShot; ++i) {
                var deviation = Random.Range(-MaxDeviation, MaxDeviation);
                var cos = Mathf.Cos((Orientation + deviation) * Mathf.Deg2Rad);
                var sin = Mathf.Sin((Orientation + deviation) * Mathf.Deg2Rad);
                var dir = new Vector3(cos, 0, -sin);
                Bullets.Create(Muzzle.position, dir, Orientation + deviation, Bullet, BulletPrefab);
            }

            _lastTimeShot = time;
        }
    }
}