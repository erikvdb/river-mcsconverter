using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MCS;
using MCS.FOUNDATIONS;
using MCS.SERVICES;
using MCS_Utilities.Morph;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RadboudVR.Avatar
{
    [RequireComponent(typeof(MCSCharacterManager))]
    public class ConvertMCS : MonoBehaviour
    {
        [SerializeField]
        public List<MeshListItem> _meshList = new List<MeshListItem>();

        string mapdir { get { return Application.dataPath + "/MCS/ConversionMaps"; } }

        void Start()
        {
            // JCT morphs are being re-imported incorrectly every time you reload Unity or hit play, so we'll have to correct it every time.
            ImportJCT();
        }

        /// <summary>
		/// Extract morphs from every selected mesh.
		/// </summary>
        public void Extract()
        {
            Directory.CreateDirectory(mapdir);

            foreach (MeshListItem ms in _meshList) {               
                string cdPath = mapdir + "/" + ms.mesh.name + ".json";
                if (File.Exists(cdPath) || !ms.isSelected) {
                    // Skip already extracted or unselected meshes
                    continue;
                }

                // Export vertices
                VertexMap vertexMap = new VertexMap();
                vertexMap.vertices = ms.mesh.sharedMesh.vertices;
                vertexMap.WriteToDisk(cdPath);

                ms.isSelected = false;
            }
            RefreshMeshList();
            Debug.Log("Extract complete. Find your extracted vertex maps in: " + mapdir);
        }

        /// <summary>
		/// Loads extracted morph data and remaps vertices to match current models
        /// /// </summary>
        public void Convert()
        {
            ProjectionMeshMap pmm = new ProjectionMeshMap();
            StreamingMorphs sm = new StreamingMorphs();
            StreamingMorphs.LoadMainThreadAssetPaths();
            
            // Grab manifest for morphs, found on MCS base character models. Don't change the prefab name before converting or this won't work.
            var manifest = sm.GetManifest(name);

            foreach (MeshListItem ms in _meshList) {
                if (!ms.isSelected) {
                    continue;
                }

                SkinnedMeshRenderer smr = ms.mesh;

                // Read old vertex map
                ShowProgress(smr.name, "Loading Vertex Map", 0);
                string cdFile = mapdir + "/" + smr.name + ".json";
                if (!File.Exists(cdFile)) {
                    Debug.LogWarning("Skipping:" + smr.name + ", vertex map not found!");
                    continue;
                }

                VertexMap vertexMap = new VertexMap();
                string cd = File.ReadAllText(cdFile);
                vertexMap = JsonUtility.FromJson<VertexMap>(cd);

                // Check if we have the required CoreMesh component
                CoreMesh coreMesh = smr.GetComponent<CoreMesh>();
                if (coreMesh == null) {
                    Debug.LogWarning("Skipping: " + smr.name + ", it does not contain a CoreMesh component");
                    continue;
                }

                string morphPath = Path.Combine(Application.streamingAssetsPath, coreMesh.runtimeMorphPath);
                Directory.CreateDirectory(morphPath); // Create temp directory for generated .morph files.

                // Check if the morphs have already been converted (we are kinda abusing the morphpath for this but it will automatically look for the .mr files anyway)
                if (sm.GetMorphDataFromResources(morphPath, "_2019compatible") != null) {
                    Debug.LogWarning("Skipping:" + smr.name + ", it has already been converted!");
                    continue;
                }                

                // Generate retarget map
                ShowProgress(smr.name, "Generating Retarget Map. This may take a while...", 0.25f);
                Dictionary<int, int> tsm = pmm.GenerateTargetToSourceMap(vertexMap.vertices, smr.sharedMesh.vertices);

                // Process morphs
                int count = 0;
                int total = manifest.names.Length;
                List<string> morphNames = new List<string>(manifest.names);
                morphNames.Add("base"); // Clothing and hair have a 'base' morph that is not in the manifest, but JCT Manager expects it so we add it manually.                

                foreach (string morph in morphNames) {
                    ShowProgress(smr.name, morph, ((float)count / (float)total));
                    // Get current morph data
                    MorphData sourceMD = sm.GetMorphDataFromResources(morphPath, morph);
                    if (sourceMD != null) {     // Not all assets have all morphs.
                        // Reorder morph vertices to match the old mapping
                        MorphData targetMD = RemapMorphData(smr, sourceMD, tsm);
                        // Save new .morph file
                        MCS_Utilities.MorphExtraction.MorphExtraction.WriteMorphDataToFile(targetMD, morphPath + "/" + targetMD.name + ".morph", false, false);
                    }
                    count++;
                }

                // Add fake morphdata as a note to prevent accidentally remapping again.
                MorphData mdNote = new MorphData();
                mdNote.name = "_2019compatible";
                MCS_Utilities.MorphExtraction.MorphExtraction.WriteMorphDataToFile(mdNote, morphPath + "/"+ mdNote.name + ".morph", false, false);
                

                // Generate .morph.mr file
                ShowProgress(smr.name, "Rebuilding MR", 0.8f);
                string baseMRPath = morphPath.Replace(@"\", @"/");
                string mrPath = baseMRPath + ".morphs.mr";
                MCS_Utilities.MorphExtraction.MorphExtraction.MergeMorphsIntoMR(baseMRPath, mrPath);

                // Cleanup
                ShowProgress(smr.name, "Deleting Temporary Files", 0.95f);
                MCS_Utilities.Paths.TryDirectoryDelete(morphPath);

                ms.isSelected = false;
            }
            RefreshMeshList();
            Debug.Log("Conversion Complete!");

            #if UNITY_EDITOR
                EditorUtility.ClearProgressBar();
            #endif
        }

        public void RefreshMeshList()
        {
            List<MeshListItem> newMeshList = new List<MeshListItem>();
            foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>()) {
                bool select = true;
                foreach(MeshListItem ms in _meshList) {
                    if (smr == ms.mesh) {
                        select = ms.isSelected;
                        break;
                    }
                }

                newMeshList.Add(new MeshListItem(smr, select));
            }
            _meshList = newMeshList;
        }

        /// <summary>
		/// Export morph data from the JCTTransition component.
		/// </summary>
        public void ExportJCT()
        {
            JCTTransition jct = GetComponentInChildren<JCTTransition>();
            if (jct == null) {
                Debug.LogWarning("Failed to export JCT morphs: JCTTransition not found");
                return;
            }
            
            // Store in resources so we can (re)load JCTs in standalone builds
            string jctpath = Application.dataPath + "/MCS/Resources/jctmorphs.json";
            StreamWriter writer = new StreamWriter(jctpath, false);

            // Create serializable struct for morph data that we can easily write to json
            JCTMorphList morphList = new JCTMorphList();
            morphList.morphs = jct.m_morphs;
            writer.WriteLine(JsonUtility.ToJson(morphList));
            writer.Close();
            Debug.Log("JCT morph data exported!");
        }

        /// <summary>
		/// Import JCTTransition morph data
		/// </summary>
        public void ImportJCT()
        {
            JCTTransition jct = GetComponentInChildren<JCTTransition>();
            var jctData = Resources.Load<TextAsset>("jctmorphs");

            if (jct == null || jctData == null) {
                Debug.LogWarning("Failed to import JCT morphs.");
                return;
            }

            // Grab morphList from json
            JCTMorphList morphList = JsonUtility.FromJson<JCTMorphList>(jctData.text);
            // Replace current jct morph data with the extracted data
            jct.m_morphs = morphList.morphs;

            Debug.Log("JCT morph data imported.");
        }
        
        void ShowProgress(string elem, string info, float progress)
        {
            #if UNITY_EDITOR
                EditorUtility.DisplayProgressBar("Processing " + elem + "...", info, progress);
            #endif
        }

        MorphData RemapMorphData(SkinnedMeshRenderer smr, MorphData morphData, Dictionary<int, int> tsMap)
        {
            // This is taken from MCS StreamingMorphs's built-in ConvertMorphDataFromMap, but without the requirement for a projectionmap.

            Mesh mesh = smr.sharedMesh;
            Vector3[] targetVertices = mesh.vertices;

            MorphData morphDataNew = new MorphData();
            morphDataNew.name = morphData.name;
            morphDataNew.jctData = morphData.jctData;
            morphDataNew.blendshapeData = new BlendshapeData();
            morphDataNew.blendshapeData.frameIndex = morphData.blendshapeData.frameIndex;
            morphDataNew.blendshapeData.shapeIndex = morphData.blendshapeData.shapeIndex;

            morphDataNew.blendshapeData.deltaVertices = new Vector3[targetVertices.Length];
            morphDataNew.blendshapeData.deltaNormals = new Vector3[targetVertices.Length];
            morphDataNew.blendshapeData.deltaTangents = new Vector3[targetVertices.Length];
            
            foreach (var ts in tsMap) {
                if (morphData.blendshapeData.deltaNormals != null) {
                    morphDataNew.blendshapeData.deltaNormals[ts.Key] = morphData.blendshapeData.deltaNormals[ts.Value];
                }
                if (morphData.blendshapeData.deltaVertices != null) {
                    if (ts.Key >= morphDataNew.blendshapeData.deltaVertices.Length) {
                        throw new System.Exception("ts.key in: " + smr.name + " is too large for deltas: " + ts.Key + " => " + ts.Value + " | " + morphDataNew.blendshapeData.deltaVertices.Length);
                    }
                    if (ts.Value >= morphData.blendshapeData.deltaVertices.Length) {
                        throw new System.Exception("ts.value in: " + smr.name + " is too large for deltas: " + ts.Key + " => " + ts.Value + " | " + morphData.blendshapeData.deltaVertices.Length);
                    }
                    morphDataNew.blendshapeData.deltaVertices[ts.Key] = morphData.blendshapeData.deltaVertices[ts.Value];
                }
                if (morphData.blendshapeData.deltaTangents != null) {
                    morphDataNew.blendshapeData.deltaTangents[ts.Key] = morphData.blendshapeData.deltaTangents[ts.Value];
                }
            }
            return morphDataNew;
        }
    }

    [System.Serializable]
    public struct VertexMap
    {
        public Vector3[] vertices;

        public void WriteToDisk(string path)
        {
            System.IO.Stream fs = System.IO.File.Create(path);
            string json = JsonUtility.ToJson(this);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            fs.Write(bytes, 0, bytes.Length);
            fs.Close();
        }
    }

    [System.Serializable]
    public class MeshListItem
    {
        public SkinnedMeshRenderer mesh;
        public bool isSelected;

        public MeshListItem(SkinnedMeshRenderer mesh, bool select)
        {
            this.mesh = mesh;
            isSelected = select;
        }
    }

    [System.Serializable]
    public struct JCTMorphList
    {
        public JCTMorph[] morphs;
    }
}


