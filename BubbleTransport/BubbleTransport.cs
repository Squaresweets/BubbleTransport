using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class BubbleTransport : Mirror.Transport
{
    public static BubbleTransport instance;

    [DllImport("__Internal")]
    private static extern void _InitGameCenter();

    [DllImport("__Internal")]
    private static extern void _FindMatch();

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
    private static extern void RegisterOnClientDisconnectedCallback(OnClientDisconnectedDelegate onClientDisconnected);

    [DllImport("__Internal")]
    private static extern void RegisterOnClientStartCallback(OnClientStartDelegate onClientStart);
    
    [Serializable]
    /// <summary>
    /// Function definition for a button click event.
    /// </summary>
    public class MatchFoundEvent : UnityEvent { }

    // Event delegates triggered on click.
    [SerializeField]
    private MatchFoundEvent matchFound = new MatchFoundEvent();

    [HideInInspector]
    public Mirror.NetworkManager networkManager;

    bool available = true;

    #region Other
    public override bool Available()
    {
        return Application.platform == RuntimePlatform.IPhonePlayer && available;
    }

    //~~~~~~~~~~ These two functions are all sorted out by game center, and these should not be called by anything other than the transport, if you want to start a game use FindMatch(); ~~~~~~~~~~

    public override void ServerStart() { }
    public override void ClientConnect(string address) { }

    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

    public override int GetMaxPacketSize(int channelId = 0) => 16384;

    public override void Shutdown()
    {
        _Shutdown();
    }
    #endregion

    #region Client
    public override bool ClientConnected()
    {
        throw new NotImplementedException();
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
        print("Client disconnected callback");
        instance.OnClientDisconnected.Invoke();
    }

    delegate void OnClientDidDataRecievedDelegate(IntPtr data, int offset, int count);
    [AOT.MonoPInvokeCallback(typeof(OnClientDidDataRecievedDelegate))]
    static void OnClientDidDataRecieved(IntPtr data, int offset, int count)
    {
        byte[] _data = new byte[count];
        Marshal.Copy(data, _data, 0, count);
        instance.OnClientDataReceived.Invoke(new ArraySegment<byte>(_data, offset, count), 0);
    }

    delegate void OnClientStartDelegate();
    [AOT.MonoPInvokeCallback(typeof(OnClientStartDelegate))]
    static void OnClientStartCallback()
    {
        instance.matchFound.Invoke();
        instance.networkManager.StartClient();
        instance.OnClientConnected.Invoke();
    }
    #endregion

    #region Server
    public override bool ServerActive()
    {
        throw new NotImplementedException();
    }

    public override bool ServerDisconnect(int connectionId)
    {
        //Game center matchmaking does not support kicking a client
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
        instance.networkManager.StartHost();
    }

    public override void ServerStop()
    {
        _Shutdown();
    }

    #endregion

    /// <summary>
    /// <para/>This is one of the main functions you should worry about, you should call it when you want to open the matchmaking UI
    /// <para/>Do not try to start a match by using ServerStart() or ClientConnect(), due to the nature of the randomness in matchmaking this will not work
    /// <para/>Instead when gamecenter finds a match it will add all of the clients and call those functions when needed
    /// </summary>
    public void FindMatch()
    {
        _FindMatch();
    }

    private void Awake()
    {
        try{
            networkManager = GetComponent<Mirror.NetworkManager>();
        }
        catch{
            Debug.LogError("Nework Manager could not be found");
        }

        if (instance == null)
            instance = this;

        try{
            //Register all delegates
            RegisterClientDataRecieveCallback(OnClientDidDataRecieved);
            RegisterServerDataRecieveCallback(OnServerDidDataRecieved);
            RegisterOnServerConnectedCallback(OnServerConnectedCallback);
            RegisterOnServerStartCallback(OnServerStartCallback);
            RegisterOnClientStartCallback(OnClientStartCallback);
            RegisterOnServerDisconnectedCallback(ServerDisconnectedCallback);

            _InitGameCenter();
        }
        catch{
            available = false;
        }
    }
}