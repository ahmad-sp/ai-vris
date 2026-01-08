# How to Add AudioSource to CandidateInfoForm in Unity

## Problem
You're getting the error: **"Can not play a disabled audio source"** because the `questionAudioSource` field is not properly configured.

## Solution - Follow These Steps:

### Option 1: Add AudioSource to the Same GameObject (Recommended)

1. **Open Unity Editor**
2. **Find the GameObject** that has the `CandidateInfoForm` script attached:
   - Look in the **Hierarchy** panel
   - It's likely named something like "CandidateForm", "InfoPanel", "UI Canvas", or similar
   - You can search for it by typing "CandidateInfoForm" in the Hierarchy search bar

3. **Select that GameObject** by clicking on it

4. **Add an AudioSource component**:
   - With the GameObject selected, look at the **Inspector** panel on the right
   - Click the **"Add Component"** button at the bottom
   - Type **"Audio Source"** in the search box
   - Click on **"Audio Source"** to add it

5. **Configure the AudioSource** (in the Inspector):
   - ✅ **Play On Awake**: UNCHECK this (turn it OFF)
   - ✅ **Loop**: UNCHECK this (turn it OFF)
   - ✅ **Spatial Blend**: Set to **0** (2D audio)
   - ✅ **Volume**: Set to **1**

6. **Assign the AudioSource to the script**:
   - Still in the Inspector, scroll to the **CandidateInfoForm** component
   - Find the field labeled **"Question Audio Source"**
   - **Drag the AudioSource component** from the same GameObject into this field
   - OR click the small circle ⊙ next to it and select the AudioSource

### Option 2: Use an Existing AudioSource

If you already have an AudioSource somewhere in your scene:

1. **Find your CandidateInfoForm GameObject** in the Hierarchy
2. **Select it** and look at the Inspector
3. **Find the "Question Audio Source" field** in the CandidateInfoForm component
4. **Drag your existing AudioSource** from the Hierarchy into this field
5. Make sure that AudioSource has:
   - Play On Awake: OFF
   - Loop: OFF

### Option 3: Create a Dedicated Audio GameObject

1. **Right-click in the Hierarchy** → **Create Empty**
2. **Rename it** to "Question Audio Player"
3. **Add an AudioSource component** to it (Add Component → Audio Source)
4. **Configure it**:
   - Play On Awake: OFF
   - Loop: OFF
   - Spatial Blend: 0
5. **Select your CandidateInfoForm GameObject**
6. **Drag the "Question Audio Player"** into the "Question Audio Source" field

## Verify It's Working

After setup, you should see:
- In the Inspector, under CandidateInfoForm component
- The "Question Audio Source" field should show: **AudioSource (AudioSource)** or the name of your GameObject
- It should NOT be "None (AudioSource)"

## Still Having Issues?

Check the Unity Console for these messages:
- ✅ **GOOD**: `[CandidateInfoForm] Audio playback started successfully!`
- ❌ **BAD**: `questionAudioSource is NULL! Please assign an AudioSource in the Inspector.`

If you see the "NULL" error, repeat the steps above to assign the AudioSource.
