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
    Default = -100,
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
    UpdateLobbyTimerForNewPlayers = 6,
    Winner = 7
};

public interface IMessage<T>
{
    public byte[] Serialize(); //Hay que poner el Checksum siempre como ultimo parametro
    public T Deserialize(byte[] message);
}

public abstract class BaseMessage<T> : IMessage<T>
{
    protected int messageHeaderSize = sizeof(int) * 2; //MessageType y MessagePriority

    protected MessagePriority currentMessagePriority;
    protected MessageType currentMessageType;
    protected int messageOrder = 0;

    #region Properties

    public MessagePriority CurrentMessagePriority
    {
        get { return currentMessagePriority; }
        set { currentMessagePriority = value; }
    }

    public MessageType CurrentMessageType
    {
        get { return currentMessageType; }
        set { currentMessageType = value; }
    }

    public int MessageOrder
    {
        get { return messageOrder; }
        set { messageOrder = value; }
    }

    public bool IsSorteableMessage
    {
        get { return ((currentMessagePriority & MessagePriority.Sorteable) != 0); }
    }

    public bool IsNondisponsableMessage
    {
        get { return ((currentMessagePriority & MessagePriority.NonDisposable) != 0); }
    }

    #endregion

    public BaseMessage(MessagePriority messagePriority)
    {
        currentMessagePriority = messagePriority;
    }

    public void DeserializeHeader(byte[] message)
    {
        currentMessageType = (MessageType)BitConverter.ToInt32(message, 0);
        currentMessagePriority = (MessagePriority)BitConverter.ToInt32(message, sizeof(int));

        if (IsSorteableMessage)
        {
            messageOrder = BitConverter.ToInt32(message, sizeof(int) * 2);
            messageHeaderSize += sizeof(int);
        }

        if (IsNondisponsableMessage)
        {

        }
    }

    public void SerializeHeader(ref List<byte> outData)
    {
        outData.AddRange(BitConverter.GetBytes((int)currentMessageType));
        outData.AddRange(BitConverter.GetBytes((int)currentMessagePriority));

        if (IsSorteableMessage)
        {
            outData.AddRange(BitConverter.GetBytes(messageOrder));
            messageHeaderSize += sizeof(int);
        }

        if (IsNondisponsableMessage)
        {
            //Creo que no hay que serializar nada, lo dejo por las dudas
        }
    }

    public void SerializeQueue(ref List<byte> data)
    {
        data.AddRange(MessageChecker.SerializeCheckSum(data));
    }

    public abstract byte[] Serialize();

    public abstract T Deserialize(byte[] message);

}

public class ClientToServerNetHandShake : BaseMessage<(long, int, string)>
{
    (long ip, int port, string name) data;

    public ClientToServerNetHandShake(MessagePriority messagePriority, (long, int, string) data) : base(messagePriority)
    {
        currentMessageType = MessageType.ClientToServerHandShake;
        this.data = data;
    }

    public ClientToServerNetHandShake(byte[] data) : base(MessagePriority.Default)
    {
        currentMessageType = MessageType.ClientToServerHandShake;
        this.data = Deserialize(data);
    }

    public override (long, int, string) Deserialize(byte[] message)
    {
        (long, int, string) outData = (0, 0, "");

        if (MessageChecker.DeserializeCheckSum(message))
        {
            DeserializeHeader(message);

            outData.Item1 = BitConverter.ToInt64(message, messageHeaderSize);
            messageHeaderSize += sizeof(long);
            outData.Item2 = BitConverter.ToInt32(message, messageHeaderSize);
            messageHeaderSize += sizeof(int);

            outData.Item3 = MessageChecker.DeserializeString(message, messageHeaderSize);
        }

        return outData;
    }

    public (long, int, string) GetData()
    {
        return data;
    }

    public override byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        SerializeHeader(ref outData);

        outData.AddRange(BitConverter.GetBytes(data.Item1));
        outData.AddRange(BitConverter.GetBytes(data.Item2));

        outData.AddRange(MessageChecker.SerializeString(data.name.ToCharArray()));

        SerializeQueue(ref outData);

        return outData.ToArray();
    }
}

public class NetVector3 : BaseMessage<(int, Vector3)>
{
    private (int id, Vector3 position) data;

    public NetVector3(MessagePriority messagePriority, (int, Vector3) data) : base(messagePriority)
    {
        currentMessageType = MessageType.Position;
        this.data = data;
    }

    public NetVector3(byte[] data) : base(MessagePriority.Default)
    {
        currentMessageType = MessageType.Position;
        this.data = Deserialize(data);
    }

    public (int id, Vector3 position) GetData()
    {
        return data;
    }

