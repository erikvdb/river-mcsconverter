# MCS Converter
This converter remaps the morphs of the MCS avatar system (v1.7) so they will work with Unity 2019 and later. 

## The issue.
Unity introduced a new way of optimizing mesh on import, which scrambles the vertex order of the MCS models. However, the MCS morph targets will still have the old vertex order, so when applying a morph the wrong vertices get pushed and you end up with some abstract art. On top of that, morph data for the JCTTransition component, which controls joint offsets for every morph, is corrupted during import.
This tool converts the morph target maps to their new vertex order, and replaces the new JCTTransition morph data with the old.

In the future, hopefully someone will fix the MCS_Importer plugin so vertex order and JCT morph data will automatically import correctly again.

More information on the issues of MCS in Unity 2019 and other fixes can be found in this thread the Unity forums: <https://forum.unity.com/threads/released-morph-character-system-mcs-male-and-female.355675>. 

## Installing
1. Create TWO Unity projects: one in version that works with MCS 1.7 (i.e. 2017 or <2018.4), and one in your later version (2019+). Apply the following steps to both projects.
2. From <https://github.com/mcs-sdk/mcs/releases>, download and import your MCS packages. The CodeAndShaders.unitypackage and the female or male base package are mandatory; add whatever additional clothing/hair packs you have and need.
3. Import the MCSConverter.unitypackage included in this repo, or drag and drop the RadboudVR folder into your Assets folder.
4. Drag the MCSFemale or MCSMale prefab into your scene. 
5. Attach your content packs
6. Add the ConvertMCS.cs component to the MCS character.

![Convert MCS component](convertmcs.jpg)

## Usage
In your Unity 2017/2018 project:
1. Select the meshes you wish to convert (default selects all)
2. Click "Extract". This will extract vertex maps of all selected models into a file folder.
3. Click "Export JCT". This will export the JCTTransition morph data.
3. Copy the extracted maps folder (Assets/MCS/ConversionMaps) and the JCT file (Assets/MCS/Resources) into the same location in your Unity 2019+ project.

In your Unity 2019+ project:
1. Select all meshes you wish to convert.
2. Click "Convert". This may take a while and will overwrite the old morph streamingasset files. You can also choose to Import JCT data here, but it will likely revert the changes when you restart Unity (see Limitations).
3. Restart Unity.

## Limitations
Until the JCT importer is fixed, it will re-import and corrupt its morph data every time you reopen the Unity project or hit play. Keep the Convert MCS component attached to your characters and keep the jctmorphs.json file to reload the correct jcts during runtime (hence it's in the Resources folder).

## Troubleshooting
*I don't see any morphs when I adjust the sliders apart from the very first one.*
Make sure GPU Skinning is turned OFF in player options.

*I'm getting build errors saying "Assembly UnityEditor is referenced in MCS_Importer"*
Select MCS_Importer.dll and under Select platforms for plugin, only include Editor.