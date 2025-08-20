using UnityEngine;
using System.Collections.Generic;

public class SDFRaycaster : MonoBehaviour
{
    public ComputeShader raycastShader;
    public SDFTexture sdf;
    public Vector3[] directions;
    public float maxDistance = 10f;
    public int maxIterations = 64;
    public float hitThreshold = 0.0001f;
    public float minStep = 1e-4f;
    
    [Space]
    [SerializeField] Material mLidarVisualMaterial; // Material for the line renderer
    [SerializeField] float mLineWidth = 0.02f; // Width of the lines
    [SerializeField] Gradient mDistanceGradient;    
    
    
    GameObject mLineParent;
    List<LineRenderer> mLineRenderers = new List<LineRenderer>();
    ComputeBuffer directionsBuffer;
    ComputeBuffer intersectionsBuffer;
    Vector4[] intersectionData;
    private int kernel;
    protected bool mIsReading = false;

    public Vector4[] Intersections => intersectionData;

    void Start()
    {
        InitializeVisualization();
        
        if (raycastShader == null || sdf == null || directions == null || directions.Length == 0)
            return;

        directionsBuffer = new ComputeBuffer(directions.Length, sizeof(float) * 3);
        directionsBuffer.SetData(directions);
        intersectionsBuffer = new ComputeBuffer(directions.Length, sizeof(float) * 4);

        kernel = raycastShader.FindKernel("CSMain");
        raycastShader.SetBuffer(kernel, "_Directions", directionsBuffer);
        raycastShader.SetBuffer(kernel, "_Intersections", intersectionsBuffer);
        raycastShader.SetMatrix("_WorldToSDFSpace", sdf.worldToSDFTexCoords);
        raycastShader.SetMatrix("_SDFToWorldSpace", sdf.worldToSDFTexCoords.inverse);
        raycastShader.SetTexture(kernel, "_SDF", sdf.sdf);
        raycastShader.SetFloat("_Margin", 0.0f);
        raycastShader.SetFloat("_MinStep", minStep);
        raycastShader.SetInt("_MaxIterations", maxIterations);
        raycastShader.SetFloat("_MaxDistance", maxDistance);
        raycastShader.SetFloat("_HitThreshold", hitThreshold);

        uint threadGroupSizeX;
        raycastShader.GetKernelThreadGroupSizes(kernel, out threadGroupSizeX, out _, out _);
        int groupCount = Mathf.CeilToInt((float)directions.Length / threadGroupSizeX);
        raycastShader.Dispatch(kernel, groupCount, 1, 1);

        intersectionData = new Vector4[directions.Length];
        intersectionsBuffer.GetData(intersectionData);
        foreach (Vector4 v4 in intersectionData)
        {
            Debug.Log(v4); 
        }
        //Debug.Log(intersectionData);
    }

    void Update()
    {
        directionsBuffer.SetData(directions);
        raycastShader.SetBuffer(kernel, "_Directions", directionsBuffer);
        raycastShader.SetBuffer(kernel, "_Intersections", intersectionsBuffer);
        raycastShader.SetMatrix("_WorldToSDFSpace", sdf.worldToSDFTexCoords);
        raycastShader.SetMatrix("_SDFToWorldSpace", sdf.worldToSDFTexCoords.inverse);
        raycastShader.SetFloat("_MinStep", minStep);
        
        uint threadGroupSizeX;
        raycastShader.GetKernelThreadGroupSizes(kernel, out threadGroupSizeX, out _, out _);
        int groupCount = Mathf.CeilToInt((float)directions.Length / threadGroupSizeX);
        raycastShader.Dispatch(kernel, groupCount, 1, 1);
        
        intersectionData = new Vector4[directions.Length];
        intersectionsBuffer.GetData(intersectionData);
        foreach (Vector4 v4 in intersectionData)
        {
            Debug.Log(v4); 
        }
        VisualizeModifiedScan(intersectionData);
    }
    
