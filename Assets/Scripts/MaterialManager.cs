using UnityEngine;

public class MaterialManager : MonoBehaviour
{
	public Texture2D colourPalette;

	public Material  baseMaterial;

	[Range(4, 256)]
	public int numberOfDistinctColours = 64;


	public static MaterialManager Instance()
	{
		if (_instance == null)
		{
			_instance = GameObject.FindObjectOfType<MaterialManager>();
		}
		return _instance;
	}
	

	protected void CreateMaterialList()
	{
		materials = new Material[numberOfDistinctColours];
		colours   = new Vector3[numberOfDistinctColours];
		for (int idx = 0; idx < materials.Length; idx++)
		{
			Material m = new Material(baseMaterial);
			m.name = "StrokeColour_" + idx;
			m.color = colourPalette.GetPixelBilinear((float)idx / materials.Length, 0.5f);
			materials[idx] = m;
			colours[idx] = new Vector3(m.color.r, m.color.g, m.color.b);
		}
	}


	public Material FindMaterial(Color color)
	{
		if ( materials == null)
		{
			CreateMaterialList();
		}

		int     closestColourIndex = 0;
		float   maxDifference = float.MaxValue;
		Vector3 vecCol = new Vector3(color.r, color.g, color.b);
		for ( int idx = 0; idx < materials.Length; idx++)
		{
			float dist = (vecCol - colours[idx]).magnitude;
			if (dist < maxDifference)
			{
				maxDifference = dist;
				closestColourIndex = idx;
			}
		}

		return materials[closestColourIndex];
	}



	public void Start()
	{
		// trigger instance creation
		Instance();
	}


	void Update ()
	{
		// nothing to do here
	}


	private Vector3[]  colours;
	private Material[] materials;

	static private MaterialManager _instance = null;
}
