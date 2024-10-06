using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DistantObject
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
    public class FlareDraw : MonoBehaviour
    {
        private List<BodyFlare> bodyFlares = new List<BodyFlare>();
        private Dictionary<Vessel, VesselFlare> vesselFlares = new Dictionary<Vessel, VesselFlare>();

        internal static float camFOV;
        internal static Vector3d camPos;
        internal static float atmosphereFactor = 1.0f;
        internal static float dimFactor = 1.0f;

        // Track the variables relevant to determine whether the sun is
        // occluding a body flare.
        internal static double sunDistanceFromCamera = 1.0;
        internal static double sunSizeInDegrees = 1.0;
        private double sunRadiusSquared;
        internal static Vector3d cameraToSunUnitVector = Vector3d.zero;

        private static bool ExternalControl = false;

        Vessel.Situations situations = (Vessel.Situations)0;

        private string showNameString = null;
        private Transform showNameTransform = null;
        private Color showNameColor;
        static internal readonly Vector4 hslWhite = Utility.RGB2HSL(Color.white);

        // If something goes wrong (say, because another mod does something bad
        // that screws up vessels without us seeing the normal "vessel destroyed"
        // callback, we can see exceptions in Update.  If that happens, we use
        // the bigHammer to rebuild our vessel flare table outright.
        private bool bigHammer = false;
        private List<Vessel> deadVessels = new List<Vessel>();

        GameObject flarePrefab;

        //--------------------------------------------------------------------
        // AddVesselFlare
        // Add a new vessel flare to our library
        private void AddVesselFlare(Vessel referenceShip)
        {
            VesselFlare vesselFlare = new VesselFlare(referenceShip, flarePrefab);
            vesselFlares.Add(referenceShip, vesselFlare);
        }

        //private void ListChildren(PSystemBody body, int idx)
        //{
        //    StringBuilder sb = new StringBuilder();
        //    for(int i=0; i< idx; ++i) sb.Append("  ");
        //    sb.Append("Body ");
        //    sb.Append(body.celestialBody.name);
        //    Debug.Log(sb.ToString());
        //    for(int i=0; i<body.children.Count; ++i)
        //    {
        //        ListChildren(body.children[i], idx + 1);
        //    }
        //}

        //--------------------------------------------------------------------
        // GenerateBodyFlares
        // Iterate over the celestial bodies and generate flares for each of
        // them.  Add the flare info to the dictionary.
        private void GenerateBodyFlares()
        {
            // If Kerbin is parented to the Sun, set its SMA - otherwise iterate
            // through celestial bodies to locate which is parented to the Sun
            // and has Kerbin as a child. Set the highest parent's SMA to kerbinSMA.
            if (BodyFlare.kerbinSMA <= 0.0)
            {
                if (FlightGlobals.Bodies[1].referenceBody == FlightGlobals.Bodies[0])
                {
                    BodyFlare.kerbinSMA = FlightGlobals.Bodies[1].orbit.semiMajorAxis;
                }
                else
                {
                    foreach (CelestialBody current in FlightGlobals.Bodies)
                    {
                        if (current != FlightGlobals.Bodies[0])
                        {
                            if (current.referenceBody == FlightGlobals.Bodies[0] && current.HasChild(FlightGlobals.Bodies[1]))
                            {
                                BodyFlare.kerbinSMA = current.orbit.semiMajorAxis;
                            }
                        }
                    }

                    if (BodyFlare.kerbinSMA <= 0.0)
                    {
                        throw new Exception("Distant Object -- Unable to find Kerbin's relationship to Kerbol.");
                    }
                }

                BodyFlare.kerbinRadius = FlightGlobals.Bodies[1].Radius;
            }
            bodyFlares.Clear();

            Dictionary<CelestialBody, Color> bodyColors = new Dictionary<CelestialBody, Color>();
            foreach (UrlDir.UrlConfig node in GameDatabase.Instance.GetConfigs("CelestialBodyColor"))
            {
                CelestialBody body = FlightGlobals.Bodies.Find(n => n.name == node.config.GetValue("name"));
                if (FlightGlobals.Bodies.Contains(body))
                {
                    Color color = ConfigNode.ParseColor(node.config.GetValue("color"));
                    color.r = 1.0f - (DistantObjectSettings.DistantFlare.flareSaturation * (1.0f - (color.r / 255.0f)));
                    color.g = 1.0f - (DistantObjectSettings.DistantFlare.flareSaturation * (1.0f - (color.g / 255.0f)));
                    color.b = 1.0f - (DistantObjectSettings.DistantFlare.flareSaturation * (1.0f - (color.b / 255.0f)));
                    color.a = 1.0f;
                    if (!bodyColors.ContainsKey(body))
                    {
                        bodyColors.Add(body, color);
                    }
                }
            }

            double largestSMA = 0.0;
            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (body != FlightGlobals.Bodies[0] && body?.MapObject != null)
                {
                    largestSMA = Math.Max(largestSMA, body.orbit.semiMajorAxis);

                    if (!bodyColors.TryGetValue(body, out Color bodyColor))
                    {
                        bodyColor = Color.white;
                    }

                    BodyFlare bf = new BodyFlare(body, flarePrefab, bodyColor);

                    bodyFlares.Add(bf);
                }
            }
            BodyFlare.bodyFlareDistanceScalar = BodyFlare.FlareDistanceRange / largestSMA;
        }

        //--------------------------------------------------------------------
        // GenerateVesselFlares
        // Iterate over the vessels, adding and removing flares as appropriate
        private void GenerateVesselFlares()
        {
            // See if there are vessels that need to be removed from our live
            // list
            foreach (var v in vesselFlares)
            {
                if (v.Key.orbit.referenceBody != FlightGlobals.ActiveVessel.orbit.referenceBody || v.Key.loaded == true || !AllowedSituation(v.Key.situation) || v.Value.referenceShip == null)
                {
                    deadVessels.Add(v.Key);
                }
            }

            for (int v = 0; v < deadVessels.Count; ++v)
            {
                RemoveVesselFlare(deadVessels[v]);
            }
            deadVessels.Clear();

            // See which vessels we should add
            for (int i = 0; i < FlightGlobals.Vessels.Count; ++i)
            {
                Vessel vessel = FlightGlobals.Vessels[i];
                if (vessel.orbit.referenceBody == FlightGlobals.ActiveVessel.orbit.referenceBody && !vesselFlares.ContainsKey(vessel) && RenderableVesselType(vessel.vesselType) && !vessel.loaded && AllowedSituation(vessel.situation))
                {
                    AddVesselFlare(vessel);
                }
            }
        }

        //--------------------------------------------------------------------
        // RenderableVesselType
        // Indicates whether the specified vessel type is one we will render
        private bool RenderableVesselType(VesselType vesselType)
        {
            return !(vesselType == VesselType.Flag || vesselType == VesselType.EVA || (vesselType == VesselType.Debris && DistantObjectSettings.DistantFlare.ignoreDebrisFlare));
        }

		private bool AllowedSituation(Vessel.Situations situation)
		{
			return (situation & situations) != (Vessel.Situations)0;
		}

		//--------------------------------------------------------------------
		// UpdateVar()
		// Update atmosphereFactor and dimFactor
		private void UpdateVar()
        {
            Vector3d sunBodyAngle = (FlightGlobals.Bodies[0].position - camPos).normalized;
            double sunBodyDist = FlightGlobals.Bodies[0].GetAltitude(camPos) + FlightGlobals.Bodies[0].Radius;
            double sunBodySize = Math.Acos(Math.Sqrt(Math.Pow(sunBodyDist, 2.0) - Math.Pow(FlightGlobals.Bodies[0].Radius, 2.0)) / sunBodyDist) * Mathf.Rad2Deg;

            atmosphereFactor = 1.0f;

            if (FlightGlobals.currentMainBody != null && FlightGlobals.currentMainBody.atmosphere)
            {
                double camAltitude = FlightGlobals.currentMainBody.GetAltitude(camPos);
                double atmAltitude = FlightGlobals.currentMainBody.atmosphereDepth;
                double atmCurrentBrightness = (Vector3d.Distance(camPos, FlightGlobals.Bodies[0].position) - Vector3d.Distance(FlightGlobals.currentMainBody.position, FlightGlobals.Bodies[0].position)) / (FlightGlobals.currentMainBody.Radius);

                if (camAltitude > (atmAltitude / 2.0) || atmCurrentBrightness > 0.15)
                {
                    atmosphereFactor = 1.0f;
                }
                else if (camAltitude < (atmAltitude / 10.0) && atmCurrentBrightness < 0.05)
                {
                    atmosphereFactor = 0.0f;
                }
                else
                {
                    if (camAltitude < (atmAltitude / 2.0) && camAltitude > (atmAltitude / 10.0) && atmCurrentBrightness < 0.15)
                    {
                        atmosphereFactor *= (float)((camAltitude - (atmAltitude / 10.0)) / (atmAltitude - (atmAltitude / 10.0)));
                    }
                    if (atmCurrentBrightness < 0.15 && atmCurrentBrightness > 0.05 && camAltitude < (atmAltitude / 2.0))
                    {
                        atmosphereFactor *= (float)((atmCurrentBrightness - 0.05) / (0.10));
                    }
                    if (atmosphereFactor > 1.0f)
                    {
                        atmosphereFactor = 1.0f;
                    }
                }
                // atmDensityASL isn't an exact match for atmosphereMultiplier from KSP 0.90, I think, but it
                // provides a '1' for Kerbin (1.2, actually)
                float atmThickness = (float)Math.Min(Math.Sqrt(FlightGlobals.currentMainBody.atmDensityASL), 1);
                atmosphereFactor = (atmThickness) * (atmosphereFactor) + (1.0f - atmThickness);
            }

            float sunDimFactor = 1.0f;
            float skyboxDimFactor;
            if (DistantObjectSettings.SkyboxBrightness.changeSkybox == true)
            {
                // Apply fudge factors here so people who turn off the skybox don't turn off the flares, too.
                // And avoid a divide-by-zero.
                skyboxDimFactor = Mathf.Max(0.5f, GalaxyCubeControl.Instance.maxGalaxyColor.r / Mathf.Max(0.0078125f, DistantObjectSettings.SkyboxBrightness.maxBrightness));
            }
            else
            {
                skyboxDimFactor = 1.0f;
            }

            // This code applies a fudge factor to flare dimming based on the
            // angle between the camera and the sun.  We need to do this because
            // KSP's sun dimming effect is not applied to maxGalaxyColor, so we
            // really don't know how much dimming is being done.
            float angCamToSun = Vector3.Angle(FlightCamera.fetch.mainCamera.transform.forward, sunBodyAngle);
            if (angCamToSun < (camFOV * 0.5f))
            {
                bool isVisible = true;
                for (int i = 0; i < bodyFlares.Count; ++i)
                {
                    if (bodyFlares[i].distanceFromCamera < sunBodyDist && bodyFlares[i].sizeInDegrees > sunBodySize && Vector3d.Angle(bodyFlares[i].cameraToBodyUnitVector, FlightGlobals.Bodies[0].position - camPos) < bodyFlares[i].sizeInDegrees)
                    {
                        isVisible = false;
                        break;
                    }
                }
                if (isVisible)
                {
                    // Apply an arbitrary minimum value - the (x^4) function
                    // isn't right, but it does okay on its own.
                    float sunDimming = Mathf.Max(0.2f, Mathf.Pow(angCamToSun / (camFOV * 0.5f), 4.0f));
                    sunDimFactor *= sunDimming;
                }
            }
            dimFactor = DistantObjectSettings.DistantFlare.flareBrightness * Mathf.Min(skyboxDimFactor, sunDimFactor);
        }

        //--------------------------------------------------------------------
        // UpdateNameShown
        // Update the mousever name (if applicable)
        private void UpdateNameShown()
        {
            showNameTransform = null;
            if (DistantObjectSettings.DistantFlare.showNames)
            {
                Ray mouseRay = FlightCamera.fetch.mainCamera.ScreenPointToRay(Input.mousePosition);

                // Detect CelestialBody mouseovers
                double bestRadius = -1.0;
                foreach (BodyFlare bodyFlare in bodyFlares)
                {
                    if (bodyFlare.body == FlightGlobals.ActiveVessel.mainBody)
                    {
                        continue;
                    }

                    if (bodyFlare.meshRenderer.material.color.a > 0.0f)
                    {
                        Vector3d vectorToBody = bodyFlare.body.position - mouseRay.origin;
                        double mouseBodyAngle = Vector3d.Angle(vectorToBody, mouseRay.direction);
                        if (mouseBodyAngle < 1.0)
                        {
                            if (bodyFlare.body.Radius > bestRadius)
                            {
                                double distance = Vector3d.Distance(FlightCamera.fetch.mainCamera.transform.position, bodyFlare.body.position);
                                double angularSize = Mathf.Rad2Deg * bodyFlare.body.Radius / distance;
                                if (angularSize < 0.2)
                                {
                                    bestRadius = bodyFlare.body.Radius;
                                    showNameTransform = bodyFlare.body.transform;
                                    showNameString = KSP.Localization.Localizer.Format("<<1>>", bodyFlare.body.bodyDisplayName);
                                    showNameColor = bodyFlare.color;
                                }
                            }
                        }
                    }
                }

                if (showNameTransform == null)
                {
                    // Detect Vessel mouseovers
                    float bestBrightness = 0.01f; // min luminosity to show vessel name
                    foreach (VesselFlare vesselFlare in vesselFlares.Values)
                    {
                        if (vesselFlare.flareMesh.activeSelf && vesselFlare.meshRenderer.material.color.a > 0.0f)
                        {
                            Vector3d vectorToVessel = vesselFlare.referenceShip.transform.position - mouseRay.origin;
                            double mouseVesselAngle = Vector3d.Angle(vectorToVessel, mouseRay.direction);
                            if (mouseVesselAngle < 1.0)
                            {
                                float brightness = vesselFlare.brightness;
                                if (brightness > bestBrightness)
                                {
                                    bestBrightness = brightness;
                                    showNameTransform = vesselFlare.referenceShip.transform;
                                    showNameString = vesselFlare.referenceShip.vesselName;
                                    showNameColor = Color.white;
                                }
                            }
                        }
                    }
                }
            }
        }

        //--------------------------------------------------------------------
        // Awake()
        // Load configs, set up the callback, 
        private void Awake()
        {
            DistantObjectSettings.LoadConfig();

			// DistantObject/Flare/model has extents of (0.5, 0.5, 0.0), a 1/2 meter wide square.
			flarePrefab = GameDatabase.Instance.GetModel("DistantObject/Flare/model");
			GameObject.Destroy(flarePrefab.GetComponent<Collider>());

            string[] situationStrings = DistantObjectSettings.DistantFlare.situations.Split(',');

            foreach (string sit in situationStrings)
            {
                if (Enum.TryParse(sit, out Vessel.Situations situation))
                {
                    situations |= situation;
                }
                else
                {
                    UnityEngine.Debug.LogWarning(Constants.DistantObject + " -- Unable to find situation '" + sit + "' in my known situations atlas");
                }
            }

            if (DistantObjectSettings.DistantFlare.flaresEnabled)
            {
                UnityEngine.Debug.Log(Constants.DistantObject + " -- FlareDraw enabled");
            }
            else
            {
                UnityEngine.Debug.Log(Constants.DistantObject + " -- FlareDraw disabled");
            }

            sunRadiusSquared = FlightGlobals.Bodies[0].Radius * FlightGlobals.Bodies[0].Radius;
            GenerateBodyFlares();

            // Remove Vessels from our dictionaries just before they are destroyed.
            // After they are destroyed they are == null and this confuses Dictionary.
            GameEvents.onVesselWillDestroy.Add(RemoveVesselFlare);
        }

        //--------------------------------------------------------------------
        // OnDestroy()
        // Clean up after ourselves.
        private void OnDestroy()
        {
            GameEvents.onVesselWillDestroy.Remove(RemoveVesselFlare);
            foreach (VesselFlare v in vesselFlares.Values)
            {
                v.Destroy();
            }
            vesselFlares.Clear();

            foreach (BodyFlare b in bodyFlares)
            {
                b.Destroy();
            }
            bodyFlares.Clear();
        }

        //--------------------------------------------------------------------
        // RemoveVesselFlare
        // Removes a flare (either because a vessel was destroyed, or it's no
        // longer supposed to be part of the draw list).
        private void RemoveVesselFlare(Vessel v)
        {
            if (vesselFlares.TryGetValue(v, out var vesselFlare))
            {
                vesselFlare.Destroy();
                vesselFlares.Remove(v);
            }
        }

        //--------------------------------------------------------------------
        // FixedUpdate
        // Update visible vessel list
        public void FixedUpdate()
        {
            if (DistantObjectSettings.debugMode)
            {
                UnityEngine.Debug.Log(Constants.DistantObject + " -- FixedUpdate");
            }

            if (DistantObjectSettings.DistantFlare.flaresEnabled && !MapView.MapIsEnabled)
            {
                if (bigHammer)
                {
                    foreach (VesselFlare v in vesselFlares.Values)
                    {
                        v.Destroy();
                    }
                    vesselFlares.Clear();
                    bigHammer = false;
                }

                // MOARdV TODO: Make this callback-based instead of polling
                GenerateVesselFlares();
            }
            else if (!DistantObjectSettings.DistantFlare.flaresEnabled)
            {
                if (vesselFlares.Count > 0)
                {
                    foreach (VesselFlare v in vesselFlares.Values)
                    {
                        v.Destroy();
                    }
                    vesselFlares.Clear();
                }
            }
        }

        //--------------------------------------------------------------------
        // Update
        // Update flare positions and visibility
        private void Update()
        {
            showNameTransform = null;
            if (DistantObjectSettings.DistantFlare.flaresEnabled)
            {
                if (MapView.MapIsEnabled)
                {
                    // Big Hammer for map view - don't draw any flares
                    foreach (VesselFlare vesselFlare in vesselFlares.Values)
                    {
                        vesselFlare.flareMesh.SetActive(false);
                    }
                }
                else
                {
                    camPos = FlightCamera.fetch.mainCamera.transform.position;

                    Vector3d targetVectorToCam = camPos - FlightGlobals.Bodies[0].position;

                    cameraToSunUnitVector = -targetVectorToCam.normalized;
                    sunDistanceFromCamera = targetVectorToCam.magnitude;
                    sunSizeInDegrees = Math.Acos(Math.Sqrt(sunDistanceFromCamera * sunDistanceFromCamera - sunRadiusSquared) / sunDistanceFromCamera) * Mathf.Rad2Deg;

                    if (!ExternalControl)
                    {
                        camFOV = FlightCamera.fetch.mainCamera.fieldOfView;
                    }

                    if (DistantObjectSettings.debugMode)
                    {
                        UnityEngine.Debug.Log(Constants.DistantObject + " -- Update");
                    }

                    foreach (BodyFlare flare in bodyFlares)
                    {
                        flare.Update(camPos, camFOV);
                    }

                    UpdateVar();

                    foreach (VesselFlare vesselFlare in vesselFlares.Values)
                    {
                        try
                        {
                            vesselFlare.Update(camPos, camFOV);
                        }
                        catch
                        {
                            // Something went drastically wrong.
                            bigHammer = true;
                        }
                    }

                    UpdateNameShown();
                }
            }
        }

        private GUIStyle flyoverTextStyle = new GUIStyle();
        private Rect flyoverTextPosition = new Rect(0.0f, 0.0f, 100.0f, 20.0f);

        //--------------------------------------------------------------------
        // OnGUI
        // Draws flare names when enabled
        private void OnGUI()
        {
            if (DistantObjectSettings.DistantFlare.flaresEnabled && DistantObjectSettings.DistantFlare.showNames && !MapView.MapIsEnabled && showNameTransform != null)
            {
                Vector3 screenPos = FlightCamera.fetch.mainCamera.WorldToScreenPoint(showNameTransform.position);
                flyoverTextPosition.x = screenPos.x;
                flyoverTextPosition.y = Screen.height - screenPos.y - 20.0f;
                flyoverTextStyle.normal.textColor = showNameColor;
                GUI.Label(flyoverTextPosition, showNameString, flyoverTextStyle);
            }
        }

        //--------------------------------------------------------------------
        // SetFOV
        // Provides an external plugin the opportunity to set the FoV.
        public static void SetFOV(float FOV)
        {
            if (ExternalControl)
            {
                camFOV = FOV;
            }
        }

        //--------------------------------------------------------------------
        // SetExternalFOVControl
        // Used to indicate whether an external plugin wants to control the
        // field of view.
        public static void SetExternalFOVControl(bool Control)
        {
            ExternalControl = Control;
        }
    }
}
