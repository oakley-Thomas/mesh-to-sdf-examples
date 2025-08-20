using UnityEngine;

public class LiDARDispatch : MonoBehaviour
{
    // Assume you have both components in your scene
    // - SDFTexture sdfTexComp: owns the 3D SDF RenderTexture and the world->SDF matrix
    // - MeshToSDF meshToSdf: updates the field every frame from your mesh/skinned mesh

    [SerializeField] ComputeShader lidarCS;
    [SerializeField] SDFTexture sdfTexComp;    // from the package
    [SerializeField] Vector3 rayOriginWS;
    [SerializeField] Vector4[] mData;
    ComputeBuffer raysBuffer; // RWStructuredBuffer<float4> with directions in xyz, w=0

    int kernel;
    private int rayCount;
    void Awake()
    {
        kernel = lidarCS.FindKernel("RaymarchSDF");
        this.raysBuffer = new ComputeBuffer(this.mData.Length, sizeof(float) * 4);
        this.raysBuffer.SetData(this.mData);
        int rayCount = this.mData.Length;
    }

    void LateUpdate()
    {
        // 1) Get the latest SDF texture and the mapping matrix
        Texture sdfRT3D = sdfTexComp.sdf; // TextureDimension.Tex3D
        Matrix4x4 worldToSDF  = sdfTexComp.worldToSDFTexCoords; // world -> [0,1]^3

        // 2) Make sure the texture is in a good sampling state
        sdfRT3D.filterMode = FilterMode.Trilinear;
        sdfRT3D.wrapMode = TextureWrapMode.Clamp;

        // 3) Set parameters
        lidarCS.SetTexture(kernel, "_SDF", sdfRT3D);
        lidarCS.SetMatrix("_WorldToSDF", worldToSDF);

        lidarCS.SetVector("_RayOriginWS", rayOriginWS);
        lidarCS.SetFloat("_MaxDistance", 100.0f);
        lidarCS.SetInt("_MaxSteps", 128);
        lidarCS.SetFloat("_HitEpsilon", 0.005f);
        lidarCS.SetFloat("_Safety", 0.9f);

        lidarCS.SetVector("_SDFSize",
            new Vector3(sdfRT3D.width, sdfRT3D.height, sdfRT3D.width));

        lidarCS.SetInt("_RayCount", rayCount);
        lidarCS.SetBuffer(kernel, "_Rays", raysBuffer);

        // 4) Dispatch
        int groups = (rayCount + 63) / 64;
        lidarCS.Dispatch(kernel, 1, 1, 1);
        this.raysBuffer.GetData(this.mData);
        foreach (var v in  this.mData)
        {
            print(v);
        }
    }

}
