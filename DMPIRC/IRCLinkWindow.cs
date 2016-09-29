/*
KSPIRC - Internet Relay Chat plugin for Kerbal Space Program.
Copyright (C) 2013 Maik Schreiber

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KSPIRC
{
    class IRCLinkWindow : AbstractWindow
    {
        private bool stylesInitialized;
        private GUIStyle buttonStyle;

        private WWW www;
        private bool responseDecoded;
        private Texture2D texture;

        public IRCLinkWindow(IRCConfig config)
            : base("link", config, new Rect(Screen.width / 4, Screen.height / 4, Screen.width / 4, Screen.height / 4))
        {
            hidden = true;
            title = "DMPIRC Link Viewer";
        }

        internal void load(string url)
        {
            title = url;
            hidden = false;
            responseDecoded = false;

            if (texture != null)
            {
                Texture2D.Destroy(texture);
                texture = null;
            }

            if (www != null)
            {
                www.Dispose();
            }
            www = new WWW(url);
        }

        protected override void drawContents()
        {
            initStyles();

            GUILayout.BeginVertical();

            drawButtons();

            if (www == null)
            {
                GUILayout.Label("No url");
            }
            else if (!www.isDone)
            {
                GUILayout.Label("Downloading...");
            }
            else if (!responseDecoded)
            {
                Dictionary<string, string> headers = www.responseHeaders;
                if (headers == null)
                {
                    OpenLinkExternally();
                }
                else
                {
                    string contentType = "";
                    if (headers.TryGetValue("CONTENT-TYPE", out contentType))
                    {
                        if (contentType.ToLower().StartsWith("image/"))
                        {
                            texture = www.textureNonReadable;
                        }
                        else 
                        {
                            OpenLinkExternally();
                        }
                    }
                    else
                    {
                        OpenLinkExternally();
                    }
                }
                responseDecoded = true;
            }
            
            if (texture != null)
            {
                int width = texture.width;
                int height = texture.height;

                GUIStyle textureStyle = new GUIStyle(GUI.skin.box);
                textureStyle.normal.background = texture;

                GUILayout.Label("", textureStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));//, GUILayout.Height(height), GUILayout.Width(width));
            }

            GUILayout.EndVertical();
        }

        private void initStyles()
        {
            if (!stylesInitialized)
            {
                buttonStyle = new GUIStyle(GUI.skin.button);

                stylesInitialized = true;
            }
        }

        private void drawButtons()
        {
            GUILayout.BeginHorizontal();

            if (www != null)
            {
                if (GUILayout.Button("Refresh", buttonStyle))
                {
                    load(www.url);
                }
                if (GUILayout.Button("Open In Browser", buttonStyle))
                {
                    OpenLinkExternally();
                }
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", buttonStyle))
            {
                www.Dispose();
                www = null;
                hidden = true;
            }
            GUILayout.EndHorizontal();
        }

        private void OpenLinkExternally()
        {
            if (www != null)
            {
                Application.OpenURL(www.url);
                hidden = true;
            }
        }
    }
}