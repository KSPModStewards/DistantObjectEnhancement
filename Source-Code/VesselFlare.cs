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
		public float luminosity;
		public float brightness;

		public VesselFlare(Vessel vessel, GameObject flarePrefab) : base(flarePrefab, vessel.vesselName, Color.white)
		{
			referenceShip = vessel;

			luminosity = 5.0f + Mathf.Pow(referenceShip.GetTotalMass(), 1.25f);
			brightness = 0.0f;
		}

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

					FlareType flareType = referenceShip.vesselType == VesselType.Debris ? FlareType.Debris : FlareType.Vessel;

					CheckDraw(flareMesh.transform.position, referenceShip.mainBody, FlareDraw.hslWhite, 5.0, flareType);
				}
			}
			catch
			{
				// If anything went whack, let's disable ourselves
				flareMesh.SetActive(false);
				referenceShip = null;
			}
		}
	}
}