    public override (int, Vector3) Deserialize(byte[] message)
    {
        (int id, Vector3 position) outData = (-1, Vector3.zero);

        if (MessageChecker.DeserializeCheckSum(message))
        {
            DeserializeHeader(message);

            outData.id = BitConverter.ToInt32(message, messageHeaderSize);
            messageHeaderSize += sizeof(int);

            outData.position.x = BitConverter.ToSingle(message, messageHeaderSize);
            messageHeaderSize += sizeof(float);
            outData.position.y = BitConverter.ToSingle(message, messageHeaderSize);
            messageHeaderSize += sizeof(float);
            outData.position.z = BitConverter.ToSingle(message, messageHeaderSize);
        }

        return outData;
    }

    public override byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        SerializeHeader(ref outData);

        outData.AddRange(BitConverter.GetBytes(data.id));

        outData.AddRange(BitConverter.GetBytes(data.position.x));
        outData.AddRange(BitConverter.GetBytes(data.position.y));
        outData.AddRange(BitConverter.GetBytes(data.position.z));

        SerializeQueue(ref outData);

        return outData.ToArray();
    }
}

public class ServerToClientHandShake : BaseMessage<List<(int clientID, string clientName)>>
{

    private List<(int clientID, string clientName)> data;

    public ServerToClientHandShake(MessagePriority messagePriority, List<(int clientID, string clientName)> data) : base(messagePriority)
    {
        currentMessageType = MessageType.ServerToClientHandShake;
        this.data = data;

    }

    public ServerToClientHandShake(byte[] data) : base(MessagePriority.Default)
    {
        currentMessageType = MessageType.ServerToClientHandShake;
        this.data = Deserialize(data);
    }

    public List<(int clientID, string clientName)> GetData()
    {
        return data;
    }

    public override List<(int clientID, string clientName)> Deserialize(byte[] message)
    {
        List<(int clientID, string clientName)> outData = new List<(int, string)>();

        if (MessageChecker.DeserializeCheckSum(message))
        {
            DeserializeHeader(message);

            int listCount = BitConverter.ToInt32(message, messageHeaderSize);

            int offSet = messageHeaderSize + sizeof(int);
            for (int i = 0; i < listCount; i++)
            {
                int clientID = BitConverter.ToInt32(message, offSet);
                offSet += sizeof(int);
                int clientNameLength = BitConverter.ToInt32(message, offSet);
                string name = MessageChecker.DeserializeString(message, offSet);
                offSet += sizeof(char) * clientNameLength + sizeof(int);

                outData.Add((clientID, name));
            }
        }
        return outData;
    }

    public override byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        SerializeHeader(ref outData);

        outData.AddRange(BitConverter.GetBytes(data.Count));

        foreach ((int clientID, string clientName) clientInfo in data)
        {
            outData.AddRange(BitConverter.GetBytes(clientInfo.clientID)); // ID del client
            outData.AddRange(MessageChecker.SerializeString(clientInfo.clientName.ToCharArray())); //Nombre
        }

        SerializeQueue(ref outData);

        return outData.ToArray();
    }
}

[Serializable]
public class NetMessage : BaseMessage<char[]>
{
    char[] data;

    public NetMessage(MessagePriority priority, char[] data) : base(priority)
    {
        currentMessageType = MessageType.Console;
        this.data = data;
    }

    public NetMessage(byte[] data) : base(MessagePriority.Default) //Se actualiza en el Deserialize esto
    {
        currentMessageType = MessageType.Console;
        this.data = Deserialize(data);
    }

    public char[] GetData()
    {
        return data;
    }

    public override char[] Deserialize(byte[] message)
    {
        string text = "";

        DeserializeHeader(message);

        if (MessageChecker.DeserializeCheckSum(message))
        {
            text = MessageChecker.DeserializeString(message, messageHeaderSize);
        }

        return text.ToCharArray();
    }

    public override byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        SerializeHeader(ref outData);


        outData.AddRange(MessageChecker.SerializeString(data));


        SerializeQueue(ref outData);

        return outData.ToArray();
    }
}

public class NetPing
{
    MessageType messageType = MessageType.Ping;

    public MessageType GetMessageType()
    {
        return messageType;
    }

    public byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));
        outData.AddRange(BitConverter.GetBytes((int)MessagePriority.Default));

        return outData.ToArray();
    }
}

public class NetIDMessage : BaseMessage<int>
{
    int clientID;

    public NetIDMessage(MessagePriority messagePriority, int clientID) : base(messagePriority)
    {
        currentMessageType = MessageType.Disconnection;
        this.clientID = clientID;
    }

    public NetIDMessage(byte[] data) : base(MessagePriority.Default)
    {
        currentMessageType = MessageType.Disconnection;
        this.clientID = Deserialize(data);
    }

