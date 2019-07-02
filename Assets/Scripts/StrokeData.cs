using UnityEngine;
using System.Collections.Generic;
using System.IO;


public struct StrokePoint
{
	public float      timestamp;
	public Vector3    position;
	public Quaternion orientation;
	public float      strokeSize;
	public Color      strokeColour;
}


public class Stroke
{
	public List<StrokePoint> points;
	public float             startTime, endTime;
	public GameObject        gameObject;
	public Vector3           centre;

	public Stroke()
	{
		points = new List<StrokePoint>();
		centre = Vector3.zero;
	}

	public Vector3 GetCentre()
	{
		if (centre.Equals(Vector3.zero))
		{
			// centre hasn't been calculated yet - do it now
			foreach (StrokePoint p in points)
			{
				centre += p.position;
			}
			centre /= points.Count;
		}
		return centre;
	}

	public Vector3 GetWorldCentre()
	{
		return gameObject.transform.localToWorldMatrix * GetCentre();
	}
}


public class StrokeList : List<Stroke>
{
	public Bounds  bounds;
	public Vector3 average;


	public StrokeList()
	{
		bounds = new Bounds();
	}


	public bool LoadStrokeData(string baseFilename)
	{
		bool success = false;
		
		if (!baseFilename.Contains(".csv"))
		{
			baseFilename = baseFilename + ".csv";
		}

		using (StreamReader sr = new StreamReader(baseFilename))
		{
			// read header
			string header = sr.ReadLine();
			success = header.Equals("time,strokeIdx,pointIdx,posX,posY,posZ,rotX,rotY,rotZ,rotW,width,colR,colG,colB,colA");

			if (success)
			{
				int    strokeIdx = 0;
				string line;
				Stroke stroke = null;
				average = Vector3.zero;
				int pointCount = 0;
				this.Clear();

				while ((line = sr.ReadLine()) != null)
				{
					string[] parts = line.Split(',');
					// check stroke index
					int sIdx = int.Parse(parts[1]);
					if (sIdx != strokeIdx)
					{
						// new stroke starts
						if (stroke != null)
						{
							stroke.startTime = stroke.points[0].timestamp;
							stroke.endTime   = stroke.points[stroke.points.Count - 1].timestamp;
							this.Add(stroke);
						}
						strokeIdx = sIdx;
						stroke    = new Stroke();
					}

					// read point data
					StrokePoint p = new StrokePoint();
					p.timestamp = float.Parse(parts[0]);
					// [1] stroke idx
					// [2] point idx
					p.position.x     = float.Parse(parts[3]);
					p.position.y     = float.Parse(parts[4]);
					p.position.z     = float.Parse(parts[5]);
					p.orientation.x  = float.Parse(parts[6]);
					p.orientation.y  = float.Parse(parts[7]);
					p.orientation.z  = float.Parse(parts[8]);
					p.orientation.w  = float.Parse(parts[9]);
					p.strokeSize     = float.Parse(parts[10]);
					p.strokeColour.r = float.Parse(parts[11]);
					p.strokeColour.g = float.Parse(parts[12]);
					p.strokeColour.b = float.Parse(parts[13]);
					p.strokeColour.a = float.Parse(parts[14]);
					stroke.points.Add(p);

					bounds.Encapsulate(p.position);
					average += p.position;
					pointCount++;
				}

				// add last stroke
				if (stroke != null)
				{
					stroke.startTime = stroke.points[0].timestamp;
					stroke.endTime = stroke.points[stroke.points.Count - 1].timestamp;
					this.Add(stroke);
				}

				average /= pointCount;
			}
		}
		return success;
	}


	public void SaveStrokeData(string baseFilename)
	{
		// save pure data
		using (StreamWriter sw = new StreamWriter(baseFilename + ".csv"))
		{
			// write header
			sw.WriteLine("time,strokeIdx,pointIdx,posX,posY,posZ,rotX,rotY,rotZ,rotW,width,colR,colG,colB,colA");
			int strokeIdx = 1;
			foreach (Stroke stroke in this)
			{
				int pointIdx = 1;
				foreach (StrokePoint p in stroke.points)
				{
					sw.WriteLine(
						p.timestamp + "," + strokeIdx + "," + pointIdx + "," +
						p.position.x + "," + p.position.y + "," + p.position.z + "," +
						p.orientation.x + "," + p.orientation.y + "," + p.orientation.z + "," + p.orientation.w + "," +
						p.strokeSize + "," +
						p.strokeColour.r + "," + p.strokeColour.g + "," + p.strokeColour.b + "," + p.strokeColour.a
						);
					pointIdx++;
				}
				strokeIdx++;
			}
		}
	}

}
	