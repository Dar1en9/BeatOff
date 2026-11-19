using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float amount, Vector3 point, Vector3 direction);
}