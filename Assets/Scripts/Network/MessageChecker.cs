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

    public static byte[] SerializeString(char[] charArray, out int sum)
    {
        List<byte> outData = new List<byte>();
        sum = 0;

        for (int i = 0; i < charArray.Length; i++)
        {
            outData.AddRange(BitConverter.GetBytes(charArray[i]));
            sum += (int)charArray[i];
        }

        return outData.ToArray();
    }

    public static string DeserializeString(byte[] message, int stringSize, int indexToInit)
    {
        char[] charArray = new char[stringSize];
        int localSum = 0;
        int messageSum = 0;

        for (int i = 0; i < stringSize; i++)
        {
            charArray[i] = BitConverter.ToChar(message, indexToInit * i);
            localSum += (int)charArray[i];
        }

        messageSum = BitConverter.ToInt32(message, message.Length - sizeof(int));

        if (localSum != messageSum)
        {
            Debug.LogError("El Paquete esta corrompido");
            return null;
        }

       return new string(charArray);
    }
      
}
