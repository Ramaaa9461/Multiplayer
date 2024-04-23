using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MessageChecker
{
    public MessageType CheckMessageType(byte[] message)
    {
        int messageType = 0;

        messageType = BitConverter.ToInt32(message, 0);

        return (MessageType)messageType;
    }

    public static byte[] SerializeString(char[] charArray)
    {
        List<byte> outData = new List<byte>();

        for (int i = 0; i < charArray.Length; i++)
        {
            outData.AddRange(BitConverter.GetBytes(charArray[i]));
        }

        return outData.ToArray();
    }

    public static string DeserializeString(byte[] message, int stringSize, int indexToInit)
    {
        char[] charArray = new char[stringSize];

        for (int i = 0; i < stringSize; i++)
        {
            charArray[i] = BitConverter.ToChar(message, indexToInit * i);
        }

       return new string(charArray);
    }
      
}
