using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace RadboudVR.Avatar
{
    [CustomEditor(typeof(ConvertMCS))]
    public class ConvertMCSEditor : Editor
    {
        ConvertMCS convertMCS;
        Vector2 scrollPos;
        bool foldoutSelectionList = true;

        void OnEnable()
        {
            convertMCS = (ConvertMCS)target;
            convertMCS.RefreshMeshList();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUI.indentLevel++;
            foldoutSelectionList = EditorGUILayout.Foldout(foldoutSelectionList, "Meshes");
            EditorGUI.indentLevel--;
            if (foldoutSelectionList) {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("Select All", ""))) {
                    foreach(MeshListItem ms in convertMCS._meshList) {
                        ms.isSelected = true;
                    }
                }
                if (GUILayout.Button(new GUIContent("Deselect All", ""))) {
                    foreach (MeshListItem ms in convertMCS._meshList) {
                        ms.isSelected = false;
                    }
                }
                if (GUILayout.Button(new GUIContent("Refresh", "Refresh List"))) {
                    convertMCS.RefreshMeshList();
                }
                GUILayout.EndHorizontal();
                //GUILayout.BeginVertical(EditorStyles.helpBox);
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MaxHeight(200));
                foreach (MeshListItem ms in convertMCS._meshList) {
                    GUILayout.BeginHorizontal();
                    ms.isSelected = EditorGUILayout.Toggle(ms.isSelected, GUILayout.MaxWidth(14));
                    EditorGUILayout.LabelField(ms.mesh.name);
                    GUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
                //GUILayout.EndVertical();
                
            }
            GUILayout.BeginVertical();
            EditorGUILayout.HelpBox("Use Extract in Unity 2018 or earlier, then copy the extracted maps folder (MCS/ConversionMaps) to your Unity 2019 project.", MessageType.Info);
            if (GUILayout.Button(new GUIContent("Extract", "Extract vertex maps (<2019)"))) {
                convertMCS.Extract();
            }
            EditorGUILayout.HelpBox("Use Convert in Unity 2019 (or later) to remap morphs to the new vertex order. This may take a while.", MessageType.Info);

            if (GUILayout.Button(new GUIContent("Convert", "Convert morphs to new mesh (2019+)"))) {
                convertMCS.Convert();
            }
            GUILayout.EndVertical();
            serializedObject.ApplyModifiedProperties();
        }
    }
}

