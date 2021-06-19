using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using UnityEngine.Events;
#if UNITY_IOS
using UnityEngine.iOS;
#endif
using UnityEngine.SceneManagement;
using Mirror;

[DisallowMultipleComponent]
public class BubbleTransport : Transport
{
    public static BubbleTransport instance;

    #region DllImports
    [DllImport("__Internal")]
    private static extern void _InitGameCenter();

    [DllImport("__Internal")]
    private static extern void _FindMatch(int minPlayers, int maxPlayers);

    [DllImport("__Internal")]
    private static extern void _Shutdown();

    [DllImport("__Internal")]
    private static extern void SendMessageToServer(Byte[] data, int offset, int count, int channel);

    [DllImport("__Internal")]
    private static extern void SendMessageToClient(int clientId, Byte[] data, int offset, int count, int channel);

    [DllImport("__Internal")]
    private static extern void RegisterClientDataRecieveCallback(OnClientDidDataRecievedDelegate OnClientDidDataRecieved);

    [DllImport("__Internal")]
    private static extern void RegisterServerDataRecieveCallback(OnServerDidDataRecievedDelegate OnServerDidDataRecieved);

    [DllImport("__Internal")]
    private static extern void RegisterOnServerConnectedCallback(OnServerConnectedDelegate onServerConnected);

    [DllImport("__Internal")]
    private static extern void RegisterOnServerStartCallback(OnServerStartDelegate onServerStart);

    [DllImport("__Internal")]
    private static extern void RegisterOnServerDisconnectedCallback(OnServerDisconnectedDelegate onServerDisconnected);

    [DllImport("__Internal")]
    private static extern void RegisterServerStopCallback(ServerStopDelegate serverStop);

    [DllImport("__Internal")]
    private static extern void RegisterOnClientDisconnectedCallback(OnClientDisconnectedDelegate onClientDisconnected);

    [DllImport("__Internal")]
    private static extern void RegisterOnClientStartCallback(OnClientStartDelegate onClientStart);

    [DllImport("__Internal")]
    private static extern void RegisterInviteRecieveCallback(OnInviteRecievedDelegate InviteRecieved);
#endregion

    public int MinPlayers = 2;
    [Tooltip("Incase you are in a scene such as a menu when an invite is recieved, set this to the scene where matches normally start")]
    [Mirror.Scene]
    public string InviteRecievedScene;

    [Serializable]
    public class MatchFoundEvent : UnityEvent { }
    [SerializeField]
    private MatchFoundEvent matchFound = new MatchFoundEvent();

    [Serializable]
    public class InviteRecievedEvent : UnityEvent { }
    [SerializeField]
    private MatchFoundEvent inviteRecieved = new MatchFoundEvent();

    static List<ArraySegment<Byte>> clientMessageBuffer = new List<ArraySegment<byte>>();
    struct ServerMessage
    {
        public ArraySegment<Byte> message;
        public int connId;

        public ServerMessage(ArraySegment<byte> message, int connId) : this()
        {
            this.message = message;
            this.connId = connId;
        }
    }
    static List<ServerMessage> serverMessageBuffer = new List<ServerMessage>();


    bool available = true;

    bool connected = false;

    bool needToDisconnectFlag = false;

    /* Structure of the transport
    
    Without an Invite:
    - All players call the FindMatch() function
    - Game Center searches for and finds a match
    - When a match is found, which device should act as the host and the connID of all the clients is calculated (as seen below)
    - Server:
        - ServerStart() is called letting Unity know we are acting as a server
        - For every player in the list of players ServerConnected() is called, this way the connID of each player is their position in the players array
    - Client:
        - The serverPlayer variable is set
        - The client waits for 2, 3 or 4 seconds depending on its position in the players array
            - This is to make players join in the right order and prevent clients from joining too early
        - ClientStart() is called letting Unity know we are acting as a client
    
    With an Invite (https://www.youtube.com/watch?v=e-RCPvUYxr4 [Example has 3 players, four is also possible I just didn't have that many devices]):
    - One player invites all other players
    - On all other players OnInviteRecieved() is called, this shuts down the network if it is currently on one and calls FindMatch()
    - FindMatch() instantiates the match from an invite
    - Match continues as normal (see above)

    Determining the Host:
    - This is the section that probably took me the most time to figure out
    - All players are added to a player array when they join
    - All GKPlayers have a playerID
    - The player array is sorted on this playerID, so the array is in the same order on all devices
    - The first player in the array acts as the server, so a game with the same people will always have the same server

    */



