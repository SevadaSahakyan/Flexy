﻿using GameDevWare.Serialization;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

class Message {
    public string command;
    public object data;
}

public class GameController : MonoBehaviour
{
    public Slider _HealthBar;
    public GameObject _Explosion;

    public float dP = 0.1f;
    public Joystick _MovementJoystick, _ProjectileJoystick;

    private Vector2 lastProjectileJoystickPos = Vector2.zero;

    void Start()
    {
        JoinOrCreateRoom();
    }

    void Update()
    {
        // Do nothing if we don't connected
        if(GameState._Room == null)
            return;

        // Fetch user info and display
        _HealthBar.value = ((User)GetGameEntity(GameState._Room.SessionId)._Entity).health;

        if(_HealthBar.value > 0)
        {
            MovementMessage data = new MovementMessage();

            if (IsMovementHandle())
            {
                data.stateNum = GameState.CurrentCommand++;
                Debug.Log(GameState._Room.SessionId);
                Vector3 playerPos = GetGameEntity(GameState._Room.SessionId).obj.transform.position;

                data.x = playerPos.x + GameState.HeroVelocity.x;
                data.y = playerPos.y + GameState.HeroVelocity.y;
                data.z = playerPos.z + GameState.HeroVelocity.z;

                SpeculativeMovement(GameState.HeroVelocity);

                Message msg = new Message();
                msg.command = "movement";
                msg.data = data;
                GameState._Room.Send(msg);
            }

            if (IsProjectileThrown())
            {
                CreateProjectile(Mathf.Atan2(lastProjectileJoystickPos.y, lastProjectileJoystickPos.x));
            }
        }
        
        lastProjectileJoystickPos = new Vector2(_ProjectileJoystick.Horizontal, _ProjectileJoystick.Vertical);
        
        // Move all entities toward their server position
        foreach(KeyValuePair<string, GameEntity> entry in GameState.Entities)
        {
            if(entry.Value.obj != null && entry.Value._Entity is User && ((User)entry.Value._Entity).health <= 0)
            {
                Instantiate(_Explosion, entry.Value.obj.transform.position, Quaternion.identity);
                Destroy(entry.Value.obj);
            }

            if(entry.Value.obj != null && entry.Key != GameState._Room.SessionId)
            {
                Vector3 serverPos = new Vector3(entry.Value._Entity.x, entry.Value._Entity.y, entry.Value._Entity.z);
                
                if(serverPos != entry.Value.obj.transform.position)
                {
                    Vector3 interpolatedPos = Vector3.Lerp(entry.Value.obj.transform.position, serverPos, 0.1f);
                    entry.Value.obj.transform.position = interpolatedPos;
                }
            }
        }
    }

    GameEntity GetGameEntity(string key)
    {
        GameEntity entity = null;
        GameState.Entities.TryGetValue(key, out entity);

        return entity;
    }

    void SpeculativeMovement(Vector3 dP)
    {
        GetGameEntity(GameState._Room.SessionId).obj.transform.Translate(dP);
    }

    bool IsMovementHandle()
    {
        bool isMoved = false;

        GameState.HeroVelocity.Set(0, 0, 0);

        if (Input.GetKey(KeyCode.D) || _MovementJoystick.Horizontal > 0)
        {
            GameState.HeroVelocity.x = dP;
            isMoved = true;
        }

        if (Input.GetKey(KeyCode.A) || _MovementJoystick.Horizontal < 0)
        {
            GameState.HeroVelocity.x = -dP;
            isMoved = true;
        }

        if (Input.GetKey(KeyCode.W) || _MovementJoystick.Vertical > 0)
        {
            GameState.HeroVelocity.z = dP;
            isMoved = true;
        }

        if (Input.GetKey(KeyCode.S) || _MovementJoystick.Vertical < 0)
        {
            GameState.HeroVelocity.z = -dP;
            isMoved = true;
        }

        GameState.HeroVelocity = Vector3.ClampMagnitude(GameState.HeroVelocity, dP);

        return isMoved;
    }

    bool IsProjectileThrown()
    {
        if(lastProjectileJoystickPos != Vector2.zero)
        {
            if(_ProjectileJoystick.Horizontal * _ProjectileJoystick.Vertical == 0)
            {
                return true;
            }
        }

        return false;
    }

