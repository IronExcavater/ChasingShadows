
BEFORE YOU START:
- you need Unity 6.1
- you need URP SRP pipline 17.1 if you use higher please import 17.1 support pack.
- wind setup is in wind prefab at each scene

Step 1 - You can improve FPS amount by 30% if you change rendering path from forward to deferred at rendering setting. 
Find File "PC Renderer" and change Rendering path from forward to deferred. Forward render is ok too but it's slower for complex scenes

Deferred at PC Renderer setting at initial unity 6 version we notice water doesnt show up at deferred and screen space ambient occlusion turned on at the same time. 
Looks like near/far clip planes are bugged at that engine version and it send wrong depth data. It's engine bug so just be patient until they fix it.

Step 2 

 !! To turn on distortion and proper render at water find please turn on:
Find File "PC_RPAsset" 
	- Turn on "Opaque Texture" this will fix water translucency and distortion if its turned off
	- Turn on "Depth Texture" this will fix water visibility at playmode if its turned off
	- Turn on HDR if its turned off

Step 3 Find "Park_Demo" and open it.

Step 4 - HIT PLAY!:)

Step 5 -  Make note that unity often compile shaders even after you hit play for long time, so performance will rise up after unity end shader compilation
Wait a moment until it end. 

About scene construction:
		- There is post process profile: Manage post process by scene post process object.
		- Prefab wind manage wind speed and direction at the scene

