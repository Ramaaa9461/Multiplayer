using System.Collections.Generic;
using UnityEngine;

public class PingPong
{
    int timeUntilDisconnection = 5;

    private Dictionary<int, float> lastMessageReceivedFromClients = new Dictionary<int, float>(); //Lo usa el Server
    float lastMessageReceivedFromServer = 0; //Lo usan los clientes

    float sendMessageCounter = 0;

    public PingPong()
    {
    }

    public void AddClientForList(int idToAdd)
    {
        lastMessageReceivedFromClients.Add(idToAdd, 0.0f);
    }

    public void RemoveClientForList(int idToRemove)
    {
        lastMessageReceivedFromClients.Remove(idToRemove);
    }

    public void ReciveServerToClientPingMessage()
    {
        lastMessageReceivedFromServer = 0;
    }

    public void ReciveClientToServerPingMessage(int playerID)
    {
        lastMessageReceivedFromClients[playerID] = 0;
    }

    public void UpdateCheckActivity()
    {
        sendMessageCounter += Time.deltaTime;


        if (sendMessageCounter > 1.0f) //Envio cada 1 segundo el mensaje
        {
            SendPingMessage();
            sendMessageCounter = 0;
        }

        if (NetworkManager.Instance.isServer)
        {
            var keys = new List<int>(lastMessageReceivedFromClients.Keys);

            foreach (var key in keys)
            {
                lastMessageReceivedFromClients[key] += Time.deltaTime;
            }
        }
        else
        {
            lastMessageReceivedFromServer += Time.deltaTime;
        }


        if (NetworkManager.Instance.isServer)
        {
            for (int i = 0; i < lastMessageReceivedFromClients.Count; i++)
            {
                if (lastMessageReceivedFromClients[i] > timeUntilDisconnection)
                {
                    NetworkManager.Instance.RemoveClient(i);

                    NetDisconnection netDisconnection = new NetDisconnection(i);
                    NetworkManager.Instance.Broadcast(netDisconnection.Serialize());
                }
            }
        }
        else
        {
            if (lastMessageReceivedFromServer > timeUntilDisconnection)
            {
                NetDisconnection netDisconnection = new NetDisconnection(NetworkManager.Instance.actualClientId);
                NetworkManager.Instance.SendToServer(netDisconnection.Serialize());

                NetworkManager.Instance.DisconectPlayer();
            }
        }
    }

    void SendPingMessage()
    {
        NetPing netPing = new NetPing();

        if (NetworkManager.Instance.isServer)
        {
            NetworkManager.Instance.Broadcast(netPing.Serialize());
        }
        else
        {
            NetworkManager.Instance.SendToServer(netPing.Serialize());
        }
    }
}
