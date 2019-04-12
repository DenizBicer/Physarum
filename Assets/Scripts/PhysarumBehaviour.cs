using System.Runtime.InteropServices;
using UnityEngine;

public class PhysarumBehaviour : MonoBehaviour
{
    [Header("Initial values")]
    [SerializeField] [Range(0f, 1f)] private float percentageParticles = 0.02f;
    [SerializeField] private int dimension = 256;
    [SerializeField] private ComputeShader shader;
    [SerializeField] private bool stimuliActive = true;
    [SerializeField] private Texture Stimuli;

    [Header("Run time parameters")]
    [Range(0f, 1f)] public float decay = 0.002f;
    [Range(0f, 1f)] public float wProj = 0.1f;
    public float sensorAngleDegrees = 45f; 	//in degrees
    public float rotationAngleDegrees = 45f;//in degrees
    [Range(0f, 1f)] public float sensorOffsetDistance = 0.01f;
    [Range(0f, 1f)] public float stepSize = 0.001f;


    private int numberOfParticles;
    private float sensorAngle; 				//in radians
    private float rotationAngle;   			//in radians
    private RenderTexture trail;
    private RenderTexture RWStimuli;
    private int initHandle, particleHandle, trailHandle;
    private ComputeBuffer particleBuffer;

    private static int GroupCount = 8;       // Group size has to be same with the compute shader group size

    struct Particle
    {
        public Vector2 point;
        public float angle;

        public Particle(Vector2 pos, float angle)
        {
            point = pos;
            this.angle = angle;
        }
    };
    void OnValidate()
    {
        if (dimension < GroupCount) dimension = GroupCount;
    }

    void Start()
    {
        if (shader == null)
        {
            Debug.LogError("PhysarumSurface shader has to be assigned for PhysarumBehaviour to work.");
            this.enabled = false;
            return;
        }

        initHandle      = shader.FindKernel("Init");
        particleHandle  = shader.FindKernel("MoveParticles");
        trailHandle     = shader.FindKernel("StepTrail");

        InitializeParticles();
        InitializeTrail();
        InitializeStimuli();
    }
    
    void InitializeParticles()
    {
        // allocate memory for the particles
        numberOfParticles = (int)(dimension * dimension * percentageParticles);
        if (numberOfParticles < GroupCount) numberOfParticles = GroupCount;

        Particle[] data = new Particle[numberOfParticles];
        particleBuffer = new ComputeBuffer(data.Length, 12);
        particleBuffer.SetData(data);

        //initialize particles with random positions
        shader.SetInt("numberOfParticles", numberOfParticles);
        shader.SetVector("trailDimension", Vector2.one * dimension);
        shader.SetBuffer(initHandle, "particleBuffer", particleBuffer);
        shader.Dispatch(initHandle, numberOfParticles / GroupCount, 1, 1);

        shader.SetBuffer(particleHandle, "particleBuffer", particleBuffer);
    }

    void InitializeTrail()
    {
        trail = new RenderTexture(dimension, dimension, 24);
        trail.enableRandomWrite = true;
        trail.Create();

        var rend = GetComponent<Renderer>();
        rend.material.mainTexture = trail;

        shader.SetTexture(particleHandle, "TrailBuffer", trail);
        shader.SetTexture(trailHandle, "TrailBuffer", trail);
    }

    void InitializeStimuli()
    {
        if (Stimuli == null)
        {
            RWStimuli = new RenderTexture(dimension, dimension, 24);
            RWStimuli.enableRandomWrite = true;
            RWStimuli.Create();
        }
        else
        {
            RWStimuli = new RenderTexture(Stimuli.width, Stimuli.height, 0);
            RWStimuli.enableRandomWrite = true;
            Graphics.Blit(Stimuli, RWStimuli);
        }
        shader.SetBool("stimuliActive", stimuliActive);
        shader.SetTexture(trailHandle, "Stimuli", RWStimuli);
    }

    void Update()
    {
        UpdateRuntimeParameters();
        UpdateParticles();
        UpdateTrail();
    } 

    void UpdateRuntimeParameters()
    {
        sensorAngle = sensorAngleDegrees * 0.0174533f;
        rotationAngle = rotationAngleDegrees * 0.0174533f;
        shader.SetFloat("sensorAngle", sensorAngle);
        shader.SetFloat("rotationAngle", rotationAngle);
        shader.SetFloat("sensorOffsetDistance", sensorOffsetDistance);
        shader.SetFloat("stepSize", stepSize);
        shader.SetFloat("decay", decay);
        shader.SetFloat("wProj", wProj);
    }

    void UpdateParticles()
    {
        shader.Dispatch(particleHandle, numberOfParticles / GroupCount, 1, 1);
    }

    void UpdateTrail()
    {
        shader.Dispatch(trailHandle, dimension / GroupCount, dimension / GroupCount, 1);
    }

    void OnDestroy()
    {
        if (particleBuffer != null) particleBuffer.Release();
    }
}
