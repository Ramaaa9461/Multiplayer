using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviourSingleton<GameManager>
{
    public Action<int> OnBulletHit;

    public Action<int> OnNewPlayer;
    public Action<int> OnRemovePlayer;

    public Action<bool> OnInitLobbyTimer;
    public Action OnInitGameplayTimer;

    public Action<int> OnChangeLobbyPlayers;

    public Action<int, Vector3> OnInstantiateBullet;

    public TextMeshProUGUI timer;

    [SerializeField] Transform[] spawnPositions;

    [SerializeField] GameObject playerPrefab;
    public Dictionary<int, GameObject> playerList = new Dictionary<int, GameObject>();

    int spawnCounter = 0;

    NetworkManager nm;
    public bool isGameplay;

    void Start()
    {
        nm = NetworkManager.Instance;

        OnNewPlayer += SpawnPlayerPefab;
        OnRemovePlayer += RemovePlayer;
        OnInstantiateBullet += InstantiatePlayerBullets;
        OnBulletHit += OnHitRecieved;

        OnInitGameplayTimer += ActivePlayerControllers;
    }

    void SpawnPlayerPefab(int index)
    {
        if (!playerList.ContainsKey(index))
        {
            playerList.Add(index, Instantiate(playerPrefab, spawnPositions[spawnCounter].position, Quaternion.identity));
            OnChangeLobbyPlayers?.Invoke(index);
            spawnCounter++;
        }

        if (playerList[index].TryGetComponent(out PlayerController pc))
        {
            pc.clientID = index;

            if (index != nm.actualClientId)
            {
                pc.currentPlayer = false;
            }
            else
            {
                pc.currentPlayer = true;
            }

            if (!nm.isServer)
            {
                pc.enabled = false;
            }
        }
    }

    void RemovePlayer(int index)
    {
        if (playerList.ContainsKey(index))
        {
            Destroy(playerList[index]);
            playerList.Remove(index);

            if (!nm.isServer && index == nm.actualClientId)
            {
                spawnCounter = 0;
                RemoveAllPlayers();
            }
        }
    }

    public void RemoveAllPlayers()
    {
        foreach (int id in playerList.Keys)
        {
            Destroy(playerList[id]);
        }

        playerList.Clear();
    }

    void InstantiatePlayerBullets(int id, Vector3 bulletDir)
    {
        playerList[id].GetComponent<PlayerController>().ServerShoot(bulletDir);
        playerList[id].GetComponent<AudioSource>().Play();
        playerList[id].GetComponent<Animator>().SetTrigger("Shoot");
    }

    public void UpdatePlayerPosition((int index, Vector3 newPosition) playerData)
    {
        if (playerList.ContainsKey(playerData.index))
        {
            playerList[playerData.index].transform.position = playerData.newPosition;
        }
    }

    void OnHitRecieved(int playerReciveDamage)
    {
        if (nm.isServer)
        {
            if (playerList.ContainsKey(playerReciveDamage))
            {
                playerList[playerReciveDamage].transform.GetComponent<PlayerController>().OnReciveDamage();
            }
        }
    }

    public void ActivePlayerControllers()
    {
        foreach (int index in playerList.Keys)
        {
            if (playerList[index].TryGetComponent(out PlayerController pc))
            {
                pc.enabled = true;
            }
        }
    }

    public void EndMatch()
    {
        timer.text = "";
        RemoveAllPlayers();
    }
}
