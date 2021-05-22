#import <Foundation/Foundation.h>
#import <GameKit/GameKit.h>

@protocol GCHelperDelegate
- (void)matchStarted;
- (void)matchEnded;
- (void)playerDisconnected:(GKPlayer *)player;
- (void)match:(GKMatch *)match didReceiveData:(NSData *)data
   fromRemotePlayer:(GKPlayer *)playerID;
-(void)inviteReceived;
@end

@interface GCHelper : NSObject <GKMatchmakerViewControllerDelegate, GKMatchDelegate, GKLocalPlayerListener>
{
    BOOL gameCenterAvailable;
    BOOL userAuthenticated;
    
    
    UIViewController *presentingViewController;
    
    GKMatch *match;
    
    BOOL matchStarted;
    
    //__unsafe_unretained id <GCHelperDelegate> delegate;
    
    NSMutableDictionary *playersDict;
    
    GKInvite *pendingInvite;
    NSArray *pendingPlayersToInvite;
}
@property (retain) UIViewController *presentingViewController;
@property (retain) GKMatch *match;
@property (retain) id <GCHelperDelegate> delegate;
@property (retain) NSMutableArray *players;

@property (retain) GKInvite *pendingInvite;
@property (retain) NSArray *pendingPlayersToInvite;



- (void)findMatchWithMinPlayers:(int)minPlayers maxPlayers:(int)maxPlayers
    viewController:(UIViewController *) viewController;

@property (assign, readonly) BOOL gameCenterAvailable;

+ (GCHelper *)sharedInstance;
- (void)authenticateLocalUser:(id<GCHelperDelegate>)theDelegate;

@end