    /// <summary>
    /// <para/>This is one of the main functions you should worry about, you should call it when you want to open the matchmaking UI
    /// <para/>Do not try to start a match by using ServerStart() or ClientConnect(), due to the nature of the randomness in matchmaking this will not work
    /// <para/>Instead when gamecenter finds a match it will add all of the clients and call those functions when needed
    /// </summary>
    public void FindMatch()
    {
        if (Mirror.NetworkManager.singleton.maxConnections > 4)
        {
            Debug.LogError("Real time Game Center Matches support a max of 4 players");
            return;
        }
        _FindMatch(MinPlayers, NetworkManager.singleton.maxConnections);
    }


#region Misc
    public override bool Available()
    {
#if UNITY_IOS
        return Application.platform == RuntimePlatform.IPhonePlayer && available && new System.Version(Device.systemVersion) >= new System.Version("13.0");
#else
        return false;
#endif
    }

    //~~~~~~~~~~ These two functions are all sorted out by game center, and these should not be called by anything other than the transport, if you want to start a game use FindMatch(); ~~~~~~~~~~

    public override void ServerStart() { }
    public override void ClientConnect(string address) { }

    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

    //Sizes from: https://developer.apple.com/library/archive/documentation/NetworkingInternet/Conceptual/GameKit_Guide/Matchmaking/Matchmaking.html
    public override int GetMaxPacketSize(int channelId = 0) { return channelId == 0 ? 89088 : 1000;  }

    public override void Shutdown()
    {
        if(Available())
        {
            _Shutdown();
            connected = false;
        }
    }
    delegate void OnInviteRecievedDelegate();
    [AOT.MonoPInvokeCallback(typeof(OnInviteRecievedDelegate))]
    static void OnInviteRecieved()
    {
        //An invite has been recieved, we shutdown the network and then call FindMatch
        if (NetworkManager.singleton.isNetworkActive)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (NetworkServer.active)
                NetworkManager.singleton.StopHost();
            else
                NetworkManager.singleton.StopClient();
            return;
        }
        else if(instance.InviteRecievedScene != null && instance.InviteRecievedScene != SceneManager.GetActiveScene().path)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadScene(instance.InviteRecievedScene);
            return;
        }
        instance.inviteRecieved?.Invoke();

        //Numbers do not matter, it instantiates from an invite
        _FindMatch(0, 0);
    }
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        OnInviteRecieved();
    }
#endregion

#region Client
    public override bool ClientConnected()
    {
        return connected && !Mirror.NetworkServer.active;
    }

    public override void ClientDisconnect()
    {
        instance.connected = false;
        _Shutdown();
    }

    public override void ClientSend(int channelId, ArraySegment<byte> segment)
    {
        if (!connected || segment.Count > GetMaxPacketSize(channelId)) return;
        if (channelId > 1) { Debug.LogError("Only channels 0 and 1 are supported"); return; }
        SendMessageToServer(segment.Array, segment.Offset, segment.Count, channelId);
    }

    public override Uri ServerUri()
    {
        Debug.LogWarning("Game Center matchmaking does not provide a serverURI");
        return null;
    }

    delegate void OnClientDisconnectedDelegate();
    [AOT.MonoPInvokeCallback(typeof(OnClientDisconnectedDelegate))]
    static void ClientDisconnectedCallback()
    {
        instance.needToDisconnectFlag = true;
    }

    delegate void OnClientDidDataRecievedDelegate(IntPtr data, int offset, int count);
    [AOT.MonoPInvokeCallback(typeof(OnClientDidDataRecievedDelegate))]
    static void OnClientDidDataRecieved(IntPtr data, int offset, int count)
    {
        if (!instance.connected)
            return;
        /*
        We get a pointer back from the plugin containing the location of the array of bytes
        Data recieved is formatted like this:

        -----------------------------------------------------
        |        |                                          |
        | offset |     Byte Array containing all data...    |
        | 1 Byte |     Rest of the array                    |
        |        |                                          |
        -----------------------------------------------------

        Objective C code splits this up into offset and count and sends it here
        */
        byte[] _data = new byte[count];
        Marshal.Copy(data, _data, 0, count);

        if (!instance.enabled)
        {
            clientMessageBuffer.Add(new ArraySegment<byte>(_data, offset, count));
            return;
        }

        instance.OnClientDataReceived?.Invoke(new ArraySegment<byte>(_data, offset, count), 0);
    }

    delegate void OnClientStartDelegate();
    [AOT.MonoPInvokeCallback(typeof(OnClientStartDelegate))]
    static void OnClientStartCallback()
    {
        instance.connected = true;
        activeTransport = instance;
        instance.matchFound.Invoke();
        NetworkManager.singleton.StartClient();
        instance.OnClientConnected?.Invoke();
    }
#endregion

