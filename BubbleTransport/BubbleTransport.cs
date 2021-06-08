using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.iOS;
using System.Runtime.InteropServices;
using UnityEngine.Events;

using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class BubbleTransport : Mirror.Transport
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


    bool available = true;
    
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
        _FindMatch(MinPlayers, Mirror.NetworkManager.singleton.maxConnections);
    }

    #region Misc
    public override bool Available()
    {
        return Application.platform == RuntimePlatform.IPhonePlayer && available && new Version(Device.systemVersion) >= new Version("13.0");
    }

    //~~~~~~~~~~ These two functions are all sorted out by game center, and these should not be called by anything other than the transport, if you want to start a game use FindMatch(); ~~~~~~~~~~

    public override void ServerStart() { }
    public override void ClientConnect(string address) { }

    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

    public override int GetMaxPacketSize(int channelId = 0) => 16384;

    public override void Shutdown()
    {
        if(Available())
            _Shutdown();
    }
    delegate void OnInviteRecievedDelegate();
    [AOT.MonoPInvokeCallback(typeof(OnInviteRecievedDelegate))]
    static void OnInviteRecieved()
    {
        //An invite has been recieved, we shutdown the network and then call FindMatch
        if(Mirror.NetworkManager.singleton.isNetworkActive)
        {
            if (Mirror.NetworkServer.active)
                Mirror.NetworkManager.singleton.StopHost();
            else
                Mirror.NetworkManager.singleton.StopClient();
        }
        else if(instance.InviteRecievedScene != null && instance.InviteRecievedScene != SceneManager.GetActiveScene().path)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadScene(instance.InviteRecievedScene);
            return;
        }
        instance.inviteRecieved.Invoke();

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
        return Mirror.NetworkManager.singleton.isNetworkActive && !Mirror.NetworkServer.active;
    }

    public override void ClientDisconnect()
    {
        _Shutdown();
    }

    public override void ClientSend(int channelId, ArraySegment<byte> segment)
    {
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
        instance.OnClientDisconnected.Invoke();
    }

    delegate void OnClientDidDataRecievedDelegate(IntPtr data, int offset, int count);
    [AOT.MonoPInvokeCallback(typeof(OnClientDidDataRecievedDelegate))]
    static void OnClientDidDataRecieved(IntPtr data, int offset, int count)
    {
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
        instance.OnClientDataReceived.Invoke(new ArraySegment<byte>(_data, offset, count), 0);
    }

    delegate void OnClientStartDelegate();
    [AOT.MonoPInvokeCallback(typeof(OnClientStartDelegate))]
    static void OnClientStartCallback()
    {
        instance.matchFound.Invoke();
        Mirror.NetworkManager.singleton.StartClient();
        instance.OnClientConnected.Invoke();
    }
    #endregion

    #region Server
    public override bool ServerActive()
    {
        return Mirror.NetworkManager.singleton.isNetworkActive && Mirror.NetworkServer.active;
    }

    public override bool ServerDisconnect(int connectionId)
    {
        //Game center matchmaking does not support kicking a client, you may want to create your own functionallity for telling a client to disconnect
        return false;
    }

    public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
    {
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
        print("Server disconnected callback");
        instance.OnServerDisconnected.Invoke(connID);
    }

    delegate void OnServerDidDataRecievedDelegate(int connId, IntPtr data, int offset, int count);
    [AOT.MonoPInvokeCallback(typeof(OnServerDidDataRecievedDelegate))]
    static void OnServerDidDataRecieved(int connId, IntPtr data, int offset, int count)
    {
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
        instance.OnServerDataReceived.Invoke(connId, new ArraySegment<byte>(_data, offset, count), 0);
    }

    /// <summary>
    /// Callback that adds all the clients to the server when connected.
    /// </summary>
    delegate void OnServerConnectedDelegate(int connId);
    [AOT.MonoPInvokeCallback(typeof(OnServerConnectedDelegate))]
    static void OnServerConnectedCallback(int connId)
    {
        instance.OnServerConnected.Invoke(connId);
    }

    delegate void OnServerStartDelegate();
    [AOT.MonoPInvokeCallback(typeof(OnServerStartDelegate))]
    static void OnServerStartCallback()
    {
        instance.matchFound.Invoke();
        Mirror.NetworkManager.singleton.StartHost();
    }

    delegate void ServerStopDelegate();
    [AOT.MonoPInvokeCallback(typeof(ServerStopDelegate))]
    static void ServerStopCallback()
    {
        Mirror.NetworkManager.singleton.StopHost();
    }

    public override void ServerStop()
    {
        _Shutdown();
    }

    #endregion

    private void Awake()
    {
        if (instance == null)
            instance = this;

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

            _InitGameCenter();
        }
        catch
        {
            //If you get an error while registering them than the transport is not available
            available = false;
        }
    }
}