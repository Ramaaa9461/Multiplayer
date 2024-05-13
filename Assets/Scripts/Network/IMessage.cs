using System;
using System.Collections.Generic;
using UnityEngine;

// ctrl R G  -- ctrl shift V -- shitf enter


[Flags]
public enum MessagePriority
{
    Default = 0,
    Sorteable = 1,
    NonDisposable = 2
}

public enum MessageType
{
    Confirm = -5,
    Error = -4,
    Ping = -3,
    ServerToClientHandShake = -2,
    ClientToServerHandShake = -1,
    Console = 0,
    Position = 1,
    BulletInstatiate = 2,
    Disconnection = 3,
    UpdateLobbyTimer = 4,
    UpdateGameplayTimer = 5,
    Winner = 6
};

public interface IMessage<T>
{
    public MessageType GetMessageType();
    public byte[] Serialize(); //Hay que poner el Checksum siempre como ultimo parametro
    public T Deserialize(byte[] message);
}

public class ClientToServerNetHandShake : IMessage<(long, int, string)>
{
    (long ip, int port, string name) data;


    public ClientToServerNetHandShake((long, int, string) data)
    {
        this.data = data;
    }

    public ClientToServerNetHandShake(byte[] data)
    {
        this.data = Deserialize(data);
    }

    public (long, int, string) Deserialize(byte[] message)
    {
        (long, int, string) outData = (0, 0, "");

        outData.Item1 = BitConverter.ToInt64(message, 4);
        outData.Item2 = BitConverter.ToInt32(message, 12);

        outData.Item3 = MessageChecker.DeserializeString(message, sizeof(long) + sizeof(int) * 2);

        return outData;
    }

    public MessageType GetMessageType()
    {
        return MessageType.ClientToServerHandShake;
    }

    public (long, int, string) GetData()
    {
        return data;
    }

    public byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));

        outData.AddRange(BitConverter.GetBytes(data.Item1));
        outData.AddRange(BitConverter.GetBytes(data.Item2));

        outData.AddRange(MessageChecker.SerializeString(data.name.ToCharArray()));

        return outData.ToArray();
    }
}

public class NetVector3 : IMessage<(int, Vector3)>
{
    private (int id, Vector3 position) data;

    MessagePriority messagePriority = MessagePriority.Default;
    MessageType currentMessageType = MessageType.Position;

    public NetVector3((int, Vector3) data)
    {
        this.data = data;
    }

    public NetVector3(byte[] data)
    {
        this.data = Deserialize(data);
    }

    public (int id, Vector3 position) GetData()
    {
        return data;
    }


    public (int, Vector3) Deserialize(byte[] message)
    {
        (int id, Vector3 position) outData;

        outData.id = BitConverter.ToInt32(message, 4);

        outData.position.x = BitConverter.ToSingle(message, 8);
        outData.position.y = BitConverter.ToSingle(message, 12);
        outData.position.z = BitConverter.ToSingle(message, 16);

        return outData;
    }

    public void SetMessageType(MessageType type)
    {
        currentMessageType = type;
    }

    public MessageType GetMessageType()
    {
        return currentMessageType;
    }

    public byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));
        outData.AddRange(BitConverter.GetBytes(data.id));

        outData.AddRange(BitConverter.GetBytes(data.position.x));
        outData.AddRange(BitConverter.GetBytes(data.position.y));
        outData.AddRange(BitConverter.GetBytes(data.position.z));

        return outData.ToArray();
    }

    //Dictionary<Client,Dictionary<msgType,int>>
}

public class ServerToClientHandShake : IMessage<List<(int clientID, string clientName)>>
{

    private List<(int clientID, string clientName)> data;

    public ServerToClientHandShake(List<(int clientID, string clientName)> data)
    {
        this.data = data;
    }

    public ServerToClientHandShake(byte[] data)
    {
        this.data = Deserialize(data);
    }

    public List<(int clientID, string clientName)> GetData()
    {
        return data;
    }

    public List<(int clientID, string clientName)> Deserialize(byte[] message)
    {
        List<(int clientID, string clientName)> outData = new List<(int, string)>();

        int listCount = BitConverter.ToInt32(message, sizeof(int));

        int offSet = sizeof(int) * 2;
        for (int i = 0; i < listCount; i++)
        {
            int clientID = BitConverter.ToInt32(message, offSet);
            offSet += sizeof(int);
            int clientNameLength = BitConverter.ToInt32(message, offSet);
            string name = MessageChecker.DeserializeString(message, offSet);
            offSet += sizeof(char) * clientNameLength + sizeof(int);

            outData.Add((clientID, name));
        }

        return outData;
    }


    public MessageType GetMessageType()
    {
        return MessageType.ServerToClientHandShake;
    }

