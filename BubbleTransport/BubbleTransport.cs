using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

using IngameDebugConsole;

public class BubbleTransport : Mirror.Transport
{
    [DllImport("__Internal")]
    private static extern void _InitGameCenter();

    /// <summary>
    /// This is one of the main functions you should worry about, you should call it when you want to open the matchmaking UI
    /// Do not try to start a match by using ServerStart() or ClientConnect(), due to the nature of the randomness in matchmaking this will not work
    /// Instead when gamecenter finds a match it will add all of the clients and call those functions when needed
    /// </summary>
    [DllImport("__Internal")]
    private static extern void FindMatch();

    [DllImport("__Internal")]
    private static extern void _SendMessageToAll(Byte[] data, int offset, int count);

    [DllImport("__Internal")]
    private static extern void RegisterClientDataRecieveCallback(OnClientDidDataRecievedDelegate OnClientDidDataRecieved);

    [DllImport("__Internal")]
    private static extern void RegisterServerDataRecieveCallback(OnServerDidDataRecievedDelegate OnServerDidDataRecieved);
    /*
    TODO:
    - activeTransport
        -The current transport used by Mirror
    - Available
        -Easy stuff, tho it may take a callback to get whether gamecenter is supported

    -GetMaxBackSize/GetMaxPacketSize
        -Speek for themselves

    ~~~~~ C A L L B A C K S ~~~~~

    -OnClientConnected
        -For clients when they conenct to the server, called when you start the game as a client
    -OnClientDataReceived
        -Self explanatory, shouldn't be hard to implement
    -OnClientDisconnected
        -Should be possible with https://developer.apple.com/documentation/gamekit/gkmatchdelegate, or with [self.delegate matchEnded]
    
    -OnServerConnected
        -This may be hard, at the start I will have to call OnServerConnected multiple times depending on the number of people connected
    -OnServerDataReceived
        -Not too hard once again
    -OnServerDisconnected
        -For when a client disconnects

    -OnClientError / OnServerError
        -Should also be possible with https://developer.apple.com/documentation/gamekit/gkmatchdelegate, though it may be hard to convert over the error, IDK

        
    ~~~~~ M E T H O D S ~~~~~

    -ClientConnect/ServerStart
        -These must call the same thing that just opens the matchmaking menu
    -ClientConnected
        -Just says if you are connected
    -ClientDisconnect
        -Disconnects the client
    -ClientSend
        -SEND THE DATA TO THE SERVER WOOOOOOOO
    
    -ServerActive
        -Just says if the server is active
    -ServerDisconnect
        -Used to kick out a client
    -ServerGetClientAddress
        -D U N N O  W H A T  T O  D O  F O R  T H I S  O N E ! ! ! 
    -ServerSend
        -SEND THE DATA TO THE SPECIFIC CLIENT WOOOOOOOO
    -ServerStart/ServerStop
        -Speak for themselves
    -ServerUri
        -D U N N O  W H A T  T O  D O  F O R  T H I S  O N E ! ! ! 
    */

    #region Transport
    public override bool Available()
    {
        return Application.platform == RuntimePlatform.IPhonePlayer;
    }

    //~~~~~~~~~~ These two functions are all sorted out by game center, and these should not be called by anything other than the transport, if you want to start a game use FindMatch(); ~~~~~~~~~~

    public override void ServerStart() { }
    public override void ClientConnect(string address) { }

    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

    public override bool ClientConnected()
    {
        throw new NotImplementedException();
    }

    public override void ClientDisconnect()
    {
        throw new NotImplementedException();
    }

    public override void ClientSend(int channelId, ArraySegment<byte> segment)
    {
        if(channelId != 0) { Debug.LogError("only channel 0 is supported"); return; }
        Byte[] data = segment.Array;
        _SendMessageToAll(data, segment.Offset, segment.Count);
    }
    
    public override int GetMaxPacketSize(int channelId = 0) => 16384;

    public override bool ServerActive()
    {
        return true;
        //throw new NotImplementedException();
    }

    public override bool ServerDisconnect(int connectionId)
    {
        throw new NotImplementedException();
    }

    public override string ServerGetClientAddress(int connectionId)
    {
        throw new NotImplementedException();
    }

    public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
    {
        throw new NotImplementedException();
    }

    public override void ServerStop()
    {
    
    }

    public override Uri ServerUri()
    {
        throw new NotImplementedException();
    }

    public override void Shutdown()
    {
        throw new NotImplementedException();
    }
    #endregion
        
    delegate void OnClientDidDataRecievedDelegate(IntPtr data, int offset, int count);
    [AOT.MonoPInvokeCallback(typeof(OnClientDidDataRecievedDelegate))]
    private void OnClientDidDataRecieved(IntPtr data, int offset, int count)
    {
        byte[] _data = new byte[count];
        Marshal.Copy(data, _data, 0, count);
        OnClientDataReceived(new ArraySegment<byte>(_data, offset, count), 0);

        //Test
        print(System.Text.Encoding.ASCII.GetString(_data));
        print("offset: " + offset);
    }

    delegate void OnServerDidDataRecievedDelegate(int connId, Byte[] data, int offset, int count);
    [AOT.MonoPInvokeCallback(typeof(OnServerDidDataRecievedDelegate))]
    static void OnServerDidDataRecieved(int connId, Byte[] data, int offset, int count)
    {
        print("from: " + connId + " " + System.Text.Encoding.ASCII.GetString(data));
    }

    /// <summary>
    /// Callback that adds all the clients to the server when connected.
    /// </summary>
    delegate void OnServerConnectedDelegate(int connId);
    [AOT.MonoPInvokeCallback(typeof(OnServerConnectedDelegate))]
    public void OnServerConnectedCallback(int connId)
    {
        print(connId);
        //OnServerConnected(connId);
    }

    //Test
    void SendMessageToAll(string message)
    {
        OnServerConnected.Invoke(int.Parse(message));
        //Byte[] data = System.Text.Encoding.ASCII.GetBytes(message);
        //_SendMessageToAll(data, 65, data.Length);
    }

    public void FindMatch()
    {
        _FindMatch();
    }

    private void Awake()
    {
        DebugLogConsole.AddCommand<string>("msg", "Sends the message", SendMessageToAll);
        RegisterClientDataRecieveCallback(OnClientDidDataRecieved);
        
        //OnServerConnected = (connectionId) => OnServerConnected.Invoke(connectionId);
        _InitGameCenter();
    }
}
