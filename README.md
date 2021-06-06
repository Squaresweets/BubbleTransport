![BubbleTransportLogo](https://matthewcollier.co.uk/BubbleTransportLongLogo.png)
[![Showcase](https://img.shields.io/badge/showcase-brightgreen.svg)](https://www.youtube.com/watch?v=e-RCPvUYxr4)
[![Discord](https://img.shields.io/discord/672474661388288021.svg?label=&logo=discord&logoColor=ffffff&color=7389D8&labelColor=6A7EC2)](https://discord.gg/6hswr9j)
[<img src="https://img.shields.io/twitter/follow/SqSweetsGames?style=social" /></a>](https://twitter.com/SqSweetsGames)
[<img src="https://forthebadge.com/images/badges/gluten-free.svg" height=20/></a>](https://forthebadge.com)

# Bubble Transport
Bubble transport is a transport for [Mirror Networking](https://github.com/vis2k/Mirror) that uses [Game Center Matchmaking](https://developer.apple.com/game-center/) to connect players and transfer data.

Bubble is **fully functional**, although I still need to spend some more time optimising it, cleaning up code and adding comments :D

## Info
Bubble does not work in the same way as normal Mirror transports in the sense that you cannot choose to be a client or a server due to the random nature of matchmaking.

Instead you call the **FindMatch()** function and Bubble will sort out the rest for you when a match is found.

## Features
* No need for a relay server as all data is sent over Game Center, saving money and time on maintaining a server
* 2-4 player games (Limit enforced by Apple, not the transport)
* Invites supported
* Unity events when matches are found or when an invite is recieved

## Setting Up
1. With Mirror installed, download the files from the repository and add them to the project
2. Add the **BubbleTransport.cs** script to the **NetworkManager** object
3. Replace the **Transport** field with the **BubbleTransport.cs** script
4. Adjust callbacks to work with your game and add something to call the **FindMatch()** function
5. Once built for IOS, in the **UnityFramework** tab under **TARGETS** in XCode add the framework: **GameKit.framework** to the list of frameworks

## Testing your game locally
Bubble transport does not work in the inspector as Game Center only works on IOS devices, if you want to test your game use **Telepathy Transport** instad of **Bubble Transport**.

## Credits and licence

Bubble transport was made by [Matthew Collier](https://matthewcollier.co.uk/) and is **free & open source!**

Art made for this project can be found [here](https://github.com/Squaresweets/BubbleTransportArt).

Some of the code comes from [this tutorial](https://www.raywenderlich.com/2487-game-center-tutorial-how-to-make-a-simple-multiplayer-game-with-sprite-kit-part-1-2), however **ALOT** has had to be changed to work with newer IOS versions and as a transport.

If you found this transport useful, consider checking out [my game](https://matthewcollier.co.uk/in-the-slimelight/) that I made it for!
