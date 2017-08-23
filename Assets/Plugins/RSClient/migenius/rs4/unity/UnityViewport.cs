﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using com.migenius.rs4.core;
using com.migenius.rs4.viewport;
using com.migenius.rs4.unity;
using com.migenius.rs4.math;

namespace com.migenius.rs4.unity
{
    public class UnityViewport : MonoBehaviour
    {
        // This event is for other components to be able to add their event listeners
        // on start up when the viewport hasn't been created yet.
        public event ApplicationInitialisingCallback OnAppIniting;

        public RSViewport Viewport { get; protected set; }
        public RSScene Scene { get; protected set; }
        public RSService Service
        {
            get
            {
                if (Scene != null)
                {
                    return Scene.Service;
                }
                return null;
            }
        }
        public bool Connected { get; protected set; }
        private int WaitToConnectCounter = 0;

        public string Host = "localhost";
        public int Port = 8080;
        [Tooltip("Timeout for web requests in seconds")] public int Timeout = 100;
        public string SceneFilename = @"scenes\meyemII\main.mi";
        public string Renderer = "default";
        // Currently only render loop is supported.
        private bool UseRenderLoop = true;
        public string RenderLoopHandler = "default";
        public int RenderLoopInterval = 200;
        public UnityTextureRenderTarget RenderTarget = null;
        public Camera RenderCamera = null;
        public bool SceneYUp = true;

        public List<string> ExcludeLogMessageCategories = new List<string> {"debug"};

        // Used to determine if the camera has moved.
        protected Quaternion prevCameraRotation = Quaternion.identity;
        protected Vector3 prevCameraPosition = Vector3.zero;

        protected bool DisplayRender = false;
        protected bool DisplayNav = true;

        // Use this for initialization
        void Start ()
        {
            Scene = new RSScene(Host, Port, Timeout);
            Scene.Filename = SceneFilename;
            Scene.OnAppIniting += new ApplicationInitialisingCallback(OnAppInitingCallback);
            
            Viewport = new RSViewport(Scene);	
            Viewport.UseRenderLoop = UseRenderLoop;
            Viewport.RenderLoopHandler = RenderLoopHandler;
            Viewport.Renderer = Renderer;
            Viewport.RenderTarget = RenderTarget;
            Viewport.RenderLoopInterval = RenderLoopInterval / 1000.0f;
            Viewport.OnRestartRender += new RSViewport.RestartRenderCallback(OnRestartRender);
            Viewport.OnRender += new ResponseHandler(OnRender);

            migenius.rs4.core.Logger.OnLog += new migenius.rs4.core.Logger.LogHandler(onLog);
        }
        public void Connect()
        {
            Scene.ImportScene();
        }

        protected void onLog(string category, params object[] values)
        {
            if (ExcludeLogMessageCategories.Contains(category))
            {
                return;
            }

            StringBuilder str = new StringBuilder();
            foreach (object v in values)
            {
                str.Append(v.ToString());
                str.Append(' ');
            }
            Debug.Log(category + ": " + str.ToString());
        }

        protected void OnAppInitingCallback(RSCommandSequence seq)
        {
            if (OnAppIniting != null)
            {
                OnAppIniting(seq);
            }
        }

        public void UpdateCamera()
        {
            if (RenderCamera == null || Viewport.Camera == null)
            {
                return;
            }

            Transform camTrans = RenderCamera.transform;
            Vector3 camPos = camTrans.position;
            Vector3 camForward = camTrans.forward;

            // Negate the x position and direction as Unity is left handed while RS is right handed.
            Vector3D pos = new Vector3D(-camPos.x, camPos.y, camPos.z);
            Vector3D forward = new Vector3D(-camForward.x, camForward.y, camForward.z);
            Vector3D up = new Vector3D(0, 1, 0);
            Transform3D trans = new Transform3D();
            trans.SetLookAt(pos, forward, up);

            Viewport.Camera.TransformMatrix = trans.world_to_object;
        }

        // Update is called once per frame
        void Update () 
        {
            Viewport.Update(Time.deltaTime);

            WaitToConnectCounter++;

            Quaternion rot = RenderCamera.transform.rotation;
            Vector3 pos = RenderCamera.transform.position;

            if (prevCameraPosition != pos || prevCameraRotation != rot)
            {
                prevCameraPosition = pos;
                prevCameraRotation = rot;

                UpdateCamera();

                if (!Connected && WaitToConnectCounter > 10)
                {
                    Connected = true;
                    Connect();
                }
            }

            DisplayNav = !Input.GetMouseButton(0);

            if (Viewport != null)
            {
                RSCamera cam = Viewport.Camera;
                bool hasChanges = cam.HasNewChanges;

                if (hasChanges && Service != null)
                {
                    Service.InvalidateRenderRequests();
                }

                if (RenderCamera != null && Scene.Ready)
                {
                    Viewport.UpdateResolution(Screen.width, Screen.height);

                    cam.FieldOfView = RenderCamera.fieldOfView;
                    cam.Orthographic = RenderCamera.orthographic;
                    cam.OrthographicSize = RenderCamera.orthographicSize;
                
                    if (Viewport.RenderLoopRunning && hasChanges)
                    {
                        DisplayRender = false;
                        Viewport.RestartLoop();
                    }
                }
            }

            if (RenderTarget != null)
            {
                bool display = DisplayRender && DisplayNav;
                Color colour = RenderTarget.GetComponent<GUITexture>().color;
                float alpha = colour.a;
                if (display && alpha < 0.5f)
                {
                    alpha += (Time.deltaTime / 0.25f) * 0.5f;
                }
                else if (!display && alpha > 0.0f)
                {
                    alpha -= (Time.deltaTime / 0.25f) * 0.5f;
                }
                colour.a = Mathf.Clamp(alpha, 0.0f, 0.5f);
                RenderTarget.GetComponent<GUITexture>().color = colour;
            }

        }
        
        protected void OnRestartRender()
        {
            DisplayRender = false;
        }
        protected void OnRender(RSResponse resp)
        {
            if (resp.IsErrorResponse)
            {
                return;            
            }

            DisplayRender = true;
        }

        void OnApplicationQuit()
        {
            if (Viewport != null)
            {
                Viewport.Shutdown();
            }
        }
    }

}
