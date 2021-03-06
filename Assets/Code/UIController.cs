﻿using Colyseus;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    public Button /* _FetchRoomsButton, */ _JoinToTheRoomButton;

    public InputField _Endpoint;
        
    void Start()
    {
       _JoinToTheRoomButton?.onClick.AddListener(OnJoinOrCreateRoom);
    }

    public async void ConnectToTheHost()
    {
        GameState._Client = ColyseusManager.Instance.CreateClient($"ws://{_Endpoint.text}:2567");

        Debug.Log($"ID: {SystemInfo.deviceUniqueIdentifier}");

        await GameState._Client.Auth.Login();
    }
    
    public void OnJoinOrCreateRoom()
    {
        if (GameState._Client == null)
        {
            ConnectToTheHost();
        }

        SceneManager.LoadSceneAsync((int)GameState.Scenes.Game);
    }
}
