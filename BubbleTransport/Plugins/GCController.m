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
typedef void (*OnServerDidDataRecievedDelegate)(int connId, Byte data[], uint32_t offset, uint32_t count);
OnServerDidDataRecievedDelegate ServerRecievedData = NULL;
typedef void (*OnServerConnectedDelegate)(int connId);
OnServerConnectedDelegate ServerConnected = NULL;
typedef void (*OnServerStartDelegate)();
OnServerStartDelegate ServerStart = NULL;

extern GKPlayer* serverPlayer;
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
+(void)sendDataToPlayer:(NSData *)data toPlayer:(GKPlayer *)playerID
{
    NSError *error;
    
    BOOL success = [[GCHelper sharedInstance].match sendData:data toPlayers:[NSArray arrayWithObject:playerID] dataMode:GKMatchSendDataReliable error:&error];
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

void(^HostingPlayerChosen)(GKPlayer *) = ^(GKPlayer *player)
{
    [[GCHelper sharedInstance].presentingViewController dismissViewControllerAnimated:YES completion:nil];
    if(player == GKLocalPlayer.localPlayer)
    {
        NSLog(@"SERVER WOOO");
        ServerStart();
        for (GKPlayer *_player in [GCHelper sharedInstance].playersDict) {
            if(_player != player)
                ServerConnected(_player.playerID);
        }
    }
    else
    {
        NSLog(@"CLIENT WOOO");
        serverPlayer = player;
    }
};
#pragma mark GCHelperDelegate
- (void)matchStarted{
    [[GCHelper sharedInstance].match chooseBestHostingPlayerWithCompletionHandler:HostingPlayerChosen];
    
    NSLog(@"Match Started");
}

- (void)matchEnded {    
    NSLog(@"Match ended");
}

- (void)match:(GKMatch *)match didReceiveData:(NSData *)data fromRemotePlayer:(GKPlayer *)playerID {
    NSUInteger len = [data length];
    
    Byte offset;
    [data getBytes:&offset length:1];
    
    Byte byteData[len-1];
    [data getBytes:byteData range:NSMakeRange(1,len-1)];
    intptr_t i = byteData;
    ClientRecievedData(i, (int)offset, (int)len-1);
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
    typedef void (*OnServerDidDataRecievedDelegate)(int connId, Byte data[], uint32_t offset, uint32_t count);
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
