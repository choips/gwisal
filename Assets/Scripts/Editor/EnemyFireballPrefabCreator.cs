using UnityEditor;
using UnityEngine;

// 에디터 전용: 적 불덩이 프리팹을 만듦 (메뉴 [귀살 → 적 불덩이 프리팹 만들기])
// 이미 있으면 건드리지 않습니다. EnemyData(도깨비불)의 Projectile Prefab 칸에 연결합니다.
public static class EnemyFireballPrefabCreator
{
    private const string PrefabPath = "Assets/Prefabs/EnemyFireball.prefab";
    private const string MaterialPath = "Assets/Materials/EnemyFireballMat.mat";

    [MenuItem("귀살/적 불덩이 프리팹 만들기")]
    public static GameObject EnsurePrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader);
            material.color = new Color(1f, 0.45f, 0.15f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(1f, 0.45f, 0.15f, 1f));
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(1.2f, 0.4f, 0.05f));
            }
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        temp.name = "EnemyFireball";
        temp.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

        if (temp.TryGetComponent(out Collider col))
        {
            col.isTrigger = true;
        }

        Rigidbody body = temp.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;

        temp.AddComponent<EnemyProjectile>();

        MeshRenderer renderer = temp.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sharedMaterial = material;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
        Object.DestroyImmediate(temp);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"적 불덩이 프리팹을 만들었습니다: {PrefabPath}");
        return prefab;
    }
}
