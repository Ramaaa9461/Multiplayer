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

    public Action<byte[], IPEndPoint> OnRecievedMessage;

    public readonly Dictionary<int, Client> clients = new Dictionary<int, Client>(); //Esta lista la tiene el SERVER
    private readonly Dictionary<int, Player> players = new Dictionary<int, Player>(); //Esta lista la tienen los CLIENTES
    public readonly Dictionary<IPEndPoint, int> ipToId = new Dictionary<IPEndPoint, int>();

    public string userName = "Server";
    public int serverClientId = 0; //Es el id que tendra el server para asignar a los clientes que entren
    public int actualClientId = -1; // Es el ID de ESTE cliente (no aplica al server)

    GameManager gm;
    public PingPong checkActivity;

    SorteableMessages sorteableMessages;
    NondisponsablesMessages nondisponblesMessages;


    int maxPlayersPerServer = 4;
    public bool matchOnGoing = false;

    private void Start()
    {
        gm = GameManager.Instance;
        sorteableMessages = new();
        nondisponblesMessages = new();
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
    

        ClientToServerNetHandShake handShakeMesage = new ClientToServerNetHandShake(MessagePriority.NonDisposable, (UdpConnection.IPToLong(ip), port, name));
        SendToServer(handShakeMesage.Serialize());
    }

    public void AddClient(IPEndPoint ip, int newClientID, string clientName)
    {
        if (!ipToId.ContainsKey(ip) && !clients.ContainsKey(newClientID)) //Nose si hace falta los 2
        {
            Debug.Log("Adding client: " + ip.Address);

            ipToId[ip] = newClientID;
            clients.Add(newClientID, new Client(ip, newClientID, Time.realtimeSinceStartup, clientName));

            checkActivity.AddClientForList(newClientID);
            gm.OnNewPlayer?.Invoke(newClientID);

            if (isServer)
            {
                List<(int, string)> playersInServer = new List<(int, string)>();

                foreach (int id in clients.Keys)
                {
                    playersInServer.Add((clients[id].id, clients[id].clientName));
                }

                ServerToClientHandShake serverToClient = new ServerToClientHandShake(MessagePriority.NonDisposable, playersInServer);
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
        gm.OnRemovePlayer?.Invoke(idToRemove);

        if (clients.ContainsKey(idToRemove))
        {
            Debug.Log("Removing client: " + idToRemove);

            checkActivity.RemoveClientForList(idToRemove);

            ipToId.Remove(clients[idToRemove].ipEndPoint);
            players.Remove(idToRemove);
            clients.Remove(idToRemove);
        }

        if (!isServer && actualClientId == idToRemove)
        {
            gm.RemoveAllPlayers();
            connection.Close();
            NetworkScreen.Instance.SwitchToMenuScreen();
        }
    }

    public void OnReceiveData(byte[] data, IPEndPoint ip)
    {
     //   if (!MessageChecker.DeserializeCheckSum(data))
     //   {
     //       return;
     //   }

        OnRecievedMessage?.Invoke(data, ip);

        switch (MessageChecker.CheckMessageType(data))
        {
            case MessageType.Ping:

                if (isServer)
                {
                    if (ipToId.ContainsKey(ip))
                    {
                        checkActivity.ReciveClientToServerPingMessage(ipToId[ip]);
                        checkActivity.CalculateLatencyFromClients(ipToId[ip]);
                    }
                    else
                    {
                        Debug.LogError("Fail Client ID");
                    }
                }
                else
                {
                    checkActivity.ReciveServerToClientPingMessage();
                    checkActivity.CalculateLatencyFromServer();
                }

                break;

            case MessageType.ServerToClientHandShake:

                ServerToClientHandShake netGetClientID = new ServerToClientHandShake(data);

                List<(int clientId, string userName)> playerList = netGetClientID.GetData();

                if (checkActivity == null)
                {
                    checkActivity = new PingPong();
                }

                for (int i = 0; i < playerList.Count; i++) //Verifico primero que cliente soy
                {
                    if (playerList[i].userName == userName)
                    {
                        if (NetworkScreen.Instance.isInMenu)
                        {
                            NetworkScreen.Instance.SwitchToChatScreen();
                        }

                        actualClientId = playerList[i].clientId;
                    }
                }

                players.Clear();
                for (int i = 0; i < playerList.Count; i++)
                {
                    Debug.Log(playerList[i].clientId + " - " + playerList[i].userName);
                    Player playerToAdd = new Player(playerList[i].clientId, playerList[i].userName);
                    players.Add(playerList[i].clientId, playerToAdd);

                    gm.OnNewPlayer?.Invoke(playerToAdd.id);
                }

                break;

            case MessageType.ClientToServerHandShake:

                ReciveClientToServerHandShake(data, ip);

                break;
            case MessageType.Console:

                UpdateChatText(data, ip);
                break;

            case MessageType.Position:

                NetVector3 netVector3 = new(data);

                if (isServer)
                {
                    if (ipToId.ContainsKey(ip))
                    {
                        if (sorteableMessages.CheckMessageOrderRecievedFromClients(ipToId[ip], MessageChecker.CheckMessageType(data), netVector3.MessageOrder))
                        {
                            UpdatePlayerPosition(data);
                        }
                    }
                }
                else
                {
                    if (sorteableMessages.CheckMessageOrderRecievedFromServer(netVector3.GetData().id, MessageType.Position, netVector3.MessageOrder))   //TODO: Este if rompe cuando son mas de 2 jugadores
                    {
                        UpdatePlayerPosition(data);
                    }
                }

                break;
            case MessageType.BulletInstatiate:

                NetVector3 netBullet = new NetVector3(data);
                gm.OnInstantiateBullet?.Invoke(netBullet.GetData().id, netBullet.GetData().position);

                if (isServer)
                {
                    BroadcastPlayerPosition(netBullet.GetData().id, data);
                }

                break;
            case MessageType.Disconnection:

                NetIDMessage netDisconnection = new NetIDMessage(data);

                int playerID = netDisconnection.GetData();
                Debug.Log("ServerDisconect: " + playerID);

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

                break;
            case MessageType.UpdateLobbyTimer:

                if (!isServer)
                {
                    NetUpdateTimer netUpdate = new(data);
                    gm.OnInitLobbyTimer?.Invoke(netUpdate.GetData());
                }

                break;
            case MessageType.UpdateGameplayTimer:

                if (!isServer)
                {
                    gm.OnInitGameplayTimer?.Invoke();
                }

                break;
            case MessageType.Error:

                NetErrorMessage netErrorMessage = new NetErrorMessage(data);

                NetworkScreen.Instance.SwitchToMenuScreen();
                NetworkScreen.Instance.ShowErrorPanel(netErrorMessage.GetData());

                gm.RemoveAllPlayers();
                checkActivity = null;
                connection.Close();

                break;

            case MessageType.Winner:

                NetIDMessage netIDMessage = new(data);
                string winText = $"Player {players[netIDMessage.GetData()].name} has Won!!!";

                NetworkScreen.Instance.SwitchToMenuScreen();
                NetworkScreen.Instance.ShowWinPanel(winText);

                checkActivity = null;
                gm.EndMatch();

                break;

            default:
                break;
        }
    }

    public void SendToServer(byte[] data)
    {
        nondisponblesMessages.AddSentMessagesFromClients(data);
        connection.Send(data);
    }

    public void Broadcast(byte[] data, IPEndPoint ip)
    {
        if (ipToId.ContainsKey(ip))
        {
            nondisponblesMessages.AddSentMessagesFromServer(data, ipToId[ip]);
        }
        connection.Send(data, ip);
    }

    public void Broadcast(byte[] data)
    {
        using (var iterator = clients.GetEnumerator())
        {
            while (iterator.MoveNext())
            {
                nondisponblesMessages.AddSentMessagesFromServer(data, iterator.Current.Value.id);
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

            if (nondisponblesMessages != null)
            {
                nondisponblesMessages.ResendPackages();
            }

        }
        else
        {
            gm.RemoveAllPlayers();
        }
    }


    void ReciveClientToServerHandShake(byte[] data, IPEndPoint ip)
    {
        ClientToServerNetHandShake handShake = new ClientToServerNetHandShake(data);

        if (!MatchOnGoing(ip) && CheckValidUserName(handShake.GetData().Item3, ip) && !ServerIsFull(ip))
        {
            AddClient(ip, serverClientId, handShake.GetData().Item3);
            serverClientId++;
        }
    }

    bool MatchOnGoing(IPEndPoint ip)
    {
        if (matchOnGoing)
        {
            NetErrorMessage netServerIsFull = new NetErrorMessage("Match has already started");
            Broadcast(netServerIsFull.Serialize(), ip);
        }

        return matchOnGoing;
    }

    bool ServerIsFull(IPEndPoint ip)
    {
        bool serverIsFull = clients.Count >= maxPlayersPerServer;

        if (serverIsFull)
        {
            NetErrorMessage netServerIsFull = new NetErrorMessage("Server is full");
            Broadcast(netServerIsFull.Serialize(), ip);
        }

        return serverIsFull;
    }

    bool CheckValidUserName(string userName, IPEndPoint ip)
    {
        foreach (int clientID in clients.Keys)
        {
            if (userName == clients[clientID].clientName)
            {
                NetErrorMessage netInvalidUserName = new NetErrorMessage("Invalid User Name");
                Broadcast(netInvalidUserName.Serialize(), ip);

                return false;
            }
        }

        return true;
    }

    private void UpdateChatText(byte[] data, IPEndPoint ip)
    {
        string messageText = "";

        NetMessage netMessage = new NetMessage(data);
        messageText += new string(netMessage.GetData());

        if (isServer)
        {
            Broadcast(data);
        }

        ChatScreen.Instance.messages.text += messageText + System.Environment.NewLine;
    }

    void OnApplicationQuit()
    {
        if (!isServer)
        {
            gm.RemoveAllPlayers();
            NetIDMessage netDisconnection = new NetIDMessage(MessagePriority.Default, actualClientId);
            SendToServer(netDisconnection.Serialize());
        }
        else
        {
            NetErrorMessage netErrorMessage = new NetErrorMessage("Lost Connection To Server");
            Broadcast(netErrorMessage.Serialize());
            CloseServer();
        }
    }

    public void CloseServer()
    {
        if (isServer)
        {
            List<int> clientIdsToRemove = new List<int>(clients.Keys);
            foreach (int clientId in clientIdsToRemove)
            {
                Debug.Log("ServerDisconect: " + clientId);
                NetIDMessage netDisconnection = new NetIDMessage(MessagePriority.Default, clientId);
                Broadcast(netDisconnection.Serialize());
                RemoveClient(clientId);
            }
            connection.Close();
        }
    }

    public void DisconectPlayer()
    {
        if (!isServer)
        {
            gm.RemoveAllPlayers();
            connection.Close();
            NetworkScreen.Instance.SwitchToMenuScreen();
        }
    }

    private void UpdatePlayerPosition(byte[] data)
    {
        NetVector3 netPosition = new NetVector3(data);
        int clientId = netPosition.GetData().id;

        gm.UpdatePlayerPosition(netPosition.GetData());

        if (isServer)
        {
            BroadcastPlayerPosition(clientId, data);
        }
    }

    private void BroadcastPlayerPosition(int senderClientId, byte[] data)
    {
        using (var iterator = clients.GetEnumerator())
        {
            while (iterator.MoveNext())
            {
                int receiverClientId = iterator.Current.Key;

                // Evita que te mandes tuu propia posicion
                if (receiverClientId != senderClientId)
                {
                    //Chequea ambos IpEndPoint, y si el enviador es el mismo que el receptor, continua el loop sin hacer el Broadcast
                    if (clients[receiverClientId].ipEndPoint.Equals(clients[senderClientId].ipEndPoint)) continue;
                    Broadcast(data, clients[receiverClientId].ipEndPoint);
                }
            }
        }
    }

}
