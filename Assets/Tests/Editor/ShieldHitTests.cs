using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShieldHitTests
{
    [Test]
    public void IncomingSceneryHitPreservesFlightAndDirectHitHidesBall()
    {
        var type = Assembly.Load("Assembly-CSharp").GetType("Projectile");
        var ball = new GameObject("Ball");
        var scenery = new GameObject("Scenery");
        try
        {
            ball.AddComponent<SphereCollider>();
            var renderer = ball.AddComponent<MeshRenderer>();
            var projectile = ball.AddComponent(type);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            type.GetMethod("Awake", flags).Invoke(projectile, null);
            type.GetMethod("Initialize").Invoke(projectile, new object[] { Vector3.forward, null });
            float deadline = (float)type.GetField("despawnTime", flags).GetValue(projectile);
            type.GetMethod("HandleHit", flags).Invoke(projectile, new object[] { scenery.AddComponent<BoxCollider>(), Vector3.zero, Vector3.back });
            Assert.That((float)type.GetField("despawnTime", flags).GetValue(projectile), Is.EqualTo(deadline));
            Assert.That(ball.GetComponent<Rigidbody>().linearVelocity.z, Is.GreaterThan(0));
            type.GetMethod("HitPlayer", flags).Invoke(projectile, null);
            Assert.That(renderer.enabled, Is.False);
            Assert.That(ball.GetComponent<Collider>().enabled, Is.False);
            Assert.That((float)type.GetField("despawnTime", flags).GetValue(projectile) - Time.time, Is.EqualTo(0.02f).Within(0.01f));
        }
        finally { UnityEngine.Object.DestroyImmediate(ball); UnityEngine.Object.DestroyImmediate(scenery); }
    }

    [TestCase(0f, false)]
    [TestCase(0.1f, false)]
    [TestCase(4f, true)]
    public void HitRequiresActiveSwing(float handSpeed, bool expectedDeflect)
    {
        var assembly = Assembly.Load("Assembly-CSharp");
        var shieldType = assembly.GetType("Shield");
        var projectileType = assembly.GetType("Projectile");
        var hand = new GameObject("Test shield");
        var ball = new GameObject("Test ball");
        try
        {
            var collider = hand.AddComponent<BoxCollider>();
            var shield = hand.AddComponent(shieldType);
            shieldType.GetField("requireFacing").SetValue(shield, false);
            shieldType.GetField("rawVelocity", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(shield, Vector3.right * handSpeed);
            shieldType.GetProperty("Velocity").GetSetMethod(true).Invoke(shield, new object[] { Vector3.right * handSpeed });
            ball.AddComponent<SphereCollider>();
            var projectile = ball.AddComponent(projectileType);
            projectileType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(projectile, null);
            projectileType.GetField("maxLifetime").SetValue(projectile, 0f);
            projectileType.GetMethod("Initialize").Invoke(projectile, new object[] { Vector3.back, null });
            projectileType.GetMethod("HandleHit", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(projectile, new object[] { collider, Vector3.zero, Vector3.forward });
            Assert.That(projectileType.GetProperty("IsDeflected").GetValue(projectile), Is.EqualTo(expectedDeflect));
            Assert.That(projectileType.GetProperty("IsDropped").GetValue(projectile), Is.EqualTo(!expectedDeflect));
            Assert.That(ball.GetComponent<Rigidbody>().useGravity, Is.EqualTo(!expectedDeflect));
            float deadline = (float)projectileType.GetField("despawnTime", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(projectile);
            if (expectedDeflect)
                Assert.That(float.IsPositiveInfinity(deadline), Is.True, "Deflection must not use the drop timer");
            else
                Assert.That(deadline - Time.time, Is.EqualTo(0.4f).Within(0.02f));
            if (!expectedDeflect)
                Assert.That(ball.GetComponent<Rigidbody>().linearVelocity, Is.EqualTo(Vector3.zero));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ball);
            UnityEngine.Object.DestroyImmediate(hand);
        }
    }
}
