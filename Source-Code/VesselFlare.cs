using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DistantObject
{
	class VesselFlare : FlareBase
	{
		public Vessel referenceShip;
		public GameObject flareMesh;
		public MeshRenderer meshRenderer;
		public float luminosity;
		public float brightness;

		public void Update(Vector3d camPos, float camFOV)
		{
			try
			{
				Vector3d targetVectorToCam = camPos - referenceShip.transform.position;
				float targetDist = (float)Vector3d.Distance(referenceShip.transform.position, camPos);
				bool activeSelf = flareMesh.activeSelf;
				if (targetDist > 750000.0f && activeSelf)
				{
					flareMesh.SetActive(false);
					activeSelf = false;
				}
				else if (targetDist < 750000.0f && !activeSelf)
				{
					flareMesh.SetActive(true);
					activeSelf = true;
				}

				if (activeSelf)
				{
					brightness = Mathf.Log10(luminosity) * (1.0f - Mathf.Pow(targetDist / 750000.0f, 1.25f));

					flareMesh.transform.position = camPos - targetDist * targetVectorToCam.normalized;
					flareMesh.transform.LookAt(camPos);
					float resizeFactor = (0.002f * targetDist * brightness * (0.7f + .99f * camFOV) / 70.0f) * DistantObjectSettings.DistantFlare.flareSize;

					flareMesh.transform.localScale = new Vector3(resizeFactor, resizeFactor, resizeFactor);
					//Debug.Log(string.Format("Resizing vessel flare {0} to {1} - brightness {2}, luminosity {3}", referenceShip.vesselName, resizeFactor, brightness, luminosity));
				}
			}
			catch
			{
				// If anything went whack, let's disable ourselves
				flareMesh.SetActive(false);
				referenceShip = null;
			}
		}

		~VesselFlare()
		{
			// Why is this never called?
			//Debug.Log(Constants.DistantObject + string.Format(" -- VesselFlare {0} Destroy", (referenceShip != null) ? referenceShip.vesselName : "(null vessel?)"));
		}
	}
}
