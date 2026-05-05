using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

public class ScreenBlurFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public Shader blurShader;
    }

    class BlurPass : ScriptableRenderPass
    {
        private readonly Material material;
        private static readonly int BlurStrengthId = Shader.PropertyToID("_PsychedeliaBlurStrength");

        public BlurPass(Material material, RenderPassEvent renderPassEvent)
        {
            this.material = material;
            this.renderPassEvent = renderPassEvent;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null)
            {
                return;
            }

            float strength = Shader.GetGlobalFloat(BlurStrengthId);
            if (strength <= 0.001f)
            {
                return;
            }

            material.SetFloat(BlurStrengthId, strength);

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid())
            {
                return;
            }

            TextureDesc desc = source.GetDescriptor(renderGraph);
            desc.depthBufferBits = 0;
            desc.msaaSamples = MSAASamples.None;
            desc.filterMode = FilterMode.Bilinear;
            desc.wrapMode = TextureWrapMode.Clamp;
            desc.name = "_PsychedeliaBlurTemp";

            TextureHandle temp = renderGraph.CreateTexture(desc);
            var blitParams = new RenderGraphUtils.BlitMaterialParameters(source, temp, material, 0);
            renderGraph.AddBlitPass(blitParams, "Psychedelia Blur");
            renderGraph.AddCopyPass(temp, source, "Psychedelia Blur CopyBack");
        }
    }

    public Settings settings = new Settings();
    private Material material;
    private BlurPass blurPass;

    public override void Create()
    {
        if (settings.blurShader == null)
        {
            settings.blurShader = Shader.Find("Hidden/Psychedelia/ScreenBlur");
        }

        if (settings.blurShader != null)
        {
            material = CoreUtils.CreateEngineMaterial(settings.blurShader);
            blurPass = new BlurPass(material, settings.renderPassEvent);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null || blurPass == null)
        {
            return;
        }
        renderer.EnqueuePass(blurPass);
    }
}
