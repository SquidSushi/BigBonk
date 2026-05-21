using UnityEngine;

public class TestHitTarget : MonoBehaviour, IHittable
{
    public void TakeHit(GameObject attacker)
    {
        Debug.Log($"{gameObject.name} wurde von {attacker.name} getroffen!");
    }
}