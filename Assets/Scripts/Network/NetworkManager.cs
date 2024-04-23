using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public struct Client
{
    public float timeStamp;
    public int id;
    public IPEndPoint ipEndPoint;
    public string clientName;

    public Client(IPEndPoint ipEndPoint, int id, float timeStamp, string clientName)
    {
        this.timeStamp = timeStamp;
        this.id = id;
        this.ipEndPoint = ipEndPoint;
        this.clientName = clientName;
    }
}

public struct Player
{
    public int id;
    public string name;

    public Player(int id, string name)
    {
        this.id = id;
        this.name = name;
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

    private UdpConnection connection;

    private readonly Dictionary<int, Client> clients = new Dictionary<int, Client>(); //Esta lista la tiene el SERVER
    private readonly Dictionary<int, Player> players = new Dictionary<int, Player>(); //Esta lista la tienen los CLIENTES
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

    public void StartClient(IPAddress ip, int port, string name)
    {
        isServer = false;

        this.port = port;
        this.ipAddress = ip;

        connection = new UdpConnection(ip, port, this);

        ClientToServerNetHandShake handShakeMesage = new ClientToServerNetHandShake((UdpConnection.IPToLong(ip), port, name));
        SendToServer(handShakeMesage.Serialize());
    }

    public void AddClient(IPEndPoint ip, int newClientID, string clientName)
    {
        if (!ipToId.ContainsKey(ip) && !clients.ContainsKey(newClientID)) //Nose si hace falta los 2
        {
            Debug.Log("Adding client: " + ip.Address);

            ipToId[ip] = newClientID;
            clients.Add(serverClientId, new Client(ip, newClientID, Time.realtimeSinceStartup, clientName));

            if (isServer)
            {
                ServerToClientHandShake serverToClient = new ServerToClientHandShake((serverClientId, clientName));

                //Aca se deberia mandar un mensaje para avisar a los demas clientes // TENGO QUE MANDAR LA LISTA COMPLETA
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

            case MessageType.ServerToClientHandShake:

                //Recibe la lista completa de Clientes

                ServerToClientHandShake netGetClientID = new ServerToClientHandShake(data);
               // actualClientId = netGetClientID.GetData();
               // AddClient(ip, actualClientId);
                Debug.Log("Me llego el nro de cliente " + actualClientId);
                break;

            case MessageType.ClientToServerHandShake:

                ReciveClientToServerHandShake(data, ip);

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

    void ReciveClientToServerHandShake(byte[] data, IPEndPoint ip)
    {
        ClientToServerNetHandShake handShake = new ClientToServerNetHandShake(data);

        if (!clients.ContainsKey(serverClientId))
        {
            AddClient(ip, serverClientId, handShake.GetData().Item3);
            serverClientId++;
        }
    }

    private void UpdateChatText(byte[] data)
    {
        string text = "";
        NetMessage netMessage = new NetMessage(data);
        text = new string(netMessage.GetData());

        if (isServer)
        {
            Broadcast(data);
        }

        ChatScreen.Instance.messages.text += text + System.Environment.NewLine;
    }
}
