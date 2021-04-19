# Oculus Platform P2P Transport for Mirror

This is a [transport for Mirror](https://mirror-networking.gitbook.io/docs/transports) that sends data via the
[Oculus Platform P2P](https://developer.oculus.com/documentation/unity/ps-p2p/) service.

## Usage

Call `OculusTransport.LoggedIn(User user)` when you have your user's data from `Users.GetLoggedInUser().OnComplete` to start the transport.

## ⚠ Not Production Ready ⚠
This is being actively developed for an Oculus Quest app. Pull requests appreciated.

There is a problem with ping spikes with Quest 2 headsets. It is not yet clear if this is a problem with this transport or the Oculus P2P service.
## Problems
### Quest 1 & 2 pinging each-other
![quest 1 & 2 ping](https://i.imgur.com/kVvClcY.png)
### Quest 1 & 1 pinging each-other
![quest 1 & 1 ping](https://i.imgur.com/M1IZCz4.png)