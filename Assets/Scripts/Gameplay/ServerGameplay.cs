using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

enum States { Init, Lobby, Game, Finish };

public class ServerGameplay : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI timer;

    int minutesInLobby = 120;
    float minutesGameplay = 180;
    float timeUntilCloseServer = 2;

    int minPlayerToInitCounter = 2;

    NetworkManager nm;
    States currentState;

    float counter = 0;

    private void Start()
    {
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
                        timer.text = counter + "s";
                        
                        if (counter >= minutesInLobby)
                        {
                            timer.text = "";
                            currentState = States.Game;
                        }
                    }
                    else
                    {
                        counter = 0;
                        timer.text = "";
                        currentState = States.Init;
                    }

                    break;
                case States.Game:

                    minutesGameplay -= Time.deltaTime;
                    timer.text = minutesGameplay + "s";

                    if (minutesGameplay <= 0)
                    {
                        timer.text = "";
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
