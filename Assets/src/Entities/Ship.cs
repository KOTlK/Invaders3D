using UnityEngine;

public abstract class Ship : Entity, IDamageable {
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
    public Collider         Collider;
    public BulletConfig     Bullet;
    public ResourceLink     BulletPrefab;
    public Bullets          Bullets;

    private float _lastTimeShot;

    public bool Alive => Health > 0;

    [field: SerializeField] public int Health       { get; private set; }
    [field: SerializeField] public int MaxHealth    { get; private set; }

    public override void OnCreate() {
        Bullets         = Singleton<Bullets>.Instance;
        Bullet.OwnerUid = Collider.GetInstanceID();
        Bullet.Owner    = Handle;
        Health          = MaxHealth;
    }

    public override void RegisterInstanceId(EntityManager em) {
        em.EntityByInstanceId.Add(Collider.GetInstanceID(), Handle);
        base.RegisterInstanceId(em);
    }

    public override void UnRegisterInstanceId(EntityManager em) {
        em.EntityByInstanceId.Remove(Collider.GetInstanceID());
        base.UnRegisterInstanceId(em);
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

    public void ApplyDamage(float amount) {
        Health = Mathf.Clamp(Health - Mathf.CeilToInt(amount), 0, MaxHealth);
        if(Health <= 0) {
            Destroy();
        }
    }

    public void Heal(float amount) {
        Health = Mathf.Clamp(Health + Mathf.CeilToInt(amount), 0, MaxHealth);
    }
}