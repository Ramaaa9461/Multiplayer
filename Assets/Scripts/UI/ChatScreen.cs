using System.Net;
using UnityEngine.UI;

public class ChatScreen : MonoBehaviourSingleton<ChatScreen>
{
    public Text messages;
    public InputField inputMessage;

    protected override void Initialize()
    {
        inputMessage.onEndEdit.AddListener(OnEndEdit);

        this.gameObject.SetActive(false);
    }

    void OnEndEdit(string str)
    {
        if (inputMessage.text != "")
        {
            string name = NetworkManager.Instance.userName + ": ";
            str = name + str;
            if (NetworkManager.Instance.isServer)
            {
                NetMessage netMessage = new NetMessage(str.ToCharArray());

                NetworkManager.Instance.clientConsoleMessage.Enqueue(netMessage.Serialize());
                NetworkManager.Instance.Broadcast(netMessage.Serialize());
                messages.text += str + System.Environment.NewLine;
            }
            else
            {
                NetMessage netMessage = new NetMessage(str.ToCharArray());

                NetworkManager.Instance.clientConsoleMessage.Enqueue(netMessage.Serialize());
                NetworkManager.Instance.SendToServer(netMessage.Serialize());
            }

            inputMessage.ActivateInputField();
            inputMessage.Select();
            inputMessage.text = "";
        }

    }

}
