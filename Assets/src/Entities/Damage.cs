using UnityEngine;

public enum DamageType {
    Kinematic,
    Laser,
    Nuclear
}

[System.Serializable]
public struct Damage {
    public EntityHandle Sender;
    public DamageType   Type;
    public float        Amount;
    [HideInInspector]
    public int          ReceiverUid;
}

public interface IDamageable {
    int  Health     { get; }
    int  MaxHealth  { get; }
    bool Alive      { get; }
    void ApplyDamage(float amount);
    void Heal       (float amount);
}