    void VisualizeModifiedScan(Vector4[] inScan)
    {
        if (inScan == null || inScan.Length == 0)
        {
            Debug.LogWarning("No valid points to visualize in Lidar scan.");
            return;
        }
        this.mIsReading = true;
        if (inScan.Length > mLineRenderers.Count)
        {
            // Add more lines
            int newScans = inScan.Length - mLineRenderers.Count;
            for (int i = 0; i < newScans; i++)
            {
                LineRenderer lr = CreateScanVisual(i);
                mLineRenderers.Add(lr);
            }
            // Render them
            for (int i = 0; i < inScan.Length; i++)
                SetVisual(true, this.mLineRenderers[i], inScan[i]);
        }
        // "Remove" some lines
        else if (inScan.Length < mLineRenderers.Count)
        {
            for (int i = 0; i < inScan.Length; i++)
                SetVisual(true, this.mLineRenderers[i], inScan[i]);
            // Don't render excess lines
            for (int i = mLineRenderers.Count - 1; i >= inScan.Length; i--)
                SetVisual(false, this.mLineRenderers[i], Vector3.zero);
        }
        else
        {
            // Update existing lines
            for (int i = 0; i < inScan.Length; i++)
                SetVisual(true, this.mLineRenderers[i], inScan[i]);
        }
        this.mIsReading = false;
    }
    
    
    protected LineRenderer CreateScanVisual(int idx)
    {
        GameObject lineObj = new GameObject($"LidarLine_{mLineRenderers.Count + idx}");
        lineObj.transform.SetParent(mLineParent.transform);
        lineObj.transform.localPosition = Vector3.zero;
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.material = mLidarVisualMaterial;
        lr.startWidth = mLineWidth;
        lr.endWidth = mLineWidth;
        lr.positionCount = 2;
        lr.useWorldSpace = false; // Use local space relative to parent
        return lr;
    }
    
    protected void SetVisual(bool inRender, LineRenderer inLineRenderer, Vector4 inPoint)
    {
        Vector3 point = new Vector3(inPoint.x, inPoint.y, inPoint.z);
        if (inRender)
        {
            inLineRenderer.gameObject.SetActive(true);
            inLineRenderer.SetPosition(0, Vector3.zero); // Set the start point at the origin
            inLineRenderer.SetPosition(1, inPoint); // Set the end point 
            inLineRenderer.startColor = Color.white;
            if (inPoint.w > 0)
                inLineRenderer.endColor = Color.red;
            else
                inLineRenderer.endColor = Color.white;
                
        }
        else
        {
            inLineRenderer.gameObject.SetActive(false);
        }
    }

    [ContextMenu("RayCast")]
    void TestSDF()
    {
        if (raycastShader == null || sdf == null || directions == null || directions.Length == 0)
            return;

        directionsBuffer = new ComputeBuffer(directions.Length, sizeof(float) * 3);
        directionsBuffer.SetData(directions);

        intersectionsBuffer = new ComputeBuffer(directions.Length, sizeof(float) * 4);

        int kernel = raycastShader.FindKernel("CSMain");
        raycastShader.SetBuffer(kernel, "_Directions", directionsBuffer);
        raycastShader.SetBuffer(kernel, "_Intersections", intersectionsBuffer);
        raycastShader.SetMatrix("_WorldToSDFSpace", sdf.worldToSDFTexCoords);
        raycastShader.SetTexture(kernel, "_SDF", sdf.sdf);
        raycastShader.SetFloat("_Margin", 0.0f);
        raycastShader.SetInt("_MaxIterations", maxIterations);
        raycastShader.SetFloat("_MaxDistance", maxDistance);
        raycastShader.SetFloat("_HitThreshold", hitThreshold);

        uint threadGroupSizeX;
        raycastShader.GetKernelThreadGroupSizes(kernel, out threadGroupSizeX, out _, out _);
        int groupCount = Mathf.CeilToInt((float)directions.Length / threadGroupSizeX);
        raycastShader.Dispatch(kernel, groupCount, 1, 1);

        intersectionData = new Vector4[directions.Length];
        intersectionsBuffer.GetData(intersectionData);
        foreach (Vector4 v4 in intersectionData)
        {
            Debug.Log(v4); 
        }
    }
    
    protected void InitializeVisualization()
    {
        // Create parent object for organization
        mLineParent = new GameObject("LidarLines");
        mLineParent.transform.SetParent(transform);
        mLineParent.transform.localPosition = Vector3.zero;
        mLineParent.transform.localRotation = Quaternion.identity;
            
        // Initialize default gradient if not set
        if (mDistanceGradient == null)
        {
            mDistanceGradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[3];
            colorKeys[0] = new GradientColorKey(Color.red, 0.0f);    // Close range - red
            colorKeys[1] = new GradientColorKey(Color.yellow, 0.5f); // Medium range - yellow  
            colorKeys[2] = new GradientColorKey(Color.green, 1.0f);  // Far range - green
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);
            mDistanceGradient.SetKeys(colorKeys, alphaKeys);
        }
    }

    void OnDestroy()
    {
        directionsBuffer?.Release();
        intersectionsBuffer?.Release();
    }
}
