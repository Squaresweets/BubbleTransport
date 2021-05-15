#import <Foundation/Foundation.h>
#import <GameKit/GameKit.h>
#import "GCHelper.h"
#import <ifaddrs.h>
#import <arpa/inet.h>

extern UIViewController *UnityGetGLViewController();

//@interface GCController : NSObject

//@end

typedef void (*OnClientDidDataRecievedDelegate)(intptr_t data, uint32_t offset, uint32_t count);
OnClientDidDataRecievedDelegate ClientRecievedData = NULL;
typedef void (*OnServerDidDataRecievedDelegate)(int connId, intptr_t data, uint32_t offset, uint32_t count);
OnServerDidDataRecievedDelegate ServerRecievedData = NULL;
typedef void (*OnServerConnectedDelegate)(int connId);
OnServerConnectedDelegate ServerConnected = NULL;
typedef void (*OnServerStartDelegate)();
OnServerStartDelegate ServerStart = NULL;
typedef void (*OnClientStartDelegate)();
OnClientStartDelegate ClientStart = NULL;

static GKPlayer* serverPlayer = NULL;
static BOOL* isServer = NULL;
@implementation GCController

+ (void)InitGameCenter
{
    [[GCHelper sharedInstance] authenticateLocalUser:[GCController myClass]];
}
+ (void) findMatch
{
    UIViewController* viewcontroller = (UIViewController*) [UIApplication sharedApplication].delegate;
    [[GCHelper sharedInstance] findMatchWithMinPlayers:2 maxPlayers:4
    viewController:viewcontroller];
}


+(void)sendDataToServer:(NSData *)data
{
    NSError *error;
    BOOL success = [[GCHelper sharedInstance].match sendData:data toPlayers:[NSArray arrayWithObject:serverPlayer] dataMode:GKMatchSendDataReliable error:&error];
    if(!success)
    {
       NSLog(@"Error sending message");
    }
}
+(void)sendDataToPlayer:(NSData *)data toPlayer:(int)playerID
{
    NSError *error;
    BOOL success = [[GCHelper sharedInstance].match sendData:data toPlayers:[NSArray arrayWithObject:[GCHelper sharedInstance].players[playerID]]dataMode:GKMatchSendDataReliable error:&error];
    if(!success)
    {
        NSLog(@"Error sending message");
    }
}

+(id)myClass
{
    return [[self alloc] init];
}
char* convertNSStringToCString(const NSString* nsString)
{
    if(nsString == NULL)
        return NULL;
    
    const char* nsStringUtf8 = [nsString UTF8String];
    char* cString = (char*)malloc(strlen(nsStringUtf8) + 1);
    strcpy(cString, nsStringUtf8);
    
    return cString;
}

#pragma mark GCHelperDelegate
- (void)matchStarted{
    
    [[GCHelper sharedInstance].presentingViewController dismissViewControllerAnimated:YES completion:nil];
    //if([GKLocalPlayer.localPlayer.alias  isEqual: @"Squaresweets"])
    if([GCHelper sharedInstance].players[0] == GKLocalPlayer.localPlayer)
    {
        NSLog(@"SERVER WOOO");
        isServer = YES;
        ServerStart();
        
        NSLog(@"Num players: %lu", (unsigned long)[GCHelper sharedInstance].players.count);
        for(int i = 1; i < [GCHelper sharedInstance].players.count; i++)
            ServerConnected(i);
    }
    else
    {
        NSLog(@"CLIENT WOOO");
        isServer = NO;
        serverPlayer = [GCHelper sharedInstance].players[0];
        ClientStart();
    }
    
    
    NSLog(@"Match Started");
}

- (void)matchEnded {    
    NSLog(@"Match ended");
}

- (void)match:(GKMatch *)match didReceiveData:(NSData *)data fromRemotePlayer:(GKPlayer *)player {
    NSUInteger len = [data length];
    
    Byte offset;
    [data getBytes:&offset length:1];
    
    Byte byteData[len-1];
    [data getBytes:byteData range:NSMakeRange(1,len-1)];
    intptr_t i = byteData;
    NSLog(@"Player name: %@", player.alias);
    if(isServer)
    {
        int connID = -1;
        for(int j = 1; j < [GCHelper sharedInstance].players.count; j++)
        {
            if([player.playerID isEqual: ((GKPlayer *)[GCHelper sharedInstance].players[j]).playerID])
                connID = j;
        }
        if(connID == -1)
            NSLog(@"ERROR, data returned cannot be linked to a player");
        
        ServerRecievedData(connID, i, (int)offset, (int)len-1);
    }
    else
    {
            ClientRecievedData(i, (int)offset, (int)len-1);
    }
}

@end


#ifdef  _cplusplus
extern "C"
{
#endif
    void _InitGameCenter()
    {
        [GCController InitGameCenter];
    }
    void _FindMatch()
    {
        [GCController findMatch];
    }
    void SendMessageToServer(Byte data[], int offset, int count)
    {
        Byte dataoffset[(NSUInteger)count+1];
        dataoffset[0] = (Byte)offset;
        memcpy(dataoffset+1, data, count);
        NSData *dataToSend = [NSData dataWithBytes:dataoffset length:count+1];
        
        [GCController sendDataToServer:dataToSend];
    }
    void SendMessageToClient(int clientId, Byte data[], int offset, int count)
    {
        Byte dataoffset[(NSUInteger)count+1];
        dataoffset[0] = (Byte)offset;
        memcpy(dataoffset+1, data, count);
        NSData *dataToSend = [NSData dataWithBytes:dataoffset length:count+1];
        
        [GCController sendDataToPlayer:dataToSend toPlayer:clientId];
    }
    typedef void (*OnClientDidDataRecievedDelegate)(intptr_t data, uint32_t offset, uint32_t count);
    void RegisterClientDataRecieveCallback(OnClientDidDataRecievedDelegate callback)
    {
        if(ClientRecievedData == NULL)
        {
            ClientRecievedData = callback;
        }
    }
    typedef void (*OnServerConnectedDelegate)(int connId);
    void RegisterOnServerConnectedCallback(OnServerConnectedDelegate callback)
    {
        if(ServerConnected == NULL)
        {
            ServerConnected = callback;
        }
    }
    typedef void (*OnServerStartDelegate)();
    void RegisterOnServerStartCallback(OnServerStartDelegate callback)
    {
        if(ServerStart == NULL)
        {
            ServerStart = callback;
        }
    }
    typedef void (*OnClientStartDelegate)();
    void RegisterOnClientStartCallback(OnServerStartDelegate callback)
    {
        if(ClientStart == NULL)
        {
            ClientStart = callback;
        }
    }
    typedef void (*OnServerDidDataRecievedDelegate)(int connId, intptr_t data, uint32_t offset, uint32_t count);
    void RegisterServerDataRecieveCallback(OnServerDidDataRecievedDelegate callback)
    {
        if(ServerRecievedData == NULL)
        {
            ServerRecievedData = callback;
        }
    }
#ifdef  _cplusplus
}
#endif
