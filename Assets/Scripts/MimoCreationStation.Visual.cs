using UnityEngine;

namespace MimoGarden
{
    public sealed partial class MimoCreationStation
    {
        Transform steam;
        readonly Transform[] steamPuffs = new Transform[7];
        readonly Renderer[] steamRenderers = new Renderer[7];
        readonly MaterialPropertyBlock steamProperties = new MaterialPropertyBlock();

        void BuildSteam()
        {
            steam = Group("Gentle steam above the bamboo", transform);
            Mesh quad = Mesh("Reusable soft steam quad", new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) }, new[] { 0,2,1,0,3,2 });
            quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            Material material = new Material(Resources.Load<Shader>("Shaders/MimoSteam")) { name = "Quiet warm steam" };
            materials.Add("Quiet warm steam", material);
            for (int i = 0; i < steamPuffs.Length; i++)
            {
                steamPuffs[i] = Part("Steam wisp " + i, steam, quad, Vector3.zero, Vector3.one, material);
                steamRenderers[i] = steamPuffs[i].GetComponent<Renderer>();
            }
            steam.gameObject.SetActive(false);
        }

        void UpdateSteam(float progress)
        {
            bool visible = progress > .12f && progress < .82f;
            steam.gameObject.SetActive(visible);
            if (!visible) return;
            float envelope = Mathf.SmoothStep(0, 1, (progress - .12f) * 9) * (1 - Mathf.SmoothStep(0, 1, (progress - .57f) * 4));
            for (int i = 0; i < steamPuffs.Length; i++)
            {
                float age = Mathf.Repeat(progress * 3f + i * .143f, 1);
                float angle = i * Mathf.PI * 2 / steamPuffs.Length;
                steamPuffs[i].localPosition = new Vector3(Mathf.Cos(angle) * (.9f + age * .15f), .86f + age * 1.5f, Mathf.Sin(angle) * .8f);
                steamPuffs[i].rotation = view.transform.rotation;
                steamPuffs[i].localScale = new Vector3(.26f + age * .55f, .40f + age * .9f, 1);
                steamProperties.SetColor("_BaseColor", new Color(1f, .97f, .9f, Mathf.Sin(age * Mathf.PI) * envelope * .27f));
                steamRenderers[i].SetPropertyBlock(steamProperties);
            }
        }
    }
}
