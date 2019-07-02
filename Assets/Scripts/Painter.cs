using UnityEngine;
using System.Collections.Generic;
using SentienceLab.Input;
using System.Collections;

public class Painter : MonoBehaviour
{
	public string     actionNamePaint       = "fire";
	public string     actionNameUndo        = "undo";
	public string     actionNameBrushSize   = "brushSize";
	public string     actionNameBrushColour = "brushColour";
	public string     actionNameSave        = "save";
	public string     actionNameReset       = "reset";

	public Transform  brushObject           = null;
	public float      strokeWidthMinimum    = 0.01f;
	public float      strokeWidthMaximum    = 0.10f;
	public float      minimumSegmentSize    = 0.005f;

	public Transform  colourIndicator       = null;
	public Texture2D  colourPalette         = null;
	public float      colourChangeSpeed     = 0.1f;
	
	public Material     strokeBaseMaterial;
	public MeshRenderer colourIndicatorMesh = null;

	public AudioSource  spraySound;
	public AudioSource  undoSound;

	public bool saveOBJ = false;

	/// <summary>
	/// Called at the start of the program.
	/// </summary>
	/// 
	void Start()
	{
		// take care of inputs
		actionPaint  = InputHandler.Find(actionNamePaint);
		actionUndo   = InputHandler.Find(actionNameUndo);
		actionColour = InputHandler.Find(actionNameBrushColour);
		actionSize   = InputHandler.Find(actionNameBrushSize);
		actionSave   = InputHandler.Find(actionNameSave);
		actionReset  = InputHandler.Find(actionNameReset);

		// generate basic objects
		if (brushObject == null)
		{
			brushObject = transform.Find("Brush");
		}
		trailRenderer = GetComponentInChildren<TrailRenderer>();
		sizeFactor    = 0;
		strokeIndex   = 1;

		activeStroke         = null;
		strokeMaterials      = null;
		strokeMaterialIndex  = 1;
		strokeIndicatorTimer = 0;

		strokeList      = new StrokeList();
		strokeContainer = new GameObject("Strokes").transform;
	}


