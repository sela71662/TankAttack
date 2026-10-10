using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TankFixer
{
    [InitializeOnLoad]
    public class TanksGraphicsFixer
    {
        static TanksGraphicsFixer()
        {
            EditorApplication.delayCall += ExecuteFix;
        }

        [MenuItem("Tools/Fix Tanks Graphics")]
        public static void ExecuteFix()
        {
            Debug.Log("[TanksFixer] Starting Tanks Graphics Fix...");
            int fixedModels = 0;
            int fixedMaterials = 0;

            // 1. Re-import all Model Assets in Assets/_Tanks
            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Tanks" });
            foreach (string guid in modelGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null)
                {
                    bool changed = false;
                    if (importer.materialLocation != ModelImporterMaterialLocation.InPrefab)
                    {
                        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                        changed = true;
                    }
                    if (importer.materialSearch != ModelImporterMaterialSearch.Everywhere && importer.materialSearch != ModelImporterMaterialSearch.RecursiveUp)
                    {
                        importer.materialSearch = ModelImporterMaterialSearch.Everywhere;
                        changed = true;
                    }
                    if (importer.materialName != ModelImporterMaterialName.BasedOnMaterialName)
                    {
                        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                        changed = true;
                    }

                    importer.SaveAndReimport();
                    fixedModels++;
                }
            }

            // 2. Verify all Materials in Assets/_Tanks
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Tanks" });
            Shader standardShader = Shader.Find("Standard");
            Shader particlesUnlitShader = Shader.Find("Particles/Standard Unlit");
            Shader uiShader = Shader.Find("UI/Default");

            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null)
                {
                    bool needsFix = mat.shader == null || 
                                     mat.shader.name == "Hidden/InternalErrorShader" || 
                                     string.IsNullOrEmpty(mat.shader.name);

                    if (needsFix)
                    {
                        if (path.Contains("Particles"))
                        {
                            mat.shader = particlesUnlitShader ?? standardShader;
                        }
                        else if (path.Contains("UI"))
                        {
                            mat.shader = uiShader ?? standardShader;
                        }
                        else
                        {
                            mat.shader = standardShader;
                        }
                        EditorUtility.SetDirty(mat);
                        fixedMaterials++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string logResult = $"[TanksFixer] Complete! Processed {fixedModels} models and updated {fixedMaterials} materials in Assets/_Tanks.";
            Debug.Log(logResult);

            string statusPath = Path.Combine(Application.dataPath, "../scratch/tanks_fix_status.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(statusPath));
            File.WriteAllText(statusPath, logResult);
        }
    }
}
