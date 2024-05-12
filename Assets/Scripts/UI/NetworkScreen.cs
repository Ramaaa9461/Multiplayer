﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Net;
using TMPro;

public class NetworkScreen : MonoBehaviourSingleton<NetworkScreen>
{
    public Button connectBtn;
    public Button startServerBtn;
    public InputField nameInputField;
    public InputField portInputField;
    public InputField addressInputField;

    [SerializeField] GameObject panelError;
    [SerializeField] TextMeshProUGUI errorText;

    [SerializeField] GameObject winPanel;
    [SerializeField] TextMeshProUGUI winText;

    protected override void Initialize()
    {
        connectBtn.onClick.AddListener(OnConnectBtnClick);
        startServerBtn.onClick.AddListener(OnStartServerBtnClick);
    }

    void OnConnectBtnClick()
    {
        IPAddress ipAddress = IPAddress.Parse(addressInputField.text);
        int port = System.Convert.ToInt32(portInputField.text);

        NetworkManager.Instance.StartClient(ipAddress, port, nameInputField.text);

        SwitchToChatScreen();
    }

    void OnStartServerBtnClick()
    {
        int port = System.Convert.ToInt32(portInputField.text);
        NetworkManager.Instance.StartServer(port);
        SwitchToChatScreen();
    }

    void SwitchToChatScreen()
    {
        ChatScreen.Instance.gameObject.SetActive(true);
        this.gameObject.SetActive(false);
    }

    public void SwitchToMenuScreen()
    {
        ChatScreen.Instance.gameObject.SetActive(false);
        this.gameObject.SetActive(true);
    }

    public void ShowErrorPanel(string errorString)
    {
        panelError.SetActive(true);
        errorText.text = errorString;

        Invoke(nameof(TurnOffErrorPanel), 3.0f);
    }

    void TurnOffErrorPanel()
    {
       panelError.SetActive(false);
    }

    public void ShowWinPanel(string winnerText)
    {
        winPanel.SetActive(true);

        winText.text = winnerText;
        Invoke(nameof(TurnOffWinPanel), 5.0f);
    }

    void TurnOffWinPanel()
    {
        winPanel.SetActive(false);
    }
}
