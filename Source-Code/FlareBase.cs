using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DistantObject
{
	enum FlareType
	{
		Celestial,
		Vessel,
		Debris
	}

	internal class FlareBase
	{
		public GameObject flareMesh;
		public MeshRenderer meshRenderer;
		protected Transform flareTransform;
		Material flareMaterial;

		public bool Visible;

		protected FlareBase(GameObject flarePrefab, string name, Color color)
		{
			flareMesh = GameObject.Instantiate(flarePrefab);
			flareTransform = flareMesh.transform;

			meshRenderer = flareMesh.GetComponentInChildren<MeshRenderer>();

			// MOARdV: valerian recommended moving vessel and body flares to
			// layer 10, but that behaves poorly for nearby / co-orbital objects.
			// Move vessels back to layer 0 until I can find a better place to
			// put it.
			// Renderer layers: http://wiki.kerbalspaceprogram.com/wiki/API:Layers

			// With KSP 1.0, putting these on layer 10 introduces 
			// ghost flares that render for a while before fading away.
			// These flares were moved to 10 because of an
			// interaction with PlanetShine.  However, I don't see
			// that problem any longer (where flares changed brightness
			// during sunrise / sunset).  Valerian proposes instead using 15.
			meshRenderer.gameObject.layer = 15;
			meshRenderer.material.shader = Shader.Find("KSP/Alpha/Unlit Transparent");
			meshRenderer.material.color = color;
			meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			meshRenderer.receiveShadows = false;
			flareMaterial = meshRenderer.material;

			flareMesh.SetActive(DistantObjectSettings.DistantFlare.flaresEnabled);
		}

		protected void CheckDraw(Vector3d position, CelestialBody referenceBody, Vector4 hslColor, double objRadius, FlareType flareType)
		{
			Vector3d targetVectorToSun = FlightGlobals.Bodies[0].position - position;
			Vector3d targetVectorToRef = referenceBody.position - position;
			double targetRelAngle = Vector3d.Angle(targetVectorToSun, targetVectorToRef);
			double targetDist = Vector3d.Distance(position, FlareDraw.camPos);
			double targetSize;
			if (flareType == FlareType.Celestial)
			{
				targetSize = objRadius;
			}
			else
			{
				targetSize = Math.Atan2(objRadius, targetDist) * Mathf.Rad2Deg;
			}
			double targetRefDist = Vector3d.Distance(position, referenceBody.position);
			double targetRefSize = Math.Acos(Math.Sqrt(Math.Pow(targetRefDist, 2.0) - Math.Pow(referenceBody.Radius, 2.0)) / targetRefDist) * Mathf.Rad2Deg;

			bool inShadow = false;
			if (referenceBody != FlightGlobals.Bodies[0] && targetRelAngle < targetRefSize)
			{
				inShadow = true;
			}

			if (inShadow)
			{
				Visible = false;
			}
			else
			{
				Visible = true;

				// See if the sun obscures our target
				if (FlareDraw.sunDistanceFromCamera < targetDist && FlareDraw.sunSizeInDegrees > targetSize && Vector3d.Angle(FlareDraw.cameraToSunUnitVector, position - FlareDraw.camPos) < FlareDraw.sunSizeInDegrees)
				{
					Visible = false;
				}
			}

			if (targetSize < (FlareDraw.camFOV / 500.0f) && Visible && !MapView.MapIsEnabled)
			{
				// Work in HSL space.  That allows us to do dimming of color
				// by adjusting the lightness value without any hue shifting.
				// We apply atmospheric dimming using alpha.  Although maybe
				// I don't need to - it could be done by dimming, too.
				float alpha = hslColor.w;
				float dimming = 1.0f;
				alpha *= FlareDraw.atmosphereFactor;
				dimming *= FlareDraw.dimFactor;
				if (targetSize > (FlareDraw.camFOV / 1000.0f))
				{
					dimming *= (float)(((FlareDraw.camFOV / targetSize) / 500.0) - 1.0);
				}
				if (flareType == FlareType.Debris && DistantObjectSettings.DistantFlare.debrisBrightness < 1.0f)
				{
					dimming *= DistantObjectSettings.DistantFlare.debrisBrightness;
				}
				// Uncomment this to help with debugging
				//alpha = 1.0f;
				//dimming = 1.0f;
				flareMaterial.color = ResourceUtilities.HSL2RGB(hslColor.x, hslColor.y, hslColor.z * dimming, alpha);
				Visible = Visible && alpha > 0;
			}
			else
			{
				Visible = false;
			}

			flareMesh.SetActive(Visible);
		}

		public virtual void Destroy()
		{
			if (flareMaterial != null)
			{
				GameObject.Destroy(flareMaterial);
				flareMaterial = null;
			}
			if (flareMesh != null)
			{
				GameObject.Destroy(flareMesh);
				flareMesh = null;
			}

			flareTransform = null;
		}
	}
}
