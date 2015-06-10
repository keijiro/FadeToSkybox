//
// Fade-to-skybox fog effect
//
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Image Effects/Rendering/Fade To Skybox")]
public class FadeToSkybox : MonoBehaviour
{
    #region Serialized Variables

    [SerializeField] bool _useRadialDistance;
    [SerializeField] float _startDistance;
    [SerializeField] Shader _fogShader;

    #endregion

    #region Public Properties

    public bool useRadialDistance {
        get { return _useRadialDistance; }
        set { _useRadialDistance = value; }
    }

    public float startDistance {
        get { return _startDistance; }
        set { _startDistance = value; }
    }

    #endregion

    #region Private Objects

    Material  _fogMaterial;

    #endregion

    #region Private Functions

    void Setup()
    {
        if (_fogMaterial == null) {
            _fogMaterial = new Material(_fogShader);
            _fogMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
    }

    void SanitizeParameters()
    {
        _startDistance = Mathf.Max(_startDistance, 0.0f);
    }

    Matrix4x4 CalculateFrustumCorners()
    {
        var c = GetComponent<Camera>();
        var t = c.transform;

        var near = c.nearClipPlane;
        var far = c.farClipPlane;

        var tanHalfFov = Mathf.Tan(c.fieldOfView * Mathf.Deg2Rad / 2);
        var toRight = t.right * near * tanHalfFov * c.aspect;
        var toTop = t.up * near * tanHalfFov;

        var v_tl = t.forward * near - toRight + toTop;
        var v_tr = t.forward * near + toRight + toTop;
        var v_br = t.forward * near + toRight - toTop;
        var v_bl = t.forward * near - toRight - toTop;

        var scale = v_tl.magnitude * far / near;

        var m = Matrix4x4.identity;
        m.SetRow (0, v_tl.normalized * scale);
        m.SetRow (1, v_tr.normalized * scale);
        m.SetRow (2, v_br.normalized * scale);
        m.SetRow (3, v_bl.normalized * scale);
        return m;
    }

    #endregion

    #region Monobehaviour Functions

    void Start()
    {
        Setup();
    }

    [ImageEffectOpaque]
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SanitizeParameters();

        Setup();

        // Set up fog parameters.
        _fogMaterial.SetMatrix("_FrustumCorners", CalculateFrustumCorners());
        _fogMaterial.SetFloat("_DistanceOffset", _startDistance);

        var mode = RenderSettings.fogMode;
        if (mode == FogMode.Linear)
        {
            var start = RenderSettings.fogStartDistance;
            var end = RenderSettings.fogEndDistance;
            var invDiff = 1.0f / Mathf.Max(end - start, 1.0e-6f);
            _fogMaterial.SetFloat("_LinearGrad", -invDiff);
            _fogMaterial.SetFloat("_LinearOffs", end * invDiff);
            _fogMaterial.DisableKeyword("FOG_EXP");
            _fogMaterial.DisableKeyword("FOG_EXP2");
        }
        else if (mode == FogMode.Exponential)
        {
            const float coeff = 1.4426950408f; // 1/ln(2)
            var density = RenderSettings.fogDensity;
            _fogMaterial.SetFloat("_Density", coeff * density);
            _fogMaterial.EnableKeyword("FOG_EXP");
            _fogMaterial.DisableKeyword("FOG_EXP2");
        }
        else // FogMode.ExponentialSquared
        {
            const float coeff = 1.2011224087f; // 1/sqrt(ln(2))
            var density = RenderSettings.fogDensity;
            _fogMaterial.SetFloat("_Density", coeff * density);
            _fogMaterial.DisableKeyword("FOG_EXP");
            _fogMaterial.EnableKeyword("FOG_EXP2");
        }

        if (_useRadialDistance)
            _fogMaterial.EnableKeyword("RADIAL_DIST");
        else
            _fogMaterial.DisableKeyword("RADIAL_DIST");

        // Transfer the skybox parameters.
        var skybox = RenderSettings.skybox;
        _fogMaterial.SetTexture("_SkyCubemap", skybox.GetTexture("_Tex"));
        _fogMaterial.SetColor("_SkyTint", skybox.GetColor("_Tint"));
        _fogMaterial.SetFloat("_SkyExposure", skybox.GetFloat("_Exposure"));
        _fogMaterial.SetFloat ("_SkyRotation", skybox.GetFloat("_Rotation"));

        // Draw screen quad.
        _fogMaterial.SetTexture("_MainTex", source);
        _fogMaterial.SetPass(0);

        RenderTexture.active = destination;

        GL.PushMatrix();
        GL.LoadOrtho();
        GL.Begin(GL.QUADS);

        GL.MultiTexCoord2(0, 0.0f, 0.0f);
        GL.Vertex3(0.0f, 0.0f, 3.0f); // BL

        GL.MultiTexCoord2(0, 1.0f, 0.0f);
        GL.Vertex3(1.0f, 0.0f, 2.0f); // BR

        GL.MultiTexCoord2(0, 1.0f, 1.0f);
        GL.Vertex3(1.0f, 1.0f, 1.0f); // TR

        GL.MultiTexCoord2(0, 0.0f, 1.0f);
        GL.Vertex3(0.0f, 1.0f, 0.0f); // TL

        GL.End();
        GL.PopMatrix();
    }

    #endregion
}
