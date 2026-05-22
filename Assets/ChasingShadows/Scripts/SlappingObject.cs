using UnityEngine;

public class SlappingObject : MonoBehaviour
{
    public float slapForce = 12f;
    public float upwardForce = 3f;

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;

        if (rb == null) return;

        Vector3 slapDirection = transform.forward;
        Vector3 force = slapDirection * slapForce + Vector3.up * upwardForce;

        rb.AddForce(force, ForceMode.Impulse);
    }
}