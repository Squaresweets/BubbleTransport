<img src="https://img.shields.io/discord/672474661388288021" /></a>
<img src="https://img.shields.io/twitter/follow/SqSweetsGames?style=social" /></a>
<img src="https://forthebadge.com/images/badges/gluten-free.svg" height=20/></a>

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
