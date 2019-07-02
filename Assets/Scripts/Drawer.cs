using UnityEngine;
using System.Collections.Generic;
using System.Threading;

public class Drawer : MonoBehaviour
{
	public string baseFilename;

	public bool   relativePlacement  = false;

	public float  minimumSegmentSize = 0.005f;

	public int    numberOfLODs       = 1;
	

	private enum State
	{
		Standby,
		Loading,
		Loaded,
		Error,
		Complete
	}


	/// <summary>
	/// Called at the start of the program.
	/// </summary>
	/// 
	public void Start()
	{
		strokeList   = new StrokeList();
		strokeIndex  = 0;
		state        = State.Standby;
		loaderThread = null;
		lastLog      = "";
	}


	/// <summary>
	/// Kick off the loading process
	/// </summary>
	/// 
	public void StartLoading()
	{
		loaderThread = new Thread(StrokeDatasetReader);
		loaderThread.Priority = System.Threading.ThreadPriority.Lowest;
		loaderThread.Start();
		state = State.Loading;
	}


	/// <summary>
	/// Thread for loading the file.
	/// </summary>
	private void StrokeDatasetReader()
	{
		if (strokeList.LoadStrokeData(baseFilename))
		{
			state = State.Loaded;
			lastLog = "Loaded stroke file";
		}
		else
		{
			state = State.Error;
			lastLog = "Error loading stroke file";
		}
	}


	public void Update()
	{
		if (lastLog.Length > 0)
		{
			if (lastLog.Contains("Error"))
			{
				Debug.LogWarning(lastLog);
			}
			else
			{
				Debug.Log(lastLog);
			}
			lastLog = "";
		}

		if ((state == State.Loaded) && (strokeIndex < strokeList.Count))
		{
			CreateStroke(strokeIndex);
			strokeIndex++;

			if ( strokeIndex == strokeList.Count )
			{
				StaticBatchingUtility.Combine(gameObject);
				Debug.Log("Finished " + baseFilename);
				state = State.Complete;
			}
		}
	}


	/// <summary>
	/// Called once per rendered frame.
	/// </summary>
	/// 
	void CreateStroke(int strokeIdx)
	{
		Stroke stroke = strokeList[strokeIdx];

		// start new mesh for the trail and add to common container
		stroke.gameObject = new GameObject();
		stroke.gameObject.name = string.Format("Stroke_{0:D3}", strokeIdx);
		stroke.gameObject.transform.parent = this.transform;

		// find material closest to the desired colour
		Material mat = MaterialManager.Instance().FindMaterial(stroke.points[0].strokeColour);
		Material[] strokeMaterials = new Material[2];
		strokeMaterials[0] = mat;
		strokeMaterials[1] = mat;

		// create LODs
		LODGroup lodGroup = stroke.gameObject.AddComponent<LODGroup>();
		lodGroup.fadeMode = LODFadeMode.CrossFade;
		LOD[] lods = new LOD[numberOfLODs];

		for (int level = 0; level < lods.Length; level++)
		{
			LOD lod = new LOD();
			GameObject go = new GameObject("LOD" + level);
			go.transform.parent = stroke.gameObject.transform;

			MeshRenderer renderer = CreateStrokeMesh(ref stroke, ref go, 1 << level);
			renderer.receiveShadows    = true;
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
			renderer.sharedMaterials   = strokeMaterials;
			lod.renderers = new Renderer[] { renderer };
			lod.screenRelativeTransitionHeight = 0.1f / (1 << level);
			lod.fadeTransitionWidth            = 0f;
			lods[level] = lod;
		}
		lods[lods.Length - 1].screenRelativeTransitionHeight = 0; // never cull
		lodGroup.SetLODs(lods);

		if (relativePlacement)
		{
			Vector3 offset = strokeList.average;
			offset.y = 0;
			stroke.gameObject.transform.localPosition = -offset;
		}
	}

	public MeshRenderer CreateStrokeMesh(ref Stroke stroke, ref GameObject go, int stepSize)
	{
		// create renderer
		MeshRenderer renderer = go.AddComponent<MeshRenderer>();

		// create mesh
		Mesh strokeMesh = go.AddComponent<MeshFilter>().mesh;
		strokeMesh.subMeshCount = 2;
		List<Vector3> vertices     = new List<Vector3>();
		List<Vector3> normals      = new List<Vector3>();
		//uvCoords     = new List<Vector2>();
		List<int> topology     = new List<int>();
		List<int> topologyBack = new List<int>();

		int length = stroke.points.Count;
		int lIdx   = 0;
		Vector3 lastPos = Vector3.zero;
		for (int pIdx = 0; pIdx < length + stepSize; pIdx += stepSize)
		{
			StrokePoint p   = stroke.points[Mathf.Min(pIdx, length - 1)];
			Vector3     pos = p.position;

			// avoid very short (or zero length) segments
			if (stepSize == 1)
			{
				Vector3 delta = pos - lastPos;
				if (delta.magnitude < minimumSegmentSize) continue;
				lastPos = pos;
			}

			// clauclate normal
			Vector3 up  = p.orientation * Vector3.up;
			Vector3 dir = (pIdx > 0) ? (pos - stroke.points[pIdx - stepSize].position) : pos;
			dir.Normalize();
			Vector3 normal = Vector3.Cross(up, dir);

			// add top/bottom vertices for front/back
			up *= p.strokeSize * 0.5f;
			vertices.Add(pos + up);
			vertices.Add(pos + up);
			vertices.Add(pos - up);
			vertices.Add(pos - up);

			// add two normals each for front/back (but not for first point)
			normals.Add( normal);
			normals.Add(-normal);
			normals.Add( normal);
			normals.Add(-normal);

			if (lIdx == 1)
			{
				// add normals for first point = equal to second point
				normals[0] =  normal;
				normals[1] = -normal;
				normals[2] =  normal;
				normals[3] = -normal;
			}

			if (lIdx > 0)
			{
				// more than one point > start building topology
				// Front: 2  6    Back: 3  7
				//        x  x          x  x
				//        0  4          1  5
				int idx = (lIdx + 1) * 4; // two points > idx = 8
				topology.Add(idx - 8); topology.Add(idx - 4); topology.Add(idx - 6); // 0 > 4 > 2
				topology.Add(idx - 6); topology.Add(idx - 4); topology.Add(idx - 2); // 2 > 4 > 6
																						// backface > reverse direction
				topologyBack.Add(idx - 7); topologyBack.Add(idx - 5); topologyBack.Add(idx - 3); // 1 > 3 > 5
				topologyBack.Add(idx - 5); topologyBack.Add(idx - 1); topologyBack.Add(idx - 3); // 3 > 7 > 5
			}
			lIdx++;
		}

		strokeMesh.SetVertices(vertices);
		strokeMesh.SetNormals(normals);
		strokeMesh.SetTriangles(topology, 0);
		strokeMesh.SetTriangles(topologyBack, 1);
		strokeMesh.RecalculateBounds();

		return renderer;
	}

	private Thread     loaderThread;
	private StrokeList strokeList;
	private State      state;
	private int        strokeIndex;
	private string     lastLog;

}