	/// <summary>
	/// Called once per rendered frame.
	/// </summary>
	/// 
	void Update()
	{
		if (actionSave.IsActivated())
		{
			// save to file
			string baseFilename = "StrokeExport_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
			SaveStrokeData(baseFilename);
			Debug.Log("Exported strokes to " + baseFilename);
		}

		if (actionReset.IsActivated())
		{
			// clear all strokes
			ClearStrokes();
		}

		if ((strokeMesh == null) && actionUndo.IsActivated())
		{
			// only allow undo when not painting
			UndoLastStroke();
		}

		// read size/colour buttons
		float sizeChange   = actionSize.GetValue();
		float colourChange = actionColour.GetValue();
		// only allow one of the actions
		if ( Mathf.Abs(sizeChange) > Mathf.Abs(colourChange) )
		{
			colourChange = 0;
		}
		else
		{
			sizeChange = 0;
		}

		// process changing colour
		if (colourChange != 0)
		{
			strokeColourIdx += colourChange * Time.deltaTime * colourChangeSpeed;
			strokeColourIdx  = Mathf.Repeat(strokeColourIdx, 1);
			// force creation of new material
			strokeMaterials = null;
			if (colourIndicator != null)
			{
				colourIndicator.gameObject.SetActive(true);
				Quaternion q = Quaternion.AngleAxis(-strokeColourIdx * 360, Vector3.forward);
				colourIndicator.localRotation = q;
				strokeIndicatorTimer = Time.time + 1; // hide indicator 1s from now
			}
		}
		else
		{
			// hide colour indicator after timeout
			if ((colourIndicator != null) && (Time.time > strokeIndicatorTimer))
			{
				colourIndicator.gameObject.SetActive(false);
			}
		}
		strokeColour = colourPalette.GetPixelBilinear(strokeColourIdx, 0.5f);

		// process size change
		if (sizeChange != 0)
		{
			// change brush size
			sizeFactor += sizeChange * Time.deltaTime;
			sizeFactor = Mathf.Clamp(sizeFactor, 0, 1);
		}
		strokeWidth = Mathf.Lerp(strokeWidthMinimum, strokeWidthMaximum, sizeFactor);

		if (trailRenderer != null)
		{
			trailRenderer.startWidth = strokeWidth;
			foreach (Material m in trailRenderer.materials)
			{
				m.SetColor("_Color", strokeColour);
				m.SetColor("_TintColor", strokeColour);
				m.SetColor("_EmissionColor", strokeColour);
			}
		}
		if (colourIndicatorMesh != null)
		{
			// is there a mesh rendering the active colour?
			colourIndicatorMesh.material.color = strokeColour;
		}

		if (brushObject != null)
		{
			brushObject.localScale = new Vector3(brushObject.localScale.x, strokeWidth, brushObject.localScale.z);
			Renderer renderer = brushObject.GetComponent<Renderer>();
			foreach (Material m in renderer.materials)
			{
				m.SetColor("_Color", strokeColour);
				m.SetColor("_TintColor", strokeColour);
				m.SetColor("_EmissionColor", strokeColour);
			}
		}

		// handle paint button
		bool painting = actionPaint.IsActive();
		if (painting && (strokeMesh == null))
		{
			// start data structure
			activeStroke = new Stroke();
			activeStroke.startTime = Time.time;

			// start new mesh for the stroke and add to common container
			activeStroke.gameObject = new GameObject();
			activeStroke.gameObject.name = string.Format("Stroke_{0:D3}", strokeIndex);
			activeStroke.gameObject.transform.parent = strokeContainer.transform;

			// create new material?
			if (strokeMaterials == null)
			{
				Material mat = Instantiate<Material>(strokeBaseMaterial);
				mat.name  = string.Format("StrokeMaterial_{0:D3}", strokeMaterialIndex);
				mat.color = strokeColour;
				strokeMaterials    = new Material[2];
				strokeMaterials[0] = mat;
				strokeMaterials[1] = mat;
				strokeMaterialIndex++;
			}

			// create renderer
			MeshRenderer renderer = activeStroke.gameObject.AddComponent<MeshRenderer>();
			renderer.receiveShadows    = true;
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
			renderer.materials         = strokeMaterials;

			// create mesh
			strokeMesh = activeStroke.gameObject.AddComponent<MeshFilter>().mesh;
			strokeMesh.MarkDynamic(); // this is going to change a lot
			strokeMesh.subMeshCount = 2;
			strokePoints = new List<Vector3>();
			vertices     = new List<Vector3>();
			normals      = new List<Vector3>();
			//uvCoords     = new List<Vector2>();
			topology     = new List<int>();
			topologyBack = new List<int>();

			lastPoint = new Vector3();
			strokeIndex++;

			if (spraySound != null)
			{
				spraySound.loop = true;
				spraySound.Play();
			}
		}
		else if (!painting && (strokeMesh != null))
		{
			// stop painting
			strokeMesh = null;

			// save last stroke
			activeStroke.endTime = Time.time;
			strokeList.Add(activeStroke);
			activeStroke = null;

			if (spraySound != null)
			{
				spraySound.Stop();
			}
		}

		// stroke is being constructed: handle last point
		if (strokeMesh != null)
		{
			Vector3 tipPos = this.transform.position;
			if (Vector3.Distance(lastPoint, tipPos) > minimumSegmentSize)
			{
				AddPoint(tipPos, this.transform.up);
				lastPoint = tipPos;
			}
			else
			{
				SetLastPoint(tipPos, this.transform.up);
			}
		}

		// record stroke data
		if (activeStroke != null)
		{ 
			StrokePoint p = new StrokePoint();
			p.position     = this.transform.position;
			p.orientation  = this.transform.rotation;
			p.strokeSize   = strokeWidth;
			p.strokeColour = strokeColour;
			p.timestamp    = Time.time;
			activeStroke.points.Add(p);
		}
	}