    void CreateProjectile(float angle)
    {
        Message message = new Message();
        message.command = "create_projectile";
        GameEntity p = GetGameEntity(GameState._Room.SessionId);
        Projectile projectile = new Projectile();
        projectile.x = p._Entity.x;
        projectile.y = p._Entity.y;
        projectile.z = p._Entity.z;
        projectile.angle = angle;

        message.data = projectile;

        GameState._Room.Send(message);
    }

    public async void JoinOrCreateRoom()
    {
        GameState._Room = await GameState._Client.JoinOrCreate<State>(GameState.CurrentRoomName, new Dictionary<string, object>() { });

        Debug.Log($"sessionId: {GameState._Room.SessionId}");

        GameState._Room.State.entities.OnAdd += OnEntityAdd;
        GameState._Room.State.entities.OnRemove += OnEntityRemove;
        GameState._Room.State.entities.OnChange += OnEntityMove;

        PlayerPrefs.SetString("roomId", GameState._Room.Id);
        PlayerPrefs.SetString("sessionId", GameState._Room.SessionId);
        PlayerPrefs.Save();

        GameState._Room.OnLeave += (code) => 
        {
            Debug.Log("ROOM: ON LEAVE");
            SceneManager.LoadSceneAsync((int)GameState.Scenes.MainMenu);
        };

        GameState._Room.OnError += (message) => Debug.LogError(message);
        //room.OnStateChange += OnStateChangeHandler;
        GameState._Room.OnMessage += OnMessage;
    }
    
    void OnMessage(object msg)
    {
        if (msg is Message)
        {
            var message = (Message)msg;
            Debug.Log("Received schema-encoded message:");
            Debug.Log("message.command => " + message.command + ", message.data => " + message.data);

            switch(message.command)
            {
                case "movement":
                    MovementMessage data = (MovementMessage)message.data;
                    Vector3 playerPos = GetGameEntity(GameState._Room.SessionId).obj.transform.position;
                    if (data.x == playerPos.x &&
                        data.y == playerPos.y &&
                        data.z == playerPos.z &&
                        data.stateNum > GameState.CurrentCommand)
                    {
                        GameState.CurrentCommand = data.stateNum;
                    }

                    break;
            }
        }
        else
        {
            // msgpack-encoded message
            var message = (IndexedDictionary<string, object>)msg;
            Debug.Log("Received msgpack-encoded message:");
            Debug.Log(message["hello"]);
        }
    }

    void OnEntityAdd(Entity entity, string key)
    {
        GameEntity gameEntity = new GameEntity();
        
        if(entity is User)
        {
            User user = (User)entity;
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

            Debug.Log("Player add! x => " + user.x + ", y => " + user.y);

            cube.transform.position = new Vector3(user.x, user.y, user.z);

            gameEntity._Entity = user;
            gameEntity.obj = cube;
        }
        else if(entity is Projectile)
        {
            Projectile proj = (Projectile)entity;
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            sphere.transform.position = new Vector3(proj.x, proj.y, proj.z);
            
            gameEntity._Entity = proj;
            gameEntity.obj = sphere;
        }
        
        Debug.Log($"Added entity with ID: {key}");
        GameState.Entities.Add(key, gameEntity);
    }

    void OnEntityRemove(Entity entity, string key)
    {
        Debug.Log($"Deleting {key}");

        GameEntity gameEntity = GetGameEntity(key);

        if(gameEntity.obj != null)
        {
            Destroy(gameEntity.obj);
        }

        GameState.Entities.Remove(key);
    }

    void OnEntityMove(Entity entity, string key)
    {
        if(entity is User)
        {
            User user = (User)entity;
            GameEntity player = GetGameEntity(key);

            // If speculation for this client was valid then we do nothing :3
            if(key != GameState._Room.SessionId || !IsPreviousSpeculativeMovementValid(user.stateNum))
            {
                ((User)player._Entity).stateNum = user.stateNum;
                ((User)player._Entity).x = user.x;
                ((User)player._Entity).y = user.y;
                ((User)player._Entity).z = user.z;
            }
        }
        else if(entity is Projectile)
        {
            Projectile proj = (Projectile)entity;
            GameEntity gameEntity = GetGameEntity(key);

            gameEntity._Entity.x = proj.x;
            gameEntity._Entity.y = proj.y;
            gameEntity._Entity.z = proj.z;
        }
    }

    bool IsPreviousSpeculativeMovementValid(uint commandNum)
    {
        return GameState.CurrentCommand >= commandNum;
    }
}
