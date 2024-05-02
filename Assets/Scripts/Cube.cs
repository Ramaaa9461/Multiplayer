using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;

public class Cube : MonoBehaviour
{
    [SerializeField] string playerName;
    [SerializeField] float speed = 5f;

    void Update()
    {
        if (!NetworkManager.Instance.isServer)
        {
            float horizontalInput = Input.GetAxis("Horizontal");
            float verticalInput = Input.GetAxis("Vertical");

            Vector3 movement = new Vector3(horizontalInput, 0f, verticalInput) * speed * Time.deltaTime;

            transform.Translate(movement);

            //SendPosition();
        }


    }

    void SendPosition()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            NetVector3 netVector3 = new NetVector3(transform.position);
            NetworkManager.Instance.SendToServer(netVector3.Serialize());
        }
    }
}
