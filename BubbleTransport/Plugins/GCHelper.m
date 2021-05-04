#import <Foundation/Foundation.h>
#import <GameKit/GameKit.h>
#import "GCHelper.h"

//Most code from: https://www.raywenderlich.com/3074-game-center-tutorial-for-ios-how-to-make-a-simple-multiplayer-game-part-1-2

extern UIViewController *UnityGetGLViewController();

@implementation GCHelper

@synthesize gameCenterAvailable;

@synthesize presentingViewController;
@synthesize match;
@synthesize delegate;

@synthesize playersDict;

@synthesize pendingInvite;
@synthesize pendingPlayersToInvite;

#pragma mark Initialization

static GCHelper *sharedHelper = nil;
+ (GCHelper *) sharedInstance {
    if (!sharedHelper) {
        sharedHelper = [[GCHelper alloc] init];
    }
    return sharedHelper;
}

- (BOOL)isGameCenterAvailable {
    // check for presence of GKLocalPlayer API
    Class gcClass = (NSClassFromString(@"GKLocalPlayer"));
	
    // check if the device is running iOS 4.1 or later
    NSString *reqSysVer = @"4.1";
    NSString *currSysVer = [[UIDevice currentDevice] systemVersion];
    BOOL osVersionSupported = ([currSysVer compare:reqSysVer 
        options:NSNumericSearch] != NSOrderedAscending);
	
    return (gcClass && osVersionSupported);
}

- (id)init {
    if ((self = [super init])) {
        gameCenterAvailable = [self isGameCenterAvailable];
        if (gameCenterAvailable) {
            NSNotificationCenter *nc = 
            [NSNotificationCenter defaultCenter];
            [nc addObserver:self 
                   selector:@selector(authenticationChanged) 
                       name:GKPlayerAuthenticationDidChangeNotificationName 
                     object:nil];
        }
    }
    
    return self;
}

- (void)authenticationChanged {    
    
    if ([GKLocalPlayer localPlayer].isAuthenticated && !userAuthenticated) {
        NSLog(@"Authentication changed: player authenticated.");
        userAuthenticated = TRUE;
        [[GKLocalPlayer localPlayer] registerListener:self];
        
    } else if (![GKLocalPlayer localPlayer].isAuthenticated && userAuthenticated) {
       NSLog(@"Authentication changed: player not authenticated");
       userAuthenticated = FALSE;
    }
                   
}


- (void)lookupPlayers {
    
    NSLog(@"Looking up %lu players...", (unsigned long)match.players.count);
    NSArray *playerIds = [match.players valueForKey:@"playerID"];
    [GKPlayer loadPlayersForIdentifiers:playerIds withCompletionHandler:^(NSArray *players, NSError *error) {
       
        if (error != nil) {
            NSLog(@"Error retrieving player info: %@", error.localizedDescription);
            matchStarted = NO;
            [self.delegate matchEnded];
        } else {
            
            // Populate players dict
            self.playersDict = [NSMutableDictionary dictionaryWithCapacity:players.count];
            for (GKPlayer *player in players) {
                NSLog(@"Found player: %@", player.alias);
                [playersDict setObject:player forKey:player.playerID];
            }
            
            // Notify delegate match can begin
            [self.delegate matchStarted];
            
        }
    }];
    
}

#pragma mark User functions

- (void)authenticateLocalUser:(id<GCHelperDelegate>)theDelegate {
    
    if (!gameCenterAvailable) return;
    delegate = theDelegate;
    
    NSLog(@"Authenticating local user...");
    if ([GKLocalPlayer localPlayer].authenticated == NO) {
        [GKLocalPlayer localPlayer].authenticateHandler = ^(UIViewController *viewController, NSError *error)
        {
            if(error){
                NSLog(@"Error!!!");
            }
        };
    } else {
        NSLog(@"Already authenticated!");
    }
}

