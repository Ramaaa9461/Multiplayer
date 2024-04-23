using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;
using System.Text;
using System.Net;
using UnityEngine.Networking.Types;

public enum MessageType
{
    CheckActivity = -3,
    ServerToClientHandShake = -2,
    ClientToServerHandShake = -1,
    Console = 0,
    Position = 1,
    NewCustomerNotice = 2,
    Disconnection = 3,
    ThereIsNoPlace = 4,
    RepeatMessage = 5
};

public interface IMessage<T>
{
    public MessageType GetMessageType();
    public byte[] Serialize();
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
        (long, int, string) outData = (0,0,"");

        outData.Item1 = BitConverter.ToInt64(message, 4);
        outData.Item2 = BitConverter.ToInt32(message, 12);

        int nameSize = message.Length - sizeof(long) - sizeof(int) * 2; //Le resto la ip, el puerto y la suma
        outData.Item3 = MessageChecker.DeserializeString(message, nameSize, sizeof(long) + sizeof(int));

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
        char[] nameID = data.name.ToCharArray();

        outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));

        outData.AddRange(BitConverter.GetBytes(data.Item1));
        outData.AddRange(BitConverter.GetBytes(data.Item2));

        int sum = 0;
        outData.AddRange(MessageChecker.SerializeString(data.name.ToCharArray(), out sum));
        outData.AddRange(BitConverter.GetBytes(sum));

        return outData.ToArray();
    }
}

public class NetVector3 : IMessage<UnityEngine.Vector3>
{
    private static ulong lastMsgID = 0;
    private Vector3 data;

    public NetVector3(Vector3 data)
    {
        this.data = data;
    }

    public Vector3 Deserialize(byte[] message)
    {
        Vector3 outData;

        outData.x = BitConverter.ToSingle(message, 8);
        outData.y = BitConverter.ToSingle(message, 12);
        outData.z = BitConverter.ToSingle(message, 16);

        return outData;
    }

    public MessageType GetMessageType()
    {
        return MessageType.Position;
    }

    public byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));
        outData.AddRange(BitConverter.GetBytes(lastMsgID++));
        outData.AddRange(BitConverter.GetBytes(data.x));
        outData.AddRange(BitConverter.GetBytes(data.y));
        outData.AddRange(BitConverter.GetBytes(data.z));

        return outData.ToArray();
    }

    //Dictionary<Client,Dictionary<msgType,int>>
}

public class ServerToClientHandShake : IMessage<(int clientID, string clientName)>
{

    (int clientID, string clientName) data;

    public ServerToClientHandShake((int clientID, string clientName) data)
    {
        this.data = data;
    }

    public ServerToClientHandShake(byte[] data)
    {
        this.data = Deserialize(data);
    }

    public (int clientID, string clientName) GetData()
    {
        return data;
    }

    public (int clientID, string clientName) Deserialize(byte[] message)
    {
        (int clientID, string clientName) outData = (0,"");

        outData.clientID = BitConverter.ToInt32(message, 4);

        int dataSize = (message.Length - sizeof(int)) / sizeof(char);
        outData.clientName = MessageChecker.DeserializeString(message, dataSize, sizeof(int));

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
        outData.AddRange(BitConverter.GetBytes(data.clientID));

        int sum = 0;
        outData.AddRange(MessageChecker.SerializeString(data.clientName.ToCharArray(), out sum));
        outData.AddRange(BitConverter.GetBytes(sum));

        return outData.ToArray();
    }
}


[Serializable]
public class NetMessage : IMessage<char[]>
{
    char[] data;

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
        int dataSize = (message.Length - sizeof(int)) / sizeof(char);

        string text = MessageChecker.DeserializeString(message, dataSize, sizeof(int));

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
        
        int sum = 0;
        outData.AddRange(MessageChecker.SerializeString(data,out sum));

        outData.AddRange(BitConverter.GetBytes(sum));
        
        return outData.ToArray();
    }
}