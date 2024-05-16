using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class SorteableMessages
{
    GameManager gm;
    NetworkManager nm;

    Dictionary<MessageType, int> OrderLastMessageReciveFromServer;
    Dictionary<int, Dictionary<MessageType, int>> OrderLastMessageReciveFromClients;

    public SorteableMessages()
    {
        nm = NetworkManager.Instance;
        gm = GameManager.Instance;

        nm.OnRecievedMessage += OnRecievedData;

        gm.OnNewPlayer += AddNewClient;
        gm.OnRemovePlayer += RemoveClient;

        OrderLastMessageReciveFromClients = new Dictionary<int, Dictionary<MessageType, int>>();
        OrderLastMessageReciveFromServer = new Dictionary<MessageType, int>();
    }


    void OnRecievedData(byte[] data, IPEndPoint ip)
    {
        MessagePriority messagePriority = MessageChecker.CheckMessagePriority(data);
        Debug.Log(messagePriority);

        if ((messagePriority & MessagePriority.Sorteable) != 0)
        {
            MessageType messageType = MessageChecker.CheckMessageType(data);

            if (nm.isServer)
            {
                if (OrderLastMessageReciveFromClients.ContainsKey(nm.ipToId[ip]))
                {
                    if (!OrderLastMessageReciveFromClients[nm.ipToId[ip]].ContainsKey(messageType))
                    {
                        OrderLastMessageReciveFromClients[nm.ipToId[ip]].Add(messageType, 0);
                    }
                    else
                    {
                        OrderLastMessageReciveFromClients[nm.ipToId[ip]][messageType]++;
                    }
                }
            }
            else
            {
                if (!OrderLastMessageReciveFromServer.ContainsKey(messageType))
                {
                    OrderLastMessageReciveFromServer.Add(messageType, 0);

                }
                else
                {
                    OrderLastMessageReciveFromServer[messageType]++;
                }
            }
        }
    }

    public bool CheckMessageOrderRecievedFromClients(int clientID, MessageType messageType, int messageOrder)
    {
        if (!OrderLastMessageReciveFromClients[clientID].ContainsKey(messageType))
        {
            OrderLastMessageReciveFromClients[clientID].Add(messageType, 0);
        }

        Debug.Log(OrderLastMessageReciveFromClients[clientID][messageType] + " - " + messageOrder + " - " + (OrderLastMessageReciveFromClients[clientID][messageType] < messageOrder));
        return OrderLastMessageReciveFromClients[clientID][messageType] < messageOrder;
    }

    public bool CheckMessageOrderRecievedFromServer(MessageType messageType, int messageOrder)
    {
        if (!OrderLastMessageReciveFromServer.ContainsKey(messageType))
        {
            OrderLastMessageReciveFromServer.Add(messageType, 0);
        }

        Debug.Log(OrderLastMessageReciveFromServer[messageType] + " - " + messageOrder + " - " + (OrderLastMessageReciveFromServer[messageType] < messageOrder));
        return OrderLastMessageReciveFromServer[messageType] < messageOrder;
    }

    void AddNewClient(int clientID)
    {
        if (nm.isServer)
        {
            OrderLastMessageReciveFromClients.Add(clientID, new Dictionary<MessageType, int>());
        }
    }

    void RemoveClient(int clientID)
    {
        if (nm.isServer)
        {
            OrderLastMessageReciveFromClients.Remove(clientID);
        }
    }

}
