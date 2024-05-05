using UnityEngine;

public class BulletController : MonoBehaviour
{
    [SerializeField] float bulletSpeed = 10.0f;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void SetDirection(Vector3 direction)
    {
        rb.velocity = direction * bulletSpeed ;
    }

    private void OnCollisionEnter(Collision collision)
    {
        //TODO: Se puede tirar un evento o hay que hacer daño de alguna manera

        Destroy(gameObject);
    }
}
