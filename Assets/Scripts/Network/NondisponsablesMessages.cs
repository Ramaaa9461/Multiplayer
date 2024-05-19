using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class NondisponsablesMessages
{
    GameManager gm;
    NetworkManager nm;

    Dictionary<MessageType, Queue<byte[]>> LastMessageSendToServer;
    Dictionary<int, Dictionary<MessageType, Queue<byte[]>>> LastMessageBroadcastToClients;

    Dictionary<byte[], float> MessagesHistory = new();
    int secondsToDeleteMessageHistory = 15;

    PingPong pingPong;

    Dictionary<int, Dictionary<MessageType, float>> resendPackageCounterToClients;
    Dictionary<MessageType, float> resendPackageCounterToServer;

    public NondisponsablesMessages()
    {
        nm = NetworkManager.Instance;
        gm = GameManager.Instance;

        pingPong = nm.checkActivity;

        nm.OnRecievedMessage += OnRecievedData;

        gm.OnNewPlayer += AddNewClient;
        gm.OnRemovePlayer += RemoveClient;

        LastMessageSendToServer = new Dictionary<MessageType, Queue<byte[]>>();
        LastMessageBroadcastToClients = new Dictionary<int, Dictionary<MessageType, Queue<byte[]>>>();

        resendPackageCounterToClients = new Dictionary<int, Dictionary<MessageType, float>>();
        resendPackageCounterToServer = new Dictionary<MessageType, float>();
    }


    void OnRecievedData(byte[] data, IPEndPoint ip)
    {
        MessagePriority messagePriority = MessageChecker.CheckMessagePriority(data);
        MessageType messageType = MessageChecker.CheckMessageType(data);

        if ((messagePriority & MessagePriority.NonDisposable) != 0)
        {
            NetConfirmMessage netConfirmMessage = new NetConfirmMessage(MessagePriority.Default, messageType);

            if (nm.isServer)
            {
                nm.Broadcast(netConfirmMessage.Serialize(), ip);
            }
            else
            {
                nm.SendToServer(netConfirmMessage.Serialize());
            }
        }

        if (messageType == MessageType.Confirm)
        {
            NetConfirmMessage netConfirm = new(data);

            if (nm.isServer)
            {
                if (nm.ipToId.ContainsKey(ip))
                {
                    if (LastMessageBroadcastToClients.ContainsKey(nm.ipToId[ip]))
                    {
                        //Debug.Log("Se elimino el primer paquete de " + netConfirm.GetData() + " con el cliente " + nm.ipToId[ip]);

                        if (MessagesHistory.ContainsKey(LastMessageBroadcastToClients[nm.ipToId[ip]][netConfirm.GetData()].Peek()))
                        {
                            LastMessageBroadcastToClients[nm.ipToId[ip]][netConfirm.GetData()].Dequeue();
                        }
                        else
                        {
                            MessagesHistory.Add(LastMessageBroadcastToClients[nm.ipToId[ip]][netConfirm.GetData()].Dequeue(), secondsToDeleteMessageHistory);
                        }
                    }
                }
            }
            else
            {
                //Debug.Log("Se elimino el primer paquete de " + netConfirm.GetData());

                if (MessagesHistory.ContainsKey(LastMessageSendToServer[netConfirm.GetData()].Peek()))
                {
                    LastMessageSendToServer[netConfirm.GetData()].Dequeue();
                }
                else
                {
                    MessagesHistory.Add(LastMessageSendToServer[netConfirm.GetData()].Dequeue(), secondsToDeleteMessageHistory);
                }

            }
        }
    }


    public void AddSentMessagesFromServer(byte[] data, int clientId) //Cada vez que haces un broadcast
    {
        if (nm.isServer)
        {
            MessagePriority messagePriority = MessageChecker.CheckMessagePriority(data);

            if ((messagePriority & MessagePriority.NonDisposable) != 0)
            {
                if (!LastMessageBroadcastToClients.ContainsKey(clientId))
                {
                    LastMessageBroadcastToClients.Add(clientId, new Dictionary<MessageType, Queue<byte[]>>());
                }

                MessageType messageType = MessageChecker.CheckMessageType(data);

                if (!LastMessageBroadcastToClients[clientId].ContainsKey(messageType))
                {
                    LastMessageBroadcastToClients[clientId].Add(messageType, new Queue<byte[]>());
                }

                LastMessageBroadcastToClients[clientId][messageType].Enqueue(data);
            }
        }
    }

    public void AddSentMessagesFromClients(byte[] data)  
    {
        if (!nm.isServer)
        {
            MessagePriority messagePriority = MessageChecker.CheckMessagePriority(data);

            if ((messagePriority & MessagePriority.NonDisposable) != 0)
            {
                MessageType messageType = MessageChecker.CheckMessageType(data);

                if (!LastMessageSendToServer.ContainsKey(messageType))
                {
                    LastMessageSendToServer.Add(messageType, new Queue<byte[]>());
                }

                LastMessageSendToServer[messageType].Enqueue(data);
            }
        }
    }


    void AddNewClient(int clientID)
    {
        if (nm.isServer)
        {
            LastMessageBroadcastToClients.Add(clientID, new Dictionary<MessageType, Queue<byte[]>>());
            resendPackageCounterToClients.Add(clientID, new Dictionary<MessageType, float>());
        }
    }

    void RemoveClient(int clientID)
    {
        if (nm.isServer)
        {
            LastMessageBroadcastToClients.Remove(clientID);
            resendPackageCounterToClients.Remove(clientID);
        }
    }

    public void ResendPackages()
    {
        if (nm.isServer)
        {
            if (resendPackageCounterToClients.Count > 0)
            {
                foreach (int id in resendPackageCounterToClients.Keys)
                {
                    foreach (MessageType messageType in resendPackageCounterToClients[id].Keys)
                    {
                        resendPackageCounterToClients[id][messageType] += Time.deltaTime;

                        if (resendPackageCounterToClients[id][messageType] >= pingPong.GetLatencyFormServer() * 5)
                        {
                            Debug.Log("Se envio el packete de nuevo hacia el cliente " + id);
                            nm.Broadcast(LastMessageBroadcastToClients[id][messageType].Peek(), nm.clients[id].ipEndPoint);
                            resendPackageCounterToClients[id][messageType] = 0;
                        }
                    }
                }
            }
        }
        else
        {
            if (resendPackageCounterToServer.Count > 0)
            {
                foreach (MessageType messageType in resendPackageCounterToServer.Keys)
                {
                    resendPackageCounterToServer[messageType] += Time.deltaTime;

                    if (resendPackageCounterToServer[messageType] >= pingPong.GetLatencyFormServer() * 5)
                    {
                        Debug.Log("Se envio el packete de nuevo hacia el server");
                        nm.SendToServer(LastMessageSendToServer[messageType].Peek());
                        resendPackageCounterToServer[messageType] = 0;
                    }
                }
            }
        }


        if (MessagesHistory.Count > 0)
        {
            List<byte[]> keysToRemove = new List<byte[]>(MessagesHistory.Count);

            foreach (byte[] messageKey in MessagesHistory.Keys)
            {
                keysToRemove.Add(messageKey);
            }

            foreach (byte[] messageKey in keysToRemove)
            {
                MessagesHistory[messageKey] -= Time.deltaTime;

                if (MessagesHistory[messageKey] <= 0)
                {
                    MessagesHistory.Remove(messageKey);
                }
            }
        }
    }
}