#region Server
    public override bool ServerActive()
    {
        return connected && Mirror.NetworkServer.active;
    }

    public override bool ServerDisconnect(int connectionId)
    {
        //Game center matchmaking does not support kicking a client, you may want to create your own functionallity for telling a client to disconnect
        return false;
    }

    public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
    {
        if (!connected || segment.Count > GetMaxPacketSize(channelId)) return;
        if (channelId > 1) { Debug.LogError("Only channels 0 and 1 are supported"); return; }
        SendMessageToClient(connectionId, segment.Array, segment.Offset, segment.Count, channelId);
    }

    public override string ServerGetClientAddress(int connectionId)
    {
        Debug.LogWarning("Game Center matchmaking does not provide a connectionID");
        return null;
    }

    delegate void OnServerDisconnectedDelegate(int connID);
    [AOT.MonoPInvokeCallback(typeof(OnServerDisconnectedDelegate))]
    static void ServerDisconnectedCallback(int connID)
    {
        instance.connected = false;
        print("Server disconnected callback");
        instance.OnServerDisconnected?.Invoke(connID);
    }

    delegate void OnServerDidDataRecievedDelegate(int connId, IntPtr data, int offset, int count);
    [AOT.MonoPInvokeCallback(typeof(OnServerDidDataRecievedDelegate))]
    static void OnServerDidDataRecieved(int connId, IntPtr data, int offset, int count)
    {
        if (!instance.connected)
            return;
        /*
        We get a pointer back from the plugin containing the location of the array of bytes
        Data recieved is formatted like this:

        -----------------------------------------------------
        |        |                                          |
        | offset |     Byte Array containing all data...    |
        | 1 Byte |     Rest of the array                    |
        |        |                                          |
        -----------------------------------------------------

        Objective C code splits this up into offset and count and sends it here
        */
        byte[] _data = new byte[count];
        Marshal.Copy(data, _data, 0, count);

        if (!instance.enabled)
        {
            //Stores messages in a buffer to be executed after the scene change
            serverMessageBuffer.Add(new ServerMessage(new ArraySegment<byte>(_data, offset, count), connId));
            return;
        }

        instance.OnServerDataReceived?.Invoke(connId, new ArraySegment<byte>(_data, offset, count), 0);
    }

    /// <summary>
    /// Callback that adds all the clients to the server when connected.
    /// </summary>
    delegate void OnServerConnectedDelegate(int connId);
    [AOT.MonoPInvokeCallback(typeof(OnServerConnectedDelegate))]
    static void OnServerConnectedCallback(int connId)
    {
        instance.OnServerConnected?.Invoke(connId);
    }

    delegate void OnServerStartDelegate();
    [AOT.MonoPInvokeCallback(typeof(OnServerStartDelegate))]
    static void OnServerStartCallback()
    {
        instance.connected = true;
        activeTransport = instance;
        instance.matchFound?.Invoke();
        Mirror.NetworkManager.singleton.StartHost();
    }

    delegate void ServerStopDelegate();
    [AOT.MonoPInvokeCallback(typeof(ServerStopDelegate))]
    static void ServerStopCallback()
    {
        instance.connected = false;
        Mirror.NetworkManager.singleton.StopHost();
    }

    public override void ServerStop()
    {
        _Shutdown();
    }

    #endregion

    public override void ClientLateUpdate()
    {
        if (instance != this) return;
        if(needToDisconnectFlag)
        {
            OnClientDisconnected?.Invoke();
            needToDisconnectFlag = false;
        }

        //This executes any messages that were not executed during a scene change
        for (int i = 0; i < clientMessageBuffer.Count; i++)
        {
            OnClientDataReceived?.Invoke(clientMessageBuffer[0], 0);
            clientMessageBuffer.RemoveAt(0);
        }
    }
    public override void ServerLateUpdate()
    {
        if (instance != this) return;

        //This executes any messages that were not executed during a scene change
        for (int i = 0; i < serverMessageBuffer.Count; i++)
        {
            OnServerDataReceived?.Invoke(serverMessageBuffer[0].connId, serverMessageBuffer[0].message, 0);
            serverMessageBuffer.RemoveAt(0);
        }
    }
    private void Start()
    {
        //Done on start so we only accept an invite when when the scene has fully finished loading
        if(Available())
            _InitGameCenter();
    }
    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(this.gameObject);

        try
        {
            //Register all delegates
            RegisterClientDataRecieveCallback(OnClientDidDataRecieved);
            RegisterServerDataRecieveCallback(OnServerDidDataRecieved);
            RegisterOnServerConnectedCallback(OnServerConnectedCallback);
            RegisterOnServerStartCallback(OnServerStartCallback);
            RegisterOnClientStartCallback(OnClientStartCallback);
            RegisterOnClientDisconnectedCallback(ClientDisconnectedCallback);
            RegisterOnServerDisconnectedCallback(ServerDisconnectedCallback);
            RegisterServerStopCallback(ServerStopCallback);
            RegisterInviteRecieveCallback(OnInviteRecieved);
        }
        catch
        {
            //If you get an error while registering them than the transport is not available
            available = false;
        }
    }
}