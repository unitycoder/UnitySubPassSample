using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomRPAsset : RenderPipelineAsset
{
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/CreateCustomRPAsset")]
    public static void Create()
    {
        CustomRPAsset asset = CustomRPAsset.CreateInstance< CustomRPAsset>();
        UnityEditor.AssetDatabase.CreateAsset(asset,"Assets/CustomRp.asset");

    }
#endif

    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRPInstance();
    }
}

public class CustomRPInstance : RenderPipeline
{

    static Mesh s_FullscreenMesh = null;
    public static Mesh fullscreenMesh
    {
        get
        {
            if (s_FullscreenMesh != null)
                return s_FullscreenMesh;

            float topV = 1.0f;
            float bottomV = 0.0f;

            Mesh mesh = new Mesh { name = "Fullscreen Quad" };
            mesh.SetVertices(new List<Vector3>
                {
                    new Vector3(-1.0f, -1.0f, 0.0f),
                    new Vector3(-1.0f,  1.0f, 0.0f),
                    new Vector3(1.0f, -1.0f, 0.0f),
                    new Vector3(1.0f,  1.0f, 0.0f)
                });

            mesh.SetUVs(0, new List<Vector2>
                {
                    new Vector2(0.0f, bottomV),
                    new Vector2(0.0f, topV),
                    new Vector2(1.0f, bottomV),
                    new Vector2(1.0f, topV)
                });

            mesh.SetIndices(new[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false);
            mesh.UploadMeshData(true);
            return mesh;
        }
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        // �ǂ������o�b�t�@�[������̂���`���܂�
        var final = new AttachmentDescriptor(RenderTextureFormat.ARGB32);
        var color = new AttachmentDescriptor(RenderTextureFormat.ARGB32);
        var depth = new AttachmentDescriptor(RenderTextureFormat.Depth);
        final.ConfigureTarget(new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget), false, true);
        depth.ConfigureTarget(new RenderTargetIdentifier(BuiltinRenderTextureType.Depth), false, true);

        NativeArray<AttachmentDescriptor> descriptors = 
            new NativeArray<AttachmentDescriptor>(new[] {
                color,depth,
                final
            }, 
            Allocator.Temp);
        foreach (var camera in cameras)
        {
            if (!camera.enabled)
                continue;

            int depthIndex = 1;
            int samples = 1;
            using (context.BeginScopedRenderPass( camera.pixelWidth, camera.pixelHeight, 
                samples, descriptors, depthIndex))
            {
                SubPassStep01(context, camera);
                SubPassStep02(context, camera);
            }
            context.Submit();
        }
        descriptors.Dispose();
    }

    // �ŏ��̃p�X
    private void SubPassStep01(ScriptableRenderContext context, Camera camera)
    {
        // Clear���āACamera��Matrix�Z�b�g���s���܂�
        CommandBuffer cmd = new CommandBuffer();
        cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
        cmd.ClearRenderTarget(true, true, camera.backgroundColor);
        // �`��ݒ����
        ScriptableCullingParameters cullingParams;
        camera.TryGetCullingParameters(out cullingParams);

        var cullResults = context.Cull(ref cullingParams);
        var sortingSettings = new SortingSettings(camera);
        var settings = new DrawingSettings(new ShaderTagId("ForwardBase"), sortingSettings);


        // Descriptor�Őݒ肵�������̂ǂ̃o�b�t�@�[�𗘗p���邩�H
        var index = new NativeArray<int>(new[] { 0 }, Allocator.Temp);
        using (context.BeginScopedSubPass(index))
        {
            context.ExecuteCommandBuffer(cmd);
            var fs = new FilteringSettings(RenderQueueRange.opaque);
            context.DrawRenderers(cullResults, ref settings, ref fs);
        }
        index.Dispose();
    }

    // Step01�ŕ`�悵�� Color��Depth���g���čŏI�I�ȉ�ʂɂ��������܂�
    private void SubPassStep02(ScriptableRenderContext context, Camera camera)
    {
        Material material= Resources.Load<Material>("CustomBlit");
        CommandBuffer cmd = new CommandBuffer();
        cmd.ClearRenderTarget(false, true, Color.black);
        // Descripter�Őݒ肳�ꂽ 0�Ԗ�(color)��1�Ԗ�(depth)���󂯎���āc
        var inputIndex = new NativeArray<int>(new[] { 0,1 }, Allocator.Temp);
        // Descripter�Őݒ肳�ꂽ 2�Ԗ�(final)�ɏ����܂�
        var index = new NativeArray<int>(new[] { 2 }, Allocator.Temp);
        using (context.BeginScopedSubPass(index, inputIndex,true))
        {
            // CustomBlit.shader�őS��ʂ�`�悵�܂�
            cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, material, 0, 0);
            context.ExecuteCommandBuffer(cmd);
        }
        inputIndex.Dispose();
        index.Dispose();
    }
}