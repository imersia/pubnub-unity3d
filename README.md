pubnub-unity3d
==============

This project and the enclosed script (pubnub.cs) were created to satisfy a need for a c# unity3D script that allows simple publish and subscribe to and from PubNub.

We created it for our own use, but are happy to share it to others who may find it useful.

Details
=======
This script uses Threads to keep alive pubnub subscribes and to ensure the publishes don't 'hiccup' the main Unity3D thread. When the project closes, it waits to make sure all the threads have finished before relinquishing control. I've found this is necessary, otherwise, there are stray threads lying around causing problems.

Help
====
If you see bugs or improvements, please let me know...

Disclaimer
==========
There is no warranty. Use this code at your own risk. We take no liability for any damage using this code might cause.
