using System.Collections.Generic;
using UnityEngine;

public class PingPong
{
    int timeUntilDisconnection = 30;
    private Dictionary<int, float> lastMessageReceivedFromClients = new Dictionary<int, float>();
    float lastMessageReceivedFromServer = 0;

    float counter = 0;
    float sendMessageCounter = 0;

    public void AddClientForList(int idToAdd)
    {
        lastMessageReceivedFromClients.Add(idToAdd, 0.0f);
    }

    public void RemoveClientForList(int idToRemove)
    {
        lastMessageReceivedFromClients.Remove(idToRemove);
    }

    public void ReciveCheckActivityMessage(int playerID)
    {
        if (NetworkManager.Instance.isServer)
        {
            lastMessageReceivedFromClients[playerID] = 0;
        }
        else
        {
            lastMessageReceivedFromServer = 0;
        }
    }

    public void UpdateCheckActivity()
    {
        counter += Time.deltaTime;
        sendMessageCounter += Time.deltaTime;

        if (sendMessageCounter > 1.0f) //Envio cada 1 segundo el mensaje
        {
            SendPingMessage();
            sendMessageCounter = 0;
        }

        if (NetworkManager.Instance.isServer)
        {
            for (int i = 0; i < lastMessageReceivedFromClients.Count; i++)
            {
                if (lastMessageReceivedFromClients[i] > timeUntilDisconnection)
                {
                    NetworkManager.Instance.RemoveClient(i);
                }
            }
        }
        else
        {
            if (lastMessageReceivedFromServer > timeUntilDisconnection)
            {
                // Se murio el server
                 // Mando mensaje por las dudas y cierro la conexcion
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
