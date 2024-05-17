using System;
using System.Collections.Generic;
using UnityEngine;

public class PingPong
{
    int timeUntilDisconnection = 5;

    private Dictionary<int, float> lastMessageReceivedFromClients = new Dictionary<int, float>(); //Lo usa el Server
    float lastMessageReceivedFromServer = 0; //Lo usan los clientes

    float sendMessageCounter = 0;
    float secondsPerCheck = 1.0f;


    private Dictionary<int, float> latencyFromClients = new Dictionary<int, float>(); //Lo usa el Server
    float latencyFromServer = 0;
    DateTime currentDateTime;

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

        if (sendMessageCounter > secondsPerCheck) //Envio cada 1 segundo el mensaje
        {
            SendPingMessage();
            sendMessageCounter = 0;
        }

            CheckActivityCounter();
            CheckTimeUntilDisconection();
    }

    void CheckActivityCounter()
    {
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
    }

    void CheckTimeUntilDisconection()
    {
        if (NetworkManager.Instance.isServer)
        {
            foreach (int clientID in lastMessageReceivedFromClients.Keys)
            {
                if (lastMessageReceivedFromClients[clientID] > timeUntilDisconnection)
                {
                    NetworkManager.Instance.RemoveClient(clientID);

                    NetIDMessage netDisconnection = new NetIDMessage(MessagePriority.Default, clientID);
                    NetworkManager.Instance.Broadcast(netDisconnection.Serialize());
                }
            }
        }
        else
        {
            if (lastMessageReceivedFromServer > timeUntilDisconnection)
            {
                NetIDMessage netDisconnection = new NetIDMessage(MessagePriority.Default, NetworkManager.Instance.actualClientId);
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

        currentDateTime = DateTime.UtcNow;
    }

    public void CalculateLatencyFromServer()
    {
        TimeSpan newDateTime = DateTime.UtcNow - currentDateTime;
        latencyFromServer = (float)newDateTime.Milliseconds;
      //Debug.Log("Latency from Server " + latencyFromServer / 1000);
    }

    public void CalculateLatencyFromClients(int clientID)
    {
        TimeSpan newDateTime = DateTime.UtcNow - currentDateTime;
        latencyFromClients[clientID] = (float)newDateTime.TotalMilliseconds;
      //Debug.Log("Latency from client " + clientID + " - " + latencyFromClients[clientID] /1000);
    }

    public float GetLatencyFormClient(int clientId)
    {
        if (latencyFromClients.ContainsKey(clientId))
        {
            return latencyFromClients[clientId];
        }

        return -1;
    }
    public float GetLatencyFormServer()
    {
        return latencyFromServer;
    }
}
