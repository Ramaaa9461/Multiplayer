using TMPro;
using UnityEngine;

enum States { Init, Lobby, Game, Finish };

public class ServerGameplay : MonoBehaviour
{
    int minutesInLobby = 1; // 2 minutos
    float minutesGameplay = 180; //3 minutos
    float timeUntilCloseServer = 5;

    int minPlayerToInitCounter = 2;

    GameManager gm;
    NetworkManager nm;
    States currentState;

    bool counterInit = false;

    bool initLobby = true;
    bool initGameplay = true;

    float counter = 0;

    bool clientLobbyTimer = false;
    bool clientGameplayTimer = false;


    private void Start()
    {
        gm = GameManager.Instance;
        nm = NetworkManager.Instance;

        gm.OnInitLobbyTimer += SetLobbyTimer;
        gm.OnInitGameplayTimer += SetGameplayTimer;
    }

    private void Update()
    {
        if (nm.isServer)
        {
            UpdateServer();
        }
        else
        {
            UpdateClient();
        }
    }

    void UpdateServer()
    {
        if (nm != null && nm.isServer)
        {
            switch (currentState)
            {
                case States.Init:

                    currentState = States.Lobby;

                    break;
                case States.Lobby:


                    if (nm.clients.Count >= minPlayerToInitCounter)
                    {
                        counterInit = true;

                        if (initLobby)
                        {
                            NetUpdateTimer netUpdateLobbyTimer = new NetUpdateTimer(true);
                            netUpdateLobbyTimer.SetMessageType(MessageType.UpdateLobbyTimer);
                            nm.Broadcast(netUpdateLobbyTimer.Serialize());
                            initLobby = false;
                        }

                        counter += Time.deltaTime;
                        gm.timer.text = counter.ToString("F2") + "s";

                        if (counter >= minutesInLobby)
                        {
                            counter = 0;
                            gm.timer.text = "";
                            nm.matchOnGoing = true;
                            currentState = States.Game;
                        }
                    }
                    else
                    {
                        if (counterInit)
                        {
                            NetUpdateTimer netUpdateLobbyTimer = new NetUpdateTimer(false);
                            netUpdateLobbyTimer.SetMessageType(MessageType.UpdateLobbyTimer);
                            nm.Broadcast(netUpdateLobbyTimer.Serialize());

                            counterInit = false;
                            initLobby = true;

                            counter = 0;
                            gm.timer.text = "";

                            currentState = States.Init;

                        }
                    }

                    break;
                case States.Game:

                    if (initGameplay)
                    {
                        NetUpdateTimer netUpdateGameplayTimer = new NetUpdateTimer(true);
                        netUpdateGameplayTimer.SetMessageType(MessageType.UpdateGameplayTimer);
                        nm.Broadcast(netUpdateGameplayTimer.Serialize());

                        initGameplay = false;
                    }

                    counter += Time.deltaTime;
                    gm.timer.text = counter.ToString("F2") + "s";

                    if (counter >= minutesGameplay)
                    {
                        SendMatchWinner();

                        gm.timer.text = "";
                        currentState = States.Finish;
                    }
                    break;

                case States.Finish:

                    timeUntilCloseServer -= Time.deltaTime;

                    if (timeUntilCloseServer <= 0)
                    {
                        nm.CloseServer();
                    }

                    break;


                default:
                    break;
            }
        }
    }

    void UpdateClient()
    {
        if (clientLobbyTimer)
        {
            counter += Time.deltaTime;
            gm.timer.text = counter.ToString("F2") + "s";
        }

        if (clientGameplayTimer)
        {
            clientLobbyTimer = false;

            counter += Time.deltaTime;
            gm.timer.text = counter.ToString("F2") + "s";
        }
    }

    void SetGameplayTimer()
    {
        clientGameplayTimer = true;
        counter = 0;
    }

    void SetLobbyTimer(bool init)
    {
        clientLobbyTimer = init;
    }

    void SendMatchWinner()
    {
        PlayerController playerWithMaxHealth = null;
        int maxHealth = int.MinValue;

        foreach (int index in gm.playerList.Keys)
        {
            if (gm.playerList[index].TryGetComponent(out PlayerController pc))
            {
                if (pc.health > maxHealth)
                {
                    maxHealth = pc.health;
                    playerWithMaxHealth = pc;
                }
            }
        }

        NetIDMessage netIDMessage = new NetIDMessage(playerWithMaxHealth.clientID);
        netIDMessage.SetMessageType(MessageType.Winner);
        nm.Broadcast(netIDMessage.Serialize());
    }
}
