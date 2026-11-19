<p align="center">
  <img src="images/logo-iade.png" alt="IADE - Faculdade de Design, Tecnologia e Comunicação | Universidade Europeia" width="450">
</p>

<h1 align="center">B.O.L.T.</h1>
<h3 align="center">Project Instructions</h3>

## Team

| Name | Student No. | Role |
|---|---|---|
| Miroslava Mikhaylova | 20240139 | VR Developer, Level Designer |
| Francisco Vilhena | 20250431 | 3D Model Designer |
| Mark Sutormin | 20250087 | Game Designer, Sound Designer |
| Dennis Hanstein | 20262028 | VR Developer, Interaction Developer |

## Hardware requirements

- **VR Headset:** Compatible PCVR headset such as Meta Quest 2/3 via Link cable or AirLink, Valve Index, HTC Vive.
- **PC (for installing):** Windows 10/11 with a USB-C cable, plus SideQuest or Android Platform Tools (adb) [or a PC with SteamVR / Oculus Link if the build is a Windows build].
- **Unity:** version 6000.3.23, with Android Build Support if building for Quest.
- **Packages:** XR Hands, XR Interaction Toolkit and OpenXR (installed through the Package Manager and listed in `Packages/manifest.json`).

## Installation and launch

### Option A: Install the build on computer and run as an application

1. Download the build from the [Releases page](https://github.com/franciscoxvilhena/VR_Project_BOLT/releases/latest). Unzip it, then run `VR_Project_BOLT.exe`.
2. Connect the headset to the PC with a USB-C cable and accept the "Allow USB debugging" prompt inside the headset or via AirLink.
3. On the headset, enable Developer Mode (Meta Horizon phone app > Devices > Headset Settings > Developer Mode).
4. Start the application and put your controllers down. Your hands should appear as virtual hands.

### Option B: Open and run the Unity project

1. Clone the repository:
   ```bash
   git clone https://github.com/franciscoxvilhena/VR_Project_BOLT.git
   ```
2. Open Unity Hub, click "Add project from disk" and select the cloned folder. Open it with Unity version 6000.3.23. Wait for the packages to import, then open the main scene (`Assets/Scenes/BasicScene`).
3. On the headset, enable Developer Mode (Meta Horizon phone app > Devices > Headset Settings > Developer Mode).
4. Connect the headset to the PC with a USB-C cable and accept the "Allow USB debugging" prompt inside the headset or via AirLink.
5. Press play.
6. Put your controllers down, and your hands should appear as virtual hands.

## Controls

The prototype uses no physical controllers. Everything is controlled with hand gestures and touch. The tool buttons are on the right forearm. Select a tool by touching (poking) the matching button with the index finger of the left hand.

| Tool | Gesture | |
|---|---|---|
| **Hammer** | Fist | <img src="images/fig01-fist.png" alt="Fig 01 - Fist" width="220"><br><sub>Fig 01 - Fist</sub> |
| **Knife** | The whole hand | <img src="images/fig02-palm.png" alt="Fig 02 - Palm" width="220"><br><sub>Fig 02 - Palm</sub> |
| **Scissors** | Peace sign, moving the index and middle fingers | <img src="images/fig03-peace-sign.png" alt="Fig 03 - Peace sign" width="220"><br><sub>Fig 03 - Peace sign</sub> |
| **Blow torch** | L-shaped gesture | <img src="images/fig04-l-shaped-gesture.png" alt="Fig 04 - L-shaped gesture" width="160"><br><sub>Fig 04 - L-shaped gesture</sub> |

> **Important:** an action only works if the correct tool is selected and the correct gesture is held. If you select one tool but make another gesture, nothing happens. To change the tool, touch another button on the forearm.

## Gameplay

1. Grab the cube in front of you by pinching it. The interaction gets triggered and the platform moves up.

   <img src="images/fig05-grab-cube.png" alt="Fig 05 - Grabbing first object by pinching" width="300">

   <sub>Fig 05 - Grabbing first object by pinching, screenshot from Unity</sub>

2. Release the cube as the platform moves up.
3. The platform moves to the 2nd room, wait.
4. The platform moves to the 3rd room, wait.
5. The platform moves to the 4th room. Select any tool you want (green button - scissors, blue button - hammer, red button - knife, yellow button - torch, grey button - null).

   <img src="images/fig06-button-selection.png" alt="Fig 06 - Button selection UI" width="450">

   <sub>Fig 06 - Button selection UI, screenshot from Unity</sub>

6. **For the nail:** select the hammer on the panel (blue button), put your hand in a fist, and hit with the hammer 3 times for the nail to be fully hammered into the surface.

   <img src="images/fig07-hammer.png" alt="Fig 07 - Hammer interaction" width="300">

   <sub>Fig 07 - Hammer Interaction, screenshot from Unity</sub>

7. **For the green cube:** select the scissors on the panel (green button), move your fingers like scissors, and make 3 cuts while colliding the scissors with the green cube. Wait for the material to change.

   <img src="images/fig08a-scissors.png" alt="Fig 08 - Scissors interaction" width="340"> <img src="images/fig08b-scissors.png" alt="Fig 08 - Scissors interaction, after cutting" width="340">

   <sub>Fig 08 - Scissors Interaction, screenshots from Unity</sub>

8. **For the yellow cube:** select the blow torch on the panel (yellow button), make the L-gesture with your hand, and make the fire reach the yellow cube for 3 seconds. Wait for the material to change.

   <img src="images/fig09a-blow-torch.png" alt="Fig 09 - Blow torch interaction" width="340"> <img src="images/fig09b-blow-torch.png" alt="Fig 09 - Blow torch interaction, after burning" width="340">

   <sub>Fig 09 - Blow Torch Interaction, screenshots from Unity</sub>

9. **For the red cube:** select the knife on the panel (red button), use the whole hand to guide the knife, and "cut" the cube by colliding the knife with it for 3 seconds. Wait for the cube to disappear.

   <img src="images/fig10-knife.png" alt="Fig 10 - Knife interaction" width="300">

   <sub>Fig 10 - Knife Interaction, screenshot from Unity</sub>

10. Turn off the experience.
