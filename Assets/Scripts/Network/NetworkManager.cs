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

    string userName = "";
    public int serverClientId = 0; //Es el id que tendra el server para asignar a los clientes que entren
    public int actualClientId = 0; // Es el ID de ESTE cliente (no aplica al server)

    MessageChecker messageChecker;
    PingPong checkActivity;

    private void Start()
    {
        messageChecker = new MessageChecker();
    }

    public void StartServer(int port)
    {
        isServer = true;
        this.port = port;
        connection = new UdpConnection(port, this);

        checkActivity = new PingPong();
    }

    public void StartClient(IPAddress ip, int port, string name)
    {
        isServer = false;

        this.port = port;
        this.ipAddress = ip;
        this.userName = name;

        connection = new UdpConnection(ip, port, this);
        checkActivity = new PingPong();

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

            checkActivity.AddClientForList(newClientID);

            if (isServer)
            {
                List<(int, string)> players = new List<(int, string)>();

                for (int i = 0; i < clients.Count; i++)
                {
                    players.Add((clients[i].id, clients[i].clientName));
                }

                ServerToClientHandShake serverToClient = new ServerToClientHandShake(players);
                Broadcast(serverToClient.Serialize());
            }
        }
        else
        {
            Debug.Log("Es un cliente repetido");
        }
    }

    public void RemoveClient(int idToRemove)
    {
        if (clients.ContainsKey(idToRemove))
        {
            Debug.Log("Removing client: " + idToRemove);

            checkActivity.RemoveClientForList(idToRemove);

            ipToId.Remove(clients[idToRemove].ipEndPoint);
            players.Remove(idToRemove);
            clients.Remove(idToRemove);


            //TODO: Tengo que avisar que se desconecto el player ID
        }
    }

    public void OnReceiveData(byte[] data, IPEndPoint ip)
    {
        //Esta fallando el IpToId porque los clientes no lo tienen, es algo que maneja unicamente el server

        switch (messageChecker.CheckMessageType(data))
        {
            case MessageType.Ping:

                if (isServer)
                {
                    if (ipToId.ContainsKey(ip))
                    {
                        checkActivity.ReciveClientToServerPingMessage(ipToId[ip]);
                    }
                    else
                    {
                        Debug.LogError("Fail Client ID");
                    }
                }
                else
                {
                    checkActivity.ReciveServerToClientPingMessage();
                }

                break;

            case MessageType.ServerToClientHandShake:

                ServerToClientHandShake netGetClientID = new ServerToClientHandShake(data);

                List<(int clientId, string userName)> playerList = netGetClientID.GetData();

                players.Clear();
                for (int i = 0; i < playerList.Count; i++)
                {
                    if (playerList[i].userName == userName)
                    {
                        actualClientId = playerList[i].clientId;
                    }

                    Debug.Log(playerList[i].clientId + " - " + playerList[i].userName);
                    Player playerToAdd = new Player(playerList[i].clientId, playerList[i].userName);
                    players.Add(playerList[i].clientId, playerToAdd);
                }

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

                if (ipToId.ContainsKey(ip))
                {
                    int playerID = ipToId[ip];
                    if (isServer)
                    {
                        Broadcast(data);
                        RemoveClient(playerID);
                    }
                    else
                    {
                        Debug.Log("Remove player " + playerID);
                        RemoveClient(playerID);
                    }
                }

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
        {
            connection.FlushReceiveData();

            if (checkActivity != null)
            {
                checkActivity.UpdateCheckActivity();
            }
        }


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

    void OnApplicationQuit()
    {
        if (!isServer)
        {
            NetDisconnection netDisconnection = new NetDisconnection(actualClientId);
            SendToServer(netDisconnection.Serialize());
        }
    }
    public void DisconectPlayer()
    {
        if (!isServer)
        {
            connection.Close();
        }
    }
}
