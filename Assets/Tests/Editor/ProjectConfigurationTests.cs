using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ProjectConfigurationTests
{
    [Test]
    public void GameplaySceneExists()
    {
        Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/BasicScene.unity"), Is.Not.Null);
    }

    [Test]
    public void ProjectilePrefabSupportsDynamicCollisions()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Projectiles/Projectile_Basic.prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<Rigidbody>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<Rigidbody>().isKinematic, Is.False);
        Assert.That(prefab.GetComponent<Collider>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<Collider>().enabled, Is.True);
    }
}
