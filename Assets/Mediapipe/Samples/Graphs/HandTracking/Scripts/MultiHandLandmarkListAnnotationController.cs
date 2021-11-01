using Mediapipe;
using System.Collections.Generic;
using UnityEngine;

class MultiHandLandmarkListAnnotationController : ListAnnotationController<HandLandmarkListAnnotationController> {
  public void Draw(Transform screenTransform, List<NormalizedLandmarkList> landmarkLists, bool isFlipped = false) {
    var drawingCount = Mathf.Min(landmarkLists.Count, MaxSize);
    /*
    if (landmarkLists.Count > 0 && landmarkLists[0].Landmark.Count > 0)
    {
        Debug.Log(landmarkLists[0].Landmark[0].X + " " + landmarkLists[0].Landmark[0].Y);
    }
    */
    for (var i = 0; i < drawingCount; i++) {
      GetAnnotationControllerAt(i).Draw(screenTransform, landmarkLists[i], isFlipped);
    }

    ClearAll(drawingCount);
  }
}
