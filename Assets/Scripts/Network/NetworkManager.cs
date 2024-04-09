using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public struct Client
{
    public float timeStamp;
    public int id;
    public IPEndPoint ipEndPoint;

    public Client(IPEndPoint ipEndPoint, int id, float timeStamp)
    {
        this.timeStamp = timeStamp;
        this.id = id;
        this.ipEndPoint = ipEndPoint;
    }
}

public class NetworkManager : MonoBehaviourSingleton<NetworkManager>, IReceiveData
{
    public IPAddress ipAddress
    {
        get; private set;
    }

    public int port
    {
        get; private set;
    }

    public bool isServer
    {
        get; private set;
    }

    public int TimeOut = 30;

    public Action<byte[], IPEndPoint> OnReceiveEvent;

    private UdpConnection connection;

    private readonly Dictionary<int, Client> clients = new Dictionary<int, Client>();
    private readonly Dictionary<IPEndPoint, int> ipToId = new Dictionary<IPEndPoint, int>();

    public int serverClientId = 0; //Es el id que tendra el server para asignar a los clientes que entren
    int actualClientId = 0; // Es el ID de ESTE cliente (no aplica al server)

    MessageChecker messageChecker;

    private void Start()
    {
        messageChecker = new MessageChecker();
    }

    public void StartServer(int port)
    {
        isServer = true;
        this.port = port;
        connection = new UdpConnection(port, this);
    }

    public void StartClient(IPAddress ip, int port)
    {
        isServer = false;

        this.port = port;
        this.ipAddress = ip;

        connection = new UdpConnection(ip, port, this);

        NetHandShake handShakeMesage = new NetHandShake((UdpConnection.IPToLong(ip), port));
        SendToServer(handShakeMesage.Serialize());
    }


    public void AddClient(IPEndPoint ip, int newClientID)
    {
        if (!ipToId.ContainsKey(ip) && !clients.ContainsKey(newClientID)) //Nose si hace falta los 2
        {
            Debug.Log("Adding client: " + ip.Address);

            ipToId[ip] = newClientID;
            clients.Add(serverClientId, new Client(ip, newClientID, Time.realtimeSinceStartup));

            if (isServer)
            {
                //Aca se deberia mandar un mensaje para avisar a los demas clientes
                //Que se agrego uno nuevo.
            }
        }
        else
        {
            Debug.Log("Es un cliente repetido");
        }
    }

    void RemoveClient(IPEndPoint ip)
    {
        if (ipToId.ContainsKey(ip))
        {
            Debug.Log("Removing client: " + ip.Address);
            clients.Remove(ipToId[ip]);
        }
    }

    public void OnReceiveData(byte[] data, IPEndPoint ip)
    {

        switch (messageChecker.CheckMessageType(data))
        {
            case MessageType.CheckActivity:

                break;
            
            case MessageType.SetClientID:

                NetSetClientID netGetClientID = new NetSetClientID(data);
                actualClientId = netGetClientID.GetData();
                AddClient(ip, actualClientId);
                Debug.Log("Me llego el nro de cliente " + actualClientId);     
                break;

            case MessageType.HandShake:

                ConnectToServer(data, ip);

                break;
            case MessageType.Console:

                UpdateChatText(data);
                break;

            case MessageType.Position:
                break;
            case MessageType.NewCustomerNotice:
                break;
            case MessageType.Disconnection:
                break;
            case MessageType.ThereIsNoPlace:
                break;
            case MessageType.RepeatMessage:
                break;
            default:
                break;
        }


        if (OnReceiveEvent != null)
            OnReceiveEvent.Invoke(data, ip);
    }

    public void SendToServer(byte[] data)
    {
        connection.Send(data);
    }

    public void Broadcast(byte[] data, IPEndPoint ip)
    {
        connection.Send(data, ip);
    }

    public void Broadcast(byte[] data)
    {
        using (var iterator = clients.GetEnumerator())
        {
            while (iterator.MoveNext())
            {
                connection.Send(data, iterator.Current.Value.ipEndPoint);
            }
        }
    }

    void Update()
    {
        // Flush the data in main thread
        if (connection != null)
            connection.FlushReceiveData();
    }

    void ConnectToServer(byte[] data, IPEndPoint ip)
    {
        NetHandShake handShake = new NetHandShake(data);

        if (!clients.ContainsKey(serverClientId))
        {
            //Le asigna un ID al cliente y despues lo broadcastea
            NetSetClientID netSetClientID = new NetSetClientID(serverClientId);
            Broadcast(netSetClientID.Serialize(), ip);

            AddClient(ip, serverClientId);
            serverClientId++;
        }
    }

    private void UpdateChatText(byte[] data)
    {
        int netMessageSum = 0;
        int sum = 0;
        char[] aux;
        string text = "";

        NetMessage.Deserialize(data, out aux, out netMessageSum);

        Debug.Log("Mensaje recibido (bytes): " + BitConverter.ToString(data));

        for (int i = 0; i < aux.Length; i++)
        {
            sum += (int)aux[i];
        }
        sum /= 2; //Nose porque pero asi funca

        if (sum != netMessageSum)
        {
            //Pido el paquete de nuevo.
            UnityEngine.Debug.Log("El mensaje llegó corrupto");

            return;
        }

        if (isServer)
        {
            Broadcast(data);
        }

        for (int i = 0; i < aux.Length; i++)
        {
            text += aux[i];
        }

        Debug.Log(text);

        ChatScreen.Instance.messages.text += text + System.Environment.NewLine;
    }
}
