using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;


public class Loader : MonoBehaviour
{
	public string basePath     = "./Drawings";
	public int    numberOfLODs = 1;

	/// <summary>
	/// Reads the list of drawing files and starts the loading process.
	/// </summary>
	/// 
	public void Start()
	{
		List<string> files = new List<string>();
		files.AddRange(Directory.GetFiles(basePath, "*.csv"));
		List<Drawer> drawings = new List<Drawer>();

		foreach (string file in files)
		{
			if (file.Contains(".meta")) continue;

			// prepare object name
			string name = file.Replace(basePath, "");
			name = name.Replace("\\", "").Replace(".csv", "");

			GameObject go = new GameObject(name);
			go.transform.parent = this.transform;
			Drawer drawer = go.AddComponent<Drawer>();
			drawer.baseFilename = file;
			drawer.numberOfLODs = numberOfLODs;

			drawings.Add(drawer);
		}

		for (int dIdx = 0; dIdx < drawings.Count; dIdx++)
		{
			Transform t = drawings[dIdx].transform;
			Drawer    d = t.GetComponent<Drawer>();
			StartCoroutine(StartLoading(d, dIdx));
		}
	}


	protected IEnumerator StartLoading(Drawer d, int delay)
	{
		yield return new WaitForSeconds(delay);
		d.StartLoading();
	}


	public void Update()
	{
		// nothing to do here
	}
}