- (void)findMatchWithMinPlayers:(int)minPlayers maxPlayers:(int)maxPlayers   
    viewController:(UIViewController *)viewController {
    
    if (!gameCenterAvailable) return;
    
    matchStarted = NO;
    self.match = nil;
    self.presentingViewController = UnityGetGLViewController();
    [presentingViewController dismissViewControllerAnimated:NO completion:nil];
    if(pendingInvite != nil)
    {
        //GKMatchRequest *request = [[[GKMatchRequest alloc] init] autorelease];
        GKMatchRequest *request = [[GKMatchRequest alloc] init];
        
        //GKMatchmakerViewController *mmvc =
        //    [[[GKMatchmakerViewController alloc] initWithMatchRequest:request] autorelease];
        
        GKMatchmakerViewController *mmvc = [[GKMatchmakerViewController alloc] initWithInvite:pendingInvite];
        mmvc.matchmakerDelegate = self;
        
        [presentingViewController presentViewController:mmvc animated:YES completion:nil];
        self.pendingInvite = nil;
        self.pendingPlayersToInvite = nil;
        
    }
    else
    {
        
        //GKMatchRequest *request = [[[GKMatchRequest alloc] init] autorelease];
        GKMatchRequest *request = [[GKMatchRequest alloc] init];
        request.minPlayers = minPlayers;
        request.maxPlayers = maxPlayers;
        request.recipients = pendingPlayersToInvite;
        
        //GKMatchmakerViewController *mmvc =
        //    [[[GKMatchmakerViewController alloc] initWithMatchRequest:request] autorelease];
        
        GKMatchmakerViewController *mmvc =
        [[GKMatchmakerViewController alloc] initWithMatchRequest:request];
        mmvc.matchmakerDelegate = self;
        
        [presentingViewController presentViewController:mmvc animated:YES completion:nil];
        self.pendingInvite = nil;
        self.pendingPlayersToInvite = nil;
    }

        
}

#pragma mark GKMatchmakerViewControllerDelegate

// The user has cancelled matchmaking
- (void)matchmakerViewControllerWasCancelled:(GKMatchmakerViewController *)viewController {
    [presentingViewController dismissViewControllerAnimated:YES completion:nil];
}

// Matchmaking has failed with an error
- (void)matchmakerViewController:(GKMatchmakerViewController *)viewController didFailWithError:(NSError *)error {
    [presentingViewController dismissViewControllerAnimated:YES completion:nil];
    NSLog(@"Error finding match: %@", error.localizedDescription);    
}

// A peer-to-peer match has been found, the game should start
- (void)matchmakerViewController:(GKMatchmakerViewController *)viewController didFindMatch:(GKMatch *)theMatch {
    self.match = theMatch;
    match.delegate = self;
    if (!matchStarted && match.expectedPlayerCount == 0) {
        NSLog(@"Ready to start match!");
[self lookupPlayers];
    }
}

#pragma mark GKMatchDelegate

// The match received data sent from the player.
- (void)match:(GKMatch *)theMatch didReceiveData:(NSData *)data fromRemotePlayer:(GKPlayer *)playerID {
    if (match != theMatch) return;
    [self.delegate match:theMatch didReceiveData:data fromRemotePlayer:playerID];
}
- (void)match:(GKMatch *)theMatch player:(GKPlayer *)player didChangeConnectionState:(GKPlayerConnectionState)state {
    if (match != theMatch) return;
    NSLog(@"Connection State Changed");
}

// The player state changed (eg. connected or disconnected)
- (void)match:(GKMatch *)theMatch player:(NSString *)playerID didChange:(GKPlayerConnectionState)state {
    if (match != theMatch) return;
    
    switch (state) {
        case GKPlayerStateConnected: 
            // handle a new player connection.
            NSLog(@"Player connected!");
            
            if (!matchStarted && theMatch.expectedPlayerCount == 0) {
                NSLog(@"Ready to start match!");
                [self lookupPlayers];
            }
            
            break; 
        case GKPlayerStateDisconnected:
            // a player just disconnected. 
            NSLog(@"Player disconnected!");
            matchStarted = NO;
            [self.delegate matchEnded];
            break;
        case GKPlayerStateUnknown:
            NSLog(@"Player state unknown!");
            matchStarted = NO;
            [self.delegate matchEnded];
            break;
    }                     
}

// The match was unable to connect with the player due to an error.
- (void)match:(GKMatch *)theMatch connectionWithPlayerFailed:(NSString *)playerID withError:(NSError *)error {
    
    if (match != theMatch) return;
    
    NSLog(@"Failed to connect to player with error: %@", error.localizedDescription);
    matchStarted = NO;
    [self.delegate matchEnded];
}

// The match was unable to be established with any players due to an error.
- (void)match:(GKMatch *)theMatch didFailWithError:(NSError *)error {
    
    if (match != theMatch) return;
    
    NSLog(@"Match failed with error: %@", error.localizedDescription);
    matchStarted = NO;
    [self.delegate matchEnded];
}

-(void)player:(GKPlayer *)player didAcceptInvite:(GKInvite *)invite
{
    NSLog(@"didAcceptInvite");
    self.pendingInvite = invite;
    [self.delegate inviteReceived];
}
-(void)player:(GKPlayer *)player didRequestMatchWithOtherPlayers:(nonnull NSArray<GKPlayer *> *)playersToInvite
{
    NSLog(@"didRequestMatchWithOtherPlayers");
    self.pendingPlayersToInvite = playersToInvite;
    [self.delegate inviteReceived];
}

@end
