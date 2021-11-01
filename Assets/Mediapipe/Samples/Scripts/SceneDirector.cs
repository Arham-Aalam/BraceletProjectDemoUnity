using Mediapipe;
using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_ANDROID
using System.Runtime.InteropServices;
#endif

public class SceneDirector : MonoBehaviour {
  [SerializeField] bool useGPU = true;

  readonly object graphLock = new object();
  WebCamDevice? webCamDevice;
  GameObject webCamScreen;
  Coroutine cameraSetupCoroutine;
  GameObject graphPrefab;
  GameObject graphContainer;
  Coroutine graphRunner;

  GpuResources gpuResources;
  GlCalculatorHelper gpuHelper;
  delegate void PluginCallback(int eventId);
  static IntPtr currentContext = IntPtr.Zero;

  const int MAX_WAIT_FRAME = 1000;
    GameObject graph;
    GameObject bracelet;

  void OnEnable() {
    // for debugging
    // System.Environment.SetEnvironmentVariable("GLOG_v", "2");
  }

#if UNITY_ANDROID && !UNITY_EDITOR_OSX && !UNITY_EDITOR_WIN
  [AOT.MonoPInvokeCallback(typeof(PluginCallback))]
  static void GetCurrentContext(int eventId) {
    currentContext = Egl.getCurrentContext();
  }
#endif

  void Start() {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_WIN
  #if UNITY_STANDALONE
    if (useGPU) {
      Debug.LogWarning("PC Standalone on macOS or Windows does not support GPU. Uncheck `Use GPU` from the Inspector window (SceneDirector).");
    }
  #endif
#endif

    webCamScreen = GameObject.Find("WebCamScreen");

#if UNITY_ANDROID && !UNITY_EDITOR_OSX && !UNITY_EDITOR_WIN
    if (IsGpuEnabled()) {
      PluginCallback callback = GetCurrentContext;

      var fp = Marshal.GetFunctionPointerForDelegate(callback);
      GL.IssuePluginEvent(fp, 1);
    }
#endif

    var resourceManager = GameObject.Find("ResourceManager");
#if UNITY_EDITOR
    resourceManager.AddComponent<LocalAssetLoader>();
#else
    resourceManager.AddComponent<AssetBundleLoader>();
#endif
        if(WebCamTexture.devices.Length > 0)
        {
            WebCamDevice? device = WebCamTexture.devices[0];
            ChangeWebCamDevice(device);
        }

        bracelet = GameObject.Find("bracelet");
    }

  void OnDisable() {
    StopGraph();
    StopCamera();
  }

  public void ChangeWebCamDevice(WebCamDevice? webCamDevice) {
    lock (graphLock) {
      ResetCamera(webCamDevice);

      if (graphPrefab != null) {
        StopGraph();
        StartGraph();
      }
    }
  }

  void ResetCamera(WebCamDevice? webCamDevice) {
    StopCamera();
    cameraSetupCoroutine = StartCoroutine(webCamScreen.GetComponent<WebCamScreenController>().ResetScreen(webCamDevice));
    this.webCamDevice = webCamDevice;
        graph = GameObject.Find("HandTracking");
        ChangeGraph(graph);
  }

  void StopCamera() {
    if (cameraSetupCoroutine != null) {
      StopCoroutine(cameraSetupCoroutine);
      cameraSetupCoroutine = null;
    }
  }

  public void ChangeGraph(GameObject graphPrefab) {
    lock (graphLock) {
      StopGraph();
            Debug.Log(graphPrefab);
      this.graphPrefab = graphPrefab;

      if (webCamDevice != null) {
        StartGraph();
      }
    }
  }

  void StartGraph() {
    if (graphRunner != null) {
      Debug.Log("The graph is already running");
      return;
    }

    if (IsGpuEnabled()) {
      SetupGpuResources();
    }
    graphRunner = StartCoroutine(RunGraph());
  }

  void StopGraph() {
    if (graphRunner != null) {
      StopCoroutine(graphRunner);
      graphRunner = null;
    }

    if (graphContainer != null) {
      Destroy(graphContainer);
    }
  }