	private void AddPoint(Vector3 pos, Vector3 up)
	{
		strokePoints.Add(pos);
		int len = strokePoints.Count;

		// calculate stroke direction
		Vector3 dir = (len > 1) ? (pos - strokePoints[len-2]) : pos;
		dir.Normalize();
		// calculate normal
		Vector3 normal = Vector3.Cross(up, dir);

		// add top/bottom vertices for front/back
		up *= strokeWidth * 0.5f;
		vertices.Add(pos + up);
		vertices.Add(pos + up);
		vertices.Add(pos - up);
		vertices.Add(pos - up);
		// add two normals each for front/back
		normals.Add( normal);
		normals.Add(-normal);
		normals.Add( normal);
		normals.Add(-normal);

		strokeMesh.SetVertices(vertices);
		strokeMesh.SetNormals(normals);

		if ( len > 1 )
		{
			// more than one point > start building topology
			// Front: 2  6    Back: 3  7
			//        x  x          x  x
			//        0  4          1  5
			int idx = len * 4; // two points > idx = 8
			topology.Add(idx - 8); topology.Add(idx - 4); topology.Add(idx - 6); // 0 > 4 > 2
			topology.Add(idx - 6); topology.Add(idx - 4); topology.Add(idx - 2); // 2 > 4 > 6
			// backface > reverse direction
			topologyBack.Add(idx - 7); topologyBack.Add(idx - 5); topologyBack.Add(idx - 3); // 1 > 3 > 5
			topologyBack.Add(idx - 5); topologyBack.Add(idx - 1); topologyBack.Add(idx - 3); // 3 > 7 > 5

			strokeMesh.SetTriangles(topology, 0);
			strokeMesh.SetTriangles(topologyBack, 1);
		}
	}


	private void SetLastPoint(Vector3 pos, Vector3 up)
	{
		int len = strokePoints.Count;
		strokePoints[len - 1] = pos; // change only last point

		// calculate direction
		Vector3 dir = (len > 1) ? (pos - strokePoints[len - 2]) : pos;
		dir.Normalize();
		// calculate normal
		Vector3 normal = Vector3.Cross(up, dir);

		int idx = len * 4 - 1;
		// change the last top/bottom vertices
		up *= strokeWidth * 0.5f;
		vertices[idx - 3] = pos + up;
		vertices[idx - 2] = pos + up;
		vertices[idx - 1] = pos - up;
		vertices[idx - 0] = pos - up;
		
		// change the last two normals
		normals[idx - 3] =  normal;
		normals[idx - 2] = -normal;
		normals[idx - 1] =  normal;
		normals[idx - 0] = -normal;

		if (len == 2)
		{
			// correct first pair of vertices now that we know where the line is going
			vertices[0] = strokePoints[0] + up;
			vertices[1] = strokePoints[0] + up;
			vertices[2] = strokePoints[0] - up;
			vertices[3] = strokePoints[0] - up;
			normals[0] =  normal;
			normals[1] = -normal;
			normals[2] =  normal;
			normals[3] = -normal;
		}

		strokeMesh.SetVertices(vertices);
		strokeMesh.SetNormals(normals);
	}


	private void UndoLastStroke()
	{
		if (strokeList.Count > 0)
		{
			Stroke s = strokeList[strokeList.Count - 1];

			// if there is a sound, play it back from the center of the stroke
			if (undoSound != null)
			{
				AudioSource.PlayClipAtPoint(undoSound.clip, s.GetWorldCentre(), undoSound.volume);
			}

			strokeList.Remove(s);
			strokeIndex--;
			Debug.Log("Last stroke undone");

			Destroy(s.gameObject);
		}
	}


	private void ClearStrokes()
	{
		foreach (Stroke s in strokeList)
		{
			Destroy(s.gameObject);
		}
		strokeList.Clear();

		strokeIndex = 1;
		strokeMaterialIndex = 1;
		strokeMaterials = null;

		Debug.Log("All strokes cleared");
	}


	private void SaveStrokeData(string baseFilename)
	{
		if (saveOBJ)
		{
			// save as OBJ
			ObjExporter.SaveMeshes(strokeContainer, baseFilename);
		}
		strokeList.SaveStrokeData(baseFilename);
	}


	private InputHandler actionPaint, actionUndo, actionColour, actionSize, actionSave, actionReset;

	private Transform     strokeContainer;
	private Mesh          strokeMesh;
	private Vector3       lastPoint;

	private Color         strokeColour;
	private float         strokeColourIdx;
    private float         strokeIndicatorTimer;
	private Material[]    strokeMaterials;
	private int           strokeMaterialIndex;

	private TrailRenderer trailRenderer;

	private float         sizeFactor, strokeWidth;
	private List<Vector3> strokePoints;
	private List<Vector3> vertices;
	private List<Vector3> normals;
	//private List<Vector2> uvCoords;
	private List<int>     topology, topologyBack;

	private Stroke        activeStroke;
	private StrokeList    strokeList;

	static private int strokeIndex = 1;
}
