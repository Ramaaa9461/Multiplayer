using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public int health = 3;
    
    [SerializeField] float speed = 5f;
    [SerializeField] float cooldownShoot = 0.2f;
    [SerializeField] GameObject bulletPrefab;

    [SerializeField] bool canShoot = true;
    CharacterController cc;

    public bool currentPlayer = false;
    public int clientID = -1;

    GameManager gm;
    NetworkManager nm;

    static int positionMessageOrder = 0;
    static int bulletsMessageOrder = 0;

    private void Awake()
    {
        cc = transform.GetComponent<CharacterController>();
    }

    private void Start()
    {
        gm = GameManager.Instance;
        nm = NetworkManager.Instance;
    }

    void Update()
    {
        if (!nm.isServer && currentPlayer)
        {
            Movement();
            Shoot();
        }
    }

    public void Movement()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(horizontalInput, verticalInput, 0.0f) * speed * Time.deltaTime;

        cc.Move(movement);

        SendPosition();
    }

    void Shoot()
    {
        if (Input.GetMouseButtonDown(0) && canShoot)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                Vector3 mousePosition = hit.point;
                mousePosition.z = 0f; // Aseg�rate de que la coordenada Z sea la misma que la del jugador
                Vector3 direction = mousePosition - transform.position;
                direction.Normalize();

                GameObject bullet = Instantiate(bulletPrefab, transform.position + direction, Quaternion.identity);
                bullet.GetComponent<BulletController>().SetDirection(direction, clientID);

                bulletsMessageOrder++;
                NetVector3 netBullet = new NetVector3(MessagePriority.Sorteable, (nm.actualClientId, direction));
                netBullet.CurrentMessageType = MessageType.BulletInstatiate;
                netBullet.MessageOrder = bulletsMessageOrder;
                nm.SendToServer(netBullet.Serialize());

                canShoot = false;
                Invoke(nameof(SetCanShoot), cooldownShoot);
            }
        }
    }

    void SendPosition()
    {
        positionMessageOrder++;
        NetVector3 netVector3 = new NetVector3(MessagePriority.NonDisposable, (nm.actualClientId, transform.position));
        netVector3.MessageOrder = positionMessageOrder;
        NetworkManager.Instance.SendToServer(netVector3.Serialize());
    }

    void SetCanShoot()
    {
        canShoot = true;
    }

    public void ServerShoot(Vector3 direction)
    {
        GameObject bullet = Instantiate(bulletPrefab, transform.position + direction, Quaternion.identity);
        bullet.GetComponent<BulletController>().SetDirection(direction, clientID);
    }

    public void OnReciveDamage() //Solo lo maneja el server esta funcion
    {
        health--;

        if (health <= 0)
        {
            //TODO: El server tiene que hecharlo de la partida
            NetIDMessage netDisconnection = new NetIDMessage(MessagePriority.Default, clientID);
            nm.Broadcast(netDisconnection.Serialize());
            nm.RemoveClient(clientID);
        }
    }
}