  void SetupGpuResources() {
    if (gpuResources != null) {
      Debug.Log("Gpu resources are already initialized");
      return;
    }

    // TODO: have to wait for currentContext to be initialized.
    if (currentContext == IntPtr.Zero) {
      Debug.LogWarning("No EGL Context Found");
    } else {
      Debug.Log($"EGL Context Found ({currentContext})");
    }

    gpuResources = GpuResources.Create(currentContext).Value();
    gpuHelper = new GlCalculatorHelper();
    gpuHelper.InitializeForTest(gpuResources);
  }

  IEnumerator RunGraph() {
    yield return WaitForGraph();

    if (graphPrefab == null) {
      Debug.LogWarning("No graph is set. Stopping...");
      yield break;
    }

    var webCamScreenController = webCamScreen.GetComponent<WebCamScreenController>();
    yield return WaitForCamera(webCamScreenController);

    if (!webCamScreenController.isPlaying) {
      Debug.LogWarning("WebCamDevice is not working. Stopping...");
      yield break;
    }

    graphContainer = Instantiate(graphPrefab);
    var graph = graphContainer.GetComponent<IDemoGraph<TextureFrame>>();

    if (IsGpuEnabled()) {
      graph.Initialize(gpuResources, gpuHelper);
    } else {
      graph.Initialize();
    }

    graph.StartRun(webCamScreenController.GetScreen()).AssertOk();

    while (true) {
      yield return new WaitForEndOfFrame();

      var nextFrameRequest = webCamScreenController.RequestNextFrame();
      yield return nextFrameRequest;

      var nextFrame = nextFrameRequest.textureFrame;

      graph.PushInput(nextFrame).AssertOk();
      graph.RenderOutput(webCamScreenController, nextFrame);
    }
  }

  IEnumerator WaitForGraph() {
    var waitFrame = MAX_WAIT_FRAME;

    yield return new WaitUntil(() => {
      waitFrame--;

      var isGraphPrefabPresent = graphPrefab != null;

      if (!isGraphPrefabPresent && waitFrame % 50 == 0) {
        Debug.Log($"Waiting for a graph");
      }

      return isGraphPrefabPresent || waitFrame < 0;
    });
  }

  IEnumerator WaitForCamera(WebCamScreenController webCamScreenController) {
    var waitFrame = MAX_WAIT_FRAME;

    yield return new WaitUntil(() => {
      waitFrame--;
      var isWebCamPlaying = webCamScreenController.isPlaying;

      if (!isWebCamPlaying && waitFrame % 50 == 0) {
        Debug.Log($"Waiting for a WebCamDevice");
      }

      return isWebCamPlaying || waitFrame < 0;
    });
  }

  bool IsGpuEnabled() {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_WIN
    return false;
#else
    return useGPU;
#endif
  }

    static List<NormalizedLandmarkList> landmarkList;

    protected Vector3 ScaleVector(Transform transform)
    {
        return new Vector3(10 * transform.localScale.x, 10 * transform.localScale.z, 1);
    }

    protected Vector3 GetPositionFromNormalizedPoint(Transform screenTransform, float x, float y, bool isFlipped)
    {
        var relX = (isFlipped ? -1 : 1) * (x - 0.5f);
        var relY = 0.5f - y;

        return Vector3.Scale(new Vector3(relX, relY, 0), ScaleVector(screenTransform)) + screenTransform.position;
    }

    public static void outputCallback(List<NormalizedLandmarkList> landmark)
    {
        landmarkList = landmark;
    }

    private void Update()
    {
        if (landmarkList != null && landmarkList.Count > 0)
        {
            NormalizedLandmarkList hand = landmarkList[0];
            if (hand.Landmark.Count >= 20)
            {
                Vector3 midPoint = GetPositionFromNormalizedPoint(webCamScreen.transform, hand.Landmark[11].X, hand.Landmark[11].Y, true);
                Vector3 wristPoint = GetPositionFromNormalizedPoint(webCamScreen.transform, hand.Landmark[0].X, hand.Landmark[0].Y, true);

                float yDiff = midPoint.y - wristPoint.y;
                float xDiff = midPoint.x - wristPoint.x;

                float angle = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);

                Vector3 angle2 = new Vector3(350, 0, angle);
                Debug.Log(angle2);
                bracelet.transform.eulerAngles = angle2;

                Vector3 newPosition = wristPoint;
                newPosition.y -= 0.200f;
                newPosition.x -= 0.500f;
                newPosition.z = -0.5f;

                bracelet.transform.position = newPosition;
            }
        }
    }
}
