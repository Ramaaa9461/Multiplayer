using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

enum States { Init, Lobby, Game, Finish };

public class ServerGameplay : MonoBehaviour
{

    int minutesInLobby = 120; // 2 minutos
    float minutesGameplay = 180; //3 minutos
    float timeUntilCloseServer = 2;

    int minPlayerToInitCounter = 2;

    GameManager gm;
    NetworkManager nm;
    States currentState;

    float counter = 0;

    private void Start()
    {
        gm = GameManager.Instance;
        nm = NetworkManager.Instance;
    }

    private void Update()
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
                        counter += Time.deltaTime;
                        gm.timer.text = counter.ToString("F2") + "s";

                        NetUpdateTimer netUpdateLobbyTimer = new NetUpdateTimer(counter);
                        netUpdateLobbyTimer.SetMessageType(MessageType.UpdateLobbyTimer);
                        nm.Broadcast(netUpdateLobbyTimer.Serialize());

                        if (counter >= minutesInLobby)
                        {
                            gm.timer.text = "";
                            nm.matchOnGoing = true;
                            currentState = States.Game;
                        }
                    }
                    else
                    {
                        counter = 0;
                        Debug.Log(gm);
                        gm.timer.text = "";
                        currentState = States.Init;
                    }

                    break;
                case States.Game:

                    minutesGameplay -= Time.deltaTime;
                    gm.timer.text = minutesGameplay.ToString("F2") + "s";

                    NetUpdateTimer netUpdateGameplayTimer = new NetUpdateTimer(minutesGameplay);
                    netUpdateGameplayTimer.SetMessageType(MessageType.UpdateGameplayTimer);
                    nm.Broadcast(netUpdateGameplayTimer.Serialize());

                    if (minutesGameplay <= 0)
                    {
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
}
