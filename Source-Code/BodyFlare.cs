using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DistantObject
{
	// @ 1920x1080, 1 pixel with 60* FoV covers about 2 minutes of arc / 0.03 degrees
	class BodyFlare : FlareBase
	{
		public static double kerbinSMA = -1.0;
		public static double kerbinRadius;

		// Scale body flare distance to try to ameliorate z-fighting of moons.
		public static double bodyFlareDistanceScalar = 0.0;

		public static readonly double MinFlareDistance = 739760.0;
		public static readonly double MaxFlareDistance = 750000.0;
		public static readonly double FlareDistanceRange = MaxFlareDistance - MinFlareDistance;

		public CelestialBody body;
		public Renderer scaledRenderer;
		public Color color;
		public Vector4 hslColor;
		public Vector3d cameraToBodyUnitVector;
		public double distanceFromCamera;
		public double sizeInDegrees;

		public double relativeRadiusSquared;
		public double bodyRadiusSquared;

		public BodyFlare(CelestialBody body, GameObject flarePrefab, Color flareColor) : base(flarePrefab, body.bodyName, flareColor)
		{
			scaledRenderer = body.MapObject.transform.GetComponent<Renderer>();

			this.body = body;
			color = flareColor;
			hslColor = Utility.RGB2HSL(flareColor);
			relativeRadiusSquared = Math.Pow(body.Radius / FlightGlobals.Bodies[1].Radius, 2.0);
			bodyRadiusSquared = body.Radius * body.Radius;
		}

		public void Update(Vector3d camPos, float camFOV)
		{
			// Update Body Flare
			Vector3d targetVectorToSun = FlightGlobals.Bodies[0].position - body.position;
			Vector3d targetVectorToCam = camPos - body.position;

			double targetSunRelAngle = Vector3d.Angle(targetVectorToSun, targetVectorToCam);

			cameraToBodyUnitVector = -targetVectorToCam.normalized;
			distanceFromCamera = targetVectorToCam.magnitude;

			double kerbinSMAOverBodyDist = kerbinSMA / targetVectorToSun.magnitude;
			double luminosity = kerbinSMAOverBodyDist * kerbinSMAOverBodyDist * relativeRadiusSquared;
			luminosity *= (0.5 + (32400.0 - targetSunRelAngle * targetSunRelAngle) / 64800.0);
			luminosity = (Math.Log10(luminosity) + 1.5) * (-2.0);

			// We need to clamp this value to remain < 5, since larger values cause a negative resizeVector.
			// This only appears to happen with some mod-generated worlds, but it's still a good practice
			// and not terribly expensive.
			float brightness = Math.Min(4.99f, (float)(luminosity + Math.Log10(distanceFromCamera / kerbinSMA)));

			//position, rotate, and scale mesh
			targetVectorToCam = ((MinFlareDistance + Math.Min(FlareDistanceRange, distanceFromCamera * bodyFlareDistanceScalar)) * targetVectorToCam.normalized);
			flareMesh.transform.position = camPos - targetVectorToCam;
			flareMesh.transform.LookAt(camPos);

			float resizeFactor = (-750.0f * (brightness - 5.0f) * (0.7f + .99f * camFOV) / 70.0f) * DistantObjectSettings.DistantFlare.flareSize;
			flareMesh.transform.localScale = new Vector3(resizeFactor, resizeFactor, resizeFactor);

			sizeInDegrees = Math.Acos(Math.Sqrt(distanceFromCamera * distanceFromCamera - bodyRadiusSquared) / distanceFromCamera) * Mathf.Rad2Deg;

			Visible = !(scaledRenderer.enabled && scaledRenderer.isVisible) && DistantObjectSettings.DistantFlare.flaresEnabled && !MapView.MapIsEnabled;

			// Disable the mesh if the scaledRenderer is enabled and visible.
			flareMesh.SetActive(Visible);

			CheckDraw(body.transform.position, body.referenceBody, hslColor, sizeInDegrees, FlareType.Celestial);
		}

		public override void Destroy()
		{
			base.Destroy();

			scaledRenderer = null;
		}
	}
}
