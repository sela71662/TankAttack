using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TankDiagnostic
{
    [InitializeOnLoad]
    public class TanksDiagnosticTool
    {
        static TanksDiagnosticTool()
        {
            EditorApplication.delayCall += RunDiagnostic;
        }

        [MenuItem("Tools/Diagnose Tanks Graphics")]
        public static void RunDiagnostic()
        {
            string reportPath = Path.Combine(Application.dataPath, "../scratch/tanks_diagnostic.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== TANKS GRAPHICS DIAGNOSTIC REPORT ===");

            // 1. Check Render Pipeline
            sb.AppendLine($"Render Pipeline: {UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name ?? "Built-in Render Pipeline"}");

            // 2. Check Materials in Assets/_Tanks
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Tanks" });
            sb.AppendLine($"\n--- MATERIALS ({matGuids.Length} found) ---");
            int brokenMaterialsCount = 0;

            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    sb.AppendLine($"[NULL] {path}");
                    brokenMaterialsCount++;
                    continue;
                }

                Shader shader = mat.shader;
                string shaderName = shader != null ? shader.name : "NULL";
                bool isBroken = shader == null || shaderName == "Hidden/InternalErrorShader" || shaderName.Contains("InternalError");

                if (isBroken)
                {
                    sb.AppendLine($"[BROKEN SHADER] {path} | Shader: {shaderName}");
                    brokenMaterialsCount++;
                }
                else
                {
                    // Check main texture / normal map references
                    sb.AppendLine($"[OK] {path} | Shader: {shaderName}");
                }
            }

            sb.AppendLine($"Total broken materials: {brokenMaterialsCount} / {matGuids.Length}");

            // 3. Check Textures in Assets/_Tanks
            string[] texGuids = AssetDatabase.FindAssets("t:Texture", new[] { "Assets/_Tanks" });
            sb.AppendLine($"\n--- TEXTURES ({texGuids.Length} found) ---");
            foreach (string guid in texGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    sb.AppendLine($"{path} | Type: {importer.textureType} | Format: {importer.textureFormat} | sRGB: {importer.sRGBTexture}");
                }
            }

            // 4. Check Prefabs in Assets/_Tanks
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Tanks" });
            sb.AppendLine($"\n--- PREFABS ({prefabGuids.Length} found) ---");
            int brokenPrefabsCount = 0;
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                {
                    Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer r in renderers)
                    {
                        foreach (Material m in r.sharedMaterials)
                        {
                            if (m == null)
                            {
                                sb.AppendLine($"[MISSING MATERIAL IN PREFAB] {path} -> {r.name}");
                                brokenPrefabsCount++;
                            }
                            else if (m.shader == null || m.shader.name == "Hidden/InternalErrorShader")
                            {
                                sb.AppendLine($"[BROKEN SHADER IN PREFAB] {path} -> {r.name} -> Material: {m.name} (Shader: {m.shader?.name})");
                                brokenPrefabsCount++;
                            }
                        }
                    }
                }
            }
            sb.AppendLine($"Total prefab material issues: {brokenPrefabsCount}");

            File.WriteAllText(reportPath, sb.ToString());
            Debug.Log($"[TanksDiagnostic] Written report to {reportPath}");
        }
    }
}