    public byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));

        outData.AddRange(BitConverter.GetBytes(data.Count));

        foreach ((int clientID, string clientName) clientInfo in data)
        {
            outData.AddRange(BitConverter.GetBytes(clientInfo.clientID)); // ID del client
            outData.AddRange(MessageChecker.SerializeString(clientInfo.clientName.ToCharArray())); //Nombre
        }

        return outData.ToArray();
    }
}

[Serializable]
public class NetMessage : IMessage<char[]>
{
    char[] data;
    MessagePriority messagePriority = MessagePriority.NonDisposable;

    public NetMessage(char[] data)
    {
        this.data = data;
    }

    public NetMessage(byte[] data)
    {
        this.data = Deserialize(data);
    }

    public char[] GetData()
    {
        return data;
    }

    public char[] Deserialize(byte[] message)
    {
        string text = "";

        messagePriority = (MessagePriority)BitConverter.ToInt32(message, 4);

        if ((messagePriority & MessagePriority.Sorteable) != 0)
        {
            Debug.Log("La flag Sorteable está activa.");
        }
        if ((messagePriority & MessagePriority.NonDisposable) != 0)
        {
            NetPing netPing = new();
            netPing.SetMessageType(MessageType.Confirm);
            NetworkManager.Instance.Broadcast(netPing.Serialize());
        }

        if (MessageChecker.DeserializeCheckSum(message))
        {
            text = MessageChecker.DeserializeString(message, sizeof(int) * 2);
        }

        return text.ToCharArray();
    }

    public MessageType GetMessageType()
    {
        return MessageType.Console;
    }

    public byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));

        outData.AddRange(BitConverter.GetBytes((int)messagePriority));

        outData.AddRange(MessageChecker.SerializeString(data));

        outData.AddRange(MessageChecker.SerializeCheckSum(outData));

        return outData.ToArray();
    }
}

public class NetPing
{
    MessageType messageType = MessageType.Ping;

    public void SetMessageType(MessageType messageType)
    {
        this.messageType = messageType;
    }
    public MessageType GetMessageType()
    {
        return messageType;
    }

    public byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));

        return outData.ToArray();
    }
}

public class NetIDMessage : IMessage<int>
{
    int clientID;

    MessageType currentMessageType = MessageType.Disconnection;

    public NetIDMessage(int clientID)
    {
        this.clientID = clientID;
    }

    public NetIDMessage(byte[] data)
    {
        this.clientID = Deserialize(data);
    }

    public int Deserialize(byte[] message)
    {
        return BitConverter.ToInt32(message, sizeof(int));
    }

    public void SetMessageType(MessageType newMessageType)
    {
        currentMessageType = newMessageType;
    }

    public MessageType GetMessageType()
    {
        return currentMessageType;
    }

    public int GetData()
    {
        return clientID;
    }

    public byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));
        outData.AddRange(BitConverter.GetBytes(clientID));

        return outData.ToArray();
    }
}

public class NetErrorMessage : IMessage<string>
{
    string error;

    public NetErrorMessage(string error)
    {
        this.error = error;
    }
    public NetErrorMessage(byte[] message)
    {
        error = Deserialize(message);
    }

    public string Deserialize(byte[] message)
    {

        if (MessageChecker.DeserializeCheckSum(message))
        {
            error = MessageChecker.DeserializeString(message, sizeof(int));
        }

        return error;
    }

    public MessageType GetMessageType()
    {
        return MessageType.Error;
    }

    public string GetData()
    {
        return error;
    }

    public byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));
        outData.AddRange(MessageChecker.SerializeString(error.ToCharArray()));
        outData.AddRange(MessageChecker.SerializeCheckSum(outData));

        return outData.ToArray();
    }
}

public class NetUpdateTimer : IMessage<bool>
{
    bool initTimer;
    MessageType currentMessageType = MessageType.UpdateLobbyTimer;

    public NetUpdateTimer(bool initTimer)
    {
        this.initTimer = initTimer;
    }

    public NetUpdateTimer(byte[] data)
    {
        this.initTimer = Deserialize(data);
    }

    public bool Deserialize(byte[] message)
    {
        if (MessageChecker.DeserializeCheckSum(message))
        {
            return BitConverter.ToBoolean(message, sizeof(int));
        }

        return false;
    }
    public bool GetData()
    {
        return initTimer;
    }

    public void SetMessageType(MessageType type)
    {
        currentMessageType = type;
    }

    public MessageType GetMessageType()
    {
        return currentMessageType;
    }

    public byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));
        outData.AddRange(BitConverter.GetBytes(initTimer));
        outData.AddRange(MessageChecker.SerializeCheckSum(outData));

        return outData.ToArray();
    }
}