    public override int Deserialize(byte[] message)
    {
        DeserializeHeader(message);

        if (MessageChecker.DeserializeCheckSum(message))
        {
            clientID = BitConverter.ToInt32(message, messageHeaderSize);
        }
        return clientID;
    }

    public int GetData()
    {
        return clientID;
    }

    public override byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        SerializeHeader(ref outData);

        outData.AddRange(BitConverter.GetBytes(clientID));

        SerializeQueue(ref outData);

        return outData.ToArray();
    }
}

public class NetErrorMessage : BaseMessage<string>
{
    string error;

    public NetErrorMessage(string error) : base(MessagePriority.Default) //Simepre van a ser default, lo dejo implicito aca desde el principio
    {
        currentMessageType = MessageType.Error;
        this.error = error;
    }

    public NetErrorMessage(byte[] message) : base(MessagePriority.Default)
    {
        currentMessageType = MessageType.Error;
        error = Deserialize(message);
    }

    public override string Deserialize(byte[] message)
    {
        DeserializeHeader(message);

        if (MessageChecker.DeserializeCheckSum(message))
        {
            error = MessageChecker.DeserializeString(message, messageHeaderSize);
        }

        return error;
    }

    public string GetData()
    {
        return error;
    }

    public override byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        SerializeHeader(ref outData);

        outData.AddRange(MessageChecker.SerializeString(error.ToCharArray()));

        SerializeQueue(ref outData);

        return outData.ToArray();
    }
}

public class NetUpdateTimer : BaseMessage<bool>
{
    bool initTimer;

    public NetUpdateTimer(MessagePriority messagePriority, bool initTimer) : base(messagePriority)
    {
        currentMessageType = MessageType.UpdateLobbyTimer;
        this.initTimer = initTimer;
    }

    public NetUpdateTimer(byte[] data) : base(MessagePriority.Default)
    {
        currentMessageType = MessageType.UpdateLobbyTimer;
        this.initTimer = Deserialize(data);
    }

    public bool GetData()
    {
        return initTimer;
    }

    public override bool Deserialize(byte[] message)
    {
        DeserializeHeader(message);
 
        if (MessageChecker.DeserializeCheckSum(message))
        {
            return BitConverter.ToBoolean(message, messageHeaderSize);
        }

        return false;
    }

    public override byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        SerializeHeader(ref outData);

        outData.AddRange(BitConverter.GetBytes(initTimer));
        outData.AddRange(BitConverter.GetBytes(initTimer));

        SerializeQueue(ref outData);

        return outData.ToArray();
    }
}

public class NetConfirmMessage : BaseMessage<MessageType>
{
    MessageType messageTypeToConfirm = MessageType.Default;

    public NetConfirmMessage(MessagePriority messagePriority, MessageType messageTypeToConfirm) : base(MessagePriority.Default)
    {
        currentMessageType = MessageType.Confirm;
        this.messageTypeToConfirm = messageTypeToConfirm;
    }

    public NetConfirmMessage(byte[] message) : base(MessagePriority.Default)
    {
        currentMessageType = MessageType.Confirm;
        this.messageTypeToConfirm = Deserialize(message);
    }

    public MessageType GetData()
    {
        return messageTypeToConfirm;
    }

    public override MessageType Deserialize(byte[] message)
    {
        DeserializeHeader(message);

        if (MessageChecker.DeserializeCheckSum(message))
        {
            messageTypeToConfirm = (MessageType)BitConverter.ToInt32(message, messageHeaderSize);
        }

        return messageTypeToConfirm;
    }

    public override byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        SerializeHeader(ref outData);

        outData.AddRange(BitConverter.GetBytes((int)messageTypeToConfirm));

        SerializeQueue(ref outData);

        return outData.ToArray();
    }
}

public class NetUpdateNewPlayersTimer : BaseMessage<float>
{
    float timer = -1;

    public NetUpdateNewPlayersTimer(MessagePriority messagePriority, float timer) : base(messagePriority)
    {
        currentMessageType = MessageType.UpdateLobbyTimerForNewPlayers;
        this.timer = timer;
    }

    public NetUpdateNewPlayersTimer(byte[] data) : base(MessagePriority.Default)
    {
        currentMessageType = MessageType.UpdateLobbyTimerForNewPlayers;
        timer = Deserialize(data);
    }

    public float GetData()
    {
        return timer;
    }

    public override float Deserialize(byte[] message)
    {
        DeserializeHeader(message);

        if (MessageChecker.DeserializeCheckSum(message))
        {
            timer = BitConverter.ToSingle(message, messageHeaderSize);
        }

        return timer;
    }

    public override byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        SerializeHeader(ref outData);

        outData.AddRange(BitConverter.GetBytes(timer));

        SerializeQueue(ref outData);

        return outData.ToArray();
    }
}
