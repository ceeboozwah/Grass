using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Put this on an empty GameObject and press Play: it sets the lighting values the leaf shader reads and grows one curved hedge on flat ground.
/// In your own game, publish the same _CC_ globals from whatever drives your sun and sky, and call HedgeBuilder.Build with your ground height.
/// </summary>
public class HedgeDemo : MonoBehaviour
{
    public float length = 14f;
    public Light sun;                                   // optional: its direction and colour drive the leaves (it should cast shadows)
    [Range(0, 1)] public float wind = 0.55f, glow = 0.6f, gloss = 0.3f, cloudShade = 0.5f;

    void Start()
    {
        Publish();
        var style = new HedgeBuilder.Style { seed = 91 };
        var line = new List<Vector2>();
        for (int i = 0; i <= 40; i++)
        {
            float u = i / 40f, x = (u - 0.5f) * length;
            line.Add(new Vector2(transform.position.x + x, transform.position.z + Mathf.Sin(u * Mathf.PI * 1.5f) * 1.5f));
        }
        float y = transform.position.y;
        HedgeBuilder.Build("Hedge", transform.parent, line, (gx, gz) => y, style, HedgeBuilder.MakeMaterial(style));
    }

    void Update() => Publish();

    void Publish()
    {
        Vector3 toSun = sun ? -sun.transform.forward : new Vector3(0.4f, 0.75f, -0.5f).normalized;
        Color sunCol = (sun ? sun.color : new Color(1f, 0.95f, 0.85f)).linear;
        Color sky = new Color(0.62f, 0.76f, 0.92f).linear, ground = new Color(0.30f, 0.45f, 0.22f).linear;
        Shader.SetGlobalVector("_CC_SunDir", new Vector4(toSun.x, toSun.y, toSun.z, 0));
        Shader.SetGlobalVector("_CC_SunColor", sunCol);
        Shader.SetGlobalVector("_CC_SunRad", sunCol * 2.4f);
        Shader.SetGlobalVector("_CC_HemiSky", sky * 1.4f);
        Shader.SetGlobalVector("_CC_HemiGround", ground * 1.4f);
        Shader.SetGlobalVector("_CC_Grass", new Vector4(0.3f, wind, glow, 0f));          // y wind, z glow
        Shader.SetGlobalVector("_CC_Wind", new Vector4(0.87f, 0.5f, 2.2f + wind * 2.5f, cloudShade));
        Shader.SetGlobalVector("_CC_Gloss", new Vector4(gloss, 0, 0, 0));
    }
}